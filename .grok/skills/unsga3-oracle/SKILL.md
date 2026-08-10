---
name: unsga3-oracle
description: >
  Run Unsga3 equivalence oracles vs pymoo (single-seed and multi-seed Mann–Whitney/Wilcoxon),
  diagnose IGD gaps, and update docs/ORACLE-RESULTS.md + docs/WILCOXON-RESULTS.md.
  Use when the user runs /unsga3-oracle, or asks to re-run the oracle, multi-seed Wilcoxon,
  pymoo parity, IGD comparison, DTLZ2/ZDT1 gap analysis, or equivalence harness.
---

# Unsga3 oracle

Repo root: Unsga3 (not Torquon). Confirm `Unsga3.slnx` / `tools/OracleCompare` exist before running.

## Defaults (protocol)

| Problem | Partitions | Pop | Gens | C# tournament |
|---------|------------|-----|------|----------------|
| zdt1 | 12 | 52 | 100 | default (`RankNicheDistance`) |
| dtlz2 | 12 | 92 | 150 | `--pymoo-mode` (`PymooCompatible`) |

IGD = **mean** nearest Euclidean distance (pymoo-compatible). Docs: `docs/EQUIVALENCE.md`, `docs/RESEARCH-STANDARDS.md`.

Requires: .NET 10 SDK; Python 3 + `pip install pymoo` for pymoo side / multi-seed.

## Modes

Ask which mode if unclear; default **quick** when validating a small fix, **full** before a release.

### 1. Quick single-seed

```powershell
dotnet build tools/OracleCompare -c Release
dotnet run --project tools/OracleCompare -c Release --no-build -- --problem zdt1 --partitions 12 --pop 52 --gens 100 --seed 1
dotnet run --project tools/OracleCompare -c Release --no-build -- --problem dtlz2 --partitions 12 --pop 92 --gens 150 --seed 1 --pymoo-mode
python tools/oracle/run_pymoo_oracle.py --problem zdt1 --partitions 12 --pop 52 --gens 100 --seed 1
python tools/oracle/run_pymoo_oracle.py --problem dtlz2 --partitions 12 --pop 92 --gens 150 --seed 1
```

Report IGD side-by-side. Fronts land in `tools/oracle/out/` (gitignored).

### 2. Full multi-seed Wilcoxon (n=15 default)

```powershell
python tools/oracle/run_multiseed_wilcoxon.py --problems zdt1 dtlz2 --seeds 15
```

Writes `docs/WILCOXON-RESULTS.md` + `tools/oracle/out/wilcoxon_results.json`. Summarize medians, ratios, MWU/WSR p-values. Commit the markdown if results changed intentionally.

### 3. Diagnose a gap (e.g. DTLZ2)

```powershell
python tools/oracle/analyze_dtlz2_gap.py
```

Checks sphere residual, niche occupancy, coverage vs true PF. Known historical root cause: inverted ASF weights in `Normalization` (fixed v0.1.2) — do not re-break preferred-axis weight = 1 / others = 1e-6.

## After runs

1. Update `docs/ORACLE-RESULTS.md` if single-seed protocol numbers moved.
2. Keep `docs/WILCOXON-RESULTS.md` from the harness (or re-run full).
3. If algorithm code changed: `dotnet test Unsga3.slnx -c Release` must stay green.
4. Do **not** commit `tools/oracle/out/*.csv` (gitignored).

## Do not

- Claim bit-identical fronts (different RNG streams).
- Tighten CI IGD bars without multi-seed evidence.
- Run full n=15 when the user only asked for a smoke check.
