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
    public void Single_objective_is_unit_scalar()
    {
        var pts = ReferenceDirections.DasDennis(1, 5);
        Assert.Single(pts);
        Assert.Equal(1.0, pts[0][0]);
    }
}
