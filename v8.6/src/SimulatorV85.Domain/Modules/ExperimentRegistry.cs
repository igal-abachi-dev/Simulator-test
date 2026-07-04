using System;
using System.Collections.Generic;
using System.Linq;
using SimulatorV85.Domain;

namespace SimulatorV85.Domain.Modules;

public static class ExperimentRegistry
{
    public static IReadOnlyDictionary<string, IExperiment> Create()
    {
        IExperiment[] experiments =
        {
            new InflationExperiment(),
            new LifeExperiment(),
            new MeasurementExperiment(),
            new BlackHoleInformationExperiment(),
            new CosmologyExperiment(),
            new ObserverExperiment(),
            new FoldingExperiment(),
            new QuantumChemistryExperiment(),
            new CosmologyNBodyExperiment()
        };
        return experiments.ToDictionary(x => x.Name, x => x, StringComparer.OrdinalIgnoreCase);
    }
}
