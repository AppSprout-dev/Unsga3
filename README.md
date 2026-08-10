# Unsga3

[![ci](https://github.com/AppSprout-dev/Unsga3/actions/workflows/ci.yml/badge.svg)](https://github.com/AppSprout-dev/Unsga3/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)

**U-NSGA-III** (Unified NSGA-III) for .NET — single-, multi-, and many-objective evolutionary optimization with Das–Dennis reference directions, SBX crossover, polynomial mutation, and **niching-based tournament selection** ([Seada & Deb, 2016](https://ieeexplore.ieee.org/document/7271063)).

> **v0.1.2** — production-usable core with pymoo-aligned normalization.  
> **15-seed IGD vs pymoo `UNSGA3`:** ZDT1 **median 0.053 vs 0.070** (we win; MWU *p*≈0.05); DTLZ2 **median 0.0045 vs 0.0028** (~1.6×, same order; pymoo still ahead).  
> Details: [`docs/WILCOXON-RESULTS.md`](docs/WILCOXON-RESULTS.md) · single-seed notes: [`docs/ORACLE-RESULTS.md`](docs/ORACLE-RESULTS.md)

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
| `ReferenceDirections.DasDennis` | Structured reference points |
| `SimulatedBinaryCrossover` / `PolynomialMutation` | Variation operators |
| `PerformanceIndicators` | IGD, GD, 2-D hypervolume |
| `TournamentMode` | Default rank→niche vs `PymooCompatible` |

Built-in problems: ZDT1–4/6, DTLZ1–4/7, Sphere, Ackley, Rosenbrock.

## Build & test

```bash
dotnet build Unsga3.slnx -c Release
dotnet test Unsga3.slnx -c Release
dotnet run --project samples/BasicUsage -c Release
```

Requires **.NET 10** SDK. Optional oracle: Python 3 + `pip install pymoo` (see [CONTRIBUTING.md](CONTRIBUTING.md)).

## Equivalence & research

| Doc | Contents |
|-----|----------|
| [docs/ORACLE-RESULTS.md](docs/ORACLE-RESULTS.md) | Single-seed C# vs pymoo |
| [docs/WILCOXON-RESULTS.md](docs/WILCOXON-RESULTS.md) | Multi-seed Mann–Whitney / Wilcoxon |
| [docs/EQUIVALENCE.md](docs/EQUIVALENCE.md) | Protocol & intentional deltas |
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
├── docs/
└── .github/workflows/       # CI + GitHub Packages publish
```

## Contributing

See **[CONTRIBUTING.md](CONTRIBUTING.md)**, **[CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md)**, and **[SECURITY.md](SECURITY.md)**. Issues and PRs welcome — especially real multi-objective use cases and oracle gaps.

## Cite

Software: use [`CITATION.cff`](CITATION.cff) (GitHub “Cite this repository”).

Algorithm papers (please cite these when publishing results):

- Seada, H. & Deb, K. (2016). *A Unified Evolutionary Optimization Procedure for Single, Multiple, and Many Objectives.* IEEE Trans. Evol. Comput.
- Deb, K. & Jain, H. (2014). *An Evolutionary Many-Objective Optimization Algorithm Using Reference-Point-Based Nondominated Sorting Approach (NSGA-III), Part I.* IEEE Trans. Evol. Comput.
- Das, I. & Dennis, J. E. (1998). *Normal-Boundary Intersection.* SIAM J. Optim.

## License

MIT — see [LICENSE](LICENSE).

**Not affiliated with pymoo.** Validation compares against pymoo as an external oracle; no pymoo code is shipped in the NuGet package ([NOTICE](docs/NOTICE.md)).
