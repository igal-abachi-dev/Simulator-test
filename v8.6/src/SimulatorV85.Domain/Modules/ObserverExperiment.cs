using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using SimulatorV85.Domain;

namespace SimulatorV85.Domain.Modules;

public sealed class ObserverExperiment : IExperiment
{
    public string Name => "observer";
    public string Description => "Observer-dependent measurement tests: non-standard Born bias, Wigner-friend toy correlations, and contextuality power analysis.";

    public ExperimentResult Run(ExperimentSpec spec, IArtifactSink artifacts, CancellationToken ct = default)
    {
        int trials = spec.GetInt("observer.trials", spec.GetInt("trials", 100_000));
        double bias = spec.Get("observer.bornBias", spec.Get("bornBias", 0.0));
        double contextual = spec.Get("observer.contextualCoupling", spec.Get("contextualCoupling", 0.0));
        double sigma = spec.Get("observer.confidenceSigma", spec.Get("confidenceSigma", 5.0));

        string nullPath = artifacts.OutputPath(Name, spec.CaseId, "observer_nonstandard_bias_null.csv");
        string powerPath = artifacts.OutputPath(Name, spec.CaseId, "observer_born_bias_power_analysis.csv");
        string wignerPath = artifacts.OutputPath(Name, spec.CaseId, "observer_wigner_friend_correlations.csv");
        string contextPath = artifacts.OutputPath(Name, spec.CaseId, "observer_contextuality_toy.csv");
        string summaryPath = artifacts.OutputPath(Name, spec.CaseId, "observer_summary.txt");

        var nullResult = WriteNonstandardBiasNull(nullPath, trials, bias, spec.Seed, sigma);
        double minDetectableAt1e6 = WriteBornBiasPowerAnalysis(powerPath, sigma);
        var wigner = WriteWignerFriendToy(wignerPath, contextual);
        double maxContextShift = WriteContextualityToy(contextPath, contextual);
        WriteSummary(summaryPath, nullResult, minDetectableAt1e6, wigner, maxContextShift, contextual);

        double zScoreInterest = MathUtil.Clamp(Math.Abs(nullResult.Z) / sigma, 0, 1);
        double wignerInterest = MathUtil.Clamp(Math.Max(0, wigner.SContextual - 2.0) / (2.0 * Math.Sqrt(2.0) - 2.0), 0, 1);
        double contextualInterest = MathUtil.Clamp(Math.Abs(maxContextShift) / 0.05, 0, 1);
        double score = MathUtil.Clamp(0.40 * zScoreInterest + 0.35 * wignerInterest + 0.25 * contextualInterest, 0, 1);

        var obs = new List<ObservableRecord>
        {
            new(Name, "nonstandard_born_bias_z", nullResult.Z, "sigma", zScoreInterest,
                "Z-score for a configured non-standard Born-rule probability shift.", spec.CaseId),
            new(Name, "five_sigma_detectable_bias", nullResult.DetectableBias, "probability", 0,
                "Smallest probability shift detectable at the configured confidence level and trial count.", spec.CaseId),
            new(Name, "wigner_friend_toy_s_standard", wigner.SStandard, "CHSH-like", MathUtil.Clamp(Math.Max(0, wigner.SStandard - 2.0) / 0.8284271247, 0, 1),
                "Quantum toy correlation benchmark; not a human-consciousness model.", spec.CaseId),
            new(Name, "wigner_friend_toy_s_contextual", wigner.SContextual, "CHSH-like", wignerInterest,
                "Observer-context toy correlation after adding the configured contextual coupling.", spec.CaseId),
            new(Name, "observer_contextuality_max_shift", maxContextShift, "probability", contextualInterest,
                "Largest probability shift induced by the toy observer-context coupling.", spec.CaseId),
            new(Name, "born_bias_power_min_detectable_at_1e6", minDetectableAt1e6, "probability", 0,
                "Reference sensitivity for one million trials.", spec.CaseId)
        };

        var warnings = new[]
        {
            "Observer-dependent outputs are non-standard-bias/null tests and Wigner-friend-style toy correlations.",
            "They do not model awareness as a physical force and do not prove observer-dependent reality."
        };

        return new ExperimentResult(artifacts.RunId, spec.CaseId, Name, "ObserverDependentMeasurement", spec.Parameters,
            obs, new[] { nullPath, powerPath, wignerPath, contextPath, summaryPath }, warnings, score);
    }

    private static (double Z, double DetectableBias, double Observed) WriteNonstandardBiasNull(
        string path, int trials, double bias, int seed, double sigma)
    {
        const double p0 = 0.5;
        var rng = new Random(seed);
        int hits = 0;
        double p = MathUtil.Clamp(p0 + bias, 0, 1);
        for (int i = 0; i < trials; i++) if (rng.NextDouble() < p) hits++;
        double observed = hits / (double)trials;
        double z = (hits - trials * p0) / Math.Sqrt(trials * p0 * (1 - p0));
        double detectable = sigma * Math.Sqrt(p0 * (1 - p0) / trials);

        using var sw = new StreamWriter(path);
        sw.WriteLine("trials,expected_p0,configured_bias,observed_p0,z_score,sigma_threshold,detectable_bias,detected");
        sw.WriteLine($"{trials},{p0:F8},{bias:E8},{observed:F8},{z:F6},{sigma:F3},{detectable:E8},{Math.Abs(z) >= sigma}");
        return (z, detectable, observed);
    }

    private static double WriteBornBiasPowerAnalysis(string path, double sigma)
    {
        const double p0 = 0.5;
        using var sw = new StreamWriter(path);
        sw.WriteLine("trials,effect_bias,expected_z_score,detectable_bias_at_sigma,has_power");
        int[] trialCounts = { 1_000, 10_000, 100_000, 1_000_000, 10_000_000 };
        double[] effects = { 1e-4, 3e-4, 1e-3, 3e-3, 1e-2 };
        double at1e6 = sigma * Math.Sqrt(p0 * (1 - p0) / 1_000_000);
        foreach (int n in trialCounts)
        {
            double detect = sigma * Math.Sqrt(p0 * (1 - p0) / n);
            foreach (double effect in effects)
            {
                double expectedZ = effect / Math.Sqrt(p0 * (1 - p0) / n);
                sw.WriteLine($"{n},{effect:E8},{expectedZ:F6},{detect:E8},{Math.Abs(expectedZ) >= sigma}");
            }
        }
        return at1e6;
    }

    private static (double SStandard, double SContextual) WriteWignerFriendToy(string path, double contextual)
    {
        double[] a = { 0.0, Math.PI / 4.0 };
        double[] b = { Math.PI / 8.0, -Math.PI / 8.0 };
        double EStandard(double x, double y) => -Math.Cos(2.0 * (x - y));
        double EContextual(double x, double y, int sign) => MathUtil.Clamp(EStandard(x, y) + sign * contextual * 0.05, -1.0, 1.0);

        using var sw = new StreamWriter(path);
        sw.WriteLine("term,setting_a_rad,setting_b_rad,E_standard,E_contextual,sign_in_s");
        var terms = new[]
        {
            ("E00", a[0], b[0], +1),
            ("E01", a[0], b[1], +1),
            ("E10", a[1], b[0], +1),
            ("E11", a[1], b[1], -1)
        };
        foreach (var (term, aa, bb, sign) in terms)
            sw.WriteLine($"{term},{aa:F8},{bb:F8},{EStandard(aa, bb):F8},{EContextual(aa, bb, sign):F8},{sign}");

        double sStandard = Math.Abs(EStandard(a[0], b[0]) + EStandard(a[0], b[1]) + EStandard(a[1], b[0]) - EStandard(a[1], b[1]));
        double sContext = Math.Abs(EContextual(a[0], b[0], +1) + EContextual(a[0], b[1], +1) + EContextual(a[1], b[0], +1) - EContextual(a[1], b[1], -1));
        sw.WriteLine($"S,,,,{sContext:F8},");
        sw.WriteLine($"S_standard,,,,{sStandard:F8},");
        return (sStandard, sContext);
    }

    private static double WriteContextualityToy(string path, double contextual)
    {
        using var sw = new StreamWriter(path);
        sw.WriteLine("context,measurement,p_standard,p_contextual,shift");
        string[] contexts = { "friend_inside_lab", "wigner_outside_lab", "joint_interference", "environment_decohered" };
        double max = 0;
        for (int i = 0; i < contexts.Length; i++)
        {
            double baseP = i switch
            {
                0 => 0.5,
                1 => 0.5,
                2 => 0.75,
                _ => 0.5
            };
            double shift = contextual * (i - 1.5) * 0.01;
            double pc = MathUtil.Clamp(baseP + shift, 0, 1);
            max = Math.Max(max, Math.Abs(pc - baseP));
            sw.WriteLine($"{contexts[i]},binary_outcome,{baseP:F8},{pc:F8},{pc - baseP:E8}");
        }
        return max;
    }

    private static void WriteSummary(
        string path,
        (double Z, double DetectableBias, double Observed) nullResult,
        double minDetectableAt1e6,
        (double SStandard, double SContextual) wigner,
        double maxContextShift,
        double contextual)
    {
        using var sw = new StreamWriter(path);
        sw.WriteLine("Observer-dependent module summary");
        sw.WriteLine($"nonstandard Born-bias z-score       : {nullResult.Z:F4}");
        sw.WriteLine($"configured p0 observed               : {nullResult.Observed:F8}");
        sw.WriteLine($"detectable bias for configured trials : {nullResult.DetectableBias:E8}");
        sw.WriteLine($"detectable bias at 1e6 trials         : {minDetectableAt1e6:E8}");
        sw.WriteLine($"Wigner-friend toy S standard          : {wigner.SStandard:F6}");
        sw.WriteLine($"Wigner-friend toy S contextual        : {wigner.SContextual:F6}");
        sw.WriteLine($"contextual coupling                   : {contextual:E8}");
        sw.WriteLine($"max contextual probability shift      : {maxContextShift:E8}");
        sw.WriteLine("Interpretation: these are null/power-analysis and foundations-toy outputs, not evidence that awareness changes collapse.");
    }
}
