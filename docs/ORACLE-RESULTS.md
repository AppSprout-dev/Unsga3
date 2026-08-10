# Oracle results: Unsga3 vs pymoo UNSGA3

Recorded **2026-08-10** · pymoo **0.6.2** · Unsga3 after IGD formula fix + `TournamentMode.PymooCompatible`.

## Protocol

| Knob | Value |
|------|--------|
| Algorithm | U-NSGA-III / pymoo `UNSGA3` |
| SBX | η=30, p_c=1 (defaults both sides) |
| PM | η=20, p_m=1/n |
| Refs | Das–Dennis |
| IGD | **mean** nearest Euclidean distance (matches pymoo `IGD`, not √Σd²/n) |
| Seeds | 1 (table); multi-seed TBD |

Reproduce:

```bash
# Python
pip install pymoo
python tools/oracle/run_pymoo_oracle.py --problem zdt1 --partitions 12 --pop 52 --gens 100 --seed 1
python tools/oracle/run_pymoo_oracle.py --problem dtlz2 --partitions 12 --pop 92 --gens 150 --seed 1

# C#
dotnet run --project tools/OracleCompare -c Release -- --problem zdt1 --partitions 12 --pop 52 --gens 100 --seed 1
dotnet run --project tools/OracleCompare -c Release -- --problem zdt1 --partitions 12 --pop 52 --gens 100 --seed 1 --pymoo-mode
dotnet run --project tools/OracleCompare -c Release -- --problem dtlz2 --partitions 12 --pop 92 --gens 150 --seed 1 --pymoo-mode
```

Apples-to-apples check: load each `*_F.csv` into pymoo `IGD(pf)(F)`.

## Results (seed=1)

| Problem | Settings | pymoo IGD | C# default IGD | C# `PymooCompatible` IGD | Verdict |
|---------|----------|-----------|----------------|--------------------------|---------|
| **ZDT1** | p=12, pop=52, 100 gen | **0.0629** (n=13 ND) | **0.0392** (n=52) | 0.0870 (n=52) | **Default wins** (better than pymoo) |
| **DTLZ2** | p=12, pop=92, 150 gen | **0.00350** (n=91) | 0.0374 (n=92) | **0.0171** (n=92) | **Behind pymoo** (~5× with pymoo-mode) |

### How to read this

1. **IGD formula bug (fixed):** we had used `(1/|Z|)√Σd²`; pymoo uses **mean d**. Metrics now match pymoo’s class to machine precision on the same front files.
2. **ZDT1:** default rank→niche-count tournament is *stronger* than pymoo mating and produces a better IGD on this seed. Shipping default stays `RankNicheDistance`.
3. **DTLZ2:** even with `PymooCompatible` mating we trail. Likely remaining deltas: survival RNG/details, normalization, lack of duplicate elimination, SBX edge cases — **not** the IGD definition. Many-objective parity is open work.
4. **`PymooCompatible`:** optional; use for fairer mating comparison. Helps DTLZ2 vs our default; not required for ZDT1 quality.

## Shipping bars (CI)

| Test | Bar |
|------|-----|
| ZDT1 seed=1, 100 gen, default tournament | IGD ≤ 1.25 × 0.0629 (beats/matches pymoo) |
| DTLZ2 smoke | IGD &lt; 0.15 (regression guard only) |

## Known intentional / open deltas

| Item | Status |
|------|--------|
| IGD mean-distance | **aligned** |
| `TournamentMode.PymooCompatible` | **implemented** |
| Survival random niche pick with RNG | **implemented** |
| Duplicate elimination | open |
| DTLZ2 IGD within ~1–2% of pymoo | **open** |
| Multi-seed Wilcoxon | open |
