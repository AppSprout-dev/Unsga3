# Equivalence vs pymoo / MATLAB

Goal: state where this port follows Seada & Deb 2016 and pymoo, and where it does not. Survival, Das–Dennis directions, and the SBX/PM shapes are the pymoo-shaped core. The default tournament is not Algorithm 2.

See also **[RESEARCH-STANDARDS.md](RESEARCH-STANDARDS.md)** for the literature + pymoo protocol,
**[ORACLE-RESULTS.md](ORACLE-RESULTS.md)** for single-seed numbers, and
**[WILCOXON-RESULTS.md](WILCOXON-RESULTS.md)** for 15-seed Mann–Whitney / Wilcoxon vs pymoo.

## Primary sources

- COIN Report 2014022; Seada & Deb, IEEE TEVC 2016  
- [pymoo `UNSGA3`](https://pymoo.org/algorithms/moo/unsga3.html)  
- pymoo `nsga3.py` — `HyperplaneNormalization`, `associate_to_niches`, `niching`  
- pymoo `Survival.filter_infeasible` — feasible niching, then ascending-CV fill (Jain & Deb, NSGA-III Part II)  
- Indicators: [pymoo performance indicators](https://pymoo.org/misc/indicators.html) (GD, IGD, IGD+, HV)

## Protocol

1. **Fixed operators:** SBX η=30, PM η=20, p_c=1.0, p_m=1/n, p_var(SBX)=0.5  
2. **Same reference set:** Das–Dennis partitions identical to the oracle  
3. **Same decision dimension:** DTLZ2 uses k=10 so `n_var = M + k − 1` (12 when M=3). pymoo’s default `n_var=10` (k=8) is a known mismatch; the oracle passes `n_var=12`.  
4. **Same pop size / generations / seed** (or 15–31 seeds for statistics)  
5. **Metrics:** IGD (primary), IGD+, HV (M=2, document ref point), front plots for M≤3.
   Score the **same front definition** and the **same reference set**. C# reports the full non-dominated front. pymoo `res.F` is the survival niche set (about one point per filled direction). `ReferenceDirectionThinning.OnePerDirection` can match cardinality; it does not reproduce `res.F` and is not a parity claim.
6. **Tolerance:** do not read the published table as median IGD within 1–2% of pymoo.
   ZDT1 median ratio 0.764107, Mann–Whitney U = 65, p = 0.0512394 (the pymoo column is `res.F`).
   DTLZ2 median ratio 1.58638, U = 222, p = 6.15164×10⁻⁶ (the pymoo column is n_var=10). That comparison rejects equal distributions at α = 0.05.
   The CI guard is 2× the published mismatched DTLZ2 scalar 0.00350, which fails a regression to about 2.9×. It is not a same-problem equivalence claim.

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
| Constrained | OSY, TNK, C1-DTLZ1 | 2–3 | yes (self-tests; no IGD table) |

## Unit checks

- Reference association & niche counts  
- Non-dominated ranks / constraint domination (equal CV is mutual non-domination)  
- Feasible niching, CV fill, and OSY / TNK / C1-DTLZ1 (`ConstrainedSurvivalTests`, `ConstrainedProblemTests`). No IGD table.  
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
| Niche ids at mating | `PrepareForSelection` re-normalizes survivors and re-associates | ids written during survival are kept |
| `WithDasDennis(1, 1)` | throws. One objective has a single direction, and N must be ≥ 2 | pass `populationSize` ≥ 2 for the single-objective degeneration |
| Reference layers | `DasDennis` / `WithDasDennis` stay single-layer (the M≤3 oracles). `TwoLayerDasDennis` adds Part I's inside layer, default scale 0.5, duplicates removed | multi-layer factory; NSGA-III consumes whatever `ref_dirs` it is given |
| Duplicate elimination | default **on**. Key is `G12` (12 significant digits), not 12 decimal places. Attempts are capped when mutation cannot produce a new key; remaining slots may be duplicates | `eliminate_duplicates=True` |
| Survival RNG | optional RNG niche pick | random among equal niches |
| IGD | **mean** nearest distance | same (verified pymoo 0.6.2) |
| Scored set | full non-dominated front | `res.F` niche optimum |
| Hyperplane norm | persistent ideal, ND extremes, correct ASF | `HyperplaneNormalization` |
| Collapsed nadir | if the span is still ≤ 1e-6, nadir = ideal + 1 | stop at worst-of-population |
| Constrained survival | Niche feasible members only. If fewer than N are feasible, fill by ascending CV (original index breaks ties) with no niche assignment. All-infeasible generations truncate by CV | `Survival.filter_infeasible` |
| Feasible hyperplane | Ideal, worst, and ASF use feasible objectives when any feasible member exists. An all-infeasible generation does not move the hyperplane | `HyperplaneNormalization.update` sees the feasible subset passed by `filter_infeasible` |
| Equal CV in the sort | Two infeasible individuals with the same violation are mutually non-dominated (Deb). Objectives are not compared | constraint-domination; CV ordering is separate from Pareto |
| Tournament equal CV | unchanged. `RankNicheDistance` still falls through to rank, niche, and distance. `PymooCompatible` still coin-flips | pymoo coin-flips |

## DTLZ2 gap history

| Stage | C# pymoo-mode IGD | vs pymoo 0.0035 |
|-------|-------------------|-----------------|
| Pre-fix (wrong ASF) | 0.017 | ~5× |
| ASF + persistent ideal | 0.0052 | ~1.5× |
| + duplicate elimination | **0.0040** | **~1.15× vs pymoo n_var=10** |

The ~1.15× denominator is pymoo at **n_var=10** (k=8), not the C# problem (n_var=12, k=10). A matched seed-1 pair is recorded in [ORACLE-RESULTS.md](ORACLE-RESULTS.md). The 15-seed table is still the mismatched-k run.
