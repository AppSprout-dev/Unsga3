# Contributing to Unsga3

Thanks for your interest. This library aims to be a **faithful, well-tested** U-NSGA-III for .NET — correctness and oracle parity matter more than feature breadth.

## Quick path

1. Fork + branch from `main`
2. `dotnet test Unsga3.slnx -c Release`
3. Open a PR against `main` (CI must be green)

## Development setup

- **.NET 10 SDK** ([download](https://dotnet.microsoft.com/download))
- Optional (oracle / multi-seed stats): Python 3.10+ with `pip install pymoo`

```bash
git clone https://github.com/AppSprout-dev/Unsga3.git
cd Unsga3
dotnet build Unsga3.slnx -c Release
dotnet test Unsga3.slnx -c Release
dotnet run --project samples/BasicUsage -c Release
```

### Oracle / equivalence (optional)

```bash
# single seed
dotnet run --project tools/OracleCompare -c Release -- --problem dtlz2 --partitions 12 --pop 92 --gens 150 --seed 1 --pymoo-mode
python tools/oracle/run_pymoo_oracle.py --problem dtlz2 --partitions 12 --pop 92 --gens 150 --seed 1

# multi-seed Mann–Whitney / Wilcoxon (writes docs/WILCOXON-RESULTS.md)
python tools/oracle/run_multiseed_wilcoxon.py --problems zdt1 dtlz2 --seeds 15
```

See [`docs/EQUIVALENCE.md`](docs/EQUIVALENCE.md) and [`docs/ORACLE-RESULTS.md`](docs/ORACLE-RESULTS.md).

## What we welcome

| Area | Notes |
|------|--------|
| Bug fixes | With a regression test when possible |
| Indicators / problems | WFG, constrained suites, IGD+ polish |
| Performance | Without changing default algorithm behaviour |
| Docs / samples | Especially real-world multi-objective sketches |
| Equivalence work | pymoo / PlatEMO / jMetal cross-checks |

## What needs discussion first

- Changing **default** tournament, operators, or normalization
- Public API breaks (prefer additive changes in 0.x when possible)
- New algorithm variants (NSGA-II, MOEA/D, …) — open an issue first

## Code style

- Prefer clear, boring C#; match existing Hungarian-ish names only where the file already uses them
- Deterministic defaults: seeded RNG, no ambient static state in the algorithm
- New behaviour that touches existing paths should be **opt-in** or golden-safe (byte-identical when off)
- XML docs on public types

## Pull requests

- One logical change per PR
- Update [`CHANGELOG.md`](CHANGELOG.md) under **Unreleased** when user-visible
- If you touch survival / normalization / metrics, note oracle impact (or re-run multi-seed)
- Do not commit `tools/oracle/out/` front CSVs

## Reporting bugs

Use [GitHub Issues](https://github.com/AppSprout-dev/Unsga3/issues). Include:

- Unsga3 version / commit
- Minimal problem + seed + pop / gens
- Expected vs actual (IGD, exception, front shape)

## Security

See [`SECURITY.md`](SECURITY.md). Do not open public issues for sensitive reports.

## License

By contributing, you agree your contributions are licensed under the MIT License (same as the project).
