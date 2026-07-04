using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using SimulatorV85.Application.Ports;
using SimulatorV85.Application.Sweeps;

namespace SimulatorV85.Infrastructure.Configuration;

public sealed class JsonConfigLoader : IConfigLoader
{
    public Dictionary<string, double> LoadParameters(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return new Dictionary<string, double>();
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var dict = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        if (doc.RootElement.TryGetProperty("parameters", out var parameters))
            AddNumbers(parameters, dict, prefix: null);
        else
            AddNumbers(doc.RootElement, dict, prefix: null);
        return dict;
    }

    public SweepDefinition LoadSweep(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        int cases = root.TryGetProperty("cases", out var c) ? c.GetInt32() : 30;
        int seed = root.TryGetProperty("seed", out var s) ? s.GetInt32() : 12345;
        string strategy = root.TryGetProperty("strategy", out var st) ? st.GetString() ?? "random" : "random";
        int replicates = root.TryGetProperty("replicates", out var reps) ? Math.Max(1, reps.GetInt32()) : 1;
        var ranges = new Dictionary<string, SweepRange>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("parameters", out var ps))
        {
            foreach (var p in ps.EnumerateObject()) ranges[p.Name] = SweepRange.FromJson(p.Value);
        }
        return new SweepDefinition(cases, seed, strategy, ranges, replicates);
    }

    private static void AddNumbers(JsonElement element, Dictionary<string, double> dict, string? prefix)
    {
        if (element.ValueKind != JsonValueKind.Object) return;
        foreach (var prop in element.EnumerateObject())
        {
            string key = string.IsNullOrWhiteSpace(prefix) ? prop.Name : prefix + "." + prop.Name;
            if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetDouble(out double d)) dict[key] = d;
            else if (prop.Value.ValueKind == JsonValueKind.Object) AddNumbers(prop.Value, dict, key);
        }
    }
}

