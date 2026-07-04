# Simulator v8.6 Completion Checklist

## Stage 1 — Experiment engine

- [x] Config-driven runs
- [x] `RunManifest`
- [x] `IExperiment`
- [x] `ExperimentSpec`
- [x] `ExperimentResult`
- [x] `ObservableRecord`
- [x] `runs/<run_id>` workspace
- [x] `parameters.csv`
- [x] `observables.csv`
- [x] `scores.csv`
- [x] Parallel sweep runner using independent parameter cases
- [x] Grid, random, and Latin-hypercube sampling
- [x] Failure handling per case/module
- [x] Python ML-surrogate adapter

## Stage 2 — New modules

- [x] Black-hole Page curve / information toy model
- [x] Dark-energy and fine-tuning sensitivity module
- [x] Observer-dependent measurement module as non-standard-bias tests

## Stage 3 — Testable predictions

- [x] Inflation: scalar peak and SIGW proxy `Omega_GW(f)`
- [x] Life: RAF growth exponent and ignition probability
- [x] Measurement: CSL visibility and X-ray proxy constraints
- [x] Cosmology: dark-energy distance residuals and sensitivity curves
- [x] Black hole: Page time, unitarity residual, information recovery
- [x] Observer: Born-bias null test, Wigner-friend toy correlations, contextuality toy table

## Stage 4 — Analysis layer

- [x] `anomaly_report.md`
- [x] `model_score.json`
- [x] `cross_module_correlations.csv`
- [x] `plots/*.png`
- [x] Cross-module correlation score
- [x] Required example: `inflation.peak_scalar_power` vs `life.growth_exponent`

## Observer milestone

- [x] Rename consciousness-null output to non-standard observer bias output
- [x] Add Wigner-friend toy correlation table
- [x] Add Born-bias power analysis
- [x] Add `observer_contextuality_toy.csv`

## Intentionally deferred

- [ ] Full DESI likelihood loader
- [ ] Full LISA/PTA sensitivity-curve matching
- [ ] Real CSL likelihood recast
- [ ] GPU acceleration
- [ ] ONNX Runtime inference adapter
- [ ] SQLite/DuckDB result store
