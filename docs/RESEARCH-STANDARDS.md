# Research notes: industry standards for U-NSGA-III validation

Sources consulted (2026-08):

| Source | Role |
|--------|------|
| Seada & Deb, IEEE TEVC 2016 (U-NSGA-III) | Algorithm definition |
| Deb et al. ZDT / DTLZ suites | Standard test problems |
| pymoo 0.6.x `UNSGA3`, indicators, ref directions | De-facto open-source reference implementation |
| Coello / Van Veldhuizen GD; Coello IGD | Classic indicators |
| Ishibuchi et al. IGD+ / GD+ | Weak Pareto compliance |
| Fonseca et al. hypervolume | HV when PF unknown |

## 1. Problem suite (what “industry” runs)

| Band | Problems | Why |
|------|----------|-----|
| Single-objective | Sphere, Ackley, Rosenbrock | U-NSGA-III *unifies* SO; pymoo demo uses **Ackley** n=30, pop=100, 150 gens |
| Bi-objective | ZDT1–4, ZDT6 | Convex / non-convex / disconnected / multimodal / biased density |
| Many-objective | DTLZ1–4, DTLZ7 | Linear simplex, sphere, multimodal sphere, bias, disconnected |
| Hard (later) | WFG1–9 | Non-separable, scaled, deceptive |
| Constrained (later) | OSY, TNK, C1-DTLZ1 | Constraint-domination path |

**Seada selection paper** stresses ZDT4 (many local fronts) and DTLZ1 for M=3,5.

Default dimensions (Deb / pymoo convention):

- ZDT1–3: n=30; ZDT4: n=10; ZDT6: n=10  
- DTLZ: n = M + k − 1 with k=5 (DTLZ1) or k=10 (DTLZ2–4), k=20 (DTLZ7)

## 2. Algorithm hyperparameters (match paper + pymoo)

| Knob | Standard value |
|------|----------------|
| Crossover | SBX, η_c = **30**, p_c = 1.0 |
| Mutation | Polynomial, η_m = **20**, p_m = **1/n** |
| Reference set | **Das–Dennis** (uniform) on unit simplex |
| Population size | Often = #reference directions (or slightly larger) |
| Selection | U-NSGA-III **tournament** (not NSGA-III random mating) |

### Tournament detail (alignment note)

**pymoo** `comp_by_rank_and_ref_line_dist`:

1. If either infeasible → smaller CV wins  
2. Else if **same niche** → better rank, else smaller distance-to-niche  
3. Else → random  

**This library (v0.1)** prefers rank → niche count → perpendicular distance (Seada-style pressure even across niches). Documented difference for equivalence work; a `PymooCompatibleTournament` mode can be added if bit-identical mating is required.

## 3. Performance indicators (what to report)

| Metric | Direction | Needs true PF? | Notes |
|--------|-----------|----------------|-------|
| **IGD** | ↓ | Yes | **Primary** — pymoo = **mean** nearest Euclidean distance (not √Σd²/n) |
| **IGD+** | ↓ | Yes | Weakly Pareto compliant (Ishibuchi) |
| **GD** | ↓ | Yes | Convergence only (can miss spread) |
| **HV** | ↑ | No (needs ref point) | Prefer when PF unknown; 2-D closed form here |

Definitions implemented in `Unsga3.Metrics.PerformanceIndicators` follow **pymoo’s formulas** (p=2 Euclidean averages).

### Reference fronts

- ZDT1/2/4/6: closed form f₂(f₁)  
- ZDT3: known f₁ intervals  
- DTLZ1: Das–Dennis × 0.5 on simplex  
- DTLZ2/3/4: Das–Dennis projected to unit sphere  

Sample **≥ 500** points on continuous bi-objective fronts (common practice).

### Hypervolume reference points

Typical ZDT: r = (1.1, 1.1). Always document r; never compare HV across different r.

## 4. Equivalence protocol (one-to-one claim)

1. Same problem definition (bounds, n, evaluate)  
2. Same Das–Dennis partitions → identical ref set size  
3. Same pop, gens, SBX/PM η, p_m = 1/n  
4. Fixed seed **or** 15–31 seeds → median + IQR IGD  
5. Compare IGD (and HV for M=2) to pymoo `UNSGA3`  
6. Shipping bar: median IGD within ~1–2% of pymoo on ZDT1/DTLZ2 (or non-inferior Wilcoxon)

Export path: dump final `F` as CSV from both sides; compute IGD in this library.

## 5. What we implemented after this research

- Full ZDT1–4, ZDT6; DTLZ1–4, DTLZ7; Sphere, Ackley, Rosenbrock  
- GD / IGD / IGD+ / HV₂  
- Analytic PF samplers  
- Fixed-seed smoke IGD bounds (loose) + unit metric tests against hand-checked values  
- GitHub Packages publish workflow  

WFG + constrained + automated pymoo bridge remain follow-ons.
