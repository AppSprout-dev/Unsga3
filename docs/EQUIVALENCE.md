# Equivalence vs pymoo / MATLAB

Goal: prove this port is a faithful U-NSGA-III (Seada & Deb 2016), not a look-alike.

See also **[RESEARCH-STANDARDS.md](RESEARCH-STANDARDS.md)** for the literature + pymoo protocol,
**[ORACLE-RESULTS.md](ORACLE-RESULTS.md)** for single-seed numbers, and
**[WILCOXON-RESULTS.md](WILCOXON-RESULTS.md)** for 15-seed Mann–Whitney / Wilcoxon vs pymoo.

## Primary sources

- COIN Report 2014022; Seada & Deb, IEEE TEVC 2016  
- [pymoo `UNSGA3`](https://pymoo.org/algorithms/moo/unsga3.html)  
- pymoo `nsga3.py` — `HyperplaneNormalization`, `associate_to_niches`, `niching`  
- Indicators: [pymoo performance indicators](https://pymoo.org/misc/indicators.html) (GD, IGD, IGD+, HV)

## Protocol

1. **Fixed operators:** SBX η=30, PM η=20, p_c=1.0, p_m=1/n, p_var(SBX)=0.5  
2. **Same reference set:** Das–Dennis partitions identical to the oracle  
3. **Same pop size / generations / seed** (or 15–31 seeds for statistics)  
4. **Metrics:** IGD (primary), IGD+, HV (M=2, document ref point), front plots for M≤3  
5. **Tolerance:** median IGD within ~1–2× of pymoo on ZDT/DTLZ is the practical bar.
   15-seed: ZDT1 median **better** than pymoo (ratio 0.76, MWU n.s.); DTLZ2 median ~**1.6×** (pymoo still ahead).

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
- **Normalization intercepts / ASF axis extremes** (`NormalizationTests`)  
- Tournament pressure  
- `populationSize: null` ⇒ `|refs|`  
- IGD/HV formulas on hand-checked fronts (`MetricsTests`)  

## Automated IGD smoke (CI)

`tests/Unsga3.Tests/Benchmarks/IgdSmokeTests.cs` — fixed seeds against pymoo baselines.

## Known intentional deltas

| Item | This library | pymoo |
|------|--------------|-------|
| Tournament (default) | rank → niche count → dist | — |
| Tournament (`PymooCompatible`) | same niche → rank/dist; else random | `comp_by_rank_and_ref_line_dist` |
| Duplicate elimination | default **on** | `eliminate_duplicates=True` |
| Survival RNG | optional RNG niche pick | random among equal niches |
| IGD | **mean** nearest distance | same (verified pymoo 0.6.2) |
| Hyperplane norm | persistent ideal, ND extremes, correct ASF | `HyperplaneNormalization` |

## DTLZ2 gap history

| Stage | C# pymoo-mode IGD | vs pymoo 0.0035 |
|-------|-------------------|-----------------|
| Pre-fix (wrong ASF) | 0.017 | ~5× |
| ASF + persistent ideal | 0.0052 | ~1.5× |
| + duplicate elimination | **0.0040** | **~1.15×** |
