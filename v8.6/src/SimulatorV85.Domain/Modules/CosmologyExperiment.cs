using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using SimulatorV85.Domain;

namespace SimulatorV85.Domain.Modules;

public sealed class CosmologyExperiment : IExperiment
{
    public string Name => "cosmology";
    public string Description => "Dark-energy and fine-tuning sensitivity toy module using Friedmann distances.";

    public ExperimentResult Run(ExperimentSpec spec, IArtifactSink artifacts, CancellationToken ct = default)
    {
        double omegaM = spec.Get("cosmology.omegaM", spec.Get("omegaM", 0.315));
        double h0 = spec.Get("cosmology.H0", spec.Get("H0", 67.4));
        double w0 = spec.Get("cosmology.w0", spec.Get("w0", -1.0));
        double wa = spec.Get("cosmology.wa", spec.Get("wa", 0.0));
        double omegaL = 1 - omegaM;
        string history = artifacts.OutputPath(Name, spec.CaseId, "cosmology_expansion_history.csv");
        string sensitivity = artifacts.OutputPath(Name, spec.CaseId, "cosmology_finetuning_sensitivity.csv");

        double maxResidual = 0;
        using (var sw = new StreamWriter(history))
        {
            sw.WriteLine("z,H_z,D_L_Mpc,mu,residual_mu_vs_lcdm");
            for (double z = 0.01; z <= 3.0; z += 0.03)
            {
                double dl = LuminosityDistance(z, omegaM, omegaL, h0, w0, wa);
                double lcdm = LuminosityDistance(z, 0.315, 0.685, 67.4, -1, 0);
                double mu = 5 * Math.Log10(dl) + 25;
                double mu0 = 5 * Math.Log10(lcdm) + 25;
                maxResidual = Math.Max(maxResidual, Math.Abs(mu - mu0));
                sw.WriteLine($"{z:F4},{H(z, omegaM, omegaL, h0, w0, wa):F6},{dl:F6},{mu:F6},{mu - mu0:E8}");
            }
        }

        using (var sw = new StreamWriter(sensitivity))
        {
            sw.WriteLine("parameter,relative_step,sensitivity_proxy");
            foreach (var (name, value) in new[] { ("omegaM", omegaM), ("H0", h0), ("w0", w0), ("wa", wa) })
            {
                double step = Math.Abs(value) < 1e-12 ? 1e-3 : Math.Abs(value) * 0.01;
                double plus = DistanceAtParam(name, value + step, omegaM, h0, w0, wa);
                double minus = DistanceAtParam(name, value - step, omegaM, h0, w0, wa);
                double sens = Math.Abs((plus - minus) / (2 * step));
                sw.WriteLine($"{name},0.01,{sens:E8}");
            }
        }
        double interesting = MathUtil.Clamp(maxResidual / 0.1, 0, 1);
        var obs = new List<ObservableRecord>
        {
            new(Name, "max_distance_modulus_residual", maxResidual, "mag", interesting,
                "Maximum distance-modulus residual versus reference LCDM.", spec.CaseId),
            new(Name, "w0", w0, "dimensionless", 0, "Dark-energy equation-of-state parameter.", spec.CaseId),
            new(Name, "wa", wa, "dimensionless", 0, "CPL evolution parameter.", spec.CaseId),
            new(Name, "omegaM", omegaM, "dimensionless", 0, "Matter density fraction.", spec.CaseId)
        };
        return new ExperimentResult(artifacts.RunId, spec.CaseId, Name, "Friedmann+CPL", spec.Parameters,
            obs, new[] { history, sensitivity }, Array.Empty<string>(), interesting);
    }

    private static double DistanceAtParam(string name, double value, double omegaM, double h0, double w0, double wa)
    {
        if (name == "omegaM") omegaM = MathUtil.Clamp(value, 0.01, 0.99);
        if (name == "H0") h0 = Math.Max(1, value);
        if (name == "w0") w0 = value;
        if (name == "wa") wa = value;
        return LuminosityDistance(1.0, omegaM, 1 - omegaM, h0, w0, wa);
    }

    private static double H(double z, double om, double ol, double h0, double w0, double wa)
    {
        double a = 1.0 / (1 + z);
        double de = ol * Math.Pow(a, -3 * (1 + w0 + wa)) * Math.Exp(-3 * wa * (1 - a));
        return h0 * Math.Sqrt(om * Math.Pow(1 + z, 3) + de);
    }

    private static double LuminosityDistance(double z, double om, double ol, double h0, double w0, double wa)
    {
        const double c = 299792.458;
        int n = 500;
        double dz = z / n, sum = 0;
        for (int i = 0; i <= n; i++)
        {
            double zz = i * dz;
            double weight = i == 0 || i == n ? 1 : (i % 2 == 0 ? 2 : 4);
            sum += weight / H(zz, om, ol, h0, w0, wa);
        }
        double dc = c * dz * sum / 3.0;
        return (1 + z) * dc;
    }
}
