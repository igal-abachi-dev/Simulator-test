using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using SimulatorV85.Domain;

namespace SimulatorV85.Domain.Modules;

public sealed class CosmologyNBodyExperiment : IExperiment
{
    public string Name => "nbody";
    public string Description => "Small cosmological N-body structure-formation toy model: Barnes-Hut gravity plus causal inflation-derived initial-amplitude handoff.";

    public ExperimentResult Run(ExperimentSpec spec, IArtifactSink artifacts, CancellationToken ct = default)
    {
        int n = Math.Clamp(spec.GetInt("nbody.particles", spec.GetInt("particles", 2500)), 128, 200000);
        int steps = Math.Clamp(spec.GetInt("nbody.steps", spec.GetInt("steps", 100)), 10, 20000);
        double box = Math.Max(1e-3, spec.Get("nbody.boxSize", 1.0));
        double theta = MathUtil.Clamp(spec.Get("nbody.theta", 0.65), 0.1, 1.5);
        double softening = MathUtil.Clamp(spec.Get("nbody.softening", 0.012), 1e-5, 0.2);
        double dt = MathUtil.Clamp(spec.Get("nbody.dt", 0.01), 1e-5, 0.1);
        double amplitude = MathUtil.Clamp(spec.Get("nbody.initialAmplitudeFromInflation", spec.Get("nbody.initialAmplitude", 0.075)), 0.0, 0.5);
        double baryonFraction = MathUtil.Clamp(spec.Get("nbody.baryonFraction", 0.16), 0.0, 0.5);
        double pressure = Math.Max(0, spec.Get("nbody.baryonPressure", 0.015));
        double cooling = MathUtil.Clamp(spec.Get("nbody.baryonCooling", 0.015), 0.0, 1.0);
        var rng = new Random(spec.Seed + 42042);

        var p = InitParticles(n, box, amplitude, baryonFraction, rng);
        string statsPath = artifacts.OutputPath(Name, spec.CaseId, "nbody_structure_stats.csv");
        string finalPath = artifacts.OutputPath(Name, spec.CaseId, "nbody_particles_final.csv");
        if (spec.WriteSeries)
            File.WriteAllText(statsPath, "step,scale_factor,clumpiness,max_density_contrast,halo_count,baryon_dm_bias,kinetic_energy\n");

        double finalClump = 0, finalMaxContrast = 0, finalHaloCount = 0, finalBias = 0, finalKinetic = 0;
        int outEvery = Math.Max(1, steps / 100);
        for (int step = 0; step <= steps; step++)
        {
            ct.ThrowIfCancellationRequested();
            double a = 0.02 + 0.98 * step / Math.Max(1.0, steps);
            var st = StructureStats(p, box, grid: 24);
            finalClump = st.clumpiness; finalMaxContrast = st.maxContrast; finalHaloCount = st.haloCount; finalBias = st.baryonDmBias; finalKinetic = Kinetic(p);
            if (spec.WriteSeries && step % outEvery == 0)
                Csv.AppendLine(statsPath, new[] { step.ToString(), Csv.D(a), Csv.D(st.clumpiness), Csv.D(st.maxContrast), Csv.D(st.haloCount), Csv.D(st.baryonDmBias), Csv.D(finalKinetic) });
            if (step == steps) break;

            var root = Octree.Build(p, box);
            var acc = new Vec3[p.Length];
            for (int i = 0; i < p.Length; i++) acc[i] = root.AccelerationOn(i, p[i].Pos, theta, softening);
            ApplySimpleBaryonHydro(p, acc, box, pressure, cooling);

            double drag = 0.025 / (0.1 + a); // toy Hubble drag in comoving coordinates
            for (int i = 0; i < p.Length; i++)
            {
                p[i].Vel = (p[i].Vel + acc[i] * dt) * Math.Max(0, 1.0 - drag * dt);
                p[i].Pos = Wrap(p[i].Pos + p[i].Vel * dt, box);
            }
        }

        using (var sw = new StreamWriter(finalPath))
        {
            sw.WriteLine("i,type,x,y,z,vx,vy,vz,mass");
            int limit = Math.Min(p.Length, 20000);
            for (int i = 0; i < limit; i++)
                sw.WriteLine($"{i},{(p[i].IsBaryon ? "baryon" : "dm")},{Csv.D(p[i].Pos.X)},{Csv.D(p[i].Pos.Y)},{Csv.D(p[i].Pos.Z)},{Csv.D(p[i].Vel.X)},{Csv.D(p[i].Vel.Y)},{Csv.D(p[i].Vel.Z)},{Csv.D(p[i].Mass)}");
        }

        double structureScore = MathUtil.Clamp(Math.Log10(1 + finalClump) / 1.5, 0, 1);
        double haloScore = MathUtil.Clamp(finalHaloCount / 30.0, 0, 1);
        double biasScore = MathUtil.Clamp(Math.Abs(finalBias) / 0.8, 0, 1);
        double score = MathUtil.Clamp(0.45 * structureScore + 0.35 * haloScore + 0.20 * biasScore, 0, 1);

        var obs = new List<ObservableRecord>
        {
            new(Name, "structure_clumpiness", finalClump, "variance/mean^2", structureScore, "Density-field variance proxy from a small N-body box.", spec.CaseId),
            new(Name, "halo_count_proxy", finalHaloCount, "count", haloScore, "Number of grid cells above a high-density threshold, used as a toy halo count.", spec.CaseId),
            new(Name, "baryon_dm_bias", finalBias, "dimensionless", biasScore, "Difference between baryon and dark-matter concentration proxies.", spec.CaseId),
            new(Name, "max_density_contrast", finalMaxContrast, "delta", MathUtil.Clamp(Math.Log10(1 + finalMaxContrast) / 2.5, 0, 1), "Maximum grid-cell density contrast.", spec.CaseId),
            new(Name, "nbody_particles", n, "count", MathUtil.Clamp(Math.Log10(n) / 6.0, 0, 1), "Number of simulated particles.", spec.CaseId),
            new(Name, "initial_amplitude_used", amplitude, "dimensionless", MathUtil.Clamp(amplitude / 0.22, 0, 1), "Initial displacement amplitude, preferably propagated from the inflation scalar spectrum in causal sweeps.", spec.CaseId)
        };
        var warnings = new[]
        {
            "Toy comoving N-body box; not a replacement for GADGET/AREPO/IllustrisTNG.",
            "v8.6 can receive nbody.initialAmplitudeFromInflation from the inflation module; this is a causal toy handoff, not a Boltzmann transfer calculation.",
            "Hydrodynamics is a pressure/cooling proxy, not real SPH or moving-mesh hydrodynamics."
        };
        return new ExperimentResult(artifacts.RunId, spec.CaseId, Name, "Barnes-Hut cosmological toy N-body", spec.Parameters,
            obs, new[] { statsPath, finalPath }, warnings, score);
    }

    private static Particle[] InitParticles(int n, double box, double amp, double fb, Random rng)
    {
        var p = new Particle[n];
        for (int i = 0; i < n; i++)
        {
            double x = rng.NextDouble() - 0.5, y = rng.NextDouble() - 0.5, z = rng.NextDouble() - 0.5;
            // Zel'dovich-like toy displacement from a few long modes.
            x += amp * Math.Sin(2 * Math.PI * y) / (2 * Math.PI);
            y += amp * Math.Sin(2 * Math.PI * z) / (2 * Math.PI);
            z += amp * Math.Sin(2 * Math.PI * x) / (2 * Math.PI);
            var pos = Wrap(new Vec3(x * box, y * box, z * box), box);
            bool baryon = rng.NextDouble() < fb;
            double mass = baryon ? fb / Math.Max(1, n * fb) : (1 - fb) / Math.Max(1, n * (1 - fb));
            var vel = new Vec3(0.01 * NextGaussian(rng), 0.01 * NextGaussian(rng), 0.01 * NextGaussian(rng));
            p[i] = new Particle(pos, vel, mass, baryon);
        }
        return p;
    }

    private static void ApplySimpleBaryonHydro(Particle[] p, Vec3[] acc, double box, double pressure, double cooling)
    {
        if (pressure <= 0 && cooling <= 0) return;
        double h2 = Math.Pow(0.035 * box, 2);
        int n = Math.Min(p.Length, 6000); // keep the toy hydro bounded even for larger dark-matter sweeps
        for (int i = 0; i < n; i++)
        {
            if (!p[i].IsBaryon) continue;
            for (int j = i + 1; j < n; j++)
            {
                if (!p[j].IsBaryon) continue;
                var d = MinimumImage(p[i].Pos - p[j].Pos, box);
                double r2 = d.Norm2();
                if (r2 > h2 || r2 < 1e-12) continue;
                double q = 1 - Math.Sqrt(r2 / h2);
                var push = d * (pressure * q / Math.Sqrt(r2 + 1e-12));
                acc[i] += push;
                acc[j] -= push;
            }
            p[i].Vel *= Math.Max(0, 1 - cooling * 0.01);
        }
    }

    private static (double clumpiness, double maxContrast, double haloCount, double baryonDmBias) StructureStats(Particle[] p, double box, int grid)
    {
        var total = new double[grid, grid, grid];
        var dm = new double[grid, grid, grid];
        var ba = new double[grid, grid, grid];
        foreach (var part in p)
        {
            int ix = Cell(part.Pos.X, box, grid), iy = Cell(part.Pos.Y, box, grid), iz = Cell(part.Pos.Z, box, grid);
            total[ix, iy, iz] += part.Mass;
            if (part.IsBaryon) ba[ix, iy, iz] += part.Mass; else dm[ix, iy, iz] += part.Mass;
        }
        double mean = p.Sum(x => x.Mass) / (grid * grid * grid);
        double var = 0, max = 0, halos = 0, dmTop = 0, baTop = 0;
        for (int i = 0; i < grid; i++) for (int j = 0; j < grid; j++) for (int k = 0; k < grid; k++)
        {
            double delta = total[i, j, k] / Math.Max(mean, 1e-30) - 1;
            var += delta * delta;
            max = Math.Max(max, delta);
            if (delta > 6) { halos++; dmTop += dm[i, j, k]; baTop += ba[i, j, k]; }
        }
        var /= grid * grid * grid;
        double fbLocal = baTop / Math.Max(baTop + dmTop, 1e-30);
        double fbGlobal = p.Where(x => x.IsBaryon).Sum(x => x.Mass) / Math.Max(p.Sum(x => x.Mass), 1e-30);
        return (var, max, halos, fbLocal - fbGlobal);
    }

    private static int Cell(double x, double box, int grid)
    {
        double u = (x / box) + 0.5;
        u -= Math.Floor(u);
        return Math.Clamp((int)Math.Floor(u * grid), 0, grid - 1);
    }

    private static double Kinetic(Particle[] p) => p.Sum(x => 0.5 * x.Mass * x.Vel.Norm2());
    private static Vec3 Wrap(Vec3 x, double box) => new(Wrap1(x.X, box), Wrap1(x.Y, box), Wrap1(x.Z, box));
    private static double Wrap1(double x, double box) { while (x < -box / 2) x += box; while (x >= box / 2) x -= box; return x; }
    private static Vec3 MinimumImage(Vec3 d, double box) => new(Min1(d.X, box), Min1(d.Y, box), Min1(d.Z, box));
    private static double Min1(double x, double box) { if (x > box / 2) x -= box; if (x < -box / 2) x += box; return x; }
    private static double NextGaussian(Random rng) { double u1 = Math.Max(1e-12, rng.NextDouble()); return Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * rng.NextDouble()); }

    private sealed class Octree
    {
        private readonly Node _root;
        private readonly Particle[] _p;
        private readonly double _box;
        private Octree(Particle[] p, double box) { _p = p; _box = box; _root = new Node(new Vec3(0, 0, 0), box / 2); for (int i = 0; i < p.Length; i++) _root.Insert(i, p, depth: 0); _root.FinalizeMass(p); }
        public static Octree Build(Particle[] p, double box) => new(p, box);
        public Vec3 AccelerationOn(int i, Vec3 pos, double theta, double eps) => _root.Accel(i, pos, _p, theta, eps, _box);
    }

    private sealed class Node
    {
        private readonly Vec3 _center;
        private readonly double _half;
        private int _particle = -1;
        private Node[]? _child;
        private double _mass;
        private Vec3 _com;
        public Node(Vec3 center, double half) { _center = center; _half = half; }
        public void Insert(int index, Particle[] p, int depth)
        {
            if (_child is null && _particle < 0) { _particle = index; return; }
            if (_child is null) Subdivide();
            if (_particle >= 0) { int old = _particle; _particle = -1; ChildFor(p[old].Pos).Insert(old, p, depth + 1); }
            if (depth > 30) { _particle = index; return; }
            ChildFor(p[index].Pos).Insert(index, p, depth + 1);
        }
        public void FinalizeMass(Particle[] p)
        {
            if (_child is null)
            {
                if (_particle >= 0) { _mass = p[_particle].Mass; _com = p[_particle].Pos; }
                return;
            }
            _mass = 0; _com = Vec3.Zero;
            foreach (var c in _child) { c.FinalizeMass(p); _mass += c._mass; _com += c._com * c._mass; }
            if (_mass > 0) _com /= _mass;
        }
        public Vec3 Accel(int target, Vec3 pos, Particle[] p, double theta, double eps, double box)
        {
            if (_mass <= 0) return Vec3.Zero;
            if (_child is null && _particle == target) return Vec3.Zero;
            var d = MinimumImage(_com - pos, box);
            double r = Math.Sqrt(d.Norm2() + eps * eps);
            if (_child is null || (_half * 2.0) / Math.Max(r, 1e-12) < theta)
                return d * (_mass / (r * r * r));
            var a = Vec3.Zero;
            foreach (var c in _child) a += c.Accel(target, pos, p, theta, eps, box);
            return a;
        }
        private void Subdivide()
        {
            _child = new Node[8];
            int idx = 0; double q = _half / 2;
            for (int sx = -1; sx <= 1; sx += 2) for (int sy = -1; sy <= 1; sy += 2) for (int sz = -1; sz <= 1; sz += 2)
                _child[idx++] = new Node(new Vec3(_center.X + sx * q, _center.Y + sy * q, _center.Z + sz * q), q);
        }
        private Node ChildFor(Vec3 x)
        {
            int idx = (x.X >= _center.X ? 4 : 0) + (x.Y >= _center.Y ? 2 : 0) + (x.Z >= _center.Z ? 1 : 0);
            return _child![idx];
        }
    }

    private struct Particle
    {
        public Vec3 Pos;
        public Vec3 Vel;
        public double Mass;
        public bool IsBaryon;
        public Particle(Vec3 pos, Vec3 vel, double mass, bool isBaryon) { Pos = pos; Vel = vel; Mass = mass; IsBaryon = isBaryon; }
    }

    private readonly record struct Vec3(double X, double Y, double Z)
    {
        public static Vec3 Zero => new(0, 0, 0);
        public double Norm2() => X * X + Y * Y + Z * Z;
        public double Norm() => Math.Sqrt(Norm2());
        public static Vec3 operator +(Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3 operator *(Vec3 a, double s) => new(a.X * s, a.Y * s, a.Z * s);
        public static Vec3 operator /(Vec3 a, double s) => new(a.X / s, a.Y / s, a.Z / s);
        public static Vec3 operator -(Vec3 a) => new(-a.X, -a.Y, -a.Z);
    }
}
