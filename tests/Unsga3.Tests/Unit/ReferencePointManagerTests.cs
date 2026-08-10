using Unsga3.Algorithm;
using Unsga3.Utilities;

namespace Unsga3.Tests.Unit;

public class ReferencePointManagerTests
{
    [Fact]
    public void Associates_to_nearest_direction()
    {
        var dirs = new[]
        {
            new[] { 1.0, 0.0 },
            new[] { 0.0, 1.0 },
            new[] { 0.5, 0.5 },
        };
        var mgr = new ReferencePointManager(dirs);
        var pop = new List<Individual>
        {
            MakeObj(0.9, 0.05),
            MakeObj(0.05, 0.9),
            MakeObj(0.4, 0.4),
        };
        var norm = pop.Select(p => (double[])p.Objectives.Clone()).ToArray();
        mgr.Associate(pop, norm);

        Assert.Equal(0, pop[0].AssociatedReference);
        Assert.Equal(1, pop[1].AssociatedReference);
        Assert.Equal(2, pop[2].AssociatedReference);
        Assert.Equal(1, mgr.GetNicheCount(0));
        Assert.Equal(1, mgr.GetNicheCount(1));
        Assert.Equal(1, mgr.GetNicheCount(2));
    }

    [Fact]
    public void Perpendicular_distance_zero_on_ray()
    {
        var w = new[] { 0.5, 0.5 };
        var f = new[] { 1.0, 1.0 };
        Assert.True(ReferencePointManager.PerpendicularDistance(f, w) < 1e-9);
    }

    private static Individual MakeObj(double f1, double f2)
    {
        var ind = new Individual(1, 2);
        ind.Objectives[0] = f1;
        ind.Objectives[1] = f2;
        return ind;
    }
}
