#!/usr/bin/env python3
"""
Measured A/B for surfaces added in v0.2.0.

Does not write docs/WILCOXON-RESULTS.md and does not change package defaults.

1. Constrained problems (OSY, TNK, C1-DTLZ1), 15 seeds by default.
   C# uses RankNicheDistance + IndependentWithReplacement (the constructor defaults).
   pymoo uses UNSGA3 defaults. Both feasible non-dominated final populations are
   scored with pymoo's IGD against one shared reference front.
2. Mating opt-ins on ZDT1 (and ZDT2 / DTLZ2 when requested). Four C# configs,
   library IGD, same seeds as the published ZDT1 table. The published table is
   only used as a G6 string check of the default ZDT1 column.

Usage:
  python tools/oracle/run_new_surfaces.py
  python tools/oracle/run_new_surfaces.py --seeds 1 --mating zdt1 --constrained osy
"""
from __future__ import annotations

import argparse
import json
import math
import statistics
import subprocess
import sys
from dataclasses import dataclass
from datetime import date
from pathlib import Path

import numpy as np
import pymoo
from pymoo.algorithms.moo.unsga3 import UNSGA3
from pymoo.indicators.igd import IGD
from pymoo.optimize import minimize
from pymoo.problems import get_problem
from pymoo.util.nds.non_dominated_sorting import NonDominatedSorting
from pymoo.util.ref_dirs import get_reference_directions

from run_multiseed_wilcoxon import mannwhitney_u, wilcoxon_signed_rank

ROOT = Path(__file__).resolve().parents[2]
OUT = Path(__file__).resolve().parent / "out"
ORACLE_COMPARE = ROOT / "tools" / "OracleCompare"

# Published ZDT1 C# column in docs/WILCOXON-RESULTS.md (G6 console strings).
# Used only to check a remeasure of the default config. Not a new measurement.
PUBLISHED_ZDT1_CSHARP_G6 = {
    1: "0.0514307",
    2: "0.0609151",
    3: "0.0377512",
    4: "0.064033",
    5: "0.0735681",
    6: "0.105668",
    7: "0.100905",
    8: "0.0836797",
    9: "0.0844962",
    10: "0.0497514",
    11: "0.0390644",
    12: "0.053173",
    13: "0.038507",
    14: "0.0422284",
    15: "0.048006",
}

# Published DTLZ2 Unsga3 column is PymooCompatible, not Algorithm2.
# Compared only as a G6 string check. The published file is not rewritten.
PUBLISHED_DTLZ2_PYMOO_COMPAT_G6 = {
    1: "0.00403168",
    2: "0.005666",
    3: "0.00513016",
    4: "0.00477695",
    5: "0.00466321",
    6: "0.00545999",
    7: "0.00361016",
    8: "0.00511116",
    9: "0.0046699",
    10: "0.00381559",
    11: "0.00397447",
    12: "0.00451168",
    13: "0.00398436",
    14: "0.00417245",
    15: "0.00362671",
}


@dataclass(frozen=True)
class ConstrainedProtocol:
    name: str
    partitions: int
    pop: int
    gens: int
    n_obj: int
    n_var: int


@dataclass(frozen=True)
class MatingProtocol:
    name: str
    partitions: int
    pop: int
    gens: int


# Bi-objective constrained: same pop and partitions as ZDT, generations of the
# ZDT2 quality budget. C1-DTLZ1: same pop / partitions / generations as DTLZ2,
# but n_var=7 (k=5), which is C1Dtlz1Problem's default. pymoo's own c1dtlz1
# default is n_var=12 and is not used.
CONSTRAINED: dict[str, ConstrainedProtocol] = {
    "osy": ConstrainedProtocol("osy", partitions=12, pop=52, gens=250, n_obj=2, n_var=6),
    "tnk": ConstrainedProtocol("tnk", partitions=12, pop=52, gens=250, n_obj=2, n_var=2),
    "c1dtlz1": ConstrainedProtocol("c1dtlz1", partitions=12, pop=92, gens=150, n_obj=3, n_var=7),
}

MATING: dict[str, MatingProtocol] = {
    "zdt1": MatingProtocol("zdt1", partitions=12, pop=52, gens=100),
    # gens=250 is the quality budget. The tournament in this A/B is still
    # RankNicheDistance on the default arm, not the ZDT2 PymooCompatible column
    # (that column was never published).
    "zdt2": MatingProtocol("zdt2", partitions=12, pop=52, gens=250),
    # Default arm is RankNicheDistance, not the published DTLZ2 PymooCompatible run.
    "dtlz2": MatingProtocol("dtlz2", partitions=12, pop=92, gens=150),
}

# (column key, --tournament, --mating). First column is the package default.
MATING_CONFIGS: tuple[tuple[str, str, str], ...] = (
    ("default", "rank-niche", "independent"),
    ("two_shuffled", "rank-niche", "two-shuffled"),
    ("algorithm2", "algorithm2", "independent"),
    ("both", "algorithm2", "two-shuffled"),
)

MATING_LABELS = {
    "default": "RankNicheDistance + IndependentWithReplacement",
    "two_shuffled": "RankNicheDistance + TwoShuffledPasses",
    "algorithm2": "Algorithm2 + IndependentWithReplacement",
    "both": "Algorithm2 + TwoShuffledPasses",
}


def fmt(x: float) -> str:
    if isinstance(x, float) and math.isnan(x):
        return "skip: undefined"
    return repr(float(x))


def assert_formulations() -> None:
    """Lock the pymoo problems to the fixtures in ConstrainedProblemTests."""
    osy = get_problem("osy")
    f_osy, g_osy = osy.evaluate(
        np.array([[5.0, 1.0, 5.0, 0.0, 5.0, 0.0]]),
        return_values_of=["F", "G"],
    )
    if not np.allclose(f_osy, [[-274.0, 76.0]]) or not np.allclose(g_osy, [[-2.0, 0.0, -3.0, 0.0, 0.0, 0.0]]):
        raise RuntimeError(f"OSY formulation mismatch: F={f_osy} G={g_osy}")

    tnk = get_problem("tnk")
    f_tnk, g_tnk = tnk.evaluate(np.array([[1.0, 1.0]]), return_values_of=["F", "G"])
    if not np.allclose(f_tnk, [[1.0, 1.0]]) or not np.allclose(g_tnk, [[-0.9, 0.0]]):
        raise RuntimeError(f"TNK formulation mismatch: F={f_tnk} G={g_tnk}")

    c1 = get_problem("c1dtlz1", n_obj=3, n_var=7)
    if int(c1.n_var) != 7:
        raise RuntimeError(f"C1-DTLZ1 n_var={c1.n_var}, expected 7")
    f_c1, g_c1 = c1.evaluate(np.full((1, 7), 0.5), return_values_of=["F", "G"])
    if not np.allclose(f_c1, [[0.125, 0.125, 0.25]]) or not np.allclose(g_c1, [[-1.0 / 12.0]]):
        raise RuntimeError(f"C1-DTLZ1 formulation mismatch: F={f_c1} G={g_c1}")


def reference_front(proto: ConstrainedProtocol) -> np.ndarray:
    ref_dirs = get_reference_directions("das-dennis", proto.n_obj, n_partitions=proto.partitions)
    if len(ref_dirs) != {2: 13, 3: 91}[proto.n_obj]:
        raise RuntimeError(f"{proto.name}: unexpected ref count {len(ref_dirs)}")
    if proto.name == "c1dtlz1":
        problem = get_problem("c1dtlz1", n_obj=proto.n_obj, n_var=proto.n_var)
        pf = np.atleast_2d(problem.pareto_front(ref_dirs))
    else:
        problem = get_problem(proto.name)
        if int(problem.n_var) != proto.n_var:
            raise RuntimeError(f"{proto.name}: pymoo n_var={problem.n_var}, expected {proto.n_var}")
        pf = np.atleast_2d(problem.pareto_front())
    if pf.size == 0 or not np.isfinite(pf).all():
        raise RuntimeError(f"{proto.name}: Pareto front missing or non-finite")
    return pf


def feasible_nd(objectives: np.ndarray, cv: np.ndarray) -> np.ndarray | None:
    objectives = np.atleast_2d(np.asarray(objectives, dtype=float))
    cv = np.asarray(cv, dtype=float).reshape(-1)
    if objectives.shape[0] != cv.shape[0]:
        raise RuntimeError(f"F rows {objectives.shape[0]} != CV {cv.shape[0]}")
    mask = cv <= 0.0
    if not np.any(mask):
        return None
    feasible = np.atleast_2d(objectives[mask])
    if feasible.shape[0] == 1:
        return feasible
    index = NonDominatedSorting().do(feasible, only_non_dominated_front=True)
    return np.atleast_2d(feasible[index])


def igd_of(obtained: np.ndarray, pf: np.ndarray) -> float:
    return float(IGD(pf)(np.atleast_2d(obtained)))


def load_population(path: Path) -> tuple[np.ndarray, np.ndarray]:
    data = np.atleast_2d(np.loadtxt(path, delimiter=","))
    if data.shape[1] < 2:
        raise RuntimeError(f"{path} has no constraint column")
    return data[:, :-1], data[:, -1]


def parse_result_line(line: str) -> dict:
    fields: dict[str, str] = {}
    for part in line.split()[1:]:
        key, value = part.split("=", 1)
        fields[key] = value
    igd = None if fields["igd"] == "skip" else float(fields["igd"])
    return {
        "seed": int(fields["seed"]),
        "igd": igd,
        "igd_g6": fields["igd_g6"],
        "n": int(fields["n"]),
        "feasible": int(fields["feasible"]),
        "pop_csv": fields["pop_csv"],
    }


def run_csharp(
    problem: str,
    partitions: int,
    pop: int,
    gens: int,
    seed_start: int,
    seed_count: int,
    tournament: str,
    mating: str,
) -> list[dict]:
    args = [
        "dotnet",
        "run",
        "--project",
        str(ORACLE_COMPARE),
        "-c",
        "Release",
        "--no-build",
        "--",
        "--problem",
        problem,
        "--partitions",
        str(partitions),
        "--pop",
        str(pop),
        "--gens",
        str(gens),
        "--seed",
        str(seed_start),
        "--seed-count",
        str(seed_count),
        "--tournament",
        tournament,
        "--mating",
        mating,
        "--out-dir",
        str(OUT),
    ]
    completed = subprocess.run(args, capture_output=True, text=True, cwd=str(ROOT))
    if completed.returncode != 0:
        raise RuntimeError(
            f"C# {problem} {tournament}/{mating} failed:\n{completed.stdout}\n{completed.stderr}"
        )
    rows = [parse_result_line(line) for line in completed.stdout.splitlines() if line.startswith("RESULT ")]
    if len(rows) != seed_count:
        raise RuntimeError(
            f"C# {problem} returned {len(rows)} RESULT lines, expected {seed_count}:\n{completed.stdout}"
        )
    return rows


def run_pymoo_constrained(proto: ConstrainedProtocol, seed: int, pf: np.ndarray) -> dict:
    ref_dirs = get_reference_directions("das-dennis", proto.n_obj, n_partitions=proto.partitions)
    if proto.name == "c1dtlz1":
        problem = get_problem("c1dtlz1", n_obj=proto.n_obj, n_var=proto.n_var)
    else:
        problem = get_problem(proto.name)
    algorithm = UNSGA3(ref_dirs, pop_size=proto.pop)
    result = minimize(
        problem,
        algorithm,
        ("n_gen", proto.gens),
        seed=seed,
        verbose=False,
        save_history=False,
    )
    objectives = np.atleast_2d(result.pop.get("F"))
    raw_cv = result.pop.get("CV")
    if raw_cv is None:
        raise RuntimeError(f"{proto.name} seed={seed}: pymoo population has no CV")
    cv = np.asarray(raw_cv, dtype=float).reshape(-1)
    nd = feasible_nd(objectives, cv)
    res_f = np.atleast_2d(result.F) if result.F is not None else np.empty((0, proto.n_obj))
    res_f_ok = res_f.size > 0 and np.isfinite(res_f).all()
    stem = f"pymoo_{proto.name}_p{proto.partitions}_pop{proto.pop}_g{proto.gens}_s{seed}"
    np.savetxt(OUT / f"{stem}_pop.csv", np.column_stack([objectives, cv]), delimiter=",")
    return {
        "seed": seed,
        "igd": None if nd is None else igd_of(nd, pf),
        "n": 0 if nd is None else int(nd.shape[0]),
        "feasible": int(np.sum(cv <= 0.0)),
        "resf_igd": igd_of(res_f, pf) if res_f_ok else None,
        "resf_n": int(res_f.shape[0]) if res_f_ok else 0,
        "n_var": int(problem.n_var),
    }


def summarize(values: list[float]) -> dict:
    ordered = sorted(values)
    n = len(ordered)
    return {
        "n": n,
        "mean": statistics.fmean(ordered),
        "std": statistics.stdev(ordered) if n > 1 else 0.0,
        "median": statistics.median(ordered),
        "min": ordered[0],
        "max": ordered[-1],
    }


def hypothesis(
    left: list[float],
    right: list[float],
    left_name: str = "Unsga3",
    right_name: str = "pymoo",
) -> dict:
    u_stat, p_mw, note_mw = mannwhitney_u(left, right)
    diffs = [a - b for a, b in zip(left, right)]
    w_stat, p_wx, note_wx = wilcoxon_signed_rank(diffs)
    left_med = statistics.median(left)
    right_med = statistics.median(right)
    if left_med < right_med:
        better = left_name
    elif right_med < left_med:
        better = right_name
    else:
        better = "tie"

    def verdict(p: float) -> str:
        if isinstance(p, float) and math.isnan(p):
            return "skip: undefined"
        if p < 0.05:
            return f"reject H₀ (better median = {better})"
        return "fail to reject H₀"

    ratio = left_med / right_med if right_med != 0.0 else float("nan")
    return {
        "mannwhitney_U": u_stat,
        "mannwhitney_p": p_mw,
        "mannwhitney_note": note_mw,
        "mannwhitney_verdict": verdict(p_mw),
        "wilcoxon_W": w_stat,
        "wilcoxon_p": p_wx,
        "wilcoxon_note": note_wx,
        "wilcoxon_verdict": verdict(p_wx),
        "median_ratio": ratio,
        "better_median": better,
        "n": len(left),
    }


def paired_or_skip(rows: list[dict], key_a: str, key_b: str) -> tuple[list[float], list[float], int]:
    left: list[float] = []
    right: list[float] = []
    skipped = 0
    for row in rows:
        a = row[key_a]
        b = row[key_b]
        if a is None or b is None:
            skipped += 1
            continue
        left.append(float(a))
        right.append(float(b))
    return left, right, skipped


def measure_constrained(names: list[str], seeds: list[int]) -> dict:
    blocks: dict = {}
    for name in names:
        proto = CONSTRAINED[name]
        print(f"\n=== constrained {name} seeds {seeds[0]}..{seeds[-1]} ===", flush=True)
        pf = reference_front(proto)
        print(f"  C# default ...", flush=True)
        csharp_rows = run_csharp(
            proto.name,
            proto.partitions,
            proto.pop,
            proto.gens,
            seeds[0],
            len(seeds),
            "rank-niche",
            "independent",
        )
        per_seed = []
        for row, seed in zip(csharp_rows, seeds):
            if row["seed"] != seed:
                raise RuntimeError(f"{name}: seed order {row['seed']} != {seed}")
            objectives, cv = load_population(OUT / row["pop_csv"])
            nd = feasible_nd(objectives, cv)
            fair = None if nd is None else igd_of(nd, pf)
            per_seed.append(
                {
                    "seed": seed,
                    "csharp_igd": fair,
                    "csharp_n": 0 if nd is None else int(nd.shape[0]),
                    "csharp_feasible": int(np.sum(cv <= 0.0)),
                    "csharp_library_igd": row["igd"],
                    "pymoo_igd": None,
                    "pymoo_n": None,
                    "pymoo_feasible": None,
                    "pymoo_resf_igd": None,
                    "pymoo_resf_n": None,
                }
            )
            print(
                f"  C#  seed={seed} fair_igd="
                f"{'skip' if fair is None else fmt(fair)} n={per_seed[-1]['csharp_n']}",
                flush=True,
            )

        for entry in per_seed:
            print(f"  py  seed={entry['seed']} ...", end=" ", flush=True)
            pymoo_row = run_pymoo_constrained(proto, entry["seed"], pf)
            repeat = run_pymoo_constrained(proto, entry["seed"], pf)
            if pymoo_row["n_var"] != proto.n_var or repeat["n_var"] != proto.n_var:
                raise RuntimeError(f"{name}: pymoo ran n_var={pymoo_row['n_var']}")
            entry["pymoo_igd"] = pymoo_row["igd"]
            entry["pymoo_n"] = pymoo_row["n"]
            entry["pymoo_feasible"] = pymoo_row["feasible"]
            entry["pymoo_resf_igd"] = pymoo_row["resf_igd"]
            entry["pymoo_resf_n"] = pymoo_row["resf_n"]
            entry["pymoo_igd_repeat"] = repeat["igd"]
            entry["pymoo_resf_igd_repeat"] = repeat["resf_igd"]
            entry["pymoo_repeat_match"] = (
                pymoo_row["igd"] == repeat["igd"] and pymoo_row["resf_igd"] == repeat["resf_igd"]
            )
            shown = "skip" if pymoo_row["igd"] is None else fmt(pymoo_row["igd"])
            flag = "" if entry["pymoo_repeat_match"] else " repeat-differs"
            print(f"fair_igd={shown} n={pymoo_row['n']}{flag}", flush=True)

        left, right, skipped = paired_or_skip(per_seed, "csharp_igd", "pymoo_igd")
        tests = None
        if len(left) >= 5:
            tests = hypothesis(left, right, left_name="Unsga3", right_name="pymoo")
        block = {
            "protocol": {
                "problem": proto.name,
                "partitions": proto.partitions,
                "pop": proto.pop,
                "gens": proto.gens,
                "n_obj": proto.n_obj,
                "n_var": proto.n_var,
                "n_ref_dirs": int(len(get_reference_directions(
                    "das-dennis", proto.n_obj, n_partitions=proto.partitions
                ))),
                "pf_points": int(pf.shape[0]),
                "pf_min": [float(v) for v in pf.min(axis=0)],
                "pf_max": [float(v) for v in pf.max(axis=0)],
                "csharp_tournament": "RankNicheDistance",
                "csharp_mating": "IndependentWithReplacement",
                "pymoo_algorithm": "UNSGA3",
                "scored_set": "feasible_nondominated_final_population",
                "igd": "pymoo.indicators.igd.IGD",
            },
            "seeds": per_seed,
            "n_skipped_pairs": skipped,
            "tests": tests,
            "csharp_summary": summarize(left) if left else None,
            "pymoo_summary": summarize(right) if right else None,
        }
        blocks[name] = block
    return blocks


def g6_check(rows: list[dict], expected: dict[int, str], compared_to: str) -> dict:
    mismatches = []
    for row in rows:
        published = expected[row["seed"]]
        if row["igd_g6"] != published:
            mismatches.append(
                {"seed": row["seed"], "measured_g6": row["igd_g6"], "published_g6": published}
            )
    return {
        "compared_to": compared_to,
        "n_match": len(rows) - len(mismatches),
        "n_seeds": len(rows),
        "mismatches": mismatches,
    }


def measure_mating(names: list[str], seeds: list[int]) -> dict:
    blocks: dict = {}
    for name in names:
        proto = MATING[name]
        print(f"\n=== mating {name} seeds {seeds[0]}..{seeds[-1]} ===", flush=True)
        columns: dict[str, list[dict]] = {}
        for key, tournament, mating in MATING_CONFIGS:
            print(f"  C# {key} ({tournament} / {mating}) ...", flush=True)
            rows = run_csharp(
                proto.name,
                proto.partitions,
                proto.pop,
                proto.gens,
                seeds[0],
                len(seeds),
                tournament,
                mating,
            )
            if any(row["igd"] is None for row in rows):
                raise RuntimeError(f"{name} {key} returned a skip IGD; unconstrained fronts should score")
            if [row["seed"] for row in rows] != seeds:
                raise RuntimeError(f"{name} {key} seed mismatch")
            columns[key] = rows
            for row in rows:
                print(f"    seed={row['seed']} igd={fmt(row['igd'])} g6={row['igd_g6']}", flush=True)

        default_rows = columns["default"]
        comparisons = {}
        for key, _, _ in MATING_CONFIGS:
            if key == "default":
                continue
            left = [float(row["igd"]) for row in columns[key]]
            right = [float(row["igd"]) for row in default_rows]
            comparisons[key] = hypothesis(left, right, left_name=key, right_name="default")

        published_checks = []
        if seeds == list(range(1, 16)) and name == "zdt1":
            published_checks.append(
                g6_check(
                    default_rows,
                    PUBLISHED_ZDT1_CSHARP_G6,
                    "default column vs docs/WILCOXON-RESULTS.md ZDT1 Unsga3 column",
                )
            )
        if seeds == list(range(1, 16)) and name == "dtlz2":
            published_checks.append(
                g6_check(
                    columns["algorithm2"],
                    PUBLISHED_DTLZ2_PYMOO_COMPAT_G6,
                    "algorithm2 column vs docs/WILCOXON-RESULTS.md DTLZ2 Unsga3 column "
                    "(that column is PymooCompatible, not Algorithm2)",
                )
            )

        per_seed = []
        for index, seed in enumerate(seeds):
            entry = {"seed": seed}
            for key, _, _ in MATING_CONFIGS:
                entry[key] = columns[key][index]["igd"]
                entry[f"{key}_g6"] = columns[key][index]["igd_g6"]
                entry[f"{key}_n"] = columns[key][index]["n"]
            per_seed.append(entry)

        blocks[name] = {
            "protocol": {
                "problem": proto.name,
                "partitions": proto.partitions,
                "pop": proto.pop,
                "gens": proto.gens,
                "seeds": seeds,
                "igd": "Unsga3 PerformanceIndicators.InvertedGenerationalDistance on the feasible non-dominated front",
                "reference": "ParetoFronts library sampler (ZDT 500 points; DTLZ2 Das-Dennis sphere at the same partitions)",
            },
            "seeds": per_seed,
            "summaries": {
                key: summarize([float(row["igd"]) for row in columns[key]])
                for key, _, _ in MATING_CONFIGS
            },
            "versus_default": comparisons,
            "published_g6_checks": published_checks,
        }
    return blocks


def summary_cells(summary: dict | None) -> str:
    if summary is None:
        return "skip: no paired feasible seeds"
    return (
        f"n={summary['n']} median={fmt(summary['median'])} "
        f"mean={fmt(summary['mean'])} std={fmt(summary['std'])} "
        f"min={fmt(summary['min'])} max={fmt(summary['max'])}"
    )


def markdown_report(payload: dict) -> str:
    lines = [
        "# New surfaces: constrained problems and mating opt-ins",
        "",
        f"Measured {payload['measured_on']}. pymoo {payload['pymoo_version']}. "
        f".NET SDK {payload['dotnet_version']}.",
        "",
        "Generated by `tools/oracle/run_new_surfaces.py`. "
        "Every numeric cell is that run. A cell that was not measured is `skip:`. "
        "This file does not replace [`WILCOXON-RESULTS.md`](WILCOXON-RESULTS.md).",
        "",
        "## Command",
        "",
        "```bash",
        payload["command"],
        "```",
        "",
        "C# side, one config, all seeds in one process:",
        "",
        "```bash",
        "dotnet run --project tools/OracleCompare -c Release -- \\",
        "  --problem zdt1 --partitions 12 --pop 52 --gens 100 --seed 1 --seed-count 15 \\",
        "  --tournament rank-niche --mating independent",
        "dotnet run --project tools/OracleCompare -c Release -- \\",
        "  --problem osy --partitions 12 --pop 52 --gens 250 --seed 1 --seed-count 15",
        "dotnet run --project tools/OracleCompare -c Release -- \\",
        "  --problem c1dtlz1 --partitions 12 --pop 92 --gens 150 --seed 1 --seed-count 15",
        "```",
        "",
        "Omitted `--gens` on `osy` and `tnk` is 250. Omitted `--gens` on `c1dtlz1` is 150. "
        "Omitted `--tournament` is `RankNicheDistance`. Omitted `--mating` is `IndependentWithReplacement`.",
        "",
        "## Protocol",
        "",
        "Operators on both sides: SBX η=30, p_c=1, PM η=20, p_m=1/n, duplicate elimination on, "
        "single-layer Das–Dennis. Seeds are `1 .. n` (published ZDT1 / DTLZ2 indexes). "
        "C# and pymoo do not share an RNG stream. Pairing is by seed index.",
        "",
        "### Constrained",
        "",
        "| Problem | M | n_var | Partitions | Refs | Pop | Gens | C# tournament | C# mating | pymoo |",
        "|---------|---|------:|------------|-----:|----:|-----:|---------------|-----------|-------|",
    ]
    for name, block in payload["constrained"].items():
        p = block["protocol"]
        lines.append(
            f"| {name} | {p['n_obj']} | {p['n_var']} | {p['partitions']} | {p['n_ref_dirs']} | "
            f"{p['pop']} | {p['gens']} | {p['csharp_tournament']} | {p['csharp_mating']} | "
            f"{p['pymoo_algorithm']} defaults |"
        )
    lines += [
        "",
        "Scored set for the comparison: feasible non-dominated subset of the **final population**, "
        "both solvers, one `pymoo.indicators.igd.IGD` call, one shared reference front. "
        "OSY and TNK use pymoo's shipped Pareto-front file (`pareto_front()`). "
        "C1-DTLZ1 uses `pareto_front(das-dennis partitions=12)` at **n_var=7** (k=5). "
        "That front is the DTLZ1 simplex (0.5 × the directions). The C1 inequality does not remove it. "
        "pymoo's default `get_problem(\"c1dtlz1\")` is n_var=12 and is not this protocol.",
        "",
        "C# constructor defaults are not pymoo's mating. pymoo `UNSGA3` uses "
        "`comp_by_rank_and_ref_line_dist` and two shuffled tournament passes. "
        "The table is default Unsga3 against default pymoo UNSGA3 on a shared front definition. "
        "It is not a mating-matched ablation. The mating section is that ablation, on unconstrained problems. "
        "Each pymoo seed is run twice. The table is the first run. A second value is listed only when it differs. "
        "The two runs are not averaged.",
        "",
        "`pymoo res.F` is reported in its own column. It is the survival result, not the feasible "
        "non-dominated population, and it is not an input to the hypothesis tests.",
        "",
        "A seed with no feasible point on either side is `skip: no feasible points` and is left out of "
        "the median and the tests. Tests run only when at least five seeds are paired.",
        "",
        "### Mating opt-ins",
        "",
        "| Problem | Partitions | Pop | Gens | Reference front | Seeds |",
        "|---------|------------|----:|-----:|-----------------|-------|",
    ]
    for name, block in payload["mating"].items():
        p = block["protocol"]
        lines.append(
            f"| {name} | {p['partitions']} | {p['pop']} | {p['gens']} | library sampler | "
            f"{p['seeds'][0]}..{p['seeds'][-1]} |"
        )
    lines += [
        "",
        "IGD is the C# feasible non-dominated front against `ParetoFronts` "
        "(ZDT: 500 points; DTLZ2: Das–Dennis sphere at the same partitions). "
        "That is the definition of the published ZDT1 C# column.",
        "",
        "| Column | Tournament | Mating pool |",
        "|--------|------------|-------------|",
        "| default | RankNicheDistance | IndependentWithReplacement |",
        "| two_shuffled | RankNicheDistance | TwoShuffledPasses |",
        "| algorithm2 | Algorithm2 | IndependentWithReplacement |",
        "| both | Algorithm2 | TwoShuffledPasses |",
        "",
        "ZDT2 uses gens=250. Its default column is `RankNicheDistance`, not `PymooCompatible`. "
        "There is no published ZDT2 Wilcoxon column. "
        "DTLZ2's default column is also `RankNicheDistance`. "
        "The published DTLZ2 column is `PymooCompatible` and is not rewritten here.",
        "",
        "Paired tests compare each opt-in with the default column (same seed index). "
        "Lower IGD is better. p-values are two-sided and not multiplicity-adjusted.",
        "",
    ]

    lines += ["## Constrained results", ""]
    if not payload["constrained"]:
        lines += ["skip: constrained sweep not requested", ""]
    for name, block in payload["constrained"].items():
        lines += [f"### {name.upper()}", ""]
        lines.append(f"- C#: {summary_cells(block['csharp_summary'])}")
        lines.append(f"- pymoo feasible ND: {summary_cells(block['pymoo_summary'])}")
        lines.append(f"- Unpaired skips: {block['n_skipped_pairs']}")
        p = block["protocol"]
        lines.append(
            "- Reference front: "
            f"n={p['pf_points']} min={fmt_vec(p['pf_min'])} max={fmt_vec(p['pf_max'])}"
        )
        tests = block["tests"]
        if tests is None:
            lines.append("- Hypothesis tests: skip: fewer than 5 paired feasible seeds")
        else:
            lines += [
                "",
                "| Test | Statistic | p-value | Verdict (α=0.05) |",
                "|------|-----------|---------|------------------|",
                f"| Mann–Whitney U (U₁) | {fmt(tests['mannwhitney_U'])} | {fmt(tests['mannwhitney_p'])} | {tests['mannwhitney_verdict']} |",
                f"| Wilcoxon signed-rank | {fmt(tests['wilcoxon_W'])} | {fmt(tests['wilcoxon_p'])} | {tests['wilcoxon_verdict']} |",
                "",
                f"- Median ratio (C# / pymoo), feasible ND: **{fmt(tests['median_ratio'])}**",
                f"- Better median: **{tests['better_median']}**",
                f"- n paired: **{tests['n']}**",
            ]
        lines += [
            "",
            "| Seed | C# feasible ND IGD | pymoo feasible ND IGD | pymoo res.F IGD | C# n | pymoo n |",
            "|------|--------------------|-----------------------|-----------------|-----:|--------:|",
        ]
        for row in block["seeds"]:
            lines.append(
                f"| {row['seed']} | {cell(row['csharp_igd'])} | {cell(row['pymoo_igd'])} | "
                f"{cell(row['pymoo_resf_igd'])} | {row['csharp_n']} | {row['pymoo_n']} |"
            )
        lines.append("")
        diffs = [row for row in block["seeds"] if not row.get("pymoo_repeat_match", True)]
        if diffs:
            lines.append(
                "A second pymoo run of the same seed is not always bit-identical on this host "
                "(SciPy OpenBLAS, dynamic threads). The table cell is the first run. "
                "The second run differed for:"
            )
            for row in diffs:
                lines.append(
                    f"- seed {row['seed']} feasible ND: table {cell(row['pymoo_igd'])}, "
                    f"repeat {cell(row['pymoo_igd_repeat'])}; "
                    f"res.F: table {cell(row['pymoo_resf_igd'])}, "
                    f"repeat {cell(row['pymoo_resf_igd_repeat'])}"
                )
            lines.append("The two runs are not averaged. Hypothesis tests use the table column.")
        else:
            lines.append(
                "A second pymoo run of each seed matched the table cell (feasible ND and res.F)."
            )
        lines.append("")

    lines += ["## Mating opt-in results", ""]
    if not payload["mating"]:
        lines += ["skip: mating sweep not requested", ""]
    for name, block in payload["mating"].items():
        lines += [f"### {name.upper()}", ""]
        lines += [
            "| Config | n | median | mean | std | min | max |",
            "|--------|--:|--------|------|-----|-----|-----|",
        ]
        for key, _, _ in MATING_CONFIGS:
            summary = block["summaries"][key]
            lines.append(
                f"| {key} | {summary['n']} | {fmt(summary['median'])} | {fmt(summary['mean'])} | "
                f"{fmt(summary['std'])} | {fmt(summary['min'])} | {fmt(summary['max'])} |"
            )
        lines.append("")
        if block["versus_default"]:
            lines += [
                "| Opt-in vs default | U₁ | MWU p | W | signed-rank p | median ratio (opt-in / default) | better median |",
                "|-------------------|----|-------|---|----------------|---------------------------------|---------------|",
            ]
            for key, tests in block["versus_default"].items():
                lines.append(
                    f"| {key} | {fmt(tests['mannwhitney_U'])} | {fmt(tests['mannwhitney_p'])} | "
                    f"{fmt(tests['wilcoxon_W'])} | {fmt(tests['wilcoxon_p'])} | "
                    f"{fmt(tests['median_ratio'])} | {tests['better_median']} |"
                )
            lines.append("")
        for check in block["published_g6_checks"]:
            if check["mismatches"]:
                lines.append(
                    f"Published G6 check ({check['compared_to']}): "
                    f"{check['n_match']}/{check['n_seeds']} match. "
                    "Mismatches (measured, published): "
                    + ", ".join(
                        f"seed {item['seed']} {item['measured_g6']} vs {item['published_g6']}"
                        for item in check["mismatches"]
                    )
                    + ". The published file was not rewritten."
                )
            else:
                lines.append(
                    f"Published G6 check ({check['compared_to']}): "
                    f"{check['n_match']}/{check['n_seeds']} match. The published file was not rewritten."
                )
            lines.append("")
        header = "| Seed | " + " | ".join(key for key, _, _ in MATING_CONFIGS) + " |"
        rule = "|------|" + "|".join("------" for _ in MATING_CONFIGS) + "|"
        lines += [header, rule]
        for row in block["seeds"]:
            cells = " | ".join(fmt(row[key]) for key, _, _ in MATING_CONFIGS)
            lines.append(f"| {row['seed']} | {cells} |")
        lines.append("")

    lines += [
        "## Notes",
        "",
        "- Package defaults are unchanged: `RankNicheDistance`, `IndependentWithReplacement`, single-layer Das–Dennis.",
        "- Front files under `tools/oracle/out/` are gitignored.",
        "- Hypothesis tests reuse `mannwhitney_u` and `wilcoxon_signed_rank` from `run_multiseed_wilcoxon.py`.",
        "",
    ]
    return "\n".join(lines) + "\n"


def fmt_vec(values: list[float]) -> str:
    return "[" + ", ".join(fmt(v) for v in values) + "]"


def cell(value: object) -> str:
    if value is None:
        return "skip: no feasible points"
    return fmt(float(value))  # type: ignore[arg-type]


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--seeds", type=int, default=15)
    parser.add_argument("--seed-start", type=int, default=1)
    parser.add_argument(
        "--constrained",
        nargs="*",
        default=["osy", "tnk", "c1dtlz1"],
        choices=sorted(CONSTRAINED),
    )
    parser.add_argument(
        "--mating",
        nargs="*",
        default=["zdt1", "zdt2", "dtlz2"],
        choices=sorted(MATING),
    )
    parser.add_argument("--skip-constrained", action="store_true")
    parser.add_argument("--skip-mating", action="store_true")
    parser.add_argument("--skip-build", action="store_true")
    parser.add_argument("--report", type=Path, default=ROOT / "docs" / "NEW-SURFACES-RESULTS.md")
    args = parser.parse_args()
    if args.seeds < 1:
        print("--seeds must be at least 1", file=sys.stderr)
        return 1

    constrained = [] if args.skip_constrained else list(args.constrained)
    mating = [] if args.skip_mating else list(args.mating)
    seeds = list(range(args.seed_start, args.seed_start + args.seeds))
    OUT.mkdir(parents=True, exist_ok=True)

    print("Checking constrained formulations against the C# fixtures...", flush=True)
    assert_formulations()

    if not args.skip_build:
        print("Building OracleCompare (Release)...", flush=True)
        build = subprocess.run(
            ["dotnet", "build", str(ORACLE_COMPARE), "-c", "Release", "--nologo", "-v", "q"],
            cwd=str(ROOT),
        )
        if build.returncode != 0:
            return build.returncode

    dotnet_version = subprocess.check_output(["dotnet", "--version"], text=True, cwd=str(ROOT)).strip()

    command = "python tools/oracle/run_new_surfaces.py"
    if constrained != ["osy", "tnk", "c1dtlz1"] or mating != ["zdt1", "zdt2", "dtlz2"] or args.seeds != 15 or args.seed_start != 1:
        command = "python tools/oracle/run_new_surfaces.py " + " ".join(sys.argv[1:])

    payload: dict = {
        "measured_on": date.today().isoformat(),
        "pymoo_version": pymoo.__version__,
        "dotnet_version": dotnet_version,
        "command": command,
        "n_seeds": len(seeds),
        "seed_start": args.seed_start,
        "constrained": measure_constrained(constrained, seeds) if constrained else {},
        "mating": measure_mating(mating, seeds) if mating else {},
    }

    json_path = OUT / "new_surfaces_results.json"
    json_path.write_text(json.dumps(payload, indent=2), encoding="utf-8")
    report = markdown_report(payload)
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(report, encoding="utf-8")
    print(f"\nwrote {json_path}")
    print(f"wrote {args.report}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
