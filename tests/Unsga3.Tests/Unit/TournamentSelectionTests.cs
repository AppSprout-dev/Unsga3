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
}
