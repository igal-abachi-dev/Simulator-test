# Folding module

`folding` is a coarse-grained HP mini-protein model. It treats a short sequence as beads connected by springs, with hydrophobic-hydrophobic attractions, excluded-volume repulsion, and Langevin noise.

Outputs:

- `folding_trajectory.csv`
- `folding_contacts.csv`
- observables: `foldability_score`, `radius_gyration_final`, `native_contact_fraction`, `hydrophobic_contacts`, `best_energy_per_residue`

Limit: this is not atomic protein prediction. It is a light foldability/compactness experiment that bridges origin-of-life chemistry to structure/function.
