using Unsga3.Algorithm;
using Unsga3.Core;
using Unsga3.Operators.Selection;
using Unsga3.Operators.Survival;
using Unsga3.Utilities;

namespace Unsga3.Tests.Unit;

/// <summary>
/// pymoo <c>Survival.filter_infeasible</c>: niche feasible members, fill a shortfall
/// by ascending constraint violation, and build the hyperplane from feasible objectives.
/// </summary>
public class ConstrainedSurvivalTests
{
    [Fact]
    public void Niches_only_feasible_members_when_they_fill_the_population()
    {
        var norm = new Normalization(2);
        var survival = Survival(norm);
        var infeasible = Mark(0.0, 0.0, cv: 0.25, tag: 9);
        infeasible.AssociatedReference = 7;

        var selected = survival.Select(
            new List<Individual>
            {
                Mark(0.2, 0.9, tag: 1),
                Mark(0.9, 0.2, tag: 2),
                Mark(0.4, 0.4, tag: 3),
                infeasible,
            },
            targetSize: 2,
            rng: null);

        Assert.Equal(2, selected.Count);
        Assert.All(selected, ind => Assert.True(ind.IsFeasible));
        Assert.DoesNotContain(selected, ind => ind.Variables[0] == 9);
        Assert.Equal(0.2, norm.IdealPoint[0], 12);
        Assert.Equal(0.2, norm.IdealPoint[1], 12);
    }

    [Fact]
    public void Shortfall_is_filled_by_ascending_cv_without_a_niche()
    {
        var norm = new Normalization(2);
        var survival = Survival(norm);
        var low = Mark(9, 9, cv: 0.2, tag: 2);
        var mid = Mark(0, 0, cv: 0.5, tag: 3);
        var high = Mark(1, 8, cv: 1.5, tag: 4);
        low.AssociatedReference = 3;
        mid.AssociatedReference = 4;
        high.AssociatedReference = 5;

        var selected = survival.Select(
            new List<Individual> { high, Mark(0.2, 0.8, tag: 1), low, mid },
            targetSize: 3,
            rng: null);

        Assert.Equal(new[] { 1.0, 2.0, 3.0 }, selected.Select(s => s.Variables[0]));
        Assert.True(selected[0].IsFeasible);
        Assert.Equal(-1, selected[1].AssociatedReference);
        Assert.Equal(-1, selected[2].AssociatedReference);
        Assert.Equal(double.PositiveInfinity, selected[1].PerpendicularDistance);
        // Infeasible (0, 0) must not set the ideal. The only feasible point is (0.2, 0.8).
        Assert.Equal(0.2, norm.IdealPoint[0], 12);
        Assert.Equal(0.8, norm.IdealPoint[1], 12);
    }

    [Fact]
    public void Equal_cv_fill_keeps_the_earlier_index()
    {
        var survival = Survival(new Normalization(2));
        var earlier = Mark(4, 4, cv: 1, tag: 8);
        var later = Mark(1, 1, cv: 1, tag: 9);
        var selected = survival.Select(
            new List<Individual> { earlier, Mark(0.3, 0.7, tag: 1), later },
            targetSize: 2,
            rng: null);

        Assert.Equal(1.0, selected[0].Variables[0]);
        Assert.Equal(8.0, selected[1].Variables[0]);
    }

    [Fact]
    public void All_infeasible_truncates_by_cv_and_leaves_the_hyperplane()
    {
        var norm = new Normalization(2);
        norm.Normalize(new List<Individual> { Mark(1, 3), Mark(3, 1) });
        double ideal0 = norm.IdealPoint[0];
        double nadir0 = norm.NadirPoint[0];

        var survival = Survival(norm);
        var selected = survival.Select(
            new List<Individual>
            {
                Mark(0, 0, cv: 3, tag: 3),
                Mark(4, 1, cv: 0.4, tag: 1),
                Mark(2, 2, cv: 1.2, tag: 2),
            },
            targetSize: 2,
            rng: null);

        Assert.Equal(new[] { 1.0, 2.0 }, selected.Select(s => s.Variables[0]));
        Assert.All(selected, ind => Assert.Equal(-1, ind.AssociatedReference));
        Assert.Equal(ideal0, norm.IdealPoint[0], 12);
        Assert.Equal(nadir0, norm.NadirPoint[0], 12);
        Assert.Equal(1.0, norm.IdealPoint[1], 12);
    }

    [Fact]
    public void PrepareForSelection_on_an_all_infeasible_pop_does_not_set_ideal()
    {
        var norm = new Normalization(2);
        var refs = new ReferencePointManager(ReferenceDirections.DasDennis(2, 1));
        var pop = new List<Individual>
        {
            Mark(1.0, 2.0, cv: 1.0),
            Mark(3.0, 0.5, cv: 2.0),
        };

        TournamentSelection.PrepareForSelection(pop, refs, norm);

        Assert.True(double.IsPositiveInfinity(norm.IdealPoint[0]));
        Assert.True(double.IsPositiveInfinity(norm.IdealPoint[1]));
        Assert.All(pop, ind => Assert.False(double.IsNaN(ind.PerpendicularDistance)));
        Assert.All(pop, ind => Assert.InRange(ind.AssociatedReference, 0, refs.Count - 1));
    }

    private static NondominatedSortingSurvival Survival(Normalization norm)
    {
        var refs = new ReferencePointManager(ReferenceDirections.DasDennis(2, 1));
        return new NondominatedSortingSurvival(refs, norm);
    }

    private static Individual Mark(double f1, double f2, double cv = 0, double tag = 0)
    {
        var ind = new Individual(1, 2, 1);
        ind.Variables[0] = tag;
        ind.Objectives[0] = f1;
        ind.Objectives[1] = f2;
        ind.Constraints[0] = cv;
        ind.RefreshConstraintViolation();
        return ind;
    }
}
