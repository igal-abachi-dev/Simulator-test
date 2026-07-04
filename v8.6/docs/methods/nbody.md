# Cosmological N-body module

`nbody` is a small cosmological structure-formation toy model. It uses Barnes-Hut gravity, periodic wrapping, a simple comoving drag term, and a baryon pressure/cooling proxy.

Outputs:

- `nbody_structure_stats.csv`
- `nbody_particles_final.csv`
- observables: `structure_clumpiness`, `halo_count_proxy`, `baryon_dm_bias`, `max_density_contrast`, `nbody_particles`

Limit: this is not IllustrisTNG, GADGET, AREPO, or a real SPH/moving-mesh simulation. It is a lightweight pattern-discovery module for small runs.
