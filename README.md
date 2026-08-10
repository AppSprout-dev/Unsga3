# Unsga3

**U-NSGA-III** (Unified NSGA-III) for .NET — single-, multi-, and many-objective evolutionary optimization with Das–Dennis reference directions, SBX crossover, polynomial mutation, and **niching-based tournament selection** (Seada & Deb).

> Status: **v0.1.2** — U-NSGA-III with pymoo-aligned hyperplane normalization + duplicate elimination.  
> Oracle (seed=1): **ZDT1 beats pymoo** (0.051 vs 0.063); **DTLZ2 ~1.15× pymoo** (0.0040 vs 0.0035, was ~5×).  
> See [`docs/ORACLE-RESULTS.md`](docs/ORACLE-RESULTS.md) · [`docs/EQUIVALENCE.md`](docs/EQUIVALENCE.md) · [`docs/RESEARCH-STANDARDS.md`](docs/RESEARCH-STANDARDS.md).

## Install

**nuget.org** (when published):

```bash
dotnet add package Unsga3
```

**GitHub Packages** (primary for early releases) — org registry:

```bash
# once per machine
dotnet nuget add source https://nuget.pkg.github.com/AppSprout-dev/index.json \
  --name github-appsprout --username YOUR_GH_USER --password YOUR_PAT --store-password-in-clear-text

dotnet add package Unsga3
```

Repo: https://github.com/AppSprout-dev/Unsga3  
PAT needs `read:packages`. Publish on `v*` tags via `.github/workflows/publish-github-packages.yml`.

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

Or:

```csharp
var algo = Unsga3Algorithm.WithDasDennis(numberOfObjectives: 3, partitions: 12, seed: 1);
```

## Public surface

| Type | Role |
|------|------|
| `IProblem` / `ProblemBase` | Problem definition (minimize objectives; g≤0 constraints) |
| `Unsga3Algorithm` | Main entry — `Run(problem, gens)` |
| `Individual` | Variables / Objectives / Constraints |
| `OptimizationResult` | Final population + non-dominated set |
| `ReferenceDirections.DasDennis` | Structured reference points |
| `SimulatedBinaryCrossover` / `PolynomialMutation` | Variation operators |

## Build & test

```bash
dotnet build Unsga3.slnx
dotnet test Unsga3.slnx
dotnet run --project samples/BasicUsage
```

Requires **.NET 10** SDK (targets `net10.0`; retarget to `net8.0` in the csproj if you need broader NuGet reach).

## Layout

```
Unsga3/
├── src/Unsga3/           # Library
├── tests/Unsga3.Tests/   # Unit / Benchmarks / Equivalence
├── samples/BasicUsage/
├── docs/
├── .github/workflows/
└── Unsga3.slnx
```

## References

- Seada, H. & Deb, K. (2016). *A Unified Evolutionary Optimization Procedure for Single, Multiple, and Many Objectives.* IEEE Trans. Evol. Comput.
- Deb, K. & Jain, H. (2014). *An Evolutionary Many-Objective Optimization Algorithm Using Reference-Point-Based Nondominated Sorting Approach (NSGA-III).*
- Das, I. & Dennis, J. E. (1998). *Normal-Boundary Intersection.*

## License

MIT — see [LICENSE](LICENSE).
