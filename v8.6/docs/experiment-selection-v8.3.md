# Experiment selection for v8.6

v8.6 adds three experiments and deliberately defers one.

## Added now

1. `folding` — coarse-grained mini-protein folding.
   - Connects origin-of-life RAF chemistry to 3D structure and function.
   - Cheap enough for sweeps.

2. `chemistry` — quantum bond-dissociation Hamiltonian.
   - Bridges quantum physics and chemical complexity.
   - Provides energy-scale observables for later chemistry modules.

3. `nbody` — cosmological dark-matter/baryon structure toy model.
   - Connects inflation/cosmology to galaxy-scale structure formation.
   - Uses Barnes-Hut gravity and simple baryon pressure/cooling proxies.

## Deferred

`collisional-nbody` is deferred to v10+.

Reason: dense star-cluster/planetary N-body requires high-precision integrators, close-encounter regularization, and stronger validation. It is less directly connected to the main narrative: inflation -> structure -> chemistry -> life -> measurement.

## Scientific stance

These modules are anomaly-oriented toys, not production science codes. Their purpose is to expose thresholds, correlations, and phase patterns worth testing with stronger tools.
