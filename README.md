# Simulator
Observable Physical Anomaly Sandbox & Emergence Simulator

testing whether simplified first-principles models produce observable anomalies across early-universe physics, autocatalytic chemistry, and quantum measurement.

1. Early-universe inflation:
   exact Mukhanov-Sasaki evolution, ultra-slow-roll enhancement, loop-closure of reconstructed potentials, and scalar-induced gravitational-wave observables.

2. Origin of life:
   RAF autocatalytic networks, structural vs kinetic essentiality, and autocatalytic growth exponents.

3. Quantum measurement:
   standard null tests, environmental decoherence, and objective-collapse observables such as CSL visibility loss and spontaneous-radiation scaling.


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


## v8
Simulation Engine
+ Parameter Sweep Runner
+ Module System
+ Observable Registry
+ Analysis Layer
+ Report Generator

The project is already moving in the right direction, but v8 should turn it from “simulation code” into a small research lab.

## Stage 1 — Run It Many Times, Improve Parameters, Add Parallel Computing and ML Fitting

### Goal

At this stage, do not add new physics yet. Add the **experiment infrastructure**.

Instead of every module freely writing its own CSV files, every run should be registered as an experiment with:

```text
run_id
module
model
config
seed
git_commit
started_at
ended_at
parameters
observables
warnings
output_files
score
```



---

## Recommended v8 Folder Structure

Right now, the project has direct files such as:

```text
Inflation.cs
OriginOfLife.cs
Measurement.cs
Util.cs
Program.cs
```

For v8, I would move to this structure:

```text
Simulator/
  src/
    Simulator.Cli/
      Program.cs

    Simulator.Core/
      IExperiment.cs
      ExperimentSpec.cs
      ParameterSet.cs
      ObservableRecord.cs
      RunManifest.cs
      ResultStore.cs
      SweepRunner.cs
      RandomSeed.cs
      AnomalyScore.cs

    Simulator.Modules.Inflation/
      InflationExperiment.cs
      MukhanovSasakiSolver.cs
      SigwObservable.cs
      InflationParameters.cs

    Simulator.Modules.Life/
      RafExperiment.cs
      MaxRafSolver.cs
      KineticIgnitionSolver.cs
      LifeParameters.cs

    Simulator.Modules.Measurement/
      MeasurementExperiment.cs
      CslModel.cs
      DecoherenceModel.cs
      MeasurementParameters.cs

    Simulator.Analysis/
      CsvLoader.cs
      PlotGenerator.cs
      ReportGenerator.cs
      ScoreAggregator.cs
      ModelScoreWriter.cs

  configs/
    inflation.usr.default.json
    life.raf.default.json
    measurement.csl.default.json
    sweeps/
      inflation_usr_island.json
      life_growth_scan.json
      csl_exclusion_scan.json

  runs/
    2026-07-03_001/

  tests/
    Simulator.Tests/
```

---

## The Core Interface

In v8, every module should implement one interface:

```csharp
public interface IExperiment
{
    string Name { get; }

    ExperimentResult Run(ExperimentSpec spec, CancellationToken ct);
}
```

The result should look like this:

```csharp
public sealed record ExperimentResult(
    string RunId,
    string Module,
    string Model,
    IReadOnlyDictionary<string, double> Parameters,
    IReadOnlyList<ObservableRecord> Observables,
    IReadOnlyList<string> OutputFiles,
    IReadOnlyList<string> Warnings,
    double Score
);
```

Each observable should have a unified format:

```csharp
public sealed record ObservableRecord(
    string Name,
    double Value,
    string Unit,
    double? ExpectedMin,
    double? ExpectedMax,
    double AnomalyScore,
    string Interpretation
);
```

This makes every module comparable. Inflation, RAF, CSL, dark energy, black holes — all of them return the same result format.

---

## Proper CLI for v8

Move from a simple `args[0]` system to a real command-line interface:

```bash
dotnet run -- simulate inflation --config configs/inflation.usr.default.json
dotnet run -- sweep inflation --config configs/sweeps/inflation_usr_island.json --parallel 12
dotnet run -- analyze --run runs/2026-07-03_001
dotnet run -- report --run runs/2026-07-03_001
```

For this, I would use `System.CommandLine`, because it provides common CLI functionality such as parsing commands/options and displaying help text.

---

## Parallel Computing

Almost every parameter point is an independent run, so parameter sweeps are a natural fit for parallel execution.

In C#:

```csharp
await Parallel.ForEachAsync(parameterSets, options, async (p, ct) =>
{
    var result = experiment.Run(p, ct);
    await resultStore.SaveAsync(result, ct);
});
```

`Parallel.ForEachAsync` is designed to run iterations in parallel over `IEnumerable` or `IAsyncEnumerable`.

For v8, add this to the sweep config:

```json
{
  "maxDegreeOfParallelism": 12,
  "seed": 12345,
  "samples": 5000,
  "strategy": "latin-hypercube"
}
```

---

## Parameter Sweeps

Do not use only a basic grid. Add four sweep modes:

```text
grid              good for 2–3 parameters
random            good for quick exploration
latin-hypercube   good for covering parameter space
adaptive          runs more samples around interesting regions
```

For the first v8 version, implement:

```text
grid
random
latin-hypercube
```

Leave `adaptive` for v9.

---

## ML Fitting

Important: ML does not replace physics. It only helps find interesting regions of parameter space.

The correct workflow is:

```text
1. Run a real parameter sweep.
2. Save observables.
3. Train a surrogate model that predicts score from parameters.
4. Find high-score regions.
5. Re-run the real physical simulation there.
```

In C#, you can start with ML.NET, Microsoft’s machine-learning stack for .NET applications. Use it only as a surrogate model, not as scientific proof.

---

## Stage 1 Deliverable

At the end of Stage 1, v8 should produce:

```text
runs/<run_id>/manifest.json
runs/<run_id>/parameters.csv
runs/<run_id>/observables.csv
runs/<run_id>/scores.csv
runs/<run_id>/warnings.txt
```

Only after that should you move to Stage 2.

---

# Stage 2 — Add New Modules

---

## 2A — Quantum Gravity / Black Hole Information

Do not try to “simulate full quantum gravity.” There is no complete agreed-upon theory that can simply be coded.

Build a **toy information-flow module**:

```text
black hole evaporation
Page curve
unitary vs non-unitary evaporation
island-inspired correction
information recovery score
```

The observable is not direct like `ΩGW`, but it is still meaningful:

```text
S_rad(t) — radiation entanglement entropy
Page time
unitarity residual
information recovery curve
```

The Page curve and island literature is the right conceptual basis, but the module must be presented as an entropy/information toy model, not a full quantum-gravity simulation.

### Files

```text
Simulator.Modules.BlackHole/
  BlackHoleInformationExperiment.cs
  PageCurveModel.cs
  EvaporationModel.cs
  IslandToyCorrection.cs
  BlackHoleParameters.cs
```

### Parameters

```json
{
  "initialMassPlanck": 1e8,
  "radiationSteps": 1000,
  "model": "unitary_page_curve",
  "islandCorrectionStrength": 1.0,
  "noiseLevel": 0.0
}
```

### Output

```text
out/blackhole_page_curve.csv
out/blackhole_information_recovery.csv
out/blackhole_summary.txt
```

Columns:

```text
t_over_lifetime
mass
hawking_entropy_semiclassical
radiation_entropy_unitary
radiation_entropy_island_toy
information_recovered
unitarity_residual
```

### Score

```text
blackhole_score =
  penalty_if_entropy_never_turns_over
+ penalty_if_final_entropy_not_zero
+ penalty_if_information_recovery_negative
```

---

## 2B — Fine-Tuning Constants / Dark Energy

```text
sensitivity analysis of constants and cosmological parameters
```

The module tests how observables change when you vary:

```text
Λ / dark energy density
Ω_m
H0
w0
wa
Q = primordial fluctuation amplitude
η = baryon-to-photon ratio
α = fine-structure constant, as toy sensitivity only
```

Dark energy is a good target for a testable module. DESI DR2 provides very precise BAO measurements and constraints on dark energy, but any v8 version should start with reference/synthetic curves before trying to reproduce real likelihoods.

### Files

```text
Simulator.Modules.Cosmology/
  DarkEnergyExperiment.cs
  FriedmannSolver.cs
  DistanceModulus.cs
  BaoObservable.cs
  FineTuningSensitivity.cs
  CosmologyParameters.cs
```

### Models

```text
lcdm
wcdm
cpl_w0_wa
early_dark_energy_toy
constant_sensitivity
```

### Output

```text
out/cosmology_expansion_history.csv
out/cosmology_distance_residuals.csv
out/cosmology_finetuning_sensitivity.csv
out/cosmology_summary.txt
```

Columns:

```text
z
H_z
D_A
D_L
mu
bao_scale_proxy
residual_vs_lcdm
```

### Score

```text
dark_energy_score =
  fit_to_reference_data_proxy
+ smoothness_penalty
+ parameter_complexity_penalty
```

For v8, do not add a full DESI likelihood yet. Start with synthetic/reference curves, then add a real-data loader in v9.

---

## 2C — Observer-Dependent Physics

This is the module that needs the most careful wording.


```text
ObserverDependentMeasurementExperiment
```

The tested hypotheses should be:

```text
standard_qm
objective_collapse
born_rule_bias
observer_contextual_bias
```

Standard physics does not include consciousness as a dynamical variable. But observer-dependence is a real topic in quantum foundations, for example in Wigner’s friend-type scenarios. This is not the same as claiming that consciousness changes physics.

### Files

```text
Simulator.Modules.Observer/
  ObserverDependentExperiment.cs
  WignerFriendToyModel.cs
  BornBiasNullTest.cs
  ContextualMeasurementModel.cs
  ObserverParameters.cs
```

### Parameters

```json
{
  "trials": 1000000,
  "bornBias": 0.0,
  "observerContextCoupling": 0.0,
  "confidenceSigma": 5.0
}
```

### Output

```text
out/observer_born_bias_null.csv
out/observer_contextuality_toy.csv
out/observer_power_analysis.csv
out/observer_summary.txt
```

### Score

```text
observer_score =
  z_score_for_bias
+ confidence_bound
- penalty_if_not_detectable
```

If there is no effect, that is not a failure. It means:

```text
No anomaly detected above the sensitivity threshold.
```

---



```text
 anomaly-oriented simulator linking early-universe observables,
autocatalytic emergence, and quantum measurement constraints.
```

---


### 3A — Inflation / Gravitational Waves


```text
For a given USR feature width/depth, the simulator predicts
a scalar-induced gravitational-wave peak at frequency f
with amplitude ΩGW.
```

This connects naturally to LISA/PTA-style observables. LISA is an ESA gravitational-wave space mission that will use three spacecraft and laser interferometry, with launch expected around 2035.

Publishable output:

```text
usr_width
usr_depth
peak_P_R
peak_frequency_Hz
peak_Omega_GW
detector_band
detectability_score
```

---


```text
Networks with structural RAF but negative or weak growth exponent
should fail ignition despite satisfying graph-theoretic autocatalysis.
```


```text
Structural RAF existence is insufficient; kinetic growth exponent is a better predictor of ignition.
```


Publishable output:

```text
maxRAF_size
num_irrafs
structural_essentiality
kinetic_essentiality
growth_exponent
ignition_probability
```

---

### 3C — Measurement / CSL

The best output here is:

```text
For a given λ and rC, predict visibility loss and spontaneous-radiation scaling.
```

This connects to real experiments. XENONnT searched for X-rays from dynamical collapse models and improved previous constraints by about two orders of magnitude.

Publishable output:

```text
lambda
rC
system_mass
superposition_distance
visibility_loss
xray_rate_proxy
excluded_by_toy_bound
```

---


---

# Stage 4 — Analysis Layer

This is the stage that should be included directly in v8.

## Goal

Today you have:

```text
CSV files
TXT summaries
```

In v8, you should have:

```text
anomaly_report.md
plots/
model_score.json
```

Command:

```bash
dotnet run -- analyze --run runs/2026-07-03_001
```

Output:

```text
runs/2026-07-03_001/analysis/
  anomaly_report.md
  model_score.json
  plots/
    inflation_scalar_spectrum.png
    inflation_sigw.png
    life_growth_phase.png
    life_structural_vs_kinetic.png
    measurement_csl_exclusion.png
    measurement_visibility_budget.png
```

---

## NuGet Packages to Add


```xml
<ItemGroup>
  <PackageReference Include="System.CommandLine" Version="..." />
  <PackageReference Include="CsvHelper" Version="..." />
  <PackageReference Include="ScottPlot" Version="..." />
</ItemGroup>
```

Why:

* `System.CommandLine` — proper CLI commands, options, and help text.
* `CsvHelper` — reliable CSV reading/writing instead of a manual parser.
* `ScottPlot` — plot generation from .NET; useful for producing PNG figures from analysis runs.

I would also consider `BenchmarkDotNet` for reproducible performance benchmarks.

---

## model_score.json


Example:

```json
{
  "runId": "2026-07-03_001",
  "overallScore": 0.73,
  "modules": {
    "inflation": {
      "score": 0.82,
      "status": "interesting",
      "topSignals": [
        {
          "name": "SIGW peak",
          "value": 1.2e-9,
          "unit": "Omega_GW",
          "anomalyScore": 0.68,
          "interpretation": "Peak is inside projected mHz observational band."
        }
      ],
      "warnings": [
        "USR background is reconstructed, not derived from a fundamental potential."
      ]
    },
    "life": {
      "score": 0.71,
      "status": "interesting",
      "topSignals": [
        {
          "name": "kinetic growth exponent",
          "value": 0.034,
          "unit": "1/time",
          "anomalyScore": 0.61,
          "interpretation": "Structural RAF ignites dynamically."
        }
      ]
    },
    "measurement": {
      "score": 0.25,
      "status": "null",
      "topSignals": [
        {
          "name": "Born bias",
          "value": 0.0,
          "unit": "probability shift",
          "anomalyScore": 0.0,
          "interpretation": "No non-standard observer bias detected."
        }
      ]
    }
  }
}
```

---

## anomaly_report.md

The report should be readable, not just a dump of numbers.

Suggested structure:

```markdown
# Anomaly Report

## Executive Summary

Overall status: interesting / null / invalid / needs rerun

## Inflation

- Peak scalar power:
- SIGW peak frequency:
- SIGW amplitude:
- Loop closure error:
- Interpretation:
- Warnings:

## Origin of Life

- RAF existence:
- Growth exponent:
- Ignition probability:
- Structural vs kinetic mismatch:
- Interpretation:

## Measurement

- CSL region:
- Visibility loss:
- X-ray proxy:
- Born-bias null test:
- Interpretation:

## Cross-domain Pattern Search

## Limitations

## Reproducibility

- git commit:
- seed:
- config:
- runtime:
```

---

## plots/

Minimum plots for v8:

```text
inflation_scalar_spectrum.png
  x = k/k*
  y = P_R

inflation_sigw.png
  x = frequency Hz
  y = Omega_GW

inflation_loop_closure.png
  x = N
  y = epsilon_input vs epsilon_forward

life_growth_phase.png
  x = catalysis level
  y = growth exponent / ignition probability

life_essentiality_scatter.png
  x = structural essentiality
  y = kinetic essentiality

measurement_csl_exclusion.png
  x = rC
  y = lambda
  color = excluded / allowed / toy

measurement_visibility_budget.png
  x = system mass or N
  y = visibility time
```

---

# How I Would Divide v8 in Practice

## Milestone 1 — Refactor Without Changing the Science

Goal: the old code still works, but through a new engine.

Tasks:

```text
[ ] Create Simulator.Core
[ ] Create IExperiment
[ ] Create ExperimentSpec
[ ] Create ExperimentResult
[ ] Create RunManifest
[ ] Move Inflation / OriginOfLife / Measurement into modules
[ ] Save outputs under runs/<run_id>
[ ] Add JSON config for each module
```

Desired command:

```bash
dotnet run -- simulate inflation --config configs/inflation.default.json
```

---

## Milestone 2 — Sweep Runner

Tasks:

```text
[ ] Create ParameterSweep
[ ] Support grid / random / latin-hypercube
[ ] Add Parallel.ForEachAsync
[ ] Add fixed seed per run
[ ] Write unified parameters.csv and observables.csv
[ ] Add failure handling: if a parameter point fails, mark it invalid without crashing the whole run
```

Command:

```bash
dotnet run -- sweep inflation --config configs/sweeps/inflation_usr_island.json --parallel 12
```

---

## Milestone 3 — Analysis Layer

Tasks:

```text
[ ] CsvLoader
[ ] ScoreAggregator
[ ] PlotGenerator
[ ] ReportGenerator
[ ] model_score.json
[ ] anomaly_report.md
[ ] plots/*.png
```

Command:

```bash
dotnet run -- analyze --run runs/2026-07-03_001
```

---

## Milestone 4 — Black Hole Information Module

Tasks:

```text
[ ] PageCurveModel
[ ] SemiclassicalEntropyCurve
[ ] UnitaryPageCurve
[ ] IslandToyCorrection
[ ] InformationRecoveryScore
[ ] blackhole_page_curve.csv
[ ] blackhole_summary.txt
```

---

## Milestone 5 — Dark Energy / Fine-Tuning Module

Tasks:

```text
[ ] FriedmannSolver
[ ] LCDM
[ ] wCDM
[ ] CPL w0-wa
[ ] distance residuals
[ ] sensitivity scan
[ ] cosmology_finetuning_sensitivity.csv
```

---

## Milestone 6 — Observer-Dependent Module

Tasks:

```text
[ ] Rename consciousness null to nonstandard observer bias
[ ] Add Wigner-friend toy correlation table
[ ] Add Born-bias power analysis
[ ] Add observer_contextuality_toy.csv
```

---

# Short Version

```text
Stage 1:
Build an engine for many runs:
configs, manifests, sweeps, parallel runs, ML surrogate, reproducibility.

Stage 2:
Add new modules:
black hole Page curve,
dark energy/fine-tuning,
observer-dependent measurement as nonstandard-bias tests.

Stage 3:
Define testable predictions:
Omega_GW(f),
RAF growth exponent,
CSL visibility/X-ray constraints,
dark-energy residuals.

Stage 4:
Build the analysis layer:
anomaly_report.md,
plots/*.png,
model_score.json.
```
Once you have the engine and the analysis layer, every new physics module you add will be comparable, scorable, reproducible, and reportable.
