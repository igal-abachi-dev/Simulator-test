# Simulator v8.6 Technical Report

v8.6 is an architecture refactor, not a new physics-claim release. The scientific models from v8 are preserved, but the code is reorganized into a hexagonal modular monolith.

## Main improvement

The core scientific logic is now separated from infrastructure concerns:

- CLI parsing: `SimulatorV86.Cli`
- Use cases and analysis: `SimulatorV86.Application`
- Scientific contracts and modules: `SimulatorV86.Domain`
- File/JSON/Python adapters: `SimulatorV86.Infrastructure`

## Why this matters

Future modules such as black-hole information, dark-energy residuals, and observer-dependent measurement tests can be added without changing the host or analysis pipeline.
