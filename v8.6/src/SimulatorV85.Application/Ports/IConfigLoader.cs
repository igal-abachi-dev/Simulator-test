using SimulatorV85.Application.Sweeps;

namespace SimulatorV85.Application.Ports;

public interface IConfigLoader
{
    Dictionary<string, double> LoadParameters(string? path);
    SweepDefinition LoadSweep(string path);
}
