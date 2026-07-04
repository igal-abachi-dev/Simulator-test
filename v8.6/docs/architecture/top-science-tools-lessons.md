# Lessons from successful scientific workflow / experiment tools

## 1. Nextflow

Lesson: build around dataflow, portability, resumability, and compute backends.

What Simulator can copy: clear process boundaries, run manifests, and eventually a resume cache.

What Simulator can do differently: be domain-observable aware rather than a generic pipeline runner.

## 2. Snakemake

Lesson: reproducible outputs should be described by rules and files, with automatic dependency resolution.

What Simulator can copy: file-oriented reproducibility, explicit configs, reports.

What Simulator can do differently: keep strongly typed C# experiment contracts instead of workflow DSL first.

## 3. MLflow

Lesson: every experiment run should have parameters, metrics, artifacts, and a stable run id.

What Simulator can copy: parameters.csv, observables.csv, artifacts, model_score.json, run directory layout.

What Simulator can do differently: support physics/biology observables and scientific anomaly scores, not only ML metrics.

## 4. OpenMDAO

Lesson: multidisciplinary models need explicit components, coupling variables, and optimization loops.

What Simulator can copy: cross-module coupling/correlation and future optimizer integration.

What Simulator can do differently: focus first on anomaly detection and falsifiable observables rather than engineering design optimization.
