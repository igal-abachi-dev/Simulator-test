# Simulator

### 1. Inflation

- Keeps exact Mukhanov-Sasaki evolution for the scalar curvature spectrum.
- Reconstructs `H(N)`, `phi(N)`, and `V(phi)` from the chosen ultra-slow-roll background.
- Forward-integrates the reconstructed potential as a loop-closure check.
- Computes a toy radiation-era scalar-induced gravitational-wave spectrum `Omega_GW(f)` from the scalar peak.

Important output:

- `out/inflation_exact_scalar_spectrum.csv`
- `out/inflation_reconstructed_potential.csv`
- `out/inflation_loop_closure.csv`
- `out/inflation_loop_closure_summary.txt`
- `out/inflation_sigw_observable.csv`
- `out/inflation_summary.txt`

### 2. Origin of life

- Keeps RAF/maxRAF structure.
- Adds an autocatalytic growth exponent under food-buffered kinetics.
- Compares structural essentiality to kinetic essentiality.

Important output:

- `out/life_growth_exponent.csv`
- `out/life_structural_vs_kinetic_essentiality.csv`
- `out/life_growth_phase_scan.csv`
- `out/life_summary.txt`

### 3. Measurement / consciousness

- Keeps the scientific framing: standard QM has no consciousness term.
- Adds a toy CSL `(lambda, rC)` exclusion landscape.
- Adds spontaneous X-ray-like radiation scaling, because this is a real observable used to constrain collapse models.
- Keeps a consciousness-bias null test as a non-standard hypothesis test.

Important output:

- `out/measurement_csl_exclusion_landscape.csv`
- `out/measurement_csl_xray_spectrum.csv`
- `out/measurement_visibility_budget.csv`
- `out/measurement_consciousness_null.csv`
- `out/measurement_summary.txt`

## Run

```bash
dotnet run -- all
```

or:

```bash
dotnet run -- inflation
dotnet run -- life
dotnet run -- consciousness
```

## scope

Gives you a better sandbox for finding patterns that connect to real observables: SIGW frequency spectra, autocatalytic growth exponents, and CSL exclusion maps.
