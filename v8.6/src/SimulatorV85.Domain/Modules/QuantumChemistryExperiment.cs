using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using SimulatorV85.Domain;

namespace SimulatorV85.Domain.Modules;

public sealed class QuantumChemistryExperiment : IExperiment
{
    public string Name => "chemistry";
    public string Description => "Toy quantum bond-dissociation experiment using a 2x2 covalent/ionic Hamiltonian and observable bond-energy proxies.";

    public ExperimentResult Run(ExperimentSpec spec, IArtifactSink artifacts, CancellationToken ct = default)
    {
        double env = MathUtil.Clamp(spec.Get("chemistry.environmentFactor", 1.0), 0.25, 2.0);
        double tempFactor = MathUtil.Clamp(spec.Get("chemistry.temperatureFactor", 1.0), 0.25, 3.0);
        double de = Math.Max(0.01, spec.Get("chemistry.dissociationEnergyEV", spec.Get("dissociationEnergyEV", 4.5)) * env);
        double re = MathUtil.Clamp(spec.Get("chemistry.equilibriumDistanceA", 1.10), 0.3, 5.0);
        double a = MathUtil.Clamp(spec.Get("chemistry.morseAInvA", 1.7), 0.1, 8.0);
        double ionicOffset = MathUtil.Clamp(spec.Get("chemistry.ionicOffsetEV", 8.5), 0.1, 50.0);
        double coupling = MathUtil.Clamp(spec.Get("chemistry.couplingEV", 0.75), 0.0, 20.0);
        double beta = MathUtil.Clamp(spec.Get("chemistry.couplingBeta", 0.85), 0.01, 10.0);

        string curve = artifacts.OutputPath(Name, spec.CaseId, "quantum_bond_dissociation_curve.csv");
        File.WriteAllText(curve, "R_A,E_covalent_eV,E_ionic_eV,coupling_eV,E_ground_eV,E_excited_eV,ionic_weight_ground\n");

        double minE = double.PositiveInfinity, minR = 0, gapMin = double.PositiveInfinity, gapR = 0;
        double lastE = 0;
        for (int i = 0; i <= 360; i++)
        {
            ct.ThrowIfCancellationRequested();
            double R = 0.45 + i * (6.0 - 0.45) / 360.0;
            var h = Hamiltonian(R, de, re, a, ionicOffset, coupling, beta);
            var ev = Eigen2(h.ec, h.ei, h.v);
            double gap = ev.e2 - ev.e1;
            if (ev.e1 < minE) { minE = ev.e1; minR = R; }
            if (gap < gapMin) { gapMin = gap; gapR = R; }
            if (i == 360) lastE = ev.e1;
            Csv.AppendLine(curve, new[] { Csv.D(R), Csv.D(h.ec), Csv.D(h.ei), Csv.D(h.v), Csv.D(ev.e1), Csv.D(ev.e2), Csv.D(ev.ionicWeightGround) });
        }

        double dissociation = Math.Max(0, lastE - minE);
        double equilibriumError = Math.Abs(minR - re);
        double complexity = MathUtil.Clamp((coupling / Math.Max(ionicOffset, 1e-9)) * (1.0 + gapMin / Math.Max(de, 1e-9)), 0, 1);
        double thermalPenalty = MathUtil.Clamp((tempFactor - 1.0) / 2.0, 0, 1);
        double prebioticFeasibility = MathUtil.Clamp((1.0 - Math.Abs(dissociation - de) / Math.Max(de, 1e-9)) * (1.0 - 0.35 * thermalPenalty), 0, 1);
        double score = MathUtil.Clamp(0.45 * prebioticFeasibility + 0.25 * complexity + 0.30 * MathUtil.Clamp(dissociation / 8.0, 0, 1), 0, 1);

        var obs = new List<ObservableRecord>
        {
            new(Name, "bond_dissociation_energy", dissociation, "eV", MathUtil.Clamp(dissociation / 8.0, 0, 1), "Toy ground-state dissociation energy from the lowest eigenvalue curve.", spec.CaseId),
            new(Name, "equilibrium_distance", minR, "angstrom", MathUtil.Clamp(1.0 - equilibriumError / Math.Max(re, 1e-9), 0, 1), "Minimum-energy bond length.", spec.CaseId),
            new(Name, "avoided_crossing_gap", gapMin, "eV", MathUtil.Clamp(1.0 / (1.0 + gapMin), 0, 1), "Minimum energy gap between ground/excited adiabatic states.", spec.CaseId),
            new(Name, "bond_complexity_proxy", complexity, "0..1", complexity, "How strongly ionic/covalent character mixes in this toy Hamiltonian.", spec.CaseId),
            new(Name, "prebiotic_feasibility_proxy", prebioticFeasibility, "0..1", prebioticFeasibility, "Sanity proxy: stable finite bond under the propagated proto-chemical environment factor.", spec.CaseId),
            new(Name, "environment_factor", env, "dimensionless", MathUtil.Clamp(Math.Abs(env - 1.0), 0, 1), "Environment factor propagated from N-body baryon/clustering observables when running causal bridge sweeps.", spec.CaseId)
        };

        var warnings = new[]
        {
            "Toy 2-state Hamiltonian, not ab-initio quantum chemistry. Use Psi4/OpenFermion later for real electronic-structure calculations.",
            "Complex chemistry did not exist during inflation; this module bridges quantum physics to later chemical complexity, not inflation-era molecules."
        };
        return new ExperimentResult(artifacts.RunId, spec.CaseId, Name, "2x2 covalent/ionic Hamiltonian", spec.Parameters,
            obs, new[] { curve }, warnings, score);
    }

    private static (double ec, double ei, double v) Hamiltonian(double R, double de, double re, double a, double ionicOffset, double coupling, double beta)
    {
        double morse = de * Math.Pow(1 - Math.Exp(-a * (R - re)), 2) - de;
        double coulomb = -14.3996 / Math.Max(R, 0.2); // eV Angstrom / R, rough ionic attraction scale
        double repulsion = 0.15 * Math.Exp(-3.0 * (R - re));
        double ionic = ionicOffset + 0.35 * coulomb + repulsion;
        double v = coupling * Math.Exp(-beta * Math.Pow(R - re, 2));
        return (morse, ionic, v);
    }

    private static (double e1, double e2, double ionicWeightGround) Eigen2(double a, double d, double b)
    {
        double tr = 0.5 * (a + d);
        double diff = 0.5 * (a - d);
        double root = Math.Sqrt(diff * diff + b * b);
        double e1 = tr - root, e2 = tr + root;
        // For H=[[a,b],[b,d]], ionic weight of lower eigenvector.
        double theta = 0.5 * Math.Atan2(2 * b, a - d);
        double ionicWeight = Math.Pow(Math.Cos(theta), 2);
        if (d > a) ionicWeight = Math.Pow(Math.Sin(theta), 2);
        return (e1, e2, MathUtil.Clamp(ionicWeight, 0, 1));
    }
}
