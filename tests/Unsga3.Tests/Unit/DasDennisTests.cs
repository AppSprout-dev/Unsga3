using Unsga3.Algorithm;
using Unsga3.Problems;
using Unsga3.Utilities;

namespace Unsga3.Tests.Unit;

public class DasDennisTests
{
    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(2, 1, 2)]
    [InlineData(2, 12, 13)]
    [InlineData(3, 12, 91)]
    [InlineData(3, 4, 15)]
    public void Count_matches_combinations(int m, int p, int expected)
    {
        Assert.Equal(expected, ReferenceDirections.Count(m, p));
        Assert.Equal(expected, ReferenceDirections.DasDennis(m, p).Length);
    }

    [Fact]
    public void Points_lie_on_unit_simplex()
    {
        var pts = ReferenceDirections.DasDennis(3, 4);
        foreach (var w in pts)
        {
            Assert.Equal(3, w.Length);
            double sum = w.Sum();
            Assert.InRange(sum, 1.0 - 1e-9, 1.0 + 1e-9);
            Assert.All(w, v => Assert.InRange(v, -1e-12, 1.0 + 1e-12));
        }
    }

    [Fact]
    public void Count_throws_when_the_combination_exceeds_int32()
    {
        // C(34 + 11 - 1, 10) = C(44, 10) = 2_481_256_778, which does not fit in Int32.
        Assert.Throws<OverflowException>(() => ReferenceDirections.Count(11, 34));
    }

    [Fact]
    public void Single_objective_is_unit_scalar()
    {
        var pts = ReferenceDirections.DasDennis(1, 5);
        Assert.Single(pts);
        Assert.Equal(1.0, pts[0][0]);
    }

    [Fact]
    public void Two_layer_prefixes_the_outer_simplex_and_scales_the_inside_layer()
    {
        var outer = ReferenceDirections.DasDennis(3, 2);
        var inner = ReferenceDirections.DasDennis(3, 1);
        var layers = ReferenceDirections.TwoLayerDasDennis(3, 2, 1);

        Assert.Equal(outer.Length + inner.Length, layers.Length);
        for (int i = 0; i < outer.Length; i++)
            AssertSamePoint(outer[i], layers[i]);

        // First inner direction (0, 0, 1), scale 0.5 → (1/6, 1/6, 2/3).
        var expected = ReferenceDirections.ScaleTowardCentroid(inner[0], 0.5);
        AssertSamePoint(expected, layers[outer.Length]);
        Assert.All(layers, w =>
        {
            Assert.Equal(1.0, w.Sum(), 9);
            Assert.All(w, v => Assert.InRange(v, -1e-12, 1.0 + 1e-12));
        });
    }

    [Fact]
    public void Two_layer_drops_the_shared_centroid()
    {
        // p = 3 includes (1/3, 1/3, 1/3). Scaling that point by 0.5 leaves it in place.
        int outerCount = ReferenceDirections.Count(3, 3);
        var layers = ReferenceDirections.TwoLayerDasDennis(3, 3, 3, innerScaling: 0.5);
        Assert.Equal(outerCount * 2 - 1, layers.Length);

        int centroids = layers.Count(w =>
            Math.Abs(w[0] - 1.0 / 3.0) < 1e-12
            && Math.Abs(w[1] - 1.0 / 3.0) < 1e-12
            && Math.Abs(w[2] - 1.0 / 3.0) < 1e-12);
        Assert.Equal(1, centroids);
    }

    [Fact]
    public void Two_layer_scaling_of_one_collapses_to_the_outer_layer()
    {
        var outer = ReferenceDirections.DasDennis(3, 4);
        var layers = ReferenceDirections.TwoLayerDasDennis(3, 4, 4, innerScaling: 1.0);
        Assert.Equal(outer.Length, layers.Length);
        for (int i = 0; i < outer.Length; i++)
            AssertSamePoint(outer[i], layers[i]);
    }

    [Fact]
    public void Two_layer_directions_are_accepted_by_survival()
    {
        // M≤3 oracles keep single-layer DasDennis. This only checks the factory
        // output is a legal direction array for one short unconstrained run.
        var dirs = ReferenceDirections.TwoLayerDasDennis(3, 3, 2);
        var algo = new Unsga3Algorithm(dirs, populationSize: 16, seed: 1);
        var result = algo.Run(new Dtlz2Problem(nObjectives: 3, k: 5), maxGenerations: 2);
        Assert.Equal(16, result.FinalPopulation.Count);
        Assert.NotEmpty(result.NonDominatedSolutions);
    }

    [Fact]
    public void Two_layer_rejects_a_non_positive_scale()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ReferenceDirections.TwoLayerDasDennis(3, 2, 1, innerScaling: 0.0));
    }

    private static void AssertSamePoint(double[] expected, double[] actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (int j = 0; j < expected.Length; j++)
            Assert.Equal(expected[j], actual[j], 12);
    }
}
