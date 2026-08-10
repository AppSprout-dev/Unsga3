using Unsga3.Algorithm;
using Unsga3.Problems;
using Unsga3.Utilities;

namespace Unsga3.Tests.Benchmarks;

/// <summary>
/// Lightweight smoke benchmarks — not full IGD equivalence (that is Equivalence/).
/// Assert the algorithm runs and improves / produces a non-empty front.
/// </summary>
public class SmokeRunTests
{
    [Fact]
    public void Sphere_single_objective_improves()
    {
        var problem = new SphereProblem(nVariables: 5);
        var dirs = ReferenceDirections.DasDennis(1, 1);
        var algo = new Unsga3Algorithm(dirs, populationSize: 20, seed: 7);
        var result = algo.Run(problem, maxGenerations: 40);

        Assert.Equal(40, result.GenerationsExecuted);
        Assert.NotEmpty(result.NonDominatedSolutions);
        double best = result.FinalPopulation.Min(i => i.Objectives[0]);
        // Random init on [-5.12,5.12]^5 has expected ||x||^2 large; 40 gens should beat 50.
        Assert.True(best < 50.0, $"Expected improvement, best={best}");
    }

    [Fact]
    public void Zdt1_produces_front()
    {
        var problem = new Zdt1Problem();
        var dirs = ReferenceDirections.DasDennis(2, 12);
        var algo = new Unsga3Algorithm(dirs, populationSize: 40, seed: 42);
        var result = algo.Run(problem, maxGenerations: 30);

        Assert.True(result.NonDominatedSolutions.Count >= 5);
        // ZDT1 front roughly f2 ≈ 1 - sqrt(f1); check some diversity in f1.
        double minF1 = result.NonDominatedSolutions.Min(i => i.Objectives[0]);
        double maxF1 = result.NonDominatedSolutions.Max(i => i.Objectives[0]);
        Assert.True(maxF1 - minF1 > 0.05, $"Front collapsed: [{minF1}, {maxF1}]");
    }

    [Fact]
    public void Dtlz2_three_objectives_runs()
    {
        var problem = new Dtlz2Problem(nObjectives: 3, k: 10);
        var dirs = ReferenceDirections.DasDennis(3, 6); // 28 dirs
        var algo = new Unsga3Algorithm(dirs, populationSize: 28, seed: 1);
        var result = algo.Run(problem, maxGenerations: 20);

        Assert.Equal(28, result.FinalPopulation.Count);
        Assert.NotEmpty(result.NonDominatedSolutions);
        Assert.All(result.NonDominatedSolutions, ind => Assert.Equal(3, ind.Objectives.Length));
    }

    [Fact]
    public void Pop_size_null_equals_reference_count()
    {
        var problem = new Zdt1Problem();
        var dirs = ReferenceDirections.DasDennis(2, 8); // 9
        var algo = new Unsga3Algorithm(dirs, populationSize: null, seed: 0);
        var result = algo.Run(problem, maxGenerations: 5);
        Assert.Equal(dirs.Length, result.FinalPopulation.Count);
    }
}
