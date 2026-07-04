# Simulator v8.6 — Statistical Rigor Fix

Simulator is an observable-anomaly sandbox

v8.6 fixes the most important remaining issue in v8.5: **pseudo-replication in cross-module correlation testing**. Replicates now estimate within-case noise, but they no longer inflate the number of independent observations used by the null test.

v8.5 fixes the main scientific weakness identified in the v8.3 peer review: cross-module correlations are no longer treated as evidence just because they are large. The project now includes causal handoffs, empirical null calibration, ensemble replicates, and restored fidelity for the two most important regressions.

## What changed from v8.3

### 1. Causal physics bridge

The sweep runner now executes selected modules in a causal order and propagates observables downstream:

```text
inflation.peak_scalar_power
  -> nbody.initialAmplitudeFromInflation
nbody.structure / baryon observables
  -> chemistry.environmentFactor / temperatureFactor
chemistry.bond_dissociation_energy
  -> folding.hydrophobicAttractionFromChemistry
folding.foldability_score
  -> life.templateBiasFromFolding
```

This does not replace a real Boltzmann-transfer + hydrodynamics pipeline. It does stop the old `physics_bridge` sweep from being a purely parallel, non-causal correlation search.

### 2. Null-calibrated anomaly search

`Analyzer` now computes:

- pairwise permutation p-values;
- Benjamini-Hochberg FDR q-values;
- max-statistic empirical family-null p-values;
- `surrogate_null_summary.csv`;
- `ensemble_observable_summary.csv`.

The cross-module score is zero unless a candidate correlation survives all null filters.

### 3. SIGW fidelity restored

Inflation no longer draws `Omega_GW(f)` as a log-normal bump. It computes a lightweight convolution over the simulated scalar spectrum with a radiation-era resonance proxy.

### 4. RAF kinetic knockout restored

Life essentiality now runs kinetic knockouts for tested RAF reactions instead of using only structural/catalytic-use proxies.

### 5. Ensemble replicates

Sweep configs support:

```json
"replicates": 3
```

Case ids become `case_0000_rep00`, `case_0000_rep01`, etc. Analysis writes mean/std summaries by base case.

## Commands

```bash
dotnet run --project src/SimulatorV86.Cli -- simulate --module all --analyze
```

```bash
dotnet run --project src/SimulatorV86.Cli -- sweep \
  --module all \
  --config configs/sweeps/physics_bridge_demo.json \
  --parallel 12
```

```bash
dotnet run --project src/SimulatorV86.Cli -- analyze --run runs/<run_id>
```

Without Python:

```bash
dotnet run --project src/SimulatorV86.Cli -- sweep \
  --module all \
  --config configs/sweeps/physics_bridge_demo.json \
  --parallel 12 \
  --no-python
```

Run tests:

```bash
dotnet run --project tests/SimulatorV86.Tests
```
## Outputs

```text
runs/<run_id>/analysis/model_score.json
runs/<run_id>/analysis/anomaly_report.md
runs/<run_id>/analysis/cross_module_correlations.csv
runs/<run_id>/analysis/surrogate_null_summary.csv
runs/<run_id>/analysis/ensemble_observable_summary.csv
runs/<run_id>/analysis/aggregated_case_observables.csv
runs/<run_id>/analysis/plots/*.png
```

## What v8.6 still does not claim

- It does not prove the simulation hypothesis.
- It does not replace CLASS/CAMB, GADGET/AREPO, Pantheon+/DESI likelihoods, or real CSL likelihood recasts.
- It does not turn correlations into causal claims automatically.
- The SIGW absolute scale is a simulator-level observable, not a real detector likelihood.


v8.6 makes the anomaly layer much harder to fool. v9 should add real-data likelihoods and true transfer functions.
v8.5 makes the simulator harder to fool. v9 should add real-data likelihoods and true transfer functions.
