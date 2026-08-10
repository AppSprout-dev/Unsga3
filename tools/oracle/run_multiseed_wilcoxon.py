#!/usr/bin/env python3
"""
Multi-seed IGD oracle: Unsga3 (C#) vs pymoo UNSGA3 + Mann–Whitney U / Wilcoxon tests.

Protocol matches docs/EQUIVALENCE.md and docs/RESEARCH-STANDARDS.md:
  15 independent seeds (default), fixed SBX/PM/Das–Dennis settings.

Usage:
  python tools/oracle/run_multiseed_wilcoxon.py
  python tools/oracle/run_multiseed_wilcoxon.py --problems zdt1 dtlz2 --seeds 15 --skip-pymoo
  python tools/oracle/run_multiseed_wilcoxon.py --problems dtlz2 --seeds 5 --csharp-only
"""
from __future__ import annotations

import argparse
import json
import math
import statistics
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = Path(__file__).resolve().parent / "out"
ORACLE_COMPARE = ROOT / "tools" / "OracleCompare"


@dataclass
class Protocol:
    name: str
    partitions: int
    pop: int
    gens: int
    n_obj: int
    csharp_pymoo_mode: bool  # True → TournamentMode.PymooCompatible


PROTOCOLS: dict[str, Protocol] = {
    "zdt1": Protocol("zdt1", partitions=12, pop=52, gens=100, n_obj=2, csharp_pymoo_mode=False),
    "zdt2": Protocol("zdt2", partitions=12, pop=52, gens=100, n_obj=2, csharp_pymoo_mode=False),
    "dtlz2": Protocol("dtlz2", partitions=12, pop=92, gens=150, n_obj=3, csharp_pymoo_mode=True),
}


def _rankdata(a: list[float]) -> list[float]:
    """Average ranks for ties (1-based)."""
    n = len(a)
    order = sorted(range(n), key=lambda i: a[i])
    ranks = [0.0] * n
    i = 0
    while i < n:
        j = i
        while j + 1 < n and a[order[j + 1]] == a[order[i]]:
            j += 1
        # ranks i..j (0-based in sorted order) → average of (i+1)..(j+1)
        avg = (i + 1 + j + 1) / 2.0
        for k in range(i, j + 1):
            ranks[order[k]] = avg
        i = j + 1
    return ranks


def mannwhitney_u(x: list[float], y: list[float]) -> tuple[float, float, str]:
    """
    Two-sided Mann–Whitney U (Wilcoxon rank-sum) with normal approximation + tie correction.
    Returns (U_x, p_two_sided, note). Lower IGD is better — report who has smaller median.
    """
    n1, n2 = len(x), len(y)
    combined = x + y
    ranks = _rankdata(combined)
    r1 = sum(ranks[:n1])
    u1 = r1 - n1 * (n1 + 1) / 2.0
    u2 = n1 * n2 - u1
    u = min(u1, u2)

    # Normal approximation
    mu = n1 * n2 / 2.0
    # Tie correction
    from collections import Counter

    counts = Counter(combined)
    tie_term = sum(t * t * t - t for t in counts.values()) / 12.0
    sigma2 = n1 * n2 / 12.0 * ((n1 + n2 + 1) - tie_term * 12.0 / ((n1 + n2) * (n1 + n2 - 1)))
    if sigma2 <= 0:
        return u1, 1.0, "degenerate variance"
    sigma = math.sqrt(sigma2)
    # continuity correction
    z = (u - mu + 0.5) / sigma if u < mu else (u - mu - 0.5) / sigma
    # two-sided from |z| via erfc
    p = math.erfc(abs(z) / math.sqrt(2.0))
    return u1, p, f"z={z:.4g}"


def wilcoxon_signed_rank(diff: list[float]) -> tuple[float, float, str]:
    """
    Two-sided Wilcoxon signed-rank on paired differences (csharp - pymoo).
    Zeros dropped. Normal approximation with continuity correction.
    """
    d = [v for v in diff if v != 0.0]
    n = len(d)
    if n < 5:
        return float("nan"), float("nan"), f"n_eff={n} too small"

    abs_d = [abs(v) for v in d]
    ranks = _rankdata(abs_d)
    w_plus = sum(r for v, r in zip(d, ranks) if v > 0)
    w_minus = sum(r for v, r in zip(d, ranks) if v < 0)
    w = min(w_plus, w_minus)

    mu = n * (n + 1) / 4.0
    # tie correction on absolute values
    from collections import Counter

    counts = Counter(abs_d)
    tie_term = sum(t * t * t - t for t in counts.values()) / 48.0
    sigma2 = n * (n + 1) * (2 * n + 1) / 24.0 - tie_term
    if sigma2 <= 0:
        return w, 1.0, "degenerate variance"
    sigma = math.sqrt(sigma2)
    z = (w - mu + 0.5) / sigma if w < mu else (w - mu - 0.5) / sigma
    p = math.erfc(abs(z) / math.sqrt(2.0))
    return w, p, f"W+={w_plus:.1f} W-={w_minus:.1f} z={z:.4g}"


def run_csharp(proto: Protocol, seed: int) -> float:
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
        proto.name,
        "--partitions",
        str(proto.partitions),
        "--pop",
        str(proto.pop),
        "--gens",
        str(proto.gens),
        "--seed",
        str(seed),
        "--out-dir",
        str(OUT),
    ]
    if proto.csharp_pymoo_mode:
        args.append("--pymoo-mode")
    r = subprocess.run(args, capture_output=True, text=True, cwd=str(ROOT))
    if r.returncode != 0:
        raise RuntimeError(f"C# seed={seed} failed:\n{r.stdout}\n{r.stderr}")
    for line in r.stdout.splitlines():
        if line.startswith("IGD="):
            return float(line.split("=", 1)[1])
    raise RuntimeError(f"No IGD= in C# output:\n{r.stdout}")


def run_pymoo(proto: Protocol, seed: int) -> float:
    try:
        from pymoo.algorithms.moo.unsga3 import UNSGA3
        from pymoo.indicators.igd import IGD
        from pymoo.optimize import minimize
        from pymoo.problems import get_problem
        from pymoo.util.ref_dirs import get_reference_directions
    except ImportError as e:
        raise RuntimeError("pymoo not installed") from e

    import numpy as np

    if proto.name == "dtlz2":
        problem = get_problem("dtlz2", n_obj=proto.n_obj)
        ref_dirs = get_reference_directions(
            "das-dennis", proto.n_obj, n_partitions=proto.partitions
        )
        pf = problem.pareto_front(ref_dirs)
    else:
        problem = get_problem(proto.name)
        ref_dirs = get_reference_directions(
            "das-dennis", proto.n_obj, n_partitions=proto.partitions
        )
        pf = problem.pareto_front()

    algo = UNSGA3(ref_dirs, pop_size=proto.pop)
    res = minimize(
        problem, algo, ("n_gen", proto.gens), seed=seed, verbose=False, save_history=False
    )
    F = np.atleast_2d(res.F)
    igd = float(IGD(pf)(F))

    OUT.mkdir(parents=True, exist_ok=True)
    stem = f"pymoo_{proto.name}_p{proto.partitions}_pop{proto.pop}_g{proto.gens}_s{seed}"
    np.savetxt(OUT / f"{stem}_F.csv", F, delimiter=",")
    (OUT / f"{stem}_meta.json").write_text(
        json.dumps(
            {
                "source": "pymoo",
                "algorithm": "UNSGA3",
                "problem": proto.name,
                "pop_size": proto.pop,
                "n_gen": proto.gens,
                "seed": seed,
                "igd": igd,
            },
            indent=2,
        ),
        encoding="utf-8",
    )
    return igd


def summarize(vals: list[float]) -> dict:
    s = sorted(vals)
    n = len(s)
    med = statistics.median(s)
    mean = statistics.fmean(s)
    std = statistics.stdev(s) if n > 1 else 0.0
    q1 = s[(n - 1) // 4]
    q3 = s[(3 * (n - 1)) // 4]
    return {
        "n": n,
        "mean": mean,
        "std": std,
        "median": med,
        "q1": q1,
        "q3": q3,
        "min": s[0],
        "max": s[-1],
        "values": vals,
    }


def fmt(x: float) -> str:
    return f"{x:.6g}"


def markdown_report(results: dict) -> str:
    lines = [
        "# Multi-seed Wilcoxon: Unsga3 vs pymoo UNSGA3",
        "",
        f"Generated by `tools/oracle/run_multiseed_wilcoxon.py`. "
        f"IGD = mean nearest Euclidean distance (pymoo-compatible).",
        "",
        "## Protocol",
        "",
        "| Problem | Pop | Gens | Partitions | C# tournament | Seeds |",
        "|---------|-----|------|------------|---------------|-------|",
    ]
    for name, block in results["problems"].items():
        p = block["protocol"]
        lines.append(
            f"| {name} | {p['pop']} | {p['gens']} | {p['partitions']} | "
            f"{'PymooCompatible' if p['csharp_pymoo_mode'] else 'RankNicheDistance'} | "
            f"{results['n_seeds']} |"
        )
    lines += [
        "",
        "Hypothesis tests (α = 0.05, two-sided):",
        "",
        "- **Mann–Whitney U** (Wilcoxon rank-sum): independent samples, H₀: same IGD distribution.",
        "- **Wilcoxon signed-rank** (paired by seed index): H₀: median(C# − pymoo) = 0. "
        "Seeds are not shared RNG streams — pairing is by run index only.",
        "",
        "Lower IGD is better.",
        "",
    ]

    for name, block in results["problems"].items():
        cs = block["csharp"]
        py = block.get("pymoo")
        lines += [
            f"## {name.upper()}",
            "",
            "### Summary",
            "",
            "| Impl | n | mean | std | median | Q1 | Q3 | min | max |",
            "|------|---|------|-----|--------|----|----|-----|-----|",
            f"| **Unsga3** | {cs['n']} | {fmt(cs['mean'])} | {fmt(cs['std'])} | "
            f"**{fmt(cs['median'])}** | {fmt(cs['q1'])} | {fmt(cs['q3'])} | "
            f"{fmt(cs['min'])} | {fmt(cs['max'])} |",
        ]
        if py:
            lines.append(
                f"| **pymoo** | {py['n']} | {fmt(py['mean'])} | {fmt(py['std'])} | "
                f"**{fmt(py['median'])}** | {fmt(py['q1'])} | {fmt(py['q3'])} | "
                f"{fmt(py['min'])} | {fmt(py['max'])} |"
            )
        lines.append("")

        if "tests" in block:
            t = block["tests"]
            lines += [
                "### Hypothesis tests",
                "",
                f"| Test | Statistic | p-value | Verdict (α=0.05) |",
                f"|------|-----------|---------|------------------|",
                f"| Mann–Whitney U (U₁) | {fmt(t['mannwhitney']['U'])} | "
                f"{fmt(t['mannwhitney']['p'])} | {t['mannwhitney']['verdict']} |",
                f"| Wilcoxon signed-rank | {fmt(t['wilcoxon']['W'])} | "
                f"{fmt(t['wilcoxon']['p'])} | {t['wilcoxon']['verdict']} |",
                "",
                f"- Median ratio (C# / pymoo): **{fmt(t['median_ratio'])}**",
                f"- Mean ratio (C# / pymoo): **{fmt(t['mean_ratio'])}**",
                f"- Better median: **{t['better_median']}**",
                "",
            ]

        lines += [
            "### Per-seed IGD",
            "",
            "| Seed | Unsga3 | pymoo | Δ (C#−pymoo) |",
            "|------|--------|-------|--------------|",
        ]
        py_vals = py["values"] if py else [None] * len(cs["values"])
        for i, (a, b) in enumerate(zip(cs["values"], py_vals), start=1):
            if b is None:
                lines.append(f"| {i} | {fmt(a)} | — | — |")
            else:
                lines.append(f"| {i} | {fmt(a)} | {fmt(b)} | {fmt(a - b)} |")
        lines.append("")

    lines += [
        "## Notes",
        "",
        "- Not bit-identical: different RNG implementations and minor operator ordering.",
        "- Practical equivalence: median IGD within ~1–2× and non-significant MWU is a strong claim; "
        "significant differences with small effect size (ratio ≈ 1) are still acceptable for a v0.x port.",
        "- Reproduce: `python tools/oracle/run_multiseed_wilcoxon.py`",
        "",
    ]
    return "\n".join(lines)


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--problems", nargs="+", default=["zdt1", "dtlz2"], choices=list(PROTOCOLS))
    ap.add_argument("--seeds", type=int, default=15, help="number of seeds starting at 1")
    ap.add_argument("--seed-start", type=int, default=1)
    ap.add_argument("--skip-pymoo", action="store_true")
    ap.add_argument("--csharp-only", action="store_true", help="alias for --skip-pymoo")
    ap.add_argument("--skip-build", action="store_true")
    ap.add_argument(
        "--report",
        type=Path,
        default=ROOT / "docs" / "WILCOXON-RESULTS.md",
    )
    args = ap.parse_args()
    skip_pymoo = args.skip_pymoo or args.csharp_only
    seeds = list(range(args.seed_start, args.seed_start + args.seeds))

    OUT.mkdir(parents=True, exist_ok=True)

    if not args.skip_build:
        print("Building OracleCompare (Release)...", flush=True)
        b = subprocess.run(
            ["dotnet", "build", str(ORACLE_COMPARE), "-c", "Release", "--nologo", "-v", "q"],
            cwd=str(ROOT),
        )
        if b.returncode != 0:
            return b.returncode

    results: dict = {
        "n_seeds": len(seeds),
        "seed_start": args.seed_start,
        "problems": {},
    }

    for pname in args.problems:
        proto = PROTOCOLS[pname]
        print(f"\n=== {pname} | seeds {seeds[0]}..{seeds[-1]} ===", flush=True)
        cs_igds: list[float] = []
        py_igds: list[float] = []

        for s in seeds:
            print(f"  C#  seed={s} ...", end=" ", flush=True)
            cigd = run_csharp(proto, s)
            cs_igds.append(cigd)
            print(f"IGD={cigd:.6g}", flush=True)

            if not skip_pymoo:
                print(f"  py  seed={s} ...", end=" ", flush=True)
                try:
                    pigd = run_pymoo(proto, s)
                except RuntimeError as e:
                    print(f"FAILED: {e}", flush=True)
                    skip_pymoo = True
                    break
                py_igds.append(pigd)
                print(f"IGD={pigd:.6g}", flush=True)

        block: dict = {
            "protocol": {
                "name": proto.name,
                "partitions": proto.partitions,
                "pop": proto.pop,
                "gens": proto.gens,
                "csharp_pymoo_mode": proto.csharp_pymoo_mode,
            },
            "csharp": summarize(cs_igds),
        }
        if py_igds and len(py_igds) == len(cs_igds):
            block["pymoo"] = summarize(py_igds)
            u, p_mw, note_mw = mannwhitney_u(cs_igds, py_igds)
            diffs = [a - b for a, b in zip(cs_igds, py_igds)]
            w, p_wx, note_wx = wilcoxon_signed_rank(diffs)
            cs_med = block["csharp"]["median"]
            py_med = block["pymoo"]["median"]
            better = "Unsga3" if cs_med < py_med else ("pymoo" if py_med < cs_med else "tie")

            def verdict(p: float) -> str:
                if math.isnan(p):
                    return "n/a"
                if p < 0.05:
                    return f"reject H₀ (differs; better median = {better})"
                return "fail to reject H₀ (no significant difference)"

            block["tests"] = {
                "mannwhitney": {"U": u, "p": p_mw, "note": note_mw, "verdict": verdict(p_mw)},
                "wilcoxon": {"W": w, "p": p_wx, "note": note_wx, "verdict": verdict(p_wx)},
                "median_ratio": cs_med / py_med if py_med else float("nan"),
                "mean_ratio": block["csharp"]["mean"] / block["pymoo"]["mean"],
                "better_median": better,
            }
            print(
                f"  → median C#={cs_med:.6g} pymoo={py_med:.6g} "
                f"ratio={block['tests']['median_ratio']:.3f} "
                f"MWU p={p_mw:.4g} WSR p={p_wx:.4g}",
                flush=True,
            )
        else:
            print(f"  → C# median={block['csharp']['median']:.6g} (pymoo skipped)", flush=True)

        results["problems"][pname] = block

    # Write outputs
    json_path = OUT / "wilcoxon_results.json"
    json_path.write_text(json.dumps(results, indent=2), encoding="utf-8")
    report = markdown_report(results)
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(report, encoding="utf-8")
    print(f"\nwrote {json_path}")
    print(f"wrote {args.report}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
