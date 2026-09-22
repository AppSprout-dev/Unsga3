# Changelog

All notable changes to this project are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

- Optional **TypeSafe / Jev System One** pass (`tools/typesafe-pareto`) for Score + Choice judgments over a small Pareto candidate sample. Additive semantic layer — does not replace NSGA-III / U-NSGA-III objectives. Live calls only when `TYPESAFE_API_KEY` is set; CI uses mocks. Metrics append to `metrics/typesafe-runs.jsonl`.

### Changed

- Docs and XML comments describe `initialPopulation` and hybrid loops in generic terms (domain-adapter warm-start / grid-seed). No product-repo names.
- DTLZ2 pymoo oracle passes `n_var=12` (k=10) to match `Dtlz2Problem`. The published 15-seed table used pymoo’s default `n_var=10` and is not rewritten. Seed 1 was remeasured at n_var=12.
- Documented that ZDT IGD compares the C# full non-dominated front with pymoo `res.F`. Added `ReferenceDirectionThinning.OnePerDirection` as a cardinality aid, not a parity claim.
- Documented the collapsed-nadir fallback (nadir = ideal + 1 when the span stays ≤ 1e-6). pymoo 0.6.2 stops at the worst point in the population. Behavior is unchanged and covered by a unit test.
- Documented that default `RankNicheDistance` is not Seada and Deb Algorithm 2. `PymooCompatible` matches the paper's same-niche split; p_c stays 1.0 (paper experiments use 0.9). The default tournament is unchanged.
- Locked the infeasible-point hyperplane rule with a fixture: feasible (1, 1) beside infeasible (0, 0) sets ideal to (0, 0). The rule is unchanged.
- Documented that mating calls `PrepareForSelection`, which re-associates survivors. pymoo keeps the niche ids from survival. A fixture locks the current ids on a five-point pool.
- Documented that `WithDasDennis(1, 1)` throws because N must be at least 2. Single-objective runs pass an explicit population size. N = 1 is not accepted.

## [0.1.4] — 2026-09-19

### Changed

- **ZDT2 oracle/smoke protocol:** quality bar is **gens=250** + `PymooCompatible` (docs and harness defaults). gens=100 is an early-stress snapshot (collapse on Bend, C#, and pymoo), not the quality bar. `RankNicheDistance` stays an optional unpublished Wilcoxon ZDT2 mating mode. ZDT1 (100) and DTLZ2 (150) unchanged. **No algorithm or public API change.** Matches [unsga3-bend](https://github.com/AppSprout-dev/unsga3-bend) A/B honesty.

## [0.1.3] — 2026-08-10

### Added

- Optional **`initialPopulation`** on `Unsga3Algorithm.Run` (grid-seed / warm-start for domain adapters)
- Multi-seed Wilcoxon / Mann–Whitney oracle harness (`tools/oracle/run_multiseed_wilcoxon.py`)
- Community files: CONTRIBUTING, SECURITY, CODE_OF_CONDUCT, ROADMAP, CITATION.cff, issue templates

## [0.1.2] — 2026-08-10

### Fixed

- **NSGA-III hyperplane normalization**: ASF weights were inverted (extremes landed on mid-edges, not axes), distorting niche association on many-objective problems (DTLZ2 IGD ~5× worse than pymoo)
- Persistent ideal / worst points and ND-only extreme points (pymoo `HyperplaneNormalization` parity)

### Added

- Default **duplicate elimination** on decision vectors (`eliminateDuplicates: true`)
- `NormalizationTests` (axis extremes + persistent ideal)
- DTLZ2 oracle CI bar; diagnostic script `tools/oracle/analyze_dtlz2_gap.py`

### Changed

- Shared `Normalization` instance across survival + tournament for one run
- Oracle docs updated: DTLZ2 ~1.15× pymoo (seed=1, pymoo-mode)

## [0.1.1] — 2026-08-10

### Fixed

- IGD formula: mean nearest distance (pymoo-compatible), not √(Σd²)/n

### Added

- `TournamentMode.PymooCompatible` for fairer mating comparison
- Oracle harness (`tools/OracleCompare`, `tools/oracle/run_pymoo_oracle.py`)

## [0.1.0] — 2026-08-10

### Added

- Initial U-NSGA-III library: Das–Dennis refs, SBX, PM, niching tournament, NSGA-III survival
- Benchmarks: ZDT1–4/6, DTLZ1–4/7, Sphere / Ackley / Rosenbrock
- Metrics: IGD, GD, 2-D HV; self-tests + GitHub Packages publish workflow

[Unreleased]: https://github.com/AppSprout-dev/Unsga3/compare/v0.1.4...HEAD
[0.1.4]: https://github.com/AppSprout-dev/Unsga3/compare/v0.1.3...v0.1.4
[0.1.3]: https://github.com/AppSprout-dev/Unsga3/compare/v0.1.2...v0.1.3
[0.1.2]: https://github.com/AppSprout-dev/Unsga3/compare/v0.1.1...v0.1.2
[0.1.1]: https://github.com/AppSprout-dev/Unsga3/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/AppSprout-dev/Unsga3/releases/tag/v0.1.0
