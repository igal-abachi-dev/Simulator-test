#!/usr/bin/env python3
"""Train a tiny surrogate model from parameters.csv -> overall per-case score.
Keras 3 Sequential is used if available. Fallback: numpy ridge regression.
This is for navigation of parameter space only, not scientific proof.
"""
from __future__ import annotations
import csv, json, os, sys, math
from collections import defaultdict

run_dir = sys.argv[1] if len(sys.argv) > 1 else "runs/latest"
analysis_dir = os.path.join(run_dir, "analysis")
os.makedirs(analysis_dir, exist_ok=True)


def read_csv(path):
    if not os.path.exists(path):
        return []
    with open(path, newline="", encoding="utf-8") as f:
        return list(csv.DictReader(f))


def fnum(x, default=float("nan")):
    try: return float(x)
    except Exception: return default

params_rows = read_csv(os.path.join(run_dir, "parameters.csv"))
scores_rows = read_csv(os.path.join(run_dir, "scores.csv"))

param_by_case = defaultdict(dict)
for r in params_rows:
    param_by_case[r["case_id"]][r["parameter"]] = fnum(r["value"])
score_by_case = defaultdict(list)
for r in scores_rows:
    score_by_case[r["case_id"]].append(fnum(r["score"]))

cases = sorted(set(param_by_case) & set(score_by_case))
features = sorted({k for c in cases for k in param_by_case[c].keys() if k not in ("seed", "caseIndex")})
if len(cases) < 8 or not features:
    with open(os.path.join(analysis_dir, "ml_surrogate_summary.json"), "w", encoding="utf-8") as f:
        json.dump({"status": "skipped", "reason": "not enough sweep cases/features", "cases": len(cases)}, f, indent=2)
    sys.exit(0)

import numpy as np
X = np.array([[param_by_case[c].get(k, 0.0) for k in features] for c in cases], dtype=float)
y = np.array([np.nanmean(score_by_case[c]) for c in cases], dtype=float)
# log-transform positive, highly-scaled columns
for j in range(X.shape[1]):
    col = X[:, j]
    if np.all(col > 0) and np.nanmax(col) / max(np.nanmin(col), 1e-300) > 100:
        X[:, j] = np.log10(col)
mu, sig = np.nanmean(X, axis=0), np.nanstd(X, axis=0) + 1e-12
Xs = (X - mu) / sig

summary = {"features": features, "cases": len(cases), "target": "mean module score"}
try:
    import keras
    from keras import layers
    model = keras.Sequential([
        keras.Input(shape=(Xs.shape[1],)),
        layers.Dense(32, activation="relu"),
        layers.Dense(16, activation="relu"),
        layers.Dense(1, activation="sigmoid")
    ])
    model.compile(optimizer="adam", loss="mse", metrics=["mae"])
    history = model.fit(Xs, y, epochs=80, batch_size=min(16, len(cases)), verbose=0, validation_split=0.2 if len(cases) >= 20 else 0.0)
    pred = model.predict(Xs, verbose=0).reshape(-1)
    out_model = os.path.join(analysis_dir, "surrogate.keras")
    model.save(out_model)
    summary.update({"status": "ok", "backend": "keras3_sequential", "model": out_model, "mae": float(np.mean(np.abs(pred - y)))})
except Exception as exc:
    # Fallback ridge regression
    reg = 1e-3
    A = Xs.T @ Xs + reg * np.eye(Xs.shape[1])
    w = np.linalg.solve(A, Xs.T @ y)
    pred = Xs @ w
    summary.update({"status": "ok", "backend": "numpy_ridge_fallback", "mae": float(np.mean(np.abs(pred - y))), "keras_error": str(exc)})

best = sorted(zip(cases, y, pred), key=lambda x: x[2], reverse=True)[:10]
summary["top_predicted_cases"] = [{"case": c, "actual": float(a), "predicted": float(p)} for c, a, p in best]
with open(os.path.join(analysis_dir, "ml_surrogate_summary.json"), "w", encoding="utf-8") as f:
    json.dump(summary, f, indent=2)
