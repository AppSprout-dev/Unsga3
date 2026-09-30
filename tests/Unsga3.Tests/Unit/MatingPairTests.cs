using Unsga3.Algorithm;
using Unsga3.Core;
using Unsga3.Operators.Crossover;
using Unsga3.Operators.Mutation;
using Unsga3.Operators.Selection;
using Unsga3.Problems;
using Unsga3.Utilities;

namespace Unsga3.Tests.Unit;

public class MatingPairTests
{
    [Theory]
    [InlineData(4)]
    [InlineData(52)]
    [InlineData(92)]
    public void Even_population_keeps_consecutive_pairs_and_wraps_to_zero(int n)
    {
        int pair = 0;
        for (int cycle = 0; cycle < 2; cycle++)
        {
            for (int i = 0; i < n; i += 2)
            {
                (int left, int right, int next) = Unsga3Algorithm.AdvanceMatingPair(pair, n);
                Assert.Equal(i, left);
                Assert.Equal(i + 1, right);
                pair = next;
            }
        }

        Assert.Equal(n, pair);
        (int wrappedLeft, int wrappedRight, _) = Unsga3Algorithm.AdvanceMatingPair(pair, n);
        Assert.Equal(0, wrappedLeft);
        Assert.Equal(1, wrappedRight);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(13)]
    [InlineData(91)]
    public void Odd_population_pairs_the_last_parent_with_parent_zero(int n)
    {
        int pair = 0;
        var pairs = new List<(int Left, int Right)>();
        for (int step = 0; step < n; step++)
        {
            (int left, int right, int next) = Unsga3Algorithm.AdvanceMatingPair(pair, n);
            pairs.Add((left, right));
            pair = next;
        }

        Assert.Contains(pairs, p => p.Left == n - 1 && p.Right == 0);
        Assert.Equal((0, 1), pairs[0]);
    }

    [Fact]
    public void Odd_n_last_parent_is_used_as_a_crossover_parent()
    {
        const int n = 13;
        var parents = IndexedParents(n);
        var seen = new List<(int Left, int Right)>();
        var algo = new Unsga3Algorithm(
            new[] { new[] { 1.0 } },
            populationSize: n,
            crossover: new RecordingCrossover(seen),
            mutation: new NoopMutation(),
            eliminateDuplicates: false);

        algo.CreateOffspring(new BoxProblem(), parents, parents, new RandomProvider(1), mutProb: 1.0);

        Assert.Contains(seen, p => p.Left == n - 1 || p.Right == n - 1);
        Assert.Equal((n - 1, 0), seen[n / 2]);
    }

    [Fact]
    public void Even_n_crossover_sequence_does_not_use_the_odd_wrap()
    {
        const int parentCount = 4;
        var parents = IndexedParents(parentCount);
        var seen = new List<(int Left, int Right)>();
        var algo = new Unsga3Algorithm(
            new[] { new[] { 1.0 } },
            populationSize: 8,
            crossover: new RecordingCrossover(seen),
            mutation: new NoopMutation(),
            eliminateDuplicates: false);

        algo.CreateOffspring(new BoxProblem(), parents, parents, new RandomProvider(1), mutProb: 1.0);

        Assert.Equal(new[] { (0, 1), (2, 3), (0, 1), (2, 3) }, seen);
    }

    [Fact]
    public void Two_shuffle_odd_population_run_returns_full_population()
    {
        // Das–Dennis (2, 2) has 3 directions, so default N is odd.
        var algo = Unsga3Algorithm.WithDasDennis(
            2,
            2,
            seed: 1,
            tournamentMode: TournamentMode.Algorithm2,
            matingPool: MatingPoolMode.TwoShuffledPasses);
        var result = algo.Run(new Zdt1Problem(nVariables: 4), maxGenerations: 1);
        Assert.Equal(3, result.FinalPopulation.Count);
    }

    private static List<Individual> IndexedParents(int n)
    {
        var parents = new List<Individual>(n);
        for (int i = 0; i < n; i++)
            parents.Add(new Individual(new[] { (double)i }, 1));
        return parents;
    }

    private sealed class BoxProblem : ProblemBase
    {
        public BoxProblem()
            : base(1, 1, 0, new[] { (0.0, 1000.0) })
        {
        }

        protected override void EvaluateCore(double[] x, double[] f, double[] g) => f[0] = x[0];
    }

    private sealed class RecordingCrossover : ICrossover
    {
        private readonly List<(int Left, int Right)> _pairs;

        public RecordingCrossover(List<(int Left, int Right)> pairs) => _pairs = pairs;

        public (Individual Child1, Individual Child2) Crossover(
            Individual parent1, Individual parent2, IProblem problem, RandomProvider rng)
        {
            _pairs.Add(((int)parent1.Variables[0], (int)parent2.Variables[0]));
            return (parent1.Clone(), parent2.Clone());
        }
    }

    private sealed class NoopMutation : IMutation
    {
        public void Mutate(Individual individual, IProblem problem, RandomProvider rng, double probabilityPerVariable)
        {
        }
    }
}
