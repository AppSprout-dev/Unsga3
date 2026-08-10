using Unsga3.Algorithm;
using Unsga3.Core;

namespace Unsga3.Tests.Unit;

public class NormalizationTests
{
    /// <summary>
    /// ASF must select axis extremes (1,0,0)/(0,1,0)/(0,0,1), not mid-edge points.
    /// The pre-fix inverted weights selected (0,√2/2,√2/2) etc. and broke DTLZ2 niching.
    /// </summary>
    [Fact]
    public void Asf_selects_axis_extremes_not_mid_edges()
    {
        // Unit-sphere orthant sample including pure axes and mid-edges.
        double s = Math.Sqrt(0.5);
        var pts = new[]
        {
            new[] { 1.0, 0.0, 0.0 },
            new[] { 0.0, 1.0, 0.0 },
            new[] { 0.0, 0.0, 1.0 },
            new[] { s, s, 0.0 },
            new[] { s, 0.0, s },
            new[] { 0.0, s, s },
            new[] { 1.0 / Math.Sqrt(3), 1.0 / Math.Sqrt(3), 1.0 / Math.Sqrt(3) },
        };

        for (int axis = 0; axis < 3; axis++)
        {
            int best = 0;
            double bestAsf = Normalization.Asf(pts[0], axis);
            for (int i = 1; i < pts.Length; i++)
            {
                double a = Normalization.Asf(pts[i], axis);
                if (a < bestAsf)
                {
                    bestAsf = a;
                    best = i;
                }
            }

            Assert.Equal(axis, best); // pure axis points are indices 0,1,2
        }
    }

    [Fact]
    public void Normalize_on_unit_sphere_axes_gives_unit_intercepts()
    {
        var pop = new List<Individual>
        {
            Make(1, 0, 0),
            Make(0, 1, 0),
            Make(0, 0, 1),
            Make(0.5, 0.5, 0.5 / Math.Sqrt(0.5)), // interior-ish on sphere-ish
        };
        // Put a true sphere mid point
        double s = Math.Sqrt(0.5);
        pop[3] = Make(s, s, 0);

        var norm = new Normalization(3);
        var nd = new[] { 0, 1, 2, 3 };
        var N = norm.Normalize(pop, nd);

        // Ideal ~ 0; nadir/intercepts ~ 1 on each axis for pure extremes.
        Assert.True(norm.IdealPoint[0] <= 1e-12);
        Assert.True(norm.IdealPoint[1] <= 1e-12);
        Assert.True(norm.IdealPoint[2] <= 1e-12);

        // Axis points normalize near e_i.
        Assert.True(Math.Abs(N[0][0] - 1.0) < 0.05);
        Assert.True(Math.Abs(N[0][1]) < 0.05);
        Assert.True(Math.Abs(N[1][1] - 1.0) < 0.05);
        Assert.True(Math.Abs(N[2][2] - 1.0) < 0.05);
    }

    [Fact]
    public void Ideal_point_is_persistent_across_calls()
    {
        var norm = new Normalization(2);
        var pop1 = new List<Individual> { Make2(0.1, 0.9), Make2(0.9, 0.1) };
        norm.Normalize(pop1);
        Assert.Equal(0.1, norm.IdealPoint[0], 9);
        Assert.Equal(0.1, norm.IdealPoint[1], 9);

        // Worse population must not raise the ideal.
        var pop2 = new List<Individual> { Make2(0.5, 0.5), Make2(0.8, 0.8) };
        norm.Normalize(pop2);
        Assert.Equal(0.1, norm.IdealPoint[0], 9);
        Assert.Equal(0.1, norm.IdealPoint[1], 9);

        // Better point lowers it.
        var pop3 = new List<Individual> { Make2(0.05, 0.5) };
        norm.Normalize(pop3);
        Assert.Equal(0.05, norm.IdealPoint[0], 9);
        Assert.Equal(0.1, norm.IdealPoint[1], 9);
    }

    private static Individual Make(double a, double b, double c)
    {
        var ind = new Individual(1, 3);
        ind.Objectives[0] = a;
        ind.Objectives[1] = b;
        ind.Objectives[2] = c;
        return ind;
    }

    private static Individual Make2(double a, double b)
    {
        var ind = new Individual(1, 2);
        ind.Objectives[0] = a;
        ind.Objectives[1] = b;
        return ind;
    }
}
