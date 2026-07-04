#!/usr/bin/env python3
"""Generate Simulator v8.6 analysis plots with matplotlib.

When `analysis/aggregated_case_observables.csv` exists, plots use base-case means
instead of raw replicate rows, matching the v8.6 pseudo-replication fix.

Optional seaborn is used only for styling if installed. This script is an
adapter: C# owns runs/manifests/observables, Python owns pretty plots.
"""
from __future__ import annotations
import csv, os, sys, math
from collections import defaultdict

run_dir = sys.argv[1] if len(sys.argv) > 1 else "runs/latest"
analysis_dir = os.path.join(run_dir, "analysis")
plots_dir = os.path.join(analysis_dir, "plots")
os.makedirs(plots_dir, exist_ok=True)

try:
    import matplotlib.pyplot as plt
    try:
        import seaborn as sns
        sns.set_theme()
    except Exception:
        pass
except Exception as exc:
    os.makedirs(analysis_dir, exist_ok=True)
    with open(os.path.join(analysis_dir, "plot_report_error.txt"), "w", encoding="utf-8") as f:
        f.write(str(exc))
    sys.exit(0)


def read_csv(path):
    if not os.path.exists(path):
        return []
    with open(path, newline="", encoding="utf-8") as f:
        return list(csv.DictReader(f))


def to_float(x):
    try:
        return float(x)
    except Exception:
        return float("nan")


def finite_pairs(xs, ys):
    outx, outy = [], []
    for x, y in zip(xs, ys):
        if math.isfinite(x) and math.isfinite(y):
            outx.append(x); outy.append(y)
    return outx, outy


def save_line(rows, x_col, y_col, file_name, x_label, y_label, logx=False, logy=False):
    if not rows or x_col not in rows[0] or y_col not in rows[0]:
        return False
    xs = [to_float(r.get(x_col)) for r in rows]
    ys = [to_float(r.get(y_col)) for r in rows]
    xs, ys = finite_pairs(xs, ys)
    if not xs:
        return False
    plt.figure(figsize=(6, 4))
    plt.plot(xs, ys)
    if logx: plt.xscale("log")
    if logy: plt.yscale("log")
    plt.xlabel(x_label); plt.ylabel(y_label)
    plt.tight_layout(); plt.savefig(os.path.join(plots_dir, file_name), dpi=170); plt.close()
    return True


def save_scatter(xs, ys, file_name, x_label, y_label, logx=False, logy=False):
    xs, ys = finite_pairs(xs, ys)
    if len(xs) < 2:
        return False
    plt.figure(figsize=(6, 4))
    plt.scatter(xs, ys, s=18)
    if logx: plt.xscale("log")
    if logy: plt.yscale("log")
    plt.xlabel(x_label); plt.ylabel(y_label)
    plt.tight_layout(); plt.savefig(os.path.join(plots_dir, file_name), dpi=170); plt.close()
    return True

obs = read_csv(os.path.join(analysis_dir, "aggregated_case_observables.csv")) or read_csv(os.path.join(run_dir, "observables.csv"))
by_name = defaultdict(list)
for r in obs:
    key = f"{r.get('module')}.{r.get('observable')}"
    by_name[key].append((r.get("case_id"), to_float(r.get("value")), to_float(r.get("anomaly_score"))))

# 1) All observable anomaly/interest scores.
plt.figure(figsize=(10, 5))
labels, xs, ys = [], [], []
for i, (key, vals) in enumerate(sorted(by_name.items())):
    for _, value, score in vals:
        if math.isfinite(score):
            xs.append(i); ys.append(score)
    labels.append(key)
if xs:
    plt.scatter(xs, ys, s=10)
    plt.xticks(range(len(labels)), labels, rotation=90, fontsize=6)
    plt.ylabel("anomaly / interest score")
    plt.tight_layout()
    plt.savefig(os.path.join(plots_dir, "all_observable_scores.png"), dpi=170)
plt.close()

# 2) Required cross-module example: inflation peak vs RAF growth exponent.
infl = {case: v for case, v, _ in by_name.get("inflation.peak_scalar_power", []) if math.isfinite(v)}
life = {case: v for case, v, _ in by_name.get("life.growth_exponent", []) if math.isfinite(v)}
common = sorted(set(infl) & set(life))
if len(common) >= 3:
    save_scatter([infl[c] for c in common], [life[c] for c in common],
                 "cross_inflation_peak_vs_life_growth.png",
                 "inflation peak scalar power P_R", "RAF growth exponent", logx=True)

# 3) Canonical plots from observables.
cat = {case: v for case, v, _ in by_name.get("life.catalysis", []) if math.isfinite(v)}
growth = {case: v for case, v, _ in by_name.get("life.growth_exponent", []) if math.isfinite(v)}
ignition = {case: v for case, v, _ in by_name.get("life.ignition_probability", []) if math.isfinite(v)}
common_life = sorted(set(cat) & (set(growth) | set(ignition)))
if common_life:
    plt.figure(figsize=(6,4))
    if set(cat) & set(growth):
        cc = sorted(set(cat) & set(growth))
        plt.scatter([cat[c] for c in cc], [growth[c] for c in cc], label="growth exponent", s=18)
    if set(cat) & set(ignition):
        cc = sorted(set(cat) & set(ignition))
        plt.scatter([cat[c] for c in cc], [ignition[c] for c in cc], label="ignition probability", s=18)
    plt.xlabel("catalysis probability"); plt.ylabel("growth / ignition")
    plt.legend(); plt.tight_layout(); plt.savefig(os.path.join(plots_dir, "life_growth_phase.png"), dpi=170); plt.close()

struct = {case: v for case, v, _ in by_name.get("life.max_raf_size", []) if math.isfinite(v)}
mismatch = {case: v for case, v, _ in by_name.get("life.structural_kinetic_mismatch", []) if math.isfinite(v)}
cc = sorted(set(struct) & set(mismatch))
if len(cc) >= 2:
    save_scatter([struct[c] for c in cc], [mismatch[c] for c in cc],
                 "life_essentiality_scatter.png", "maxRAF size", "structural/kinetic mismatch")

# 4) Per-module plots from output CSVs. Also save canonical names when possible.
for root, _, files in os.walk(os.path.join(run_dir, "outputs")):
    for name in files:
        path = os.path.join(root, name)
        try:
            rows = read_csv(path)
        except Exception:
            continue
        if not rows:
            continue
        cols = rows[0].keys()
        rel = os.path.relpath(path, os.path.join(run_dir, "outputs")).replace(os.sep, "_").replace(".csv", "")
        if "inflation_exact_scalar_spectrum" in name and "k_over_kpivot" in cols:
            save_line(rows, "k_over_kpivot", "P_R_exact", rel + ".png", "k/k_pivot", "P_R", True, True)
            save_line(rows, "k_over_kpivot", "P_R_exact", "inflation_scalar_spectrum.png", "k/k_pivot", "P_R", True, True)
        if "sigw" in name and "frequency_hz" in cols:
            y = "Omega_GW_today" if "Omega_GW_today" in cols else ("Omega_GW_kernel" if "Omega_GW_kernel" in cols else ("Omega_GW_proxy" if "Omega_GW_proxy" in cols else "Omega_GW"))
            save_line(rows, "frequency_hz", y, rel + ".png", "frequency [Hz]", "Omega_GW proxy", True, True)
            save_line(rows, "frequency_hz", y, "inflation_sigw.png", "frequency [Hz]", "Omega_GW proxy", True, True)
        if "inflation_loop_closure" in name and "N" in cols:
            if "epsilon_input" in cols and "epsilon_forward" in cols:
                xs = [to_float(r.get("N")) for r in rows]
                yi = [to_float(r.get("epsilon_input")) for r in rows]
                yf = [to_float(r.get("epsilon_forward")) for r in rows]
                plt.figure(figsize=(6,4)); plt.plot(xs, yi, label="input"); plt.plot(xs, yf, label="forward")
                plt.yscale("log"); plt.xlabel("N"); plt.ylabel("epsilon"); plt.legend(); plt.tight_layout()
                plt.savefig(os.path.join(plots_dir, "inflation_loop_closure.png"), dpi=170); plt.close()
        if "life_growth" in name and "t" in cols:
            save_line(rows, "t", "raf_population", rel + ".png", "t", "RAF population")
        if "csl_exclusion" in name and "rC" in cols:
            xs = [to_float(r.get("rC")) for r in rows]
            ys = [to_float(r.get("lambda")) for r in rows]
            cs = [to_float(r.get("rate_proxy")) for r in rows]
            xs, ys = finite_pairs(xs, ys)
            if xs and ys:
                plt.figure(figsize=(6,4)); plt.scatter(xs, ys, c=cs[:len(xs)] if cs else None, s=8)
                plt.xscale("log"); plt.yscale("log"); plt.xlabel("rC [m]"); plt.ylabel("lambda [1/s]")
                plt.tight_layout(); plt.savefig(os.path.join(plots_dir, rel + ".png"), dpi=170); plt.savefig(os.path.join(plots_dir, "measurement_csl_exclusion.png"), dpi=170); plt.close()
        if "measurement_visibility_budget" in name and "N" in cols:
            save_line(rows, "N", "collapse_time_s", "measurement_visibility_budget.png", "N", "visibility/collapse time [s]", True, True)
        if "observer_born_bias_power_analysis" in name and "trials" in cols:
            save_line(rows, "trials", "detectable_bias_at_sigma", "observer_born_bias_power.png", "trials", "detectable bias", True, True)
        if "observer_contextuality_toy" in name and "shift" in cols:
            labels = [r.get("context", str(i)) for i, r in enumerate(rows)]
            vals = [to_float(r.get("shift")) for r in rows]
            if vals:
                plt.figure(figsize=(7,4)); plt.bar(labels, vals); plt.xticks(rotation=25, ha="right"); plt.ylabel("probability shift")
                plt.tight_layout(); plt.savefig(os.path.join(plots_dir, "observer_contextuality_toy.png"), dpi=170); plt.close()

with open(os.path.join(analysis_dir, "plot_report_done.txt"), "w", encoding="utf-8") as f:
    f.write("plots generated\n")

# v8.6 plots: folding, quantum chemistry, and cosmological N-body.
for root, _, files in os.walk(os.path.join(run_dir, "outputs")):
    for name in files:
        path = os.path.join(root, name)
        try:
            rows = read_csv(path)
        except Exception:
            continue
        if not rows:
            continue
        cols = rows[0].keys()
        rel = os.path.relpath(path, os.path.join(run_dir, "outputs")).replace(os.sep, "_").replace(".csv", "")
        if "folding_trajectory" in name and "step" in cols:
            save_line(rows, "step", "energy", "folding_energy.png", "step", "energy")
            save_line(rows, "step", "radius_gyration", "folding_radius_gyration.png", "step", "Rg")
            save_line(rows, "step", "native_contact_fraction", "folding_contact_fraction.png", "step", "contact fraction")
        if "quantum_bond_dissociation_curve" in name and "R_A" in cols:
            save_line(rows, "R_A", "E_ground_eV", "chemistry_bond_dissociation.png", "R [Angstrom]", "ground energy [eV]")
            save_line(rows, "R_A", "ionic_weight_ground", "chemistry_ionic_weight.png", "R [Angstrom]", "ionic weight")
        if "nbody_structure_stats" in name and "step" in cols:
            save_line(rows, "step", "clumpiness", "nbody_clumpiness.png", "step", "clumpiness", False, True)
            save_line(rows, "step", "halo_count", "nbody_halo_count.png", "step", "halo count proxy")
