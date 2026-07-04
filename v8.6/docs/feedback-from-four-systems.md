# Feedback from Four Systems and How v8.6 Applies It

## 1. Nextflow lesson: scalable, reproducible workflows

Applied in v8.6:

- Runs are first-class directories.
- Config, parameters, observables, scores, outputs, and analysis artifacts are separated.
- Python is an adapter, not the source of truth.

What Simulator does differently:

- The workflow is not generic only; it is observable-aware: `P_R`, `Omega_GW`, RAF growth exponent, CSL visibility, Page-curve residuals, and dark-energy residuals are typed scientific outputs.

## 2. Snakemake lesson: explicit file outputs and dependency clarity

Applied in v8.6:

- Each module writes explicit artifacts under `runs/<run_id>/outputs/<module>/<case_id>/`.
- Analysis consumes `parameters.csv`, `observables.csv`, and `scores.csv`.

What Simulator does differently:

- Instead of a workflow DSL, the core is strongly typed C# contracts: `IExperiment`, `ExperimentResult`, and `ObservableRecord`.

## 3. MLflow lesson: runs, parameters, metrics, artifacts

Applied in v8.6:

- Every run has a manifest.
- Every case has parameters, observables, module score, warnings, and output files.
- The analysis layer emits `model_score.json`.

What Simulator does differently:

- Metrics are scientific observables, not just ML loss/accuracy.

## 4. OpenMDAO lesson: multidisciplinary coupling and sensitivity

Applied in v8.6:

- Modules can be swept together across shared cases.
- The analysis layer computes cross-module correlations.
- Fine-tuning/cosmology includes sensitivity outputs.

What Simulator does differently:

- The project is anomaly-first, not optimization-first: it searches for residuals, thresholds, phase transitions, and unexplained signatures.
