using System;
using System.Diagnostics;
using System.IO;

namespace SimulatorV85.Infrastructure.Interop;

public sealed class PythonProcessRunner : SimulatorV85.Application.Ports.IPythonRunner
{
    public int TryRun(string pythonExe, string scriptPath, string runDir)
    {
        if (string.IsNullOrWhiteSpace(pythonExe) || !File.Exists(scriptPath)) return -1;
        try
        {
            using var p = Process.Start(new ProcessStartInfo
            {
                FileName = pythonExe,
                Arguments = $"\"{scriptPath}\" \"{runDir}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            });
            if (p is null) return -1;
            p.WaitForExit();
            File.AppendAllText(Path.Combine(runDir, "analysis", "python_bridge.log"),
                $"$ {pythonExe} {scriptPath} {runDir}\nSTDOUT:\n{p.StandardOutput.ReadToEnd()}\nSTDERR:\n{p.StandardError.ReadToEnd()}\nEXIT:{p.ExitCode}\n\n");
            return p.ExitCode;
        }
        catch (Exception ex)
        {
            Directory.CreateDirectory(Path.Combine(runDir, "analysis"));
            File.AppendAllText(Path.Combine(runDir, "analysis", "python_bridge.log"), ex + Environment.NewLine);
            return -1;
        }
    }
}
