"""Diagnose C# vs pymoo DTLZ2 front quality gap."""
from __future__ import annotations

from pathlib import Path

import numpy as np

base = Path(__file__).resolve().parent / "out"


def load(name: str) -> np.ndarray:
    F = np.loadtxt(base / name, delimiter=",")
    return np.atleast_2d(F)


def stats(F: np.ndarray, label: str) -> None:
    n = len(F)
    uniq = np.unique(np.round(F, decimals=10), axis=0)
    r = np.linalg.norm(F, axis=1)
    res = np.abs(r - 1)
    nd = 0
    for i in range(n):
        dom = False
        for j in range(n):
            if i == j:
                continue
            if np.all(F[j] <= F[i]) and np.any(F[j] < F[i]):
                dom = True
                break
        if not dom:
            nd += 1
    print(f"=== {label} n={n} unique~={len(uniq)} ND={nd}")
    print(
        f"  radius mean={r.mean():.4f} std={r.std():.4f} "
        f"min={r.min():.4f} max={r.max():.4f}"
    )
    print(
        f"  |r-1| mean={res.mean():.4f} p50={np.median(res):.4f} "
        f"p95={np.percentile(res, 95):.4f} max={res.max():.4f}"
    )
    print(f"  f min={F.min(0)} max={F.max(0)} mean={F.mean(0)}")
    s = F.sum(1, keepdims=True)
    s[s == 0] = 1
    u = F / s
    print(f"  simplex coords range: {u.min(0)} .. {u.max(0)}")


def coverage(F: np.ndarray, pf: np.ndarray, thr: float = 0.05):
    dmin = np.array([np.min(np.linalg.norm(F - p, axis=1)) for p in pf])
    return (dmin < thr).mean(), dmin.mean(), dmin.max(), dmin


def main() -> None:
    cs = load("csharp_dtlz2_p12_pop92_g150_s1_pymoo_F.csv")
    py = load("pymoo_dtlz2_p12_pop92_g150_s1_F.csv")
    csd = load("csharp_dtlz2_p12_pop92_g150_s1_default_F.csv")

    stats(cs, "C# pymoo-mode")
    stats(csd, "C# default")
    stats(py, "pymoo")

    from pymoo.indicators.igd import IGD
    from pymoo.problems import get_problem
    from pymoo.util.ref_dirs import get_reference_directions

    ref = get_reference_directions("das-dennis", 3, n_partitions=12)
    pf = get_problem("dtlz2", n_obj=3).pareto_front(ref)
    print("PF size", len(pf), "refs", len(ref))

    for F, lab in [(cs, "cs-pymoo"), (csd, "cs-def"), (py, "pymoo")]:
        print(lab, "IGD", float(IGD(pf)(F)))

    for F, lab in [(cs, "cs-pymoo"), (csd, "cs-def"), (py, "pymoo")]:
        c, m, mx, dmin = coverage(F, pf)
        print(
            f"{lab} cover@0.05={c:.2%} mean_d={m:.4f} max_d={mx:.4f} "
            f"p90_d={np.percentile(dmin, 90):.4f}"
        )

    # Niche occupancy on unit-sphere PF directions
    def niche_counts(F: np.ndarray) -> np.ndarray:
        # associate F to ref dirs via angle (after L2-normalize F)
        Fn = F / np.maximum(np.linalg.norm(F, axis=1, keepdims=True), 1e-12)
        rn = ref / np.maximum(np.linalg.norm(ref, axis=1, keepdims=True), 1e-12)
        # cosine distance
        sim = Fn @ rn.T  # (n, n_ref)
        idx = sim.argmax(axis=1)
        counts = np.bincount(idx, minlength=len(ref))
        return counts

    for F, lab in [(cs, "cs-pymoo"), (csd, "cs-def"), (py, "pymoo")]:
        c = niche_counts(F)
        empty = (c == 0).sum()
        print(
            f"{lab} niches empty={empty}/{len(c)} max={c.max()} "
            f"mean={c.mean():.2f} gini-ish={(c.std() / (c.mean() + 1e-12)):.2f}"
        )


if __name__ == "__main__":
    main()
