# Equivalence vs pymoo / MATLAB

Goal: state where this port follows Seada & Deb 2016 and pymoo, and where it does not. Survival, Das–Dennis directions, and the SBX/PM shapes are the pymoo-shaped core. The default tournament is not Algorithm 2.

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
3. **Same decision dimension:** DTLZ2 uses k=10 so `n_var = M + k − 1` (12 when M=3). pymoo’s default `n_var=10` (k=8) is a known mismatch; the oracle passes `n_var=12`.  
4. **Same pop size / generations / seed** (or 15–31 seeds for statistics)  
5. **Metrics:** IGD (primary), IGD+, HV (M=2, document ref point), front plots for M≤3.
   Score the **same front definition** and the **same reference set**. C# reports the full non-dominated front. pymoo `res.F` is the survival niche set (about one point per filled direction). `ReferenceDirectionThinning.OnePerDirection` can match cardinality; it does not reproduce `res.F` and is not a parity claim.
6. **Tolerance:** median IGD within ~1–2× of pymoo on ZDT/DTLZ is the practical bar.
   15-seed: ZDT1 median ratio 0.76 (MWU n.s.); DTLZ2 median ~**1.6×** on the published table, whose pymoo column is **n_var=10** while C# is n_var=12.

Published A/B budgets (ZDT1 / DTLZ2 unchanged; ZDT2 matches unsga3-bend protocol honesty):

| Problem | Pop | Gens | Partitions | C# tournament (quality) |
|---------|-----|------|------------|-------------------------|
| ZDT1 | 52 | 100 | 12 | `RankNicheDistance` (published Wilcoxon) |
| ZDT2 | 52 | **250** | 12 | `PymooCompatible` (A/B quality) |
| DTLZ2 | 92 | 150 | 12 | `PymooCompatible` |

ZDT2 **gens=100** is an early-stress snapshot (collapse on Bend, C#, and pymoo), not the quality bar. `RankNicheDistance` on ZDT2 is an optional unpublished Wilcoxon mating mode — do not silently switch all ZDT defaults to it. C# has no published ZDT2 IGD / Wilcoxon table; do not invent one.

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
| Tournament (default `RankNicheDistance`) | rank → niche count → dist, including across niches | not Algorithm 2 |
| Tournament (`PymooCompatible`) | same niche → rank then dist; else random; distance tie is a coin flip | `comp_by_rank_and_ref_line_dist` (paper keeps the second parent on a distance tie) |
| SBX p_c | **1.0** (pymoo `SBX(prob=1.0)`) | paper section 4 uses **0.9** |
| Mating pool | N independent tournaments with replacement | two shuffled consecutive-pair passes |
| Duplicate elimination | default **on** | `eliminate_duplicates=True` |
| Survival RNG | optional RNG niche pick | random among equal niches |
| IGD | **mean** nearest distance | same (verified pymoo 0.6.2) |
| Scored set | full non-dominated front | `res.F` niche optimum |
| Hyperplane norm | persistent ideal, ND extremes, correct ASF | `HyperplaneNormalization` |
| Collapsed nadir | if the span is still ≤ 1e-6, nadir = ideal + 1 | stop at worst-of-population |

## DTLZ2 gap history

| Stage | C# pymoo-mode IGD | vs pymoo 0.0035 |
|-------|-------------------|-----------------|
| Pre-fix (wrong ASF) | 0.017 | ~5× |
| ASF + persistent ideal | 0.0052 | ~1.5× |
| + duplicate elimination | **0.0040** | **~1.15× vs pymoo n_var=10** |

The ~1.15× denominator is pymoo at **n_var=10** (k=8), not the C# problem (n_var=12, k=10). A matched seed-1 pair is recorded in [ORACLE-RESULTS.md](ORACLE-RESULTS.md). The 15-seed table is still the mismatched-k run.
