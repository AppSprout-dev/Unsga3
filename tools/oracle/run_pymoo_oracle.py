#!/usr/bin/env python3
"""
pymoo UNSGA3 oracle for Unsga3 equivalence.

Fixed protocol (docs/EQUIVALENCE.md + RESEARCH-STANDARDS.md):
  SBX η=30, PM η=20 (pymoo defaults for NSGA3/UNSGA3),
  Das-Dennis refs, seed=1, export final F + IGD.

The exported front is pymoo res.F (the survival niche set, about one point per
filled reference direction). It is not the final population's full non-dominated
front. C# OracleCompare scores that full front. Compare those files only after
putting both sides on the same front definition and the same reference set.

DTLZ2 decision dimension: C# Dtlz2Problem(k=10) uses n_var = M + k - 1 = 12.
pymoo get_problem("dtlz2", n_obj=3) defaults to n_var=10 (k=8). This script
passes n_var=12 unless --n-var is set. --n-var 10 reproduces the historical
mismatched column only; it is not the apples-to-apples protocol.

Usage:
  python run_pymoo_oracle.py
  python run_pymoo_oracle.py --problem zdt1 --partitions 12 --pop 52 --gens 100 --seed 1
  python run_pymoo_oracle.py --problem zdt2 --partitions 12 --pop 52 --seed 1
  python run_pymoo_oracle.py --problem dtlz2 --partitions 12 --pop 92 --gens 150 --seed 1
  # omitted --gens on zdt2 is 250 (quality protocol; matches unsga3-bend A/B).
  # --gens 100 is an early-stress snapshot, not the quality bar.
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

import numpy as np


# Deb et al. suggest k=10 for DTLZ2. C# Dtlz2Problem defaults to that k.
DTLZ2_K = 10


def dtlz2_n_var(n_obj: int, k: int = DTLZ2_K) -> int:
    """n = M + k - 1. For M=3, k=10 this is 12, not pymoo's default 10."""
    return n_obj + k - 1


def default_gens(problem: str) -> int:
    """Quality-protocol generations when --gens is omitted.

    ZDT2 A/B default is 250 (unsga3-bend honesty). ZDT1 stays 100.
    DTLZ2 stays 100 here so existing callers that omit --gens are unchanged;
    the published DTLZ2 oracle still passes --gens 150.
    """
    if problem == "zdt2":
        return 250
    return 100


def main() -> int:
    p = argparse.ArgumentParser()
    p.add_argument("--problem", default="zdt1", choices=["zdt1", "zdt2", "dtlz2"])
    p.add_argument("--partitions", type=int, default=12)
    p.add_argument("--pop", type=int, default=None, help="default = n_ref_dirs")
    p.add_argument(
        "--gens",
        type=int,
        default=None,
        help="generations (default: zdt2=250, else 100; explicit value always wins)",
    )
    p.add_argument("--seed", type=int, default=1)
    p.add_argument(
        "--n-var",
        type=int,
        default=None,
        help=(
            "Decision variables. DTLZ2 default is M+k-1 with k=10 (n_var=12), "
            "matching Dtlz2Problem(k:10). pymoo's own default is 10 (k=8); "
            "pass --n-var 10 only to reproduce that historical mismatched column."
        ),
    )
    p.add_argument("--out-dir", type=Path, default=Path(__file__).resolve().parent / "out")
    args = p.parse_args()
    gens = args.gens if args.gens is not None else default_gens(args.problem)

    try:
        from pymoo.algorithms.moo.unsga3 import UNSGA3
        from pymoo.indicators.igd import IGD
        from pymoo.optimize import minimize
        from pymoo.problems import get_problem
        from pymoo.util.ref_dirs import get_reference_directions
    except ImportError as e:
        print("pymoo not installed. Run: pip install pymoo", file=sys.stderr)
        print(e, file=sys.stderr)
        return 2

    n_var: int | None
    if args.problem == "dtlz2":
        n_obj = 3
        n_var = args.n_var if args.n_var is not None else dtlz2_n_var(n_obj)
        problem = get_problem("dtlz2", n_obj=n_obj, n_var=n_var)
        ref_dirs = get_reference_directions("das-dennis", n_obj, n_partitions=args.partitions)
        pf = problem.pareto_front(ref_dirs)
    else:
        n_obj = 2
        problem = get_problem(args.problem)
        n_var = int(problem.n_var) if args.n_var is None else args.n_var
        if args.n_var is not None:
            problem = get_problem(args.problem, n_var=n_var)
        ref_dirs = get_reference_directions("das-dennis", n_obj, n_partitions=args.partitions)
        pf = problem.pareto_front()

    pop = args.pop if args.pop is not None else len(ref_dirs)
    algo = UNSGA3(ref_dirs, pop_size=pop)

    print(f"pymoo UNSGA3 | problem={args.problem} M={n_obj} n_var={n_var} refs={len(ref_dirs)} "
          f"pop={pop} gens={gens} seed={args.seed}")

    res = minimize(
        problem,
        algo,
        ("n_gen", gens),
        seed=args.seed,
        verbose=False,
        save_history=False,
    )

    F = np.atleast_2d(res.F)
    igd = float(IGD(pf)(F))

    args.out_dir.mkdir(parents=True, exist_ok=True)
    stem = f"pymoo_{args.problem}_p{args.partitions}_pop{pop}_g{gens}_s{args.seed}"
    f_path = args.out_dir / f"{stem}_F.csv"
    meta_path = args.out_dir / f"{stem}_meta.json"
    np.savetxt(f_path, F, delimiter=",")
    meta = {
        "source": "pymoo",
        "algorithm": "UNSGA3",
        "problem": args.problem,
        "n_obj": n_obj,
        "n_var": n_var,
        "k": (n_var - n_obj + 1) if args.problem == "dtlz2" else None,
        "partitions": args.partitions,
        "n_ref_dirs": int(len(ref_dirs)),
        "pop_size": pop,
        "n_gen": gens,
        "seed": args.seed,
        "n_solutions": int(F.shape[0]),
        "front_definition": "res.F",
        "igd": igd,
        "F_csv": str(f_path.name),
    }
    meta_path.write_text(json.dumps(meta, indent=2), encoding="utf-8")

    print(f"front=res.F n={int(F.shape[0])} (survival optimum, not the full population ND front)")
    print(f"IGD={igd:.6g}")
    print(f"wrote {f_path}")
    print(f"wrote {meta_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
