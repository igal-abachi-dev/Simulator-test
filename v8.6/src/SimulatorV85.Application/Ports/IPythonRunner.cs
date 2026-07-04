namespace SimulatorV85.Application.Ports;

public interface IPythonRunner
{
    int TryRun(string pythonExe, string scriptPath, string runDir);
}
