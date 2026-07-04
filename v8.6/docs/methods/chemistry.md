# Quantum chemistry module

`chemistry` uses a 2x2 covalent/ionic Hamiltonian to generate a bond-dissociation curve. It is a small quantum-mechanical bridge between physics and chemistry.

Outputs:

- `quantum_bond_dissociation_curve.csv`
- observables: `bond_dissociation_energy`, `equilibrium_distance`, `avoided_crossing_gap`, `bond_complexity_proxy`, `prebiotic_feasibility_proxy`

Limit: this is a toy Hamiltonian, not ab-initio chemistry. Later versions can add Psi4/OpenFermion/ONNX adapters.
