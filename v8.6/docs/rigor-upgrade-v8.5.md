# Simulator v8.6 — Rigor Upgrade

v8.6 is not a new-physics release. It is a correction release that makes the anomaly pipeline harder to fool.

## What was wrong in v8.3

The `physics_bridge_demo` sweep sampled many parameters and ran modules independently. The analyzer then selected the largest absolute cross-module Pearson correlation. With many observable pairs and a small number of cases, this creates false positives by construction.

## What v8.6 changes

1. **Causal bridge runner**
   - `inflation.peak_scalar_power` propagates into `nbody.initialAmplitudeFromInflation`.
   - N-body structure observables propagate into chemistry environment factors.
   - Chemistry bond observables propagate into folding attraction and life feasibility.
   - Folding foldability propagates into the RAF template-bias proxy.

2. **Null-calibrated anomaly search**
   - Pairwise permutation p-values.
   - Benjamini-Hochberg FDR q-values.
   - Max-statistic empirical family null for the largest observed |r|.
   - Cross-module score is zero unless at least one pair survives all filters.

3. **Restored scientific fidelity**
   - SIGW output is now a lightweight convolution over the simulated scalar spectrum, not a drawn log-normal bump.
   - Life essentiality now uses kinetic knockout growth tests for the tested RAF reactions instead of a structural-use proxy.

## Still intentionally not included

- Full Boltzmann transfer functions from CLASS/CAMB.
- Real Pantheon+/DESI likelihoods.
- Real hydrodynamics/SPH/moving-mesh baryons.
- Official SIGW likelihoods.

Those belong in v9.
