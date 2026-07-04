using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using SimulatorV85.Domain;

namespace SimulatorV85.Domain.Modules;

public sealed class LifeExperiment : IExperiment
{
    public string Name => "life";
    public string Description => "RAF structure plus kinetic growth exponent and structural-vs-kinetic essentiality.";

    public ExperimentResult Run(ExperimentSpec spec, IArtifactSink artifacts, CancellationToken ct = default)
    {
        int maxLen = spec.GetInt("life.maxLen", spec.GetInt("maxLen", 5));
        double chemistryBoost = MathUtil.Clamp(spec.Get("life.chemicalFeasibilityBoost", 1.0), 0.25, 2.0);
        double catalysis = spec.Get("life.catalysis", spec.Get("catalysis", 0.010)) * chemistryBoost;
        double templateBias = spec.Get("life.templateBiasFromFolding", spec.Get("life.templateBias", spec.Get("templateBias", 0.35)));
        int seed = spec.Seed;
        var rng = new Random(seed);
        var chem = Chem.Build(maxLen);
        var cats = AssignCatalysis(chem, catalysis, templateBias, rng);
        var raf = MaxRaf(Enumerable.Range(0, chem.Rxn.Length), chem, cats);

        string growthPath = artifacts.OutputPath(Name, spec.CaseId, "life_growth_exponent.csv");
        string essentialPath = artifacts.OutputPath(Name, spec.CaseId, "life_structural_vs_kinetic_essentiality.csv");
        string summaryPath = artifacts.OutputPath(Name, spec.CaseId, "life_summary.txt");

        var ignition = EstimateGrowth(raf, chem, cats, seed + 99, growthPath);
        double structuralScore = MathUtil.Clamp(raf.Count / 30.0, 0, 1);
        double growthScore = MathUtil.Clamp((ignition.GrowthExponent + 0.02) / 0.08, 0, 1);
        double score = 0.45 * structuralScore + 0.55 * growthScore;
        int essentialMismatch = WriteEssentiality(raf, chem, cats, essentialPath, ignition.BaseGrowthExponent);

        File.WriteAllText(summaryPath,
            $"RAF reactions: {raf.Count}\n" +
            $"Growth exponent: {ignition.GrowthExponent:E4}\n" +
            $"Ignition probability proxy: {ignition.IgnitionProbability:F4}\n" +
            $"Structural/kinetic mismatch count: {essentialMismatch}\n" +
            "Interpretation: structural RAF existence is insufficient; positive kinetic growth is the stronger signal.\n");

        var obs = new List<ObservableRecord>
        {
            new(Name, "max_raf_size", raf.Count, "reactions", structuralScore,
                "Size of the maximum reflexively-autocatalytic food-generated set.", spec.CaseId),
            new(Name, "growth_exponent", ignition.GrowthExponent, "1/time", growthScore,
                "Early autocatalytic growth exponent from kinetic ignition proxy.", spec.CaseId),
            new(Name, "ignition_probability", ignition.IgnitionProbability, "probability", ignition.IgnitionProbability,
                "Probability-like score that the network ignites dynamically from food.", spec.CaseId),
            new(Name, "structural_kinetic_mismatch", essentialMismatch, "reactions", MathUtil.Clamp(essentialMismatch / 10.0, 0, 1),
                "Reactions that differ between structural and kinetic essentiality.", spec.CaseId),
            new(Name, "catalysis", catalysis, "probability", 0,
                "Effective catalysis probability after optional chemistry feasibility handoff.", spec.CaseId),
            new(Name, "template_bias", templateBias, "dimensionless", 0,
                "Template bias, optionally propagated from folding foldability in causal bridge sweeps.", spec.CaseId)
        };

        return new ExperimentResult(artifacts.RunId, spec.CaseId, Name, "RAF+growth-exponent", spec.Parameters,
            obs, new[] { growthPath, essentialPath, summaryPath }, Array.Empty<string>(), score);
    }

    private static (double GrowthExponent, double IgnitionProbability, double BaseGrowthExponent) EstimateGrowth(
        HashSet<int> raf, Chem c, List<int>[] cats, int seed, string? outputPath)
    {
        if (raf.Count == 0)
        {
            if (outputPath is not null) File.WriteAllText(outputPath, "t,raf_population,log_population\n0,0,0\n");
            return (-0.05, 0, -0.05);
        }

        var rng = new Random(seed);
        var species = new HashSet<int>(c.FoodIds);
        foreach (int r in raf)
        {
            var (a, b, p) = c.Rxn[r]; species.Add(a); species.Add(b); species.Add(p);
        }
        var nonFood = species.Where(x => !c.FoodSet.Contains(x)).ToArray();
        var pop = nonFood.ToDictionary(x => x, _ => 0.2 + rng.NextDouble() * 0.1);

        double food = 400, kCat = 2e-5, kLeak = 1e-7, dilution = 0.018;
        var series = new List<(double t, double y)>();
        for (int step = 0; step <= 120; step++)
        {
            double t = step * 2.0;
            double total = pop.Values.Sum();
            series.Add((t, total));
            var delta = nonFood.ToDictionary(x => x, _ => 0.0);
            foreach (int ri in raf)
            {
                var (a, b, p) = c.Rxn[ri];
                double ca = c.FoodSet.Contains(a) ? food : pop.GetValueOrDefault(a);
                double cb = c.FoodSet.Contains(b) ? food : pop.GetValueOrDefault(b);
                double catalystMass = cats[ri]?.Where(m => pop.ContainsKey(m)).Sum(m => pop[m]) ?? 0;
                double flux = ca * cb * (kLeak + kCat * catalystMass) / (1.0 + ca + cb);
                if (delta.ContainsKey(p)) delta[p] += flux;
            }
            foreach (int s in nonFood)
            {
                pop[s] = Math.Max(0, pop[s] + 2.0 * (delta[s] - dilution * pop[s]));
            }
        }

        if (outputPath is not null)
        {
            using var sw = new StreamWriter(outputPath);
            sw.WriteLine("t,raf_population,log_population");
            foreach (var (t, y) in series) sw.WriteLine($"{t:F2},{y:E8},{Math.Log(Math.Max(y, 1e-12)):E8}");
        }

        var fit = series.Where(x => x.t >= 10 && x.t <= 90).ToArray();
        double growth = LinearSlope(fit.Select(x => x.t).ToArray(), fit.Select(x => Math.Log(Math.Max(x.y, 1e-12))).ToArray());
        double pIgnite = MathUtil.Sigmoid((growth - 0.005) / 0.01);
        return (growth, pIgnite, growth);
    }

    private static int WriteEssentiality(HashSet<int> raf, Chem c, List<int>[] cats, string path, double baseGrowth)
    {
        int mismatch = 0;
        using var sw = new StreamWriter(path);
        sw.WriteLine("reaction,structurally_essential,kinetically_essential,product");
        int limit = 0;
        foreach (int r in raf.OrderBy(x => x))
        {
            bool structural = MaxRaf(raf.Where(x => x != r), c, cats).Count == 0;
            var knocked = new HashSet<int>(raf.Where(x => x != r));
            double knockoutGrowth = EstimateGrowth(knocked, c, cats, seed: 9917 + r, outputPath: null).GrowthExponent;
            bool kinetic = baseGrowth > 0 && (baseGrowth - knockoutGrowth) > Math.Max(0.003, 0.35 * Math.Abs(baseGrowth));
            int product = c.Rxn[r].p;
            if (structural != kinetic) mismatch++;
            sw.WriteLine($"{r},{structural},{kinetic},{c.Mol[product]}");
            if (++limit > 80) break;
        }
        return mismatch;
    }

    private static double LinearSlope(IReadOnlyList<double> x, IReadOnlyList<double> y)
    {
        double mx = x.Average(), my = y.Average(), num = 0, den = 0;
        for (int i = 0; i < x.Count; i++) { num += (x[i] - mx) * (y[i] - my); den += (x[i] - mx) * (x[i] - mx); }
        return den <= 0 ? 0 : num / den;
    }

    private static List<int>[] AssignCatalysis(Chem c, double p, double templateBias, Random rng)
    {
        var per = new List<int>[c.Rxn.Length];
        for (int ri = 0; ri < c.Rxn.Length; ri++)
        {
            List<int>? cats = null;
            string product = c.Mol[c.Rxn[ri].p];
            for (int m = 0; m < c.Mol.Length; m++)
            {
                double pp = p;
                if (SharesTemplate(c.Mol[m], product)) pp *= 1 + templateBias;
                if (rng.NextDouble() < Math.Min(pp, 1)) (cats ??= new List<int>()).Add(m);
            }
            per[ri] = cats ?? new List<int>();
        }
        return per;
    }

    private static bool SharesTemplate(string a, string b)
        => a.Length >= 2 && b.Contains(a[..Math.Min(a.Length, 3)], StringComparison.Ordinal);

    private static HashSet<int> MaxRaf(IEnumerable<int> candidate, Chem c, List<int>[] cats)
    {
        var rset = new HashSet<int>(candidate);
        while (true)
        {
            var producible = new HashSet<int>(c.FoodIds);
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (int ri in rset)
                {
                    var (a, b, p) = c.Rxn[ri];
                    if (producible.Contains(a) && producible.Contains(b) && producible.Add(p)) changed = true;
                }
            }
            var next = new HashSet<int>();
            foreach (int ri in rset)
            {
                var (a, b, _) = c.Rxn[ri];
                if (producible.Contains(a) && producible.Contains(b) && cats[ri].Any(producible.Contains)) next.Add(ri);
            }
            if (next.Count == rset.Count) return next;
            rset = next;
        }
    }

    private sealed class Chem
    {
        public required string[] Mol { get; init; }
        public required int[] FoodIds { get; init; }
        public required HashSet<int> FoodSet { get; init; }
        public required (int a, int b, int p)[] Rxn { get; init; }

        public static Chem Build(int maxLen)
        {
            var mols = new List<string>();
            for (int len = 1; len <= maxLen; len++)
            for (int v = 0; v < (1 << len); v++)
            {
                var sb = new StringBuilder(len);
                for (int bit = len - 1; bit >= 0; bit--) sb.Append(((v >> bit) & 1) == 1 ? '1' : '0');
                mols.Add(sb.ToString());
            }
            var idx = mols.Select((m, i) => (m, i)).ToDictionary(x => x.m, x => x.i);
            var food = mols.Where(m => m.Length <= 2).Select(m => idx[m]).ToArray();
            var rxn = new List<(int a, int b, int p)>();
            foreach (string a in mols) foreach (string b in mols)
                if (a.Length + b.Length <= maxLen) rxn.Add((idx[a], idx[b], idx[a + b]));
            return new Chem { Mol = mols.ToArray(), FoodIds = food, FoodSet = new HashSet<int>(food), Rxn = rxn.ToArray() };
        }
    }
}
