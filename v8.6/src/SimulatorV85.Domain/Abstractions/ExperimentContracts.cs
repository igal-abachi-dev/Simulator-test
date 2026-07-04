using System;
using System.Collections.Generic;
using System.Threading;

namespace SimulatorV85.Domain;

public interface IExperiment
{
    string Name { get; }
    string Description { get; }
    ExperimentResult Run(ExperimentSpec spec, IArtifactSink artifacts, CancellationToken ct = default);
}

public interface IArtifactSink
{
    string RunId { get; }
    string RunDir { get; }
    string AnalysisDir { get; }
    string OutputPath(string module, string caseId, string fileName);
}

public sealed record ExperimentSpec(
    string CaseId,
    int Seed,
    IReadOnlyDictionary<string, double> Parameters,
    bool WriteSeries = true)
{
    public double Get(string key, double fallback)
        => Parameters.TryGetValue(key, out double value) ? value : fallback;

    public int GetInt(string key, int fallback)
        => Parameters.TryGetValue(key, out double value) ? (int)Math.Round(value) : fallback;
}

public sealed record ObservableRecord(
    string Module,
    string Name,
    double Value,
    string Unit,
    double AnomalyScore,
    string Interpretation,
    string CaseId);

public sealed record ExperimentResult(
    string RunId,
    string CaseId,
    string Module,
    string Model,
    IReadOnlyDictionary<string, double> Parameters,
    IReadOnlyList<ObservableRecord> Observables,
    IReadOnlyList<string> OutputFiles,
    IReadOnlyList<string> Warnings,
    double Score);

public sealed record RunManifest(
    string RunId,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? FinishedAtUtc,
    string Command,
    string DotnetTarget,
    string WorkingDirectory,
    string? GitCommit,
    int Cases,
    IReadOnlyList<string> Modules,
    IReadOnlyList<string> Notes);
