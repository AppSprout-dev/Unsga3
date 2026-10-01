# Unsga3

[![ci](https://github.com/AppSprout-dev/Unsga3/actions/workflows/ci.yml/badge.svg)](https://github.com/AppSprout-dev/Unsga3/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%2010.0-purple.svg)](https://dotnet.microsoft.com/)

**U-NSGA-III** (Unified NSGA-III) for .NET — single-, multi-, and many-objective evolutionary optimization with Das–Dennis reference directions, SBX crossover, polynomial mutation, and **niching-based tournament selection** ([Seada & Deb, 2016](https://ieeexplore.ieee.org/document/7271063)).

> **v0.2.1** — `net8.0`+`net10.0`, Ishibuchi GD+, DocFX, and the matched DTLZ2 Wilcoxon re-run (`n_var=12` on C# and pymoo). Defaults stay unconstrained `RankNicheDistance`, independent mating, and single-layer Das–Dennis.  
> **15-seed IGD vs pymoo `UNSGA3`:** ZDT1 **median 0.053173 vs 0.0695884** (MWU *p*=0.0512394) compares the full C# non-dominated front with pymoo `res.F`. DTLZ2 **median 0.00451168 vs 0.00388327** (ratio 1.16183, MWU *p*=0.0225311) uses **n_var=12** on both sides. ZDT2 has no Wilcoxon table. Notes: [`docs/WILCOXON-RESULTS.md`](docs/WILCOXON-RESULTS.md).  
> Details: [`docs/WILCOXON-RESULTS.md`](docs/WILCOXON-RESULTS.md) · single-seed notes: [`docs/ORACLE-RESULTS.md`](docs/ORACLE-RESULTS.md) · constrained + mating opt-ins: [`docs/NEW-SURFACES-RESULTS.md`](docs/NEW-SURFACES-RESULTS.md)

```text
https://github.com/AppSprout-dev/Unsga3
```

## Why this exists

Most strong MOEA reference stacks are Python/MATLAB/Java. **Unsga3** brings a careful U-NSGA-III port to **idiomatic C# / .NET** — deterministic seeds, no native runtime deps, NuGet-friendly — validated against pymoo on standard ZDT/DTLZ IGD protocols (not just “it runs”).

## Install

**GitHub Packages** (current):

```bash
dotnet nuget add source https://nuget.pkg.github.com/AppSprout-dev/index.json \
  --name github-appsprout --username YOUR_GH_USER --password YOUR_PAT --store-password-in-clear-text

dotnet add package Unsga3
```

PAT needs `read:packages`. Releases publish on `v*` tags.

**nuget.org** — planned (see [roadmap](docs/ROADMAP.md)).

## Quick start

```csharp
using Unsga3.Algorithm;
using Unsga3.Problems;
using Unsga3.Utilities;

var problem = new Zdt1Problem();
var dirs = ReferenceDirections.DasDennis(numberOfObjectives: 2, partitions: 12);
var algo = new Unsga3Algorithm(dirs, populationSize: 40, seed: 42);
var result = algo.Run(problem, maxGenerations: 100);

foreach (var ind in result.NonDominatedSolutions)
    Console.WriteLine($"{ind.Objectives[0]:F4}  {ind.Objectives[1]:F4}");
```

Many-objective:

```csharp
var algo = Unsga3Algorithm.WithDasDennis(numberOfObjectives: 3, partitions: 12, seed: 1);
var result = algo.Run(new Dtlz2Problem(nObjectives: 3), maxGenerations: 150);
```

Fairer mating comparison vs pymoo:

```csharp
using Unsga3.Operators.Selection;

var algo = new Unsga3Algorithm(dirs, populationSize: 92, seed: 1,
    tournamentMode: TournamentMode.PymooCompatible);
```

## Public surface

| Type | Role |
|------|------|
| `IProblem` / `ProblemBase` | Problem definition (minimize; g≤0 constraints) |
| `Unsga3Algorithm` | Main entry — `Run(problem, gens)` |
| `Individual` | Variables / Objectives / Constraints |
| `OptimizationResult` | Final population + non-dominated set |
| `ReferenceDirections.DasDennis` | Single-layer reference points (default) |
| `ReferenceDirections.TwoLayerDasDennis` | Outer layer plus an inside layer for larger M |
| `SimulatedBinaryCrossover` / `PolynomialMutation` | Variation operators |
| `PerformanceIndicators` | IGD, IGD+, GD, GD+, 2-D hypervolume |
| `TournamentMode` | Default rank→niche, `PymooCompatible` (distance-tie coin flip), or opt-in `Algorithm2` (keep the second parent) |
| `MatingPoolMode` | Default independent tournaments; opt-in `TwoShuffledPasses` (paper / pymoo 0.6.2) |

Built-in problems: ZDT1–4/6, DTLZ1–4/7, Sphere, Ackley, Rosenbrock, and constrained OSY, TNK, and C1-DTLZ1 (self-tests only; no IGD table yet).

## Build & test

```bash
dotnet build Unsga3.slnx -c Release
dotnet test Unsga3.slnx -c Release
dotnet run --project samples/BasicUsage -c Release -f net10.0
```

The library, tests, and sample target **`net8.0` and `net10.0`**. Building both needs the .NET 8 and .NET 10 SDKs. `tools/OracleCompare` is `net10.0` only. Optional oracle: Python 3 + `pip install pymoo` (see [CONTRIBUTING.md](CONTRIBUTING.md)).

API reference (DocFX, local tool):

```bash
dotnet tool restore
dotnet docfx docfx.json
```

That writes `_site/` and does not publish a package.

## TypeSafe / Jev Pareto scoring (optional)

After a classical U-NSGA-III front, you can run a **TypeSafe System One** (Jev) pass over a small candidate sample and compare calibrated Score / Choice answers to raw objective vectors. This is an **additive semantic layer** — it does **not** replace NSGA-III / U-NSGA-III objectives, constraint-domination, or IGD.

The helper lives in `tools/typesafe-pareto/` (stdlib Python; official `typesafe-sdk` is optional for live calls). One `POST /v1/systemone` fans out per-candidate **Score** (`constraint_satisfaction`, `diversity_value`, `exploit_vs_explore`) and **Choice** (`keep` | `drop` | `review`). Smoke batches ≤10 fixture candidates.

```bash
# Mock (default when TYPESAFE_API_KEY is unset; what CI runs)
python tools/typesafe-pareto/score_pareto.py --smoke --force-mock

# Live Jev (key from the environment only — never commit it)
export TYPESAFE_API_KEY=...          # https://docs.typesafe.ai/sdk/python.md
pip install -r tools/typesafe-pareto/requirements-optional.txt   # optional SDK
python tools/typesafe-pareto/score_pareto.py --smoke
python tools/typesafe-pareto/score_pareto.py --candidates path/to/front.json
```

Candidate JSON is an object with `candidates` (or `NonDominatedSolutions`), each with `objectives` and optional `variables` / `constraints` / `constraint_violation` / `feasible` / `rank`. A synthetic ZDT1-like fixture is at `tools/typesafe-pareto/fixtures/zdt1_candidates.json`.

Metrics append one JSON line per run to **`metrics/typesafe-runs.jsonl`** (gitignored):

`{ts, experiment:"unsga3_pareto_score", repo:"AppSprout-dev/Unsga3", model, latency_ms, usage, candidate_count, answers, notes}`

API docs used: [HTTP](https://docs.typesafe.ai/api.md) · [Python SDK](https://docs.typesafe.ai/sdk/python.md) · [fan-out](https://docs.typesafe.ai/patterns/fan-out.md). No API keys in this repo.

## Equivalence & research

| Doc | Contents |
|-----|----------|
| [docs/ORACLE-RESULTS.md](docs/ORACLE-RESULTS.md) | Single-seed C# vs pymoo |
| [docs/WILCOXON-RESULTS.md](docs/WILCOXON-RESULTS.md) | Multi-seed Mann–Whitney / Wilcoxon |
| [docs/NEW-SURFACES-RESULTS.md](docs/NEW-SURFACES-RESULTS.md) | 15-seed constrained IGD and mating opt-ins |
| [docs/EQUIVALENCE.md](docs/EQUIVALENCE.md) | Protocol & intentional deltas (ZDT2 quality A/B = gens=250, `PymooCompatible`; matches unsga3-bend) |
| [docs/RESEARCH-STANDARDS.md](docs/RESEARCH-STANDARDS.md) | Literature + indicator standards |
| [docs/NOTICE.md](docs/NOTICE.md) | Attribution (papers + validation tools) |
| [docs/ROADMAP.md](docs/ROADMAP.md) | Near / medium term plan |

## Layout

```
Unsga3/
├── src/Unsga3/              # Library (no third-party runtime deps)
├── tests/Unsga3.Tests/
├── samples/BasicUsage/
├── tools/oracle/            # pymoo oracle + multi-seed stats (optional)
├── tools/OracleCompare/     # C# side of the oracle
├── tools/typesafe-pareto/   # optional TypeSafe / Jev Score+Choice on a front sample
├── metrics/                 # typesafe-runs.jsonl (local; gitignored)
├── docs/
├── docfx.json               # API docs (`dotnet docfx docfx.json`)
└── .github/workflows/       # CI + GitHub Packages publish
```

## Contributing

See **[CONTRIBUTING.md](CONTRIBUTING.md)**, **[CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md)**, and **[SECURITY.md](SECURITY.md)**. Issues and PRs welcome — especially real multi-objective use cases and oracle gaps.

## Cite

Software: use [`CITATION.cff`](CITATION.cff) (GitHub “Cite this repository”).

Algorithm papers (please cite these when publishing results):

- Seada, H. & Deb, K. (2016). *A Unified Evolutionary Optimization Procedure for Single, Multiple, and Many Objectives.* IEEE Trans. Evol. Comput.
- Deb, K. & Jain, H. (2014). *An Evolutionary Many-Objective Optimization Algorithm Using Reference-Point-Based Nondominated Sorting Approach (NSGA-III), Part I.* IEEE Trans. Evol. Comput.
- Jain, H. & Deb, K. (2014). *NSGA-III, Part II: Handling Constraints and Extending to an Adaptive Approach.* IEEE Trans. Evol. Comput.
- Das, I. & Dennis, J. E. (1998). *Normal-Boundary Intersection.* SIAM J. Optim.

## License

MIT — see [LICENSE](LICENSE).

**Not affiliated with pymoo.** Validation compares against pymoo as an external oracle; no pymoo code is shipped in the NuGet package ([NOTICE](docs/NOTICE.md)).
