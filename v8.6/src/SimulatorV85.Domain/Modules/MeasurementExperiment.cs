using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using SimulatorV85.Domain;

namespace SimulatorV85.Domain.Modules;

public sealed class MeasurementExperiment : IExperiment
{
    public string Name => "measurement";
    public string Description => "Standard null tests plus CSL visibility/radiation observables and exclusion proxy.";

    public ExperimentResult Run(ExperimentSpec spec, IArtifactSink artifacts, CancellationToken ct = default)
    {
        double lambda = spec.Get("csl.lambda", spec.Get("lambda", 1e-16));
        double rC = spec.Get("csl.rC", spec.Get("rC", 1e-7));
        double massN = spec.Get("csl.N", spec.Get("particleN", 1e8));
        double separation = spec.Get("csl.separation", spec.Get("separation", rC));
        int trials = spec.GetInt("observer.trials", spec.GetInt("trials", 100_000));
        double bornBias = spec.Get("observer.bornBias", spec.Get("bornBias", 0.0));

        string landscape = artifacts.OutputPath(Name, spec.CaseId, "measurement_csl_exclusion_landscape.csv");
        string xray = artifacts.OutputPath(Name, spec.CaseId, "measurement_csl_xray_spectrum.csv");
        string nullTest = artifacts.OutputPath(Name, spec.CaseId, "measurement_nonstandard_bias_null.csv");
        string visibility = artifacts.OutputPath(Name, spec.CaseId, "measurement_visibility_budget.csv");

        double collapseRate = CslRate(lambda, massN, separation, rC);
        double visibilityTime = collapseRate > 0 ? 1.0 / collapseRate : double.PositiveInfinity;
        double xrayProxy = WriteXrayProxy(xray, lambda, rC, massN);
        double excludedScore = WriteExclusionLandscape(landscape, lambda, rC);
        WriteVisibilityBudget(visibility, lambda, rC, separation);
        var nullResult = WriteBornBiasNull(nullTest, trials, bornBias, spec.Seed);

        var obs = new List<ObservableRecord>
        {
            new(Name, "csl_collapse_rate", collapseRate, "1/s", MathUtil.InterestNearThreshold(collapseRate, 1, 8),
                "Objective-collapse rate for the selected mass and separation.", spec.CaseId),
            new(Name, "visibility_time", visibilityTime, "s", MathUtil.InterestNearThreshold(visibilityTime, 1, 8),
                "Time-scale for CSL visibility loss under the selected parameters.", spec.CaseId),
            new(Name, "xray_rate_proxy", xrayProxy, "arb", MathUtil.Clamp(xrayProxy / 1e-20, 0, 1),
                "Toy spontaneous-radiation proxy proportional to lambda/rC^2.", spec.CaseId),
            new(Name, "toy_exclusion_score", excludedScore, "score", excludedScore,
                "Toy exclusion score; not a real experimental likelihood.", spec.CaseId),
            new(Name, "born_bias_z", nullResult.Z, "sigma", MathUtil.Clamp(Math.Abs(nullResult.Z) / 5.0, 0, 1),
                "Null-test z-score for a non-standard Born-rule bias.", spec.CaseId),
            new(Name, "born_bias_detectable", nullResult.DetectableBias, "probability", 0,
                "Smallest probability shift detectable at about five sigma with this many trials.", spec.CaseId)
        };
        double score = MathUtil.Clamp(0.35 * obs[0].AnomalyScore + 0.25 * excludedScore + 0.25 * obs[4].AnomalyScore + 0.15 * obs[2].AnomalyScore, 0, 1);
        var warnings = new[]
        {
            "CSL exclusion and X-ray outputs are toy recasts / scaling maps, not official experimental likelihoods.",
            "The observer null test models non-standard probability bias, not consciousness as a physical field."
        };
        return new ExperimentResult(artifacts.RunId, spec.CaseId, Name, "CSL+null-tests", spec.Parameters,
            obs, new[] { landscape, xray, nullTest, visibility }, warnings, score);
    }

    private static double CslRate(double lambda, double n, double d, double rC)
    {
        double geo = 1 - Math.Exp(-d * d / (4 * rC * rC));
        double domains = Math.Max(1.0, n / 1e9); // finite-size toy: N^2 within domains, incoherent across domains
        double nPerDomain = n / domains;
        return lambda * domains * nPerDomain * nPerDomain * geo;
    }

    private static double WriteExclusionLandscape(string path, double selectedLambda, double selectedRc)
    {
        using var sw = new StreamWriter(path);
        sw.WriteLine("rC,lambda,rate_proxy,excluded_by_toy_bound,selected_point");
        double score = 0;
        foreach (double rC in MathUtil.LogSpace(1e-9, 1e-4, 28))
        foreach (double lam in MathUtil.LogSpace(1e-20, 1e-6, 28))
        {
            double proxy = lam / (rC * rC);
            double bound = 1e-2; // arbitrary toy line for visualization only
            bool excluded = proxy > bound;
            bool selected = Math.Abs(Math.Log10(lam / selectedLambda)) < 0.15 && Math.Abs(Math.Log10(rC / selectedRc)) < 0.15;
            if (selected && excluded) score = 1;
            sw.WriteLine($"{rC:E8},{lam:E8},{proxy:E8},{excluded},{selected}");
        }
        return score;
    }

    private static double WriteXrayProxy(string path, double lambda, double rC, double n)
    {
        double norm = lambda * n / (rC * rC);
        using var sw = new StreamWriter(path);
        sw.WriteLine("energy_keV,rate_proxy");
        double total = 0;
        for (double e = 1; e <= 50; e += 1)
        {
            double rate = norm * Math.Exp(-e / 12.0) / (e + 1);
            total += rate;
            sw.WriteLine($"{e:F1},{rate:E8}");
        }
        return total;
    }

    private static void WriteVisibilityBudget(string path, double lambda, double rC, double separation)
    {
        using var sw = new StreamWriter(path);
        sw.WriteLine("N,collapse_time_s,environment_decoherence_time_s,dominant_loss");
        foreach (double n in MathUtil.LogSpace(1, 1e24, 40))
        {
            double csl = 1.0 / Math.Max(CslRate(lambda, n, separation, rC), 1e-300);
            double env = 1e8 / Math.Pow(n, 0.7); // toy environmental loss
            string dom = csl < env ? "CSL" : "environment";
            sw.WriteLine($"{n:E8},{csl:E8},{env:E8},{dom}");
        }
    }

    private static (double Z, double DetectableBias) WriteBornBiasNull(string path, int trials, double bias, int seed)
    {
        var rng = new Random(seed);
        double p0 = 0.75;
        int zeros = 0;
        double sample = MathUtil.Clamp(p0 + bias, 0, 1);
        for (int i = 0; i < trials; i++) if (rng.NextDouble() < sample) zeros++;
        double z = (zeros - trials * p0) / Math.Sqrt(trials * p0 * (1 - p0));
        double detectable = 5 * Math.Sqrt(p0 * (1 - p0) / trials);
        using var sw = new StreamWriter(path);
        sw.WriteLine("trials,hypothetical_bias,p0_expected,p0_observed,z_score,five_sigma_detectable_bias");
        sw.WriteLine($"{trials},{bias:E8},{p0:F8},{zeros / (double)trials:F8},{z:F4},{detectable:E8}");
        return (z, detectable);
    }
}
