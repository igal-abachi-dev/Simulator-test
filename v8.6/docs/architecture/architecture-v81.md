# Architecture v8.6

## Decision

Use a **hexagonal modular monolith**.

## Why not many projects per feature?

The scientific modules are still small. A project per module would add ceremony without enough benefit. Instead, each module is a vertical slice inside `SimulatorV86.Domain/Modules`.

## Why not pure Clean Architecture / DDD?

Classic Clean Architecture and DDD are useful for business domains with aggregates, policies, and transactional boundaries. This project is a computational research tool. The important boundaries are not aggregates; they are:

- experiment contracts
- artifact storage
- config loading
- sweep execution
- analysis/reporting
- Python/ML/plot adapters

So hexagonal architecture is the better fit.

## Dependency direction

```text
SimulatorV86.Cli
  -> SimulatorV86.Infrastructure
  -> SimulatorV86.Application
  -> SimulatorV86.Domain
```

The domain exposes ports such as `IArtifactSink`. Infrastructure implements them. The CLI is the composition root.

## Vertical slices

Scientific modules remain vertical and isolated:

```text
Domain/Modules/InflationExperiment.cs
Domain/Modules/LifeExperiment.cs
Domain/Modules/MeasurementExperiment.cs
Domain/Modules/BlackHoleInformationExperiment.cs
Domain/Modules/CosmologyExperiment.cs
Domain/Modules/ObserverExperiment.cs
```

A future v8.6 can move each module into its own project only if module size or dependencies justify it.
