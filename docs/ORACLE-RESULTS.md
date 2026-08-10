# Oracle results: Unsga3 vs pymoo UNSGA3

Recorded **2026-08-10** (post DTLZ2 gap fix) · pymoo **0.6.2** · Unsga3 **0.1.2**.

## Protocol

| Knob | Value |
|------|--------|
| Algorithm | U-NSGA-III / pymoo `UNSGA3` |
| SBX | η=30, p_c=1, p_var=0.5 |
| PM | η=20, p_m=1/n |
| Refs | Das–Dennis |
| IGD | **mean** nearest Euclidean distance (pymoo `IGD`) |
| Duplicates | eliminated (pymoo default) |
| Seeds | 1 (table); DTLZ2 multi-seed below |

Reproduce:

```bash
# Python
pip install pymoo
python tools/oracle/run_pymoo_oracle.py --problem zdt1 --partitions 12 --pop 52 --gens 100 --seed 1
python tools/oracle/run_pymoo_oracle.py --problem dtlz2 --partitions 12 --pop 92 --gens 150 --seed 1

# C#
dotnet run --project tools/OracleCompare -c Release -- --problem zdt1 --partitions 12 --pop 52 --gens 100 --seed 1
dotnet run --project tools/OracleCompare -c Release -- --problem dtlz2 --partitions 12 --pop 92 --gens 150 --seed 1 --pymoo-mode
```

## Results (seed=1)

| Problem | Settings | pymoo IGD | C# default IGD | C# `PymooCompatible` IGD | Verdict |
|---------|----------|-----------|----------------|--------------------------|---------|
| **ZDT1** | p=12, pop=52, 100 gen | **0.0629** (n=13 ND) | **0.0514** (n=52) | — | **Default wins** |
| **DTLZ2** | p=12, pop=92, 150 gen | **0.00350** (n=91) | 0.0070 (n=92) | **0.00403** (n=92) | **~1.15× pymoo** (pymoo-mode) |

### DTLZ2 multi-seed (C# `PymooCompatible`, same protocol)

| Seed | IGD |
|------|-----|
| 1 | 0.00403 |
| 2 | 0.00567 |
| 3 | 0.00513 |
| 4 | 0.00478 |
| 5 | 0.00466 |
| **mean** | **~0.00485** |

All seeds stay in the same band as pymoo’s single-seed 0.0035 (within ~1.4–1.6×).

## Root cause of the old ~5× DTLZ2 gap (fixed)

Deep-dive vs pymoo `HyperplaneNormalization` / `ReferenceDirectionSurvival` (pymoo 0.6.2):

| Bug | Effect | Fix |
|-----|--------|-----|
| **ASF weights inverted** | Extreme points landed on *mid-edges* `(0,√½,√½)` instead of axes `(1,0,0)` → wrong hyperplane intercepts → distorted niche association | Preferred axis weight = 1, others = 1e-6 (divide form ≡ pymoo multiply form) |
| Ideal recomputed only on current pop | Lost historical ideal | **Persistent** ideal / worst across generations |
| Extremes from whole pop | Contaminated by dominated points | Extremes from **ND front only** + persist prior extremes |
| No duplicate elimination | Extra clones, uneven niches | `eliminateDuplicates=true` (default), decision-vector key |

### How we diagnosed it

1. Front quality: C# was *already on the unit sphere* (`|r−1|≈0.004`) — convergence was fine.
2. Diversity: pymoo had **0 empty niches / max 1 per niche**; C# had empty niches + max_d ≈ 0.13 on PF.
3. Synthetic ASF unit test: inverted weights selected mid-edge points; corrected weights match pymoo axis extremes.

## Shipping bars (CI)

| Test | Bar |
|------|-----|
| ZDT1 seed=1, 100 gen, default tournament | IGD ≤ 1.5 × 0.0629 |
| DTLZ2 seed=1, 150 gen, pymoo-mode | IGD ≤ 3 × 0.00350 (currently ~1.15×) |
| DTLZ2 short smoke (80 gen) | IGD &lt; 0.15 |

## Known remaining deltas (intentional / minor)

| Item | Status |
|------|--------|
| IGD mean-distance | **aligned** |
| ASF / hyperplane normalization | **aligned** |
| Persistent ideal + ND extremes | **aligned** |
| `TournamentMode.PymooCompatible` | **implemented** |
| Duplicate elimination | **implemented** (default on) |
| RNG path / batch niche pick order | residual ~10–50% IGD noise (expected) |
| Multi-seed Wilcoxon vs pymoo | open |
| Exact bit-identical fronts | not a goal (different RNG streams) |
