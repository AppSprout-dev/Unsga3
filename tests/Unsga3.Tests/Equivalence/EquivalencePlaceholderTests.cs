using Unsga3.Algorithm;
using Unsga3.Metrics;
using Unsga3.Operators.Selection;
using Unsga3.Problems;
using Unsga3.Utilities;

namespace Unsga3.Tests.Equivalence;

/// <summary>
/// Seed-1 DTLZ2 regression guard. This used to assert <c>true</c>, so a broken run still passed.
/// The published mismatched-k pymoo scalar is 0.00350 and the C# seed-1 IGD is about 0.00403
/// (~1.15× that scalar). A bar of 3× that scalar still passes a regression to about 2.9×.
/// </summary>
public class EquivalencePlaceholderTests
{
    [Fact]
    public void Dtlz2_seed1_rejects_a_near_3x_regression()
    {
        // 0.00350 is the published pymoo seed-1 scalar at n_var=10 (k=8), not the matched
        // n_var=12 problem. Passing this test is a regression guard, not a same-problem claim.
        const double publishedMismatchedPymoo = 0.00350;
        const double maxFactor = 2.0;

        var problem = new Dtlz2Problem(nObjectives: 3, k: 10);
        var dirs = ReferenceDirections.DasDennis(3, 12);
        var algo = new Unsga3Algorithm(
            dirs, populationSize: 92, seed: 1, tournamentMode: TournamentMode.PymooCompatible);
        var result = algo.Run(problem, maxGenerations: 150);
        var obtained = result.NonDominatedSolutions.Select(i => (double[])i.Objectives.Clone()).ToArray();
        double igd = PerformanceIndicators.InvertedGenerationalDistance(obtained, ParetoFronts.Dtlz2(3, 12));

        Assert.True(
            igd <= publishedMismatchedPymoo * maxFactor,
            $"DTLZ2 IGD={igd} exceeds {maxFactor}× {publishedMismatchedPymoo}. " +
            "A factor of 3 would still pass a regression to about 2.9×.");
    }
}
