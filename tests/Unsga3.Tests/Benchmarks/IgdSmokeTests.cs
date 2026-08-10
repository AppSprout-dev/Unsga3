using Unsga3.Algorithm;
using Unsga3.Metrics;
using Unsga3.Operators.Selection;
using Unsga3.Problems;
using Unsga3.Utilities;

namespace Unsga3.Tests.Benchmarks;

/// <summary>
/// Fixed-seed IGD smoke bars — not full pymoo equivalence (see docs/EQUIVALENCE.md).
/// Bounds are intentionally loose; tighten after oracle harness lands.
/// </summary>
public class IgdSmokeTests
{
    [Fact]
    public void Zdt1_igd_improves_below_loose_bar()
    {
        var problem = new Zdt1Problem();
        var dirs = ReferenceDirections.DasDennis(2, 12);
        var algo = new Unsga3Algorithm(dirs, populationSize: 52, seed: 1);
        var result = algo.Run(problem, maxGenerations: 100);

        var obtained = result.NonDominatedSolutions
            .Where(i => i.IsFeasible)
            .Select(i => (double[])i.Objectives.Clone())
            .ToArray();
        Assert.True(obtained.Length >= 5);

        double igd = PerformanceIndicators.InvertedGenerationalDistance(obtained, ParetoFronts.Zdt1(500));
        double hv = PerformanceIndicators.Hypervolume2D(obtained, new[] { 1.1, 1.1 });

        // Mean-distance IGD (pymoo-compatible). Working MOEA on ZDT1 should clear 0.15 in 100 gens.
        Assert.True(igd < 0.15, $"ZDT1 IGD={igd} (want < 0.15; pymoo baseline ~0.06 same protocol)");
        Assert.True(hv > 0.2, $"ZDT1 HV={hv} (want > 0.2 vs r=(1.1,1.1))");
    }

    [Fact]
    public void Zdt2_runs_with_measurable_igd()
    {
        var problem = new Zdt2Problem();
        var dirs = ReferenceDirections.DasDennis(2, 12);
        var algo = new Unsga3Algorithm(dirs, populationSize: 52, seed: 2);
        var result = algo.Run(problem, maxGenerations: 150);
        var obtained = result.NonDominatedSolutions.Select(i => i.Objectives).ToArray();
        double igd = PerformanceIndicators.InvertedGenerationalDistance(obtained, ParetoFronts.Zdt2());
        // ZDT2 non-convex; mean-IGD smoke bar (not oracle parity).
        Assert.True(igd < 0.75, $"ZDT2 IGD={igd}");
    }

    [Fact]
    public void Dtlz2_three_obj_igd_finite()
    {
        var problem = new Dtlz2Problem(nObjectives: 3, k: 10);
        var dirs = ReferenceDirections.DasDennis(3, 12); // 91
        var algo = new Unsga3Algorithm(
            dirs, populationSize: 92, seed: 3, tournamentMode: TournamentMode.PymooCompatible);
        var result = algo.Run(problem, maxGenerations: 80);
        var obtained = result.NonDominatedSolutions.Select(i => i.Objectives).ToArray();
        double igd = PerformanceIndicators.InvertedGenerationalDistance(obtained, ParetoFronts.Dtlz2(3, 12));
        // Loose bar: many-obj still trails pymoo (~0.0035 @150 gens); track regression not parity.
        Assert.True(double.IsFinite(igd) && igd < 0.15, $"DTLZ2 IGD={igd}");
    }

    [Fact]
    public void Zdt1_beats_or_matches_pymoo_oracle_bar()
    {
        // Same protocol as tools/oracle (seed=1, p=12, pop=52, 100 gens).
        // pymoo UNSGA3 IGD ≈ 0.063. After HyperplaneNormalization parity (correct ASF),
        // single-seed IGD sits near pymoo; allow 1.5× for RNG path differences.
        var problem = new Zdt1Problem();
        var dirs = ReferenceDirections.DasDennis(2, 12);
        var algo = new Unsga3Algorithm(dirs, populationSize: 52, seed: 1);
        var result = algo.Run(problem, maxGenerations: 100);
        var obtained = result.NonDominatedSolutions.Select(i => (double[])i.Objectives.Clone()).ToArray();
        double igd = PerformanceIndicators.InvertedGenerationalDistance(obtained, ParetoFronts.Zdt1(500));
        const double pymooBaseline = 0.0629;
        Assert.True(igd <= pymooBaseline * 1.5,
            $"ZDT1 IGD={igd} should be ≤ 1.5× pymoo baseline {pymooBaseline}");
    }

    [Fact]
    public void Dtlz2_within_factor_of_pymoo_oracle()
    {
        // Protocol: p=12, pop=92, 150 gen, seed=1, PymooCompatible tournament.
        // pymoo IGD ≈ 0.0035; post-ASF-fix target is same order of magnitude.
        var problem = new Dtlz2Problem(nObjectives: 3, k: 10);
        var dirs = ReferenceDirections.DasDennis(3, 12);
        var algo = new Unsga3Algorithm(
            dirs, populationSize: 92, seed: 1, tournamentMode: TournamentMode.PymooCompatible);
        var result = algo.Run(problem, maxGenerations: 150);
        var obtained = result.NonDominatedSolutions.Select(i => (double[])i.Objectives.Clone()).ToArray();
        double igd = PerformanceIndicators.InvertedGenerationalDistance(obtained, ParetoFronts.Dtlz2(3, 12));
        const double pymooBaseline = 0.00350;
        // ~3× still tracks parity work; was ~5× (0.017) pre-fix and ~10× (0.037) on default.
        Assert.True(igd <= pymooBaseline * 3.0,
            $"DTLZ2 IGD={igd} should be ≤ 3× pymoo baseline {pymooBaseline}");
    }

    [Fact]
    public void Ackley_single_objective_improves()
    {
        // pymoo UNSGA3 demo shape: 1 ref direction, pop 100
        var problem = new AckleyProblem(nVariables: 10); // smaller n for fast CI
        var dirs = ReferenceDirections.DasDennis(1, 1);
        var algo = new Unsga3Algorithm(dirs, populationSize: 40, seed: 1);
        var result = algo.Run(problem, maxGenerations: 60);
        double best = result.FinalPopulation.Min(i => i.Objectives[0]);
        // Ackley at origin = 0; random on [-32,32]^10 is huge
        Assert.True(best < 15.0, $"Ackley best={best}");
    }

    [Theory]
    [InlineData(nameof(Zdt3Problem))]
    [InlineData(nameof(Zdt4Problem))]
    [InlineData(nameof(Zdt6Problem))]
    [InlineData(nameof(Dtlz1Problem))]
    [InlineData(nameof(Dtlz3Problem))]
    [InlineData(nameof(Dtlz4Problem))]
    [InlineData(nameof(Dtlz7Problem))]
    [InlineData(nameof(RosenbrockProblem))]
    public void Suite_problem_runs_without_throwing(string name)
    {
        var (problem, m) = Create(name);
        var dirs = ReferenceDirections.DasDennis(m, m == 1 ? 1 : 6);
        var algo = new Unsga3Algorithm(dirs, populationSize: Math.Max(12, dirs.Length), seed: 0);
        var result = algo.Run(problem, maxGenerations: 8);
        Assert.Equal(8, result.GenerationsExecuted);
        Assert.NotEmpty(result.FinalPopulation);
    }

    private static (Core.IProblem problem, int m) Create(string name) => name switch
    {
        nameof(Zdt3Problem) => (new Zdt3Problem(), 2),
        nameof(Zdt4Problem) => (new Zdt4Problem(), 2),
        nameof(Zdt6Problem) => (new Zdt6Problem(), 2),
        nameof(Dtlz1Problem) => (new Dtlz1Problem(3, 5), 3),
        nameof(Dtlz3Problem) => (new Dtlz3Problem(3, 10), 3),
        nameof(Dtlz4Problem) => (new Dtlz4Problem(3, 10), 3),
        nameof(Dtlz7Problem) => (new Dtlz7Problem(3, 20), 3),
        nameof(RosenbrockProblem) => (new RosenbrockProblem(5), 1),
        _ => throw new ArgumentException(name)
    };
}
