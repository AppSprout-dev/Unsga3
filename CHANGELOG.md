# Changelog

All notable changes to this project are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/).

## [Unreleased]

## [0.1.3] — 2026-08-10

### Added

- Optional **`initialPopulation`** on `Unsga3Algorithm.Run` (grid-seed / warm-start for domain adapters; Torquon dogfood)
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

[Unreleased]: https://github.com/AppSprout-dev/Unsga3/compare/v0.1.2...HEAD
[0.1.2]: https://github.com/AppSprout-dev/Unsga3/compare/v0.1.1...v0.1.2
[0.1.1]: https://github.com/AppSprout-dev/Unsga3/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/AppSprout-dev/Unsga3/releases/tag/v0.1.0
