# Equivalence vs pymoo / MATLAB

Goal: prove this port is a faithful U-NSGA-III (Seada & Deb 2016), not a look-alike.

See also **[RESEARCH-STANDARDS.md](RESEARCH-STANDARDS.md)** for the literature + pymoo protocol this suite follows.

## Primary sources

- COIN Report 2014022; Seada & Deb, IEEE TEVC 2016  
- [pymoo `UNSGA3`](https://pymoo.org/algorithms/moo/unsga3.html)  
- Indicators: [pymoo performance indicators](https://pymoo.org/misc/indicators.html) (GD, IGD, IGD+, HV)

## Protocol

1. **Fixed operators:** SBX η=30, PM η=20, p_c=1.0, p_m=1/n  
2. **Same reference set:** Das–Dennis partitions identical to the oracle  
3. **Same pop size / generations / seed** (or 15–31 seeds for statistics)  
4. **Metrics:** IGD (primary), IGD+, HV (M=2, document ref point), front plots for M≤3  
5. **Tolerance:** median IGD within ~1–2% of pymoo on ZDT/DTLZ is the v1 shipping bar  

## Problems (must-pass)

| Class | Problems | M | In library |
|-------|----------|---|------------|
| Single | Sphere, Ackley, Rosenbrock | 1 | yes |
| Bi | ZDT1–4, ZDT6 | 2 | yes |
| Many | DTLZ1–4, DTLZ7 | 3+ | yes |
| Hard | WFG1, WFG2, WFG9 | 3–5 | planned |
| Constrained | OSY, TNK, C1-DTLZ1 | 2–3 | planned |

## Unit checks

- Reference association & niche counts  
- Non-dominated ranks / constraint domination  
- Normalization intercepts  
- Tournament pressure  
- `populationSize: null` ⇒ `|refs|`  
- IGD/HV formulas on hand-checked fronts (`MetricsTests`)  

## Automated IGD smoke (CI)

`tests/Unsga3.Tests/Benchmarks/IgdSmokeTests.cs` — fixed seeds, **loose** bars so CI stays green while the algorithm is still being calibrated. Tighten only after pymoo oracle comparison.

## pymoo export recipe (manual oracle)

```python
# oracle_zdt1.py — run with pymoo installed
import numpy as np
from pymoo.algorithms.moo.unsga3 import UNSGA3
from pymoo.problems import get_problem
from pymoo.optimize import minimize
from pymoo.util.ref_dirs import get_reference_directions
from pymoo.indicators.igd import IGD

problem = get_problem("zdt1")
ref_dirs = get_reference_directions("das-dennis", 2, n_partitions=12)
algo = UNSGA3(ref_dirs, pop_size=52)
res = minimize(problem, algo, ("n_gen", 100), seed=1, verbose=False)
np.savetxt("pymoo_zdt1_F.csv", res.F, delimiter=",")
print("IGD", IGD(problem.pareto_front())(res.F))
```

Then in C#: load CSV, call `PerformanceIndicators.InvertedGenerationalDistance` against `ParetoFronts.Zdt1()`.

## Known intentional deltas

| Item | This library | pymoo |
|------|--------------|-------|
| Tournament (default) | rank → niche count → dist | — |
| Tournament (`PymooCompatible`) | same niche → rank/dist; else random | `comp_by_rank_and_ref_line_dist` |
| Duplicate elimination | not yet | `eliminate_duplicates=True` default |
| Survival RNG | optional RNG niche pick | random among equal niches |
| IGD | **mean** nearest distance | same (verified pymoo 0.6.2) |

Latest numbers: **[ORACLE-RESULTS.md](ORACLE-RESULTS.md)** (ZDT1: we beat pymoo; DTLZ2: still ~5× behind with pymoo-mode).
