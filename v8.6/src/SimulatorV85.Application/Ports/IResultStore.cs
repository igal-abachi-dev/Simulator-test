using SimulatorV85.Domain;

namespace SimulatorV85.Application.Ports;

public interface IResultStore
{
    void Save(ExperimentResult result);
}
