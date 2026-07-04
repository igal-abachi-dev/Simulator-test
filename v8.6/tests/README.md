# Simulator v8.6 Tests

Run from the repository root:

```bash
dotnet run --project tests/SimulatorV86.Tests
```

Current coverage focuses on the statistical layer that gates cross-module discoveries:

- Pearson known vectors
- Benjamini-Hochberg q-values
- pseudo-replication guard: 8 base cases × 3 replicates must test as n=8, not n=24

The next tests should cover the SIGW kernel regression and causal-bridge propagation.
