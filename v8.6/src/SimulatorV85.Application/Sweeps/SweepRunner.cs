using System.Collections.Concurrent;
using SimulatorV85.Domain;

namespace SimulatorV85.Application.Sweeps;

public sealed class SweepRunner
{
    private readonly IReadOnlyDictionary<string, IExperiment> _experiments;

    private static readonly string[] CausalOrder =
    {
        "inflation",
        "nbody",
        "cosmology",
        "chemistry",
        "folding",
        "life",
        "measurement",
        "observer",
        "blackhole"
    };

    public SweepRunner(IReadOnlyDictionary<string, IExperiment> experiments)
    {
        _experiments = experiments;
    }

    public async Task<IReadOnlyList<ExperimentResult>> RunAsync(
        string module,
        SweepDefinition sweep,
        IArtifactSink artifacts,
        int parallel,
        CancellationToken ct = default)
    {
        var selected = Select(module).ToArray();
        var cases = BuildCases(sweep);
        var bag = new ConcurrentBag<ExperimentResult>();
        var options = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, parallel), CancellationToken = ct };

        await Parallel.ForEachAsync(cases, options, async (spec, token) =>
        {
            foreach (var result in RunCaseCausally(spec, selected, artifacts, token)) bag.Add(result);
            await Task.CompletedTask;
        });

        return bag.OrderBy(x => x.CaseId).ThenBy(x => ModuleRank(x.Module)).ToArray();
    }

    public IReadOnlyList<ExperimentResult> RunSingle(string module, Dictionary<string, double> parameters, IArtifactSink artifacts)
    {
        var spec = new ExperimentSpec("case_0000", parameters.TryGetValue("seed", out double s) ? (int)s : 12345, parameters);
        return RunCaseCausally(spec, Select(module).ToArray(), artifacts).ToArray();
    }

    private IEnumerable<ExperimentResult> RunCaseCausally(ExperimentSpec initial, IReadOnlyList<IExperiment> selected, IArtifactSink artifacts, CancellationToken ct = default)
    {
        var ordered = selected.OrderBy(x => ModuleRank(x.Name)).ToArray();
        var shared = new Dictionary<string, double>(initial.Parameters, StringComparer.OrdinalIgnoreCase);
        var results = new List<ExperimentResult>();

        foreach (var exp in ordered)
        {
            ct.ThrowIfCancellationRequested();
            var spec = new ExperimentSpec(initial.CaseId, initial.Seed, shared, initial.WriteSeries);
            try
            {
                var result = exp.Run(spec, artifacts, ct);
                results.Add(result);
                PropagateObservables(result, shared);
            }
            catch (Exception ex)
            {
                results.Add(new ExperimentResult(
                    artifacts.RunId, initial.CaseId, exp.Name, "failed", shared,
                    new[] { new ObservableRecord(exp.Name, "failed", 1, "bool", 1, ex.Message, initial.CaseId) },
                    Array.Empty<string>(), new[] { ex.ToString() }, 1));
            }
        }
        return results;
    }

    private static void PropagateObservables(ExperimentResult result, Dictionary<string, double> p)
    {
        double? Obs(string name) => result.Observables.FirstOrDefault(o => o.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value;

        if (result.Module.Equals("inflation", StringComparison.OrdinalIgnoreCase))
        {
            double peak = Obs("peak_scalar_power") ?? 2.1e-9;
            double omega = Obs("sigw_peak_omega") ?? 0;
            double ratio = Math.Max(1.0, peak / 2.1e-9);
            // A causal handoff: the scalar spectrum controls the initial structure amplitude.
            p["bridge.inflation.peak_scalar_power"] = peak;
            p["bridge.inflation.sigw_peak_omega"] = omega;
            p["nbody.initialAmplitudeFromInflation"] = MathUtil.Clamp(0.025 + 0.028 * Math.Log10(ratio), 0.015, 0.22);
            p["cosmology.primordialFeatureStrength"] = MathUtil.Clamp(Math.Log10(ratio) / 8.0, 0, 1);
        }
        else if (result.Module.Equals("nbody", StringComparison.OrdinalIgnoreCase))
        {
            double clump = Obs("structure_clumpiness") ?? 0;
            double halos = Obs("halo_count_proxy") ?? 0;
            double bias = Obs("baryon_dm_bias") ?? 0;
            p["bridge.nbody.structure_clumpiness"] = clump;
            p["bridge.nbody.halo_count_proxy"] = halos;
            p["bridge.nbody.baryon_dm_bias"] = bias;
            p["chemistry.environmentFactor"] = MathUtil.Clamp(0.75 + 0.20 * Math.Log10(1 + clump) + 0.08 * Math.Abs(bias), 0.55, 1.45);
            p["chemistry.temperatureFactor"] = MathUtil.Clamp(1.0 + 0.10 * Math.Log10(1 + halos), 0.75, 1.6);
        }
        else if (result.Module.Equals("chemistry", StringComparison.OrdinalIgnoreCase))
        {
            double bond = Obs("bond_dissociation_energy") ?? 4.5;
            double feasibility = Obs("prebiotic_feasibility_proxy") ?? 0.5;
            p["bridge.chemistry.bond_dissociation_energy"] = bond;
            p["bridge.chemistry.prebiotic_feasibility_proxy"] = feasibility;
            p["folding.hydrophobicAttractionFromChemistry"] = MathUtil.Clamp(0.55 + 0.16 * bond, 0.4, 2.2);
            p["life.chemicalFeasibilityBoost"] = MathUtil.Clamp(0.65 + 0.70 * feasibility, 0.65, 1.35);
        }
        else if (result.Module.Equals("folding", StringComparison.OrdinalIgnoreCase))
        {
            double fold = Obs("foldability_score") ?? 0;
            p["bridge.folding.foldability_score"] = fold;
            p["life.templateBiasFromFolding"] = MathUtil.Clamp(0.10 + 0.75 * fold, 0.05, 0.95);
        }
    }

    private IEnumerable<IExperiment> Select(string module)
    {
        if (module.Equals("all", StringComparison.OrdinalIgnoreCase)) return _experiments.Values;
        if (_experiments.TryGetValue(module, out var exp)) return new[] { exp };
        throw new ArgumentException($"Unknown module '{module}'. Available: {string.Join(", ", _experiments.Keys)}");
    }

    private static IReadOnlyList<ExperimentSpec> BuildCases(SweepDefinition sweep)
    {
        var specs = new List<ExperimentSpec>();
        var master = new Random(sweep.Seed);
        for (int i = 0; i < sweep.Cases; i++)
        {
            var rng = new Random(master.Next());
            var baseParams = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in sweep.Parameters) baseParams[kv.Key] = kv.Value.Sample(rng, i, sweep.Cases, sweep.Strategy);
            baseParams["caseIndex"] = i;
            for (int rep = 0; rep < Math.Max(1, sweep.Replicates); rep++)
            {
                var p = new Dictionary<string, double>(baseParams, StringComparer.OrdinalIgnoreCase);
                int seed = sweep.Seed + i * 1009 + rep;
                p["seed"] = seed;
                p["replicate"] = rep;
                string caseId = sweep.Replicates <= 1 ? $"case_{i:0000}" : $"case_{i:0000}_rep{rep:00}";
                specs.Add(new ExperimentSpec(caseId, seed, p, WriteSeries: i == 0 && rep == 0));
            }
        }
        return specs;
    }

    private static int ModuleRank(string module)
    {
        for (int i = 0; i < CausalOrder.Length; i++)
            if (CausalOrder[i].Equals(module, StringComparison.OrdinalIgnoreCase)) return i;
        return 999;
    }
}
