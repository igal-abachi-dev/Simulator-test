using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using SimulatorV85.Application.Ports;
using SimulatorV85.Domain;

namespace SimulatorV85.Infrastructure.Storage;

public sealed class FileResultStore : IResultStore
{
    private readonly FileRunWorkspace _context;
    private readonly object _gate = new();

    public FileResultStore(FileRunWorkspace context)
    {
        _context = context;
        InitCsv(Path.Combine(_context.RunDir, "parameters.csv"), "run_id,case_id,module,parameter,value");
        InitCsv(Path.Combine(_context.RunDir, "observables.csv"), "run_id,case_id,module,observable,value,unit,anomaly_score,interpretation");
        InitCsv(Path.Combine(_context.RunDir, "scores.csv"), "run_id,case_id,module,score,warnings,output_files");
    }

    public void Save(ExperimentResult result)
    {
        lock (_gate)
        {
            string parametersPath = Path.Combine(_context.RunDir, "parameters.csv");
            foreach (var kv in result.Parameters.OrderBy(x => x.Key))
            {
                Csv.AppendLine(parametersPath, new[]
                {
                    result.RunId, result.CaseId, result.Module, kv.Key, Csv.D(kv.Value)
                });
            }

            string observablesPath = Path.Combine(_context.RunDir, "observables.csv");
            foreach (var o in result.Observables)
            {
                Csv.AppendLine(observablesPath, new[]
                {
                    result.RunId, result.CaseId, result.Module, o.Name, Csv.D(o.Value), o.Unit,
                    Csv.D(o.AnomalyScore), o.Interpretation
                });
            }

            string scoresPath = Path.Combine(_context.RunDir, "scores.csv");
            Csv.AppendLine(scoresPath, new[]
            {
                result.RunId, result.CaseId, result.Module, Csv.D(result.Score),
                string.Join(" | ", result.Warnings), string.Join(" | ", result.OutputFiles)
            });

            string jsonPath = Path.Combine(_context.ModuleDir(result.Module, result.CaseId), "result.json");
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(jsonPath, JsonSerializer.Serialize(result, options));
        }
    }

    private static void InitCsv(string path, string header)
    {
        if (!File.Exists(path)) File.WriteAllText(path, header + Environment.NewLine);
    }
}
