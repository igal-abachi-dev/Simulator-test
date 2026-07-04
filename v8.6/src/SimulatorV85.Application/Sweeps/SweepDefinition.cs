using System.Text.Json;

namespace SimulatorV85.Application.Sweeps;

public sealed record SweepDefinition(
    int Cases,
    int Seed,
    string Strategy,
    IReadOnlyDictionary<string, SweepRange> Parameters,
    int Replicates = 1);

public sealed record SweepRange(double Min, double Max, bool Log, double[]? Values)
{
    public static SweepRange FromJson(JsonElement e)
    {
        if (e.ValueKind == JsonValueKind.Array)
            return new SweepRange(0, 0, false, e.EnumerateArray().Select(x => x.GetDouble()).ToArray());
        double min = e.TryGetProperty("min", out var mi) ? mi.GetDouble() : 0;
        double max = e.TryGetProperty("max", out var ma) ? ma.GetDouble() : min;
        bool log = e.TryGetProperty("log", out var lo) && lo.GetBoolean();
        return new SweepRange(min, max, log, null);
    }

    public double Sample(Random rng, int index, int total, string strategy)
    {
        if (Values is { Length: > 0 }) return Values[Math.Min(index, Values.Length - 1) % Values.Length];
        double u = strategy.Equals("grid", StringComparison.OrdinalIgnoreCase)
            ? (total <= 1 ? 0.5 : index / (total - 1.0))
            : rng.NextDouble();
        if (strategy.Equals("latin-hypercube", StringComparison.OrdinalIgnoreCase))
            u = (index + rng.NextDouble()) / Math.Max(total, 1);
        return Log ? Math.Pow(10, Math.Log10(Min) + u * (Math.Log10(Max) - Math.Log10(Min))) : Min + u * (Max - Min);
    }
}
