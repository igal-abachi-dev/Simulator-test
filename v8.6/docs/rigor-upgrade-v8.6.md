# v8.6 Rigor Upgrade

v8.6 is a statistical rigor release.

## Fixed

1. **Pseudo-replication**
   - Raw replicate rows are no longer independent correlation points.
   - Correlations run on base-case means in `aggregated_case_observables.csv`.
   - `cross_module_correlations.csv` now reports `independent_base_cases`.

2. **Shared-permutation max-stat null**
   - One shared case-label permutation is used across all observable pairs per iteration.
   - This preserves the dependence structure among observables better than independent pairwise shuffling.

3. **Expected bridge annotation**
   - Correlation rows include `is_expected_bridge`.
   - Expected causal-chain pairs are weighted more strongly than exploratory pairs in the cross-module score.

4. **Removed unused cosmology handoff**
   - `cosmology.primordialFeatureStrength` was removed because it was not consumed by the cosmology module.

5. **SIGW kernel restored**
   - The v7-style radiation-era kernel with log and imaginary terms is used inside the convolution.

6. **Test vectors**
   - Added a no-framework C# test project under `tests/SimulatorV86.Tests`.

## Still deferred to v9

- Real DESI/Pantheon+ likelihoods
- Real transfer function into N-body initial conditions
- True detector sensitivity matching for SIGW
- Full statistical mixed model for replicate effects
- Larger test suite and CI
