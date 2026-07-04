using System.Text.Json;
using SimulatorV85.Domain;

namespace SimulatorV85.Application.Analysis;

public sealed class Analyzer
{
    private const int DefaultPermutationCount = 499;
    private const double DiscoveryQThreshold = 0.10;
    private const double MaxStatisticPThreshold = 0.10;

    public AnalysisResult Analyze(string runDir)
    {
        string obsPath = Path.Combine(runDir, "observables.csv");
        string scorePath = Path.Combine(runDir, "scores.csv");
        var obsRows = Csv.Read(obsPath);
        var scoreRows = Csv.Read(scorePath);
        var observables = obsRows.Select(Observation.FromRow).Where(x => x is not null).Cast<Observation>().ToArray();
        var moduleScores = scoreRows
            .GroupBy(r => r.GetValueOrDefault("module", "unknown"))
            .ToDictionary(g => g.Key, g => g.Select(r => Parse(r.GetValueOrDefault("score"))).Where(x => !double.IsNaN(x)).DefaultIfEmpty(0).Average());

        var correlations = ComputeCorrelationsWithNull(observables, DefaultPermutationCount, seed: 90210);
        double crossScore = ComputeCalibratedCrossScore(correlations);
        double moduleAverage = moduleScores.Count == 0 ? 0 : moduleScores.Values.Average();
        double overall = MathUtil.Clamp(0.85 * moduleAverage + 0.15 * crossScore, 0, 1);
        return new AnalysisResult(overall, moduleScores, correlations, observables, crossScore, DefaultPermutationCount);
    }

    public void Write(string runDir, AnalysisResult result)
    {
        string analysisDir = Path.Combine(runDir, "analysis");
        Directory.CreateDirectory(analysisDir);
        WriteJson(Path.Combine(analysisDir, "model_score.json"), result);
        WriteCorrelations(Path.Combine(analysisDir, "cross_module_correlations.csv"), result.Correlations);
        WriteMarkdown(Path.Combine(analysisDir, "anomaly_report.md"), result);
        WriteNullSummary(Path.Combine(analysisDir, "surrogate_null_summary.csv"), result.Correlations);
        WriteEnsembleSummary(Path.Combine(analysisDir, "ensemble_observable_summary.csv"), result.Observations);
    }

    private static List<CorrelationRecord> ComputeCorrelationsWithNull(IReadOnlyList<Observation> obs, int permutations, int seed)
    {
        var byCase = obs.GroupBy(o => o.CaseId).ToDictionary(g => g.Key, g => g.ToArray());
        var names = obs.Select(o => o.FullName).Distinct().OrderBy(x => x).ToArray();
        var raw = new List<PairWork>();

        for (int i = 0; i < names.Length; i++)
        for (int j = i + 1; j < names.Length; j++)
        {
            string a = names[i], b = names[j];
            if (a.Split('.')[0] == b.Split('.')[0]) continue;
            var xs = new List<double>();
            var ys = new List<double>();
            foreach (var kv in byCase)
            {
                var oa = kv.Value.FirstOrDefault(o => o.FullName == a);
                var ob = kv.Value.FirstOrDefault(o => o.FullName == b);
                if (oa is not null && ob is not null && IsFinite(oa.Value) && IsFinite(ob.Value))
                {
                    xs.Add(oa.Value);
                    ys.Add(ob.Value);
                }
            }
            if (xs.Count >= 5)
            {
                double r = MathUtil.Pearson(xs, ys);
                if (IsFinite(r)) raw.Add(new PairWork(a, b, xs.ToArray(), ys.ToArray(), r));
            }
        }

        if (raw.Count == 0) return new List<CorrelationRecord>();

        var rng = new Random(seed);
        var exceed = new int[raw.Count];
        var maxExceed = new int[raw.Count];
        double[] nullMax = new double[permutations];

        for (int p = 0; p < permutations; p++)
        {
            double maxAbs = 0;
            double[] permAbs = new double[raw.Count];
            for (int i = 0; i < raw.Count; i++)
            {
                var y = (double[])raw[i].Y.Clone();
                Shuffle(y, rng);
                double rr = MathUtil.Pearson(raw[i].X, y);
                double ar = Math.Abs(rr);
                permAbs[i] = ar;
                maxAbs = Math.Max(maxAbs, ar);
                if (ar >= Math.Abs(raw[i].R)) exceed[i]++;
            }
            nullMax[p] = maxAbs;
            for (int i = 0; i < raw.Count; i++)
                if (maxAbs >= Math.Abs(raw[i].R)) maxExceed[i]++;
        }

        double[] pvals = exceed.Select(x => (x + 1.0) / (permutations + 1.0)).ToArray();
        double[] qvals = BenjaminiHochberg(pvals);

        var records = new List<CorrelationRecord>();
        for (int i = 0; i < raw.Count; i++)
        {
            double maxP = (maxExceed[i] + 1.0) / (permutations + 1.0);
            bool significant = qvals[i] <= DiscoveryQThreshold && maxP <= MaxStatisticPThreshold;
            records.Add(new CorrelationRecord(
                raw[i].A, raw[i].B, raw[i].R, raw[i].X.Length,
                pvals[i], qvals[i], maxP, significant,
                InterpretCorrelation(raw[i].A, raw[i].B, raw[i].R, pvals[i], qvals[i], maxP, significant)));
        }

        return records
            .OrderByDescending(x => x.IsSignificant)
            .ThenBy(x => x.MaxStatisticPValue)
            .ThenByDescending(x => Math.Abs(x.R))
            .Take(250)
            .ToList();
    }

    private static double ComputeCalibratedCrossScore(IReadOnlyList<CorrelationRecord> records)
    {
        var discoveries = records.Where(x => x.IsSignificant).ToArray();
        if (discoveries.Length == 0) return 0;
        double strength = discoveries.Max(x => Math.Abs(x.R));
        double confidence = discoveries.Min(x => 1.0 - Math.Min(1, x.MaxStatisticPValue));
        return MathUtil.Clamp(strength * confidence, 0, 1);
    }

    private static double[] BenjaminiHochberg(double[] p)
    {
        int m = p.Length;
        var indexed = p.Select((value, index) => (value, index)).OrderBy(x => x.value).ToArray();
        var q = new double[m];
        double min = 1;
        for (int rank = m; rank >= 1; rank--)
        {
            var item = indexed[rank - 1];
            min = Math.Min(min, item.value * m / rank);
            q[item.index] = MathUtil.Clamp(min, 0, 1);
        }
        return q;
    }

    private static void Shuffle(double[] a, Random rng)
    {
        for (int i = a.Length - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (a[i], a[j]) = (a[j], a[i]);
        }
    }

    private static string InterpretCorrelation(string a, string b, double r, double p, double q, double maxP, bool significant)
    {
        string note = Math.Abs(r) > 0.8 ? "strong" : Math.Abs(r) > 0.5 ? "moderate" : "weak";
        string causal = IsExpectedBridge(a, b) ? "expected bridge pair" : "exploratory pair";
        if (!significant)
            return $"{note} {causal}, but NOT significant after permutation/BH/max-stat null calibration (p={p:F3}, q={q:F3}, maxP={maxP:F3}). Treat as null.";
        return $"{note} {causal}; survives permutation p-value, Benjamini-Hochberg FDR, and max-statistic family null (p={p:F3}, q={q:F3}, maxP={maxP:F3}).";
    }

    private static bool IsExpectedBridge(string a, string b)
    {
        string s = a + "|" + b;
        return s.Contains("inflation.peak_scalar_power") && s.Contains("nbody.structure_clumpiness")
            || s.Contains("inflation.peak_scalar_power") && s.Contains("nbody.halo_count_proxy")
            || s.Contains("nbody.baryon_dm_bias") && s.Contains("chemistry.prebiotic_feasibility_proxy")
            || s.Contains("chemistry.bond_dissociation_energy") && s.Contains("folding.foldability_score")
            || s.Contains("folding.foldability_score") && s.Contains("life.growth_exponent");
    }

    private static void WriteJson(string path, AnalysisResult result)
    {
        var obj = new
        {
            generatedAtUtc = DateTimeOffset.UtcNow,
            overallScore = result.OverallScore,
            modules = result.ModuleScores.ToDictionary(kv => kv.Key, kv => new { score = kv.Value, status = Status(kv.Value) }),
            crossModuleCorrelationScore = result.CrossModuleCorrelationScore,
            permutationCount = result.PermutationCount,
            nullCalibration = new
            {
                method = "pairwise permutation p-values + Benjamini-Hochberg q-values + max-statistic empirical family null",
                discoveryQThreshold = DiscoveryQThreshold,
                maxStatisticPThreshold = MaxStatisticPThreshold,
                note = "Max(|r|) alone is never used as evidence. Cross-score is zero unless a pair survives null calibration."
            },
            topCorrelations = result.Correlations.Take(25)
        };
        File.WriteAllText(path, JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void WriteCorrelations(string path, IReadOnlyList<CorrelationRecord> records)
    {
        File.WriteAllText(path, "observable_a,observable_b,pearson_r,n,permutation_p,bh_q,max_stat_p,is_significant,interpretation\n");
        foreach (var c in records)
            Csv.AppendLine(path, new[]
            {
                c.ObservableA, c.ObservableB, Csv.D(c.R), c.N.ToString(), Csv.D(c.PValue), Csv.D(c.QValue),
                Csv.D(c.MaxStatisticPValue), c.IsSignificant ? "1" : "0", c.Interpretation
            });
    }

    private static void WriteNullSummary(string path, IReadOnlyList<CorrelationRecord> records)
    {
        File.WriteAllText(path, "metric,value\n");
        Csv.AppendLine(path, new[] { "tested_cross_module_pairs", records.Count.ToString() });
        Csv.AppendLine(path, new[] { "significant_pairs_after_null", records.Count(x => x.IsSignificant).ToString() });
        Csv.AppendLine(path, new[] { "min_bh_q", Csv.D(records.Count == 0 ? 1 : records.Min(x => x.QValue)) });
        Csv.AppendLine(path, new[] { "min_max_stat_p", Csv.D(records.Count == 0 ? 1 : records.Min(x => x.MaxStatisticPValue)) });
    }


    private static void WriteEnsembleSummary(string path, IReadOnlyList<Observation> observations)
    {
        File.WriteAllText(path, "base_case,module,observable,n,mean,std\n");
        var groups = observations
            .Where(o => IsFinite(o.Value))
            .GroupBy(o => (BaseCase(o.CaseId), o.Module, o.Name))
            .OrderBy(g => g.Key.Item1).ThenBy(g => g.Key.Module).ThenBy(g => g.Key.Name);
        foreach (var g in groups)
        {
            var vals = g.Select(x => x.Value).ToArray();
            double mean = vals.Average();
            double std = vals.Length <= 1 ? 0 : Math.Sqrt(vals.Sum(x => (x - mean) * (x - mean)) / (vals.Length - 1));
            Csv.AppendLine(path, new[] { g.Key.Item1, g.Key.Module, g.Key.Name, vals.Length.ToString(), Csv.D(mean), Csv.D(std) });
        }
    }

    private static string BaseCase(string caseId)
    {
        int idx = caseId.IndexOf("_rep", StringComparison.OrdinalIgnoreCase);
        return idx < 0 ? caseId : caseId[..idx];
    }

    private static void WriteMarkdown(string path, AnalysisResult result)
    {
        using var sw = new StreamWriter(path);
        sw.WriteLine("# Anomaly Report");
        sw.WriteLine();
        sw.WriteLine($"Generated: {DateTimeOffset.UtcNow:O}");
        sw.WriteLine();
        sw.WriteLine("## Executive Summary");
        sw.WriteLine();
        sw.WriteLine($"Overall score: **{result.OverallScore:F3}** ({Status(result.OverallScore)})");
        sw.WriteLine($"Null-calibrated cross-module score: **{result.CrossModuleCorrelationScore:F3}**");
        sw.WriteLine($"Permutation count: **{result.PermutationCount}**");
        sw.WriteLine();
        sw.WriteLine("v8.5 does **not** use `max(|r|)` as evidence. Cross-module discoveries must survive permutation p-values, Benjamini-Hochberg FDR correction, and a max-statistic empirical null.");
        sw.WriteLine();
        sw.WriteLine("## Module Scores");
        sw.WriteLine();
        foreach (var kv in result.ModuleScores.OrderByDescending(x => x.Value)) sw.WriteLine($"- **{kv.Key}**: {kv.Value:F3} ({Status(kv.Value)})");
        sw.WriteLine();
        sw.WriteLine("## Top Observables");
        sw.WriteLine();
        foreach (var o in result.Observations.OrderByDescending(x => x.AnomalyScore).Take(20))
            sw.WriteLine($"- `{o.FullName}` = {o.Value:E4} {o.Unit}; score={o.AnomalyScore:F3}. {o.Interpretation}");
        sw.WriteLine();
        sw.WriteLine("## Cross-Module Pattern Search");
        sw.WriteLine();
        if (result.Correlations.Count == 0) sw.WriteLine("Not enough shared cases to compute cross-module correlations.");
        foreach (var c in result.Correlations.Take(30))
            sw.WriteLine($"- `{c.ObservableA}` vs `{c.ObservableB}`: r={c.R:F3}, n={c.N}, p={c.PValue:F3}, q={c.QValue:F3}, maxP={c.MaxStatisticPValue:F3}, significant={c.IsSignificant}. {c.Interpretation}");
        sw.WriteLine();
        sw.WriteLine("## Limitations");
        sw.WriteLine();
        sw.WriteLine("- This is not proof of the simulation hypothesis.");
        sw.WriteLine("- Correlations are only candidate patterns; v8.5 reduces false positives but does not replace real likelihoods or experiments.");
        sw.WriteLine("- Causal bridges are simplified parameter handoffs, not a full Boltzmann+hydro pipeline.");
        sw.WriteLine();
        sw.WriteLine("## Reproducibility");
        sw.WriteLine();
        sw.WriteLine("See `manifest.json`, `parameters.csv`, `observables.csv`, `scores.csv`, `cross_module_correlations.csv`, `surrogate_null_summary.csv`, and `ensemble_observable_summary.csv` in the run directory.");
    }

    private static string Status(double score) => score switch
    {
        >= 0.75 => "interesting",
        >= 0.45 => "watch",
        >= 0.15 => "weak/null",
        _ => "null"
    };

    private static bool IsFinite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
    private static double Parse(string? s) => double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double v) ? v : double.NaN;

    private sealed record PairWork(string A, string B, double[] X, double[] Y, double R);
}

public sealed record AnalysisResult(
    double OverallScore,
    IReadOnlyDictionary<string, double> ModuleScores,
    IReadOnlyList<CorrelationRecord> Correlations,
    IReadOnlyList<Observation> Observations,
    double CrossModuleCorrelationScore,
    int PermutationCount);

public sealed record CorrelationRecord(
    string ObservableA,
    string ObservableB,
    double R,
    int N,
    double PValue,
    double QValue,
    double MaxStatisticPValue,
    bool IsSignificant,
    string Interpretation);

public sealed record Observation(string CaseId, string Module, string Name, double Value, string Unit, double AnomalyScore, string Interpretation)
{
    public string FullName => Module + "." + Name;

    public static Observation? FromRow(Dictionary<string, string> row)
    {
        if (!row.TryGetValue("case_id", out var caseId)) return null;
        string module = row.GetValueOrDefault("module", "unknown");
        string name = row.GetValueOrDefault("observable", "unknown");
        double value = Parse(row.GetValueOrDefault("value"));
        double anomaly = Parse(row.GetValueOrDefault("anomaly_score"));
        return new Observation(caseId, module, name, value, row.GetValueOrDefault("unit", ""), anomaly, row.GetValueOrDefault("interpretation", ""));
    }

    private static double Parse(string? s) => double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double v) ? v : double.NaN;
}
