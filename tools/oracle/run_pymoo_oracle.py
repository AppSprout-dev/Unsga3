#!/usr/bin/env python3
"""
pymoo UNSGA3 oracle for Unsga3 equivalence.

Fixed protocol (docs/EQUIVALENCE.md + RESEARCH-STANDARDS.md):
  SBX η=30, PM η=20 (pymoo defaults for NSGA3/UNSGA3),
  Das-Dennis refs, seed=1, export final F + IGD.

Usage:
  python run_pymoo_oracle.py
  python run_pymoo_oracle.py --problem zdt1 --partitions 12 --pop 52 --gens 100 --seed 1
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

import numpy as np


def main() -> int:
    p = argparse.ArgumentParser()
    p.add_argument("--problem", default="zdt1", choices=["zdt1", "zdt2", "dtlz2"])
    p.add_argument("--partitions", type=int, default=12)
    p.add_argument("--pop", type=int, default=None, help="default = n_ref_dirs")
    p.add_argument("--gens", type=int, default=100)
    p.add_argument("--seed", type=int, default=1)
    p.add_argument("--out-dir", type=Path, default=Path(__file__).resolve().parent / "out")
    args = p.parse_args()

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

    if args.problem == "dtlz2":
        n_obj = 3
        problem = get_problem("dtlz2", n_obj=n_obj)
        ref_dirs = get_reference_directions("das-dennis", n_obj, n_partitions=args.partitions)
        pf = problem.pareto_front(ref_dirs)
    else:
        n_obj = 2
        problem = get_problem(args.problem)
        ref_dirs = get_reference_directions("das-dennis", n_obj, n_partitions=args.partitions)
        pf = problem.pareto_front()

    pop = args.pop if args.pop is not None else len(ref_dirs)
    algo = UNSGA3(ref_dirs, pop_size=pop)

    print(f"pymoo UNSGA3 | problem={args.problem} M={n_obj} refs={len(ref_dirs)} "
          f"pop={pop} gens={args.gens} seed={args.seed}")

    res = minimize(
        problem,
        algo,
        ("n_gen", args.gens),
        seed=args.seed,
        verbose=False,
        save_history=False,
    )

    F = np.atleast_2d(res.F)
    igd = float(IGD(pf)(F))

    args.out_dir.mkdir(parents=True, exist_ok=True)
    stem = f"pymoo_{args.problem}_p{args.partitions}_pop{pop}_g{args.gens}_s{args.seed}"
    f_path = args.out_dir / f"{stem}_F.csv"
    meta_path = args.out_dir / f"{stem}_meta.json"
    np.savetxt(f_path, F, delimiter=",")
    meta = {
        "source": "pymoo",
        "algorithm": "UNSGA3",
        "problem": args.problem,
        "n_obj": n_obj,
        "partitions": args.partitions,
        "n_ref_dirs": int(len(ref_dirs)),
        "pop_size": pop,
        "n_gen": args.gens,
        "seed": args.seed,
        "n_solutions": int(F.shape[0]),
        "igd": igd,
        "F_csv": str(f_path.name),
    }
    meta_path.write_text(json.dumps(meta, indent=2), encoding="utf-8")

    print(f"IGD={igd:.6g}")
    print(f"wrote {f_path}")
    print(f"wrote {meta_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
