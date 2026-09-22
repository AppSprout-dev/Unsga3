using Unsga3.Algorithm;
using Unsga3.Core;
using Unsga3.Operators.Selection;
using Unsga3.Operators.Survival;
using Unsga3.Utilities;

namespace Unsga3.Tests.Unit;

/// <summary>
/// After survival, <see cref="TournamentSelection.PrepareForSelection"/> normalizes the
/// survivors again and overwrites niche ids. pymoo keeps the ids written during survival.
/// </summary>
public class MatingRenormalizeTests
{
    [Fact]
    public void PrepareForSelection_overwrites_survival_niche_ids()
    {
        var dirs = ReferenceDirections.DasDennis(2, 1);
        var refs = new ReferencePointManager(dirs);
        var norm = new Normalization(2);
        var survival = new NondominatedSortingSurvival(refs, norm);

        var pool = new List<Individual>
        {
            Point(0.0, 1.0),
            Point(1.0, 0.0),
            Point(0.2, 0.9),
            Point(0.9, 0.2),
            Point(0.4, 0.4),
        };

        // targetSize < |ND front| forces last-front niching, which writes niche ids.
        var survivors = survival.Select(pool, targetSize: 3, rng: null);
        int[] survivalNiches = survivors.Select(s => s.AssociatedReference).ToArray();
        Assert.All(survivalNiches, id => Assert.InRange(id, 0, dirs.Length - 1));

        foreach (var s in survivors)
        {
            s.AssociatedReference = 999;
            s.PerpendicularDistance = -1;
        }

        TournamentSelection.PrepareForSelection(survivors, refs, norm);
        int[] matingNiches = survivors.Select(s => s.AssociatedReference).ToArray();

        Assert.DoesNotContain(999, matingNiches);
        Assert.All(survivors, s => Assert.True(s.PerpendicularDistance >= 0));

        // Locked dump for this pool (Das–Dennis p=1, target 3, no RNG).
        // The ids match, and the sentinel 999 is gone, so mating rewrote the fields
        // pymoo would have kept. Matching ids here is not a claim that every generation matches.
        Assert.Equal(new[] { 0, 1, 0 }, survivalNiches);
        Assert.Equal(new[] { 0, 1, 0 }, matingNiches);
        Assert.Equal(0.0, survivors[0].PerpendicularDistance, 9);
        Assert.Equal(0.0, survivors[1].PerpendicularDistance, 9);
        Assert.Equal(0.2, survivors[2].PerpendicularDistance, 9);
    }

    private static Individual Point(double f1, double f2)
    {
        var ind = new Individual(1, 2);
        ind.Objectives[0] = f1;
        ind.Objectives[1] = f2;
        return ind;
    }
}
