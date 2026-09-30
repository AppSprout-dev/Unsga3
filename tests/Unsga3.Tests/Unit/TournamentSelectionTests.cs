using Unsga3.Algorithm;
using Unsga3.Operators.Crossover;
using Unsga3.Operators.Selection;
using Unsga3.Utilities;

namespace Unsga3.Tests.Unit;

public class TournamentSelectionTests
{
    [Fact]
    public void Prefers_better_rank()
    {
        var a = new Individual(1, 1) { Rank = 0, NicheCount = 5 };
        var b = new Individual(1, 1) { Rank = 1, NicheCount = 0 };
        a.Objectives[0] = 1;
        b.Objectives[0] = 0;
        var rng = new RandomProvider(1);
        Assert.Same(a, TournamentSelection.Winner(a, b, rng, TournamentMode.RankNicheDistance));
    }

    [Fact]
    public void Same_rank_prefers_lower_niche()
    {
        var a = new Individual(1, 1) { Rank = 0, NicheCount = 1 };
        var b = new Individual(1, 1) { Rank = 0, NicheCount = 4 };
        var rng = new RandomProvider(1);
        Assert.Same(a, TournamentSelection.Winner(a, b, rng, TournamentMode.RankNicheDistance));
    }

    [Fact]
    public void Feasible_beats_infeasible()
    {
        var a = new Individual(1, 1, 1);
        var b = new Individual(1, 1, 1);
        a.Rank = b.Rank = 0;
        b.Constraints[0] = 1;
        b.RefreshConstraintViolation();
        var rng = new RandomProvider(1);
        Assert.Same(a, TournamentSelection.Winner(a, b, rng, TournamentMode.RankNicheDistance));
    }

    [Fact]
    public void Pymoo_same_niche_prefers_rank()
    {
        var a = new Individual(1, 2) { Rank = 0, AssociatedReference = 3, PerpendicularDistance = 0.9 };
        var b = new Individual(1, 2) { Rank = 1, AssociatedReference = 3, PerpendicularDistance = 0.1 };
        var rng = new RandomProvider(0);
        Assert.Same(a, TournamentSelection.Winner(a, b, rng, TournamentMode.PymooCompatible));
    }

    [Fact]
    public void Pymoo_different_niche_is_random_not_rank()
    {
        // With different niches, pymoo ignores rank; over many trials both should win sometimes.
        var better = new Individual(1, 2) { Rank = 0, AssociatedReference = 0, PerpendicularDistance = 0.1 };
        var worse = new Individual(1, 2) { Rank = 5, AssociatedReference = 1, PerpendicularDistance = 0.1 };
        int betterWins = 0;
        for (int seed = 0; seed < 40; seed++)
        {
            var w = TournamentSelection.Winner(better, worse, new RandomProvider(seed), TournamentMode.PymooCompatible);
            if (ReferenceEquals(w, better)) betterWins++;
        }
        Assert.InRange(betterWins, 5, 35); // not deterministic rank dominance
    }

    [Fact]
    public void Pymoo_same_niche_equal_rank_prefers_shorter_distance()
    {
        var closer = new Individual(1, 2)
        {
            Rank = 0,
            AssociatedReference = 2,
            PerpendicularDistance = 0.1,
            NicheCount = 9,
        };
        var farther = new Individual(1, 2)
        {
            Rank = 0,
            AssociatedReference = 2,
            PerpendicularDistance = 0.4,
            NicheCount = 1,
        };
        Assert.Same(closer, TournamentSelection.Winner(
            closer, farther, new RandomProvider(0), TournamentMode.PymooCompatible));
    }

    [Fact]
    public void Pymoo_same_niche_distance_tie_is_a_coin_flip()
    {
        var a = new Individual(1, 2) { Rank = 0, AssociatedReference = 4, PerpendicularDistance = 0.2 };
        var b = new Individual(1, 2) { Rank = 0, AssociatedReference = 4, PerpendicularDistance = 0.2 };
        int aWins = 0;
        for (int seed = 0; seed < 40; seed++)
        {
            var w = TournamentSelection.Winner(a, b, new RandomProvider(seed), TournamentMode.PymooCompatible);
            if (ReferenceEquals(w, a)) aWins++;
        }
        Assert.InRange(aWins, 5, 35);
    }

    [Fact]
    public void RankNiche_prefers_lower_niche_count_across_different_niches()
    {
        var sparse = new Individual(1, 2)
        {
            Rank = 0,
            NicheCount = 1,
            AssociatedReference = 0,
            PerpendicularDistance = 0.5,
        };
        var crowded = new Individual(1, 2)
        {
            Rank = 0,
            NicheCount = 6,
            AssociatedReference = 1,
            PerpendicularDistance = 0.01,
        };
        Assert.Same(sparse, TournamentSelection.Winner(
            sparse, crowded, new RandomProvider(1), TournamentMode.RankNicheDistance));
    }

    [Fact]
    public void Pymoo_ignores_niche_count_when_niches_differ()
    {
        var sparse = new Individual(1, 2)
        {
            Rank = 1,
            NicheCount = 1,
            AssociatedReference = 0,
            PerpendicularDistance = 0.5,
        };
        var crowded = new Individual(1, 2)
        {
            Rank = 0,
            NicheCount = 6,
            AssociatedReference = 1,
            PerpendicularDistance = 0.01,
        };
        int sparseWins = 0;
        for (int seed = 0; seed < 40; seed++)
        {
            var w = TournamentSelection.Winner(sparse, crowded, new RandomProvider(seed), TournamentMode.PymooCompatible);
            if (ReferenceEquals(w, sparse)) sparseWins++;
        }
        Assert.InRange(sparseWins, 5, 35);
    }

    [Fact]
    public void Default_sbx_probability_is_one()
    {
        // pymoo NSGA3/UNSGA3 uses SBX(prob=1.0). Seada & Deb section 4 uses pc = 0.9.
        Assert.Equal(1.0, new SimulatedBinaryCrossover().Probability);
    }

    [Fact]
    public void Defaults_stay_rank_niche_and_independent_pool()
    {
        var selection = new TournamentSelection();
        Assert.Equal(TournamentMode.RankNicheDistance, selection.Mode);
        Assert.Equal(MatingPoolMode.IndependentWithReplacement, selection.MatingPool);
    }

    [Fact]
    public void Algorithm2_same_niche_distance_tie_keeps_the_second_parent()
    {
        var first = TiedNicheParents();
        var second = TiedNicheParents();
        for (int seed = 0; seed < 20; seed++)
        {
            var winner = TournamentSelection.Winner(
                first, second, new RandomProvider(seed), TournamentMode.Algorithm2);
            Assert.Same(second, winner);
        }
    }

    [Fact]
    public void Algorithm2_same_niche_still_prefers_the_shorter_distance()
    {
        var closer = TiedNicheParents();
        var farther = TiedNicheParents();
        farther.PerpendicularDistance = 0.8;
        Assert.Same(closer, TournamentSelection.Winner(
            closer, farther, new RandomProvider(0), TournamentMode.Algorithm2));
        Assert.Same(closer, TournamentSelection.Winner(
            farther, closer, new RandomProvider(0), TournamentMode.Algorithm2));
    }

    [Fact]
    public void Algorithm2_equal_constraint_violation_stays_a_coin_flip()
    {
        var a = new Individual(1, 1, 1);
        var b = new Individual(1, 1, 1);
        a.Constraints[0] = 1.5;
        b.Constraints[0] = 1.5;
        a.RefreshConstraintViolation();
        b.RefreshConstraintViolation();
        a.Rank = b.Rank = 0;
        a.AssociatedReference = b.AssociatedReference = 1;
        a.PerpendicularDistance = b.PerpendicularDistance = 0.2;

        int aWins = 0;
        for (int seed = 0; seed < 40; seed++)
        {
            var winner = TournamentSelection.Winner(a, b, new RandomProvider(seed), TournamentMode.Algorithm2);
            if (ReferenceEquals(winner, a)) aWins++;
        }
        Assert.InRange(aWins, 5, 35);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(13)]
    [InlineData(52)]
    [InlineData(91)]
    public void Two_shuffle_each_index_is_a_contestant_twice(int n)
    {
        for (int seed = 0; seed < 40; seed++)
        {
            int[] contests = TournamentSelection.BuildTwoShuffleContestants(n, n, new RandomProvider(seed));
            Assert.Equal(n * 2, contests.Length);

            var times = new int[n];
            for (int i = 0; i < n; i++)
            {
                int a = contests[2 * i];
                int b = contests[2 * i + 1];
                Assert.NotEqual(a, b);
                times[a]++;
                times[b]++;
            }
            Assert.All(times, t => Assert.Equal(2, t));
        }
    }

    [Fact]
    public void Two_shuffle_select_parents_follows_the_contest_order()
    {
        const int n = 13;
        const int seed = 7;
        var population = RankedPopulation(n);
        int[] contests = TournamentSelection.BuildTwoShuffleContestants(n, n, new RandomProvider(seed));
        var selection = new TournamentSelection(
            TournamentMode.RankNicheDistance,
            MatingPoolMode.TwoShuffledPasses);
        var parents = selection.SelectParents(population, n, new RandomProvider(seed));

        Assert.Equal(n, parents.Count);
        for (int i = 0; i < n; i++)
        {
            int expected = Math.Min(contests[2 * i], contests[2 * i + 1]);
            Assert.Equal(expected, parents[i].Variables[0]);
        }
    }

    [Fact]
    public void Default_pool_stays_independent_draws_with_replacement()
    {
        const int n = 13;
        const int seed = 4;
        var population = RankedPopulation(n);
        var rng = new RandomProvider(seed);
        var expected = new double[n];
        for (int i = 0; i < n; i++)
        {
            int a = rng.Next(n);
            int b = rng.NextExcept(n, a);
            expected[i] = Math.Min(a, b);
        }

        var parents = new TournamentSelection().SelectParents(population, n, new RandomProvider(seed));
        for (int i = 0; i < n; i++)
            Assert.Equal(expected[i], parents[i].Variables[0]);
    }

    [Fact]
    public void Two_shuffle_algorithm2_distance_tie_keeps_the_second_contestant()
    {
        const int n = 8;
        const int seed = 3;
        var population = new List<Individual>(n);
        for (int i = 0; i < n; i++)
        {
            var ind = TiedNicheParents();
            ind.Variables[0] = i;
            population.Add(ind);
        }

        int[] contests = TournamentSelection.BuildTwoShuffleContestants(n, n, new RandomProvider(seed));
        var selection = new TournamentSelection(TournamentMode.Algorithm2, MatingPoolMode.TwoShuffledPasses);
        var parents = selection.SelectParents(population, n, new RandomProvider(seed));
        for (int i = 0; i < n; i++)
            Assert.Equal(contests[2 * i + 1], parents[i].Variables[0]);
    }

    [Fact]
    public void Short_odd_pool_still_pairs_distinct_contestants()
    {
        for (int seed = 0; seed < 30; seed++)
        {
            int[] contests = TournamentSelection.BuildTwoShuffleContestants(5, 3, new RandomProvider(seed));
            Assert.Equal(6, contests.Length);
            for (int i = 0; i < 3; i++)
                Assert.NotEqual(contests[2 * i], contests[2 * i + 1]);
        }
    }

    private static Individual TiedNicheParents() =>
        new(1, 1)
        {
            Rank = 0,
            AssociatedReference = 2,
            PerpendicularDistance = 0.2,
        };

    private static List<Individual> RankedPopulation(int n)
    {
        var population = new List<Individual>(n);
        for (int i = 0; i < n; i++)
        {
            population.Add(new Individual(new[] { (double)i }, 1) { Rank = i });
        }
        return population;
    }
}
