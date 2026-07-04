#!/usr/bin/env python3
"""Validate that a Simulator v8.6 run contains the expected milestone artifacts."""
from __future__ import annotations
import os, sys, json

run_dir = sys.argv[1] if len(sys.argv) > 1 else "runs/latest"
required_root = ["manifest.json", "parameters.csv", "observables.csv", "scores.csv"]
required_analysis = [
    "anomaly_report.md",
    "model_score.json",
    "cross_module_correlations.csv",
    "surrogate_null_summary.csv",
    "ensemble_observable_summary.csv",
    "aggregated_case_observables.csv",
]
required_plots = [
    "all_observable_scores.png",
]
errors = []
for name in required_root:
    if not os.path.exists(os.path.join(run_dir, name)):
        errors.append(f"missing {name}")
analysis = os.path.join(run_dir, "analysis")
for name in required_analysis:
    if not os.path.exists(os.path.join(analysis, name)):
        errors.append(f"missing analysis/{name}")
plots = os.path.join(analysis, "plots")
if os.path.isdir(plots):
    existing = set(os.listdir(plots))
    for name in required_plots:
        if name not in existing:
            errors.append(f"missing analysis/plots/{name}")
else:
    errors.append("missing analysis/plots")

result = {"runDir": run_dir, "ok": not errors, "errors": errors}
print(json.dumps(result, indent=2))
sys.exit(0 if not errors else 1)
