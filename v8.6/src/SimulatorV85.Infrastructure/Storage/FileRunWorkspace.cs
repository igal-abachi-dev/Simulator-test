using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using SimulatorV85.Domain;

namespace SimulatorV85.Infrastructure.Storage;

public sealed class FileRunWorkspace : IArtifactSink
{
    public string RunId { get; }
    public string RootDir { get; }
    public string RunDir { get; }

    public FileRunWorkspace(string runId, string rootDir = "runs")
    {
        RunId = runId;
        RootDir = Path.GetFullPath(rootDir);
        RunDir = Path.Combine(RootDir, runId);
        Directory.CreateDirectory(RunDir);
        Directory.CreateDirectory(Path.Combine(RunDir, "outputs"));
        Directory.CreateDirectory(Path.Combine(RunDir, "analysis"));
    }

    public string ModuleDir(string module, string caseId)
    {
        string dir = Path.Combine(RunDir, "outputs", Safe(module), Safe(caseId));
        Directory.CreateDirectory(dir);
        return dir;
    }

    public string OutputPath(string module, string caseId, string fileName)
        => Path.Combine(ModuleDir(module, caseId), fileName);

    public string AnalysisDir
    {
        get
        {
            string dir = Path.Combine(RunDir, "analysis");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public void WriteManifest(RunManifest manifest)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(Path.Combine(RunDir, "manifest.json"), JsonSerializer.Serialize(manifest, options));
    }

    public static string NewRunId() => DateTimeOffset.UtcNow.ToString("yyyyMMdd_HHmmss");

    public static string? TryGetGitCommit()
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "rev-parse --short HEAD",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            });
            if (p is null) return null;
            p.WaitForExit(1000);
            return p.ExitCode == 0 ? p.StandardOutput.ReadToEnd().Trim() : null;
        }
        catch { return null; }
    }

    private static string Safe(string value)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
        return value.Replace(' ', '_');
    }
}
