using Unsga3.Algorithm;
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
        Assert.Same(a, TournamentSelection.Winner(a, b, rng));
    }

    [Fact]
    public void Same_rank_prefers_lower_niche()
    {
        var a = new Individual(1, 1) { Rank = 0, NicheCount = 1 };
        var b = new Individual(1, 1) { Rank = 0, NicheCount = 4 };
        var rng = new RandomProvider(1);
        Assert.Same(a, TournamentSelection.Winner(a, b, rng));
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
        Assert.Same(a, TournamentSelection.Winner(a, b, rng));
    }
}
