using Unsga3.Algorithm;
using Unsga3.Core;

namespace Unsga3.Tests.Unit;

public class NonDominatedSortTests
{
    [Fact]
    public void Dominates_simple_biobjective()
    {
        var a = Make(1, 1);
        var b = Make(2, 2);
        Assert.True(NonDominatedSort.Dominates(a, b));
        Assert.False(NonDominatedSort.Dominates(b, a));
    }

    [Fact]
    public void Sort_assigns_rank_zero_to_front()
    {
        var pop = new List<Individual>
        {
            Make(1, 3),
            Make(2, 2),
            Make(3, 1),
            Make(3, 3), // dominated
        };
        var fronts = NonDominatedSort.Sort(pop);
        Assert.Equal(3, fronts[0].Count);
        Assert.Equal(0, pop[0].Rank);
        Assert.Equal(0, pop[1].Rank);
        Assert.Equal(0, pop[2].Rank);
        Assert.True(pop[3].Rank > 0);
    }

    [Fact]
    public void Constraint_domination_prefers_feasible()
    {
        var feas = Make(5, 5);
        var infeas = Make(0, 0);
        infeas.Constraints[0] = 2;
        infeas.RefreshConstraintViolation();

        Assert.Equal(-1, NonDominatedSort.CompareConstraintDominated(feas, infeas));
        Assert.Equal(1, NonDominatedSort.CompareConstraintDominated(infeas, feas));
    }

    private static Individual Make(double f1, double f2)
    {
        var ind = new Individual(1, 2, 1);
        ind.Variables[0] = 0;
        ind.Objectives[0] = f1;
        ind.Objectives[1] = f2;
        ind.Evaluated = true;
        return ind;
    }
}
