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

    [Fact]
    public void Equal_constraint_violation_is_mutually_non_dominated()
    {
        // (0, 0) Pareto-dominates (1, 1), but Deb's constraint-domination stops
        // when the violations are equal. Both land on the first front.
        var better = Make(0, 0);
        var worse = Make(1, 1);
        better.Constraints[0] = 1.5;
        worse.Constraints[0] = 1.5;
        better.RefreshConstraintViolation();
        worse.RefreshConstraintViolation();

        Assert.Equal(0, NonDominatedSort.CompareConstraintDominated(better, worse));
        Assert.Equal(0, NonDominatedSort.CompareConstraintDominated(worse, better));
        Assert.False(better.ConstraintDominates(worse));
        Assert.False(worse.ConstraintDominates(better));

        var fronts = NonDominatedSort.Sort(new List<Individual> { better, worse });
        Assert.Single(fronts);
        Assert.Equal(2, fronts[0].Count);
        Assert.Equal(0, better.Rank);
        Assert.Equal(0, worse.Rank);

        // A strictly smaller violation still dominates, objectives aside.
        worse.Constraints[0] = 1.5 + 1e-6;
        worse.RefreshConstraintViolation();
        Assert.Equal(-1, NonDominatedSort.CompareConstraintDominated(better, worse));
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
