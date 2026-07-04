using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using SimulatorV85.Domain;

namespace SimulatorV85.Domain.Modules;

public sealed class FoldingExperiment : IExperiment
{
    public string Name => "folding";
    public string Description => "Coarse-grained mini-protein folding: HP bead-chain Langevin dynamics with contact/foldability observables.";

    public ExperimentResult Run(ExperimentSpec spec, IArtifactSink artifacts, CancellationToken ct = default)
    {
        int length = Math.Clamp(spec.GetInt("folding.length", spec.GetInt("length", 28)), 8, 96);
        int steps = Math.Clamp(spec.GetInt("folding.steps", spec.GetInt("steps", 3500)), 100, 250000);
        double hydrophobicFraction = MathUtil.Clamp(spec.Get("folding.hydrophobicFraction", 0.42), 0.05, 0.95);
        double temperature = Math.Max(1e-4, spec.Get("folding.temperature", 0.45));
        double dt = MathUtil.Clamp(spec.Get("folding.dt", 0.002), 1e-5, 0.02);
        double attraction = Math.Max(0, spec.Get("folding.hydrophobicAttractionFromChemistry", spec.Get("folding.hydrophobicAttraction", 1.25)));
        double stiffness = Math.Max(1, spec.Get("folding.bondStiffness", 55.0));
        var rng = new Random(spec.Seed + 1701);

        var seq = BuildHpSequence(length, hydrophobicFraction, rng);
        var pos = new Vec3[length];
        for (int i = 0; i < length; i++)
            pos[i] = new Vec3((i - length / 2.0) * 1.12, 0.06 * rng.NextDouble(), 0.06 * rng.NextDouble());

        string trajectory = artifacts.OutputPath(Name, spec.CaseId, "folding_trajectory.csv");
        string contactsPath = artifacts.OutputPath(Name, spec.CaseId, "folding_contacts.csv");
        if (spec.WriteSeries)
            File.WriteAllText(trajectory, "step,energy,radius_gyration,native_contact_fraction,hydrophobic_contacts\n");

        double initialRg = RadiusOfGyration(pos);
        double bestEnergy = double.PositiveInfinity;
        double finalContactFraction = 0;
        int finalHydroContacts = 0;
        var forces = new Vec3[length];
        int outEvery = Math.Max(1, steps / 120);

        for (int step = 0; step <= steps; step++)
        {
            ct.ThrowIfCancellationRequested();
            double e = EnergyAndForces(pos, seq, forces, stiffness, attraction);
            if (e < bestEnergy) bestEnergy = e;
            var (contactFraction, hydroContacts) = ContactStats(pos, seq);
            finalContactFraction = contactFraction;
            finalHydroContacts = hydroContacts;

            if (spec.WriteSeries && step % outEvery == 0)
                Csv.AppendLine(trajectory, new[] { step.ToString(), Csv.D(e), Csv.D(RadiusOfGyration(pos)), Csv.D(contactFraction), hydroContacts.ToString() });

            if (step == steps) break;
            double noise = Math.Sqrt(2.0 * temperature * dt);
            for (int i = 0; i < length; i++)
            {
                var kick = new Vec3(NextGaussian(rng), NextGaussian(rng), NextGaussian(rng)) * noise;
                pos[i] += forces[i] * dt + kick;
            }
            RemoveCenterOfMass(pos);
        }

        double finalRg = RadiusOfGyration(pos);
        double collapseRatio = initialRg / Math.Max(finalRg, 1e-9);
        double compactnessScore = MathUtil.Clamp((collapseRatio - 1.1) / 2.5, 0, 1);
        double contactScore = MathUtil.Clamp(finalContactFraction, 0, 1);
        double energyScore = MathUtil.Clamp(-bestEnergy / Math.Max(length, 1) / 1.8, 0, 1);
        double foldability = MathUtil.Clamp(0.35 * compactnessScore + 0.45 * contactScore + 0.20 * energyScore, 0, 1);

        using (var sw = new StreamWriter(contactsPath))
        {
            sw.WriteLine("i,j,type,distance,is_contact");
            for (int i = 0; i < length; i++)
            for (int j = i + 3; j < length; j++)
            {
                double d = (pos[i] - pos[j]).Norm();
                bool hh = seq[i] == 'H' && seq[j] == 'H';
                bool contact = d < 1.55;
                if (hh || contact)
                    sw.WriteLine($"{i},{j},{seq[i]}{seq[j]},{Csv.D(d)},{(contact ? 1 : 0)}");
            }
        }

        var obs = new List<ObservableRecord>
        {
            new(Name, "foldability_score", foldability, "0..1", foldability, "Combined compactness/contact/energy score for this coarse-grained mini-protein.", spec.CaseId),
            new(Name, "radius_gyration_final", finalRg, "bead_length", MathUtil.Clamp(1.0 / (1.0 + finalRg / Math.Sqrt(length)), 0, 1), "Final radius of gyration.", spec.CaseId),
            new(Name, "native_contact_fraction", finalContactFraction, "fraction", contactScore, "Fraction of possible hydrophobic contacts formed.", spec.CaseId),
            new(Name, "hydrophobic_contacts", finalHydroContacts, "count", MathUtil.Clamp(finalHydroContacts / Math.Max(1.0, length), 0, 1), "Number of hydrophobic nonlocal contacts.", spec.CaseId),
            new(Name, "best_energy_per_residue", bestEnergy / length, "arb/residue", energyScore, "Lowest energy per residue observed during the trajectory.", spec.CaseId),
            new(Name, "hydrophobic_attraction_used", attraction, "arb", MathUtil.Clamp(attraction / 2.2, 0, 1), "Hydrophobic attraction, optionally propagated from chemistry bond stability in causal bridge sweeps.", spec.CaseId)
        };

        var warnings = new[]
        {
            "Toy HP coarse-grained dynamics only; not an AlphaFold/OpenMM-quality structural prediction.",
            "Use this for phase-patterns and foldability proxies, not atomic accuracy."
        };
        return new ExperimentResult(artifacts.RunId, spec.CaseId, Name, "HP coarse-grained Langevin", spec.Parameters,
            obs, new[] { trajectory, contactsPath }, warnings, foldability);
    }

    private static char[] BuildHpSequence(int n, double hFrac, Random rng)
    {
        var seq = new char[n];
        for (int i = 0; i < n; i++) seq[i] = rng.NextDouble() < hFrac ? 'H' : 'P';
        // Add a weak amphipathic pattern so some sequences are foldable without hard-coding a native state.
        for (int i = 3; i < n; i += 7) seq[i] = 'H';
        return seq;
    }

    private static double EnergyAndForces(Vec3[] p, char[] seq, Vec3[] f, double kBond, double hhAttraction)
    {
        Array.Fill(f, Vec3.Zero);
        double e = 0;
        int n = p.Length;
        const double r0 = 1.0;
        for (int i = 0; i < n - 1; i++)
        {
            var d = p[i + 1] - p[i];
            double r = Math.Max(d.Norm(), 1e-9);
            double x = r - r0;
            double mag = kBond * x / r;
            var force = d * mag;
            f[i] += force;
            f[i + 1] -= force;
            e += 0.5 * kBond * x * x;
        }

        for (int i = 0; i < n; i++)
        for (int j = i + 2; j < n; j++)
        {
            var d = p[j] - p[i];
            double r = Math.Max(d.Norm(), 0.72);
            double inv = 1.0 / r;
            double inv6 = Math.Pow(inv, 6);
            double rep = 0.035 * inv6 * inv6;
            double attr = (seq[i] == 'H' && seq[j] == 'H') ? -hhAttraction * inv6 : 0.0;
            e += rep + attr;
            double dEdr = -12 * 0.035 * Math.Pow(inv, 13) + (seq[i] == 'H' && seq[j] == 'H' ? 6 * hhAttraction * Math.Pow(inv, 7) : 0.0);
            var force = d * (-dEdr / r);
            f[i] -= force;
            f[j] += force;
        }
        return e;
    }

    private static (double fraction, int hydroContacts) ContactStats(Vec3[] p, char[] seq)
    {
        int possible = 0, contacts = 0;
        for (int i = 0; i < p.Length; i++)
        for (int j = i + 3; j < p.Length; j++)
        {
            if (seq[i] != 'H' || seq[j] != 'H') continue;
            possible++;
            if ((p[i] - p[j]).Norm() < 1.55) contacts++;
        }
        return (possible == 0 ? 0 : contacts / (double)possible, contacts);
    }

    private static double RadiusOfGyration(Vec3[] p)
    {
        var c = Vec3.Zero;
        foreach (var x in p) c += x;
        c /= p.Length;
        double s = 0;
        foreach (var x in p) s += (x - c).Norm2();
        return Math.Sqrt(s / p.Length);
    }

    private static void RemoveCenterOfMass(Vec3[] p)
    {
        var c = Vec3.Zero;
        foreach (var x in p) c += x;
        c /= p.Length;
        for (int i = 0; i < p.Length; i++) p[i] -= c;
    }

    private static double NextGaussian(Random rng)
    {
        double u1 = Math.Max(1e-12, rng.NextDouble());
        double u2 = rng.NextDouble();
        return Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
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
    }
}
