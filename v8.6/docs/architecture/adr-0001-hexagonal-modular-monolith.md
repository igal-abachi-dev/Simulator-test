# ADR-0001: Hexagonal Modular Monolith

## Status
Accepted.

## Context
Simulator needs reproducible experiment runs, independent scientific modules, Python interop, and analysis artifacts. It does not need web services or distributed deployment.

## Decision
Use four projects:

1. `SimulatorV86.Domain` — contracts, modules, math helpers.
2. `SimulatorV86.Application` — use cases, sweeps, analysis, ports.
3. `SimulatorV86.Infrastructure` — file system, JSON config, Python process adapter.
4. `SimulatorV86.Cli` — System.CommandLine host and composition root.

## Consequences

Positive:

- Clear dependency direction.
- Easy to replace Python/plot/ML adapters.
- Easy to add tests around modules and sweep runner.
- Still simple enough for a research codebase.

Negative:

- More files than v8.
- Some simulation modules still write artifact files through an artifact path port. A future cleanup can introduce streaming artifact writers.
