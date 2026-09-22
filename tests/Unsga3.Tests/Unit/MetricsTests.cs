using Unsga3.Metrics;

namespace Unsga3.Tests.Unit;

public class MetricsTests
{
    [Fact]
    public void IGD_zero_when_obtained_covers_reference()
    {
        var pf = ParetoFronts.Zdt1(50);
        double igd = PerformanceIndicators.InvertedGenerationalDistance(pf, pf);
        Assert.True(igd < 1e-12, $"IGD={igd}");
    }

    [Fact]
    public void IGD_positive_when_front_shifted()
    {
        // Single PF point (0,0); obtained (1,1) → IGD = √2 (pymoo: mean nearest distance).
        var pf = new[] { new[] { 0.0, 0.0 } };
        var obtained = new[] { new[] { 1.0, 1.0 } };
        double igd = PerformanceIndicators.InvertedGenerationalDistance(obtained, pf);
        Assert.InRange(igd, Math.Sqrt(2) - 1e-9, Math.Sqrt(2) + 1e-9);
    }

    [Fact]
    public void IGD_matches_mean_of_nearest_distances()
    {
        // Two PF points; one obtained at origin → mean of ||z||.
        var pf = new[] { new[] { 3.0, 0.0 }, new[] { 0.0, 4.0 } };
        var obtained = new[] { new[] { 0.0, 0.0 } };
        double igd = PerformanceIndicators.InvertedGenerationalDistance(obtained, pf);
        Assert.InRange(igd, 3.5 - 1e-9, 3.5 + 1e-9); // (3+4)/2
    }

    [Fact]
    public void HV2D_unit_square_corner()
    {
        // Single point (0,0) vs r=(1,1) → HV = 1
        double hv = PerformanceIndicators.Hypervolume2D(
            new[] { new[] { 0.0, 0.0 } },
            new[] { 1.0, 1.0 });
        Assert.InRange(hv, 0.999, 1.001);
    }

    [Fact]
    public void HV2D_two_points()
    {
        // Points (0.2,0.5) and (0.5,0.2) vs (1,1)
        // Area = (1-0.5)*(1-0.2) + (0.5-0.2)*(1-0.5) = 0.5*0.8 + 0.3*0.5 = 0.4 + 0.15 = 0.55
        double hv = PerformanceIndicators.Hypervolume2D(
            new[] { new[] { 0.2, 0.5 }, new[] { 0.5, 0.2 } },
            new[] { 1.0, 1.0 });
        Assert.InRange(hv, 0.54, 0.56);
    }

    [Fact]
    public void IgdPlus_and_gd_match_the_hand_case()
    {
        // A = {(0, 1)} against Z = {(0, 0), (1, 0)}.
        // IGD+ modified distance is 1 for both reference points. GD nearest distance is 1.
        var obtained = new[] { new[] { 0.0, 1.0 } };
        var reference = new[] { new[] { 0.0, 0.0 }, new[] { 1.0, 0.0 } };
        double igdPlus = PerformanceIndicators.InvertedGenerationalDistancePlus(obtained, reference);
        double gd = PerformanceIndicators.GenerationalDistance(obtained, reference);
        Assert.Equal(1.0, igdPlus, 9);
        Assert.Equal(1.0, gd, 9);
    }

    [Fact]
    public void Indicators_reject_mismatched_objective_lengths()
    {
        var wide = new[] { new[] { 0.0, 0.0 } };
        var shortVector = new[] { new[] { 0.0 } };
        Assert.Throws<ArgumentException>(() =>
            PerformanceIndicators.InvertedGenerationalDistance(wide, shortVector));
        Assert.Throws<ArgumentException>(() =>
            PerformanceIndicators.GenerationalDistance(shortVector, wide));
        Assert.Throws<ArgumentException>(() =>
            PerformanceIndicators.InvertedGenerationalDistancePlus(wide, shortVector));
    }

    [Fact]
    public void Zdt_samplers_reject_a_single_point()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ParetoFronts.Zdt1(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ParetoFronts.Zdt2(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ParetoFronts.Zdt4(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ParetoFronts.Zdt6(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ParetoFronts.Zdt3(1));
    }

    [Fact]
    public void Zdt3_second_segment_starts_at_the_library_literal()
    {
        // pymoo 0.6.2 uses 0.182228780. This library uses 0.1822287280.
        var front = ParetoFronts.Zdt3(pointsPerSegment: 2);
        Assert.Equal(0.1822287280, front[2][0], 12);
    }

    [Fact]
    public void Zdt6_floor_is_below_the_sampled_minimum()
    {
        var front = ParetoFronts.Zdt6(2);
        Assert.Equal(0.280775, front[0][0], 12);

        double min = double.PositiveInfinity;
        const int steps = 200_000;
        for (int i = 0; i <= steps; i++)
        {
            double x = i / (double)steps;
            double s = Math.Sin(6.0 * Math.PI * x);
            double f1 = 1.0 - Math.Exp(-4.0 * x) * Math.Pow(s * s, 3);
            if (f1 < min) min = f1;
        }

        double gap = min - front[0][0];
        Assert.InRange(gap, 1e-7, 1e-6);
    }

    [Fact]
    public void ParetoFronts_Zdt1_on_curve()
    {
        foreach (var p in ParetoFronts.Zdt1(20))
            Assert.InRange(p[1], 1.0 - Math.Sqrt(p[0]) - 1e-9, 1.0 - Math.Sqrt(p[0]) + 1e-9);
    }

    [Fact]
    public void OnePerDirection_keeps_the_closer_point_on_a_shared_ray()
    {
        var directions = new[] { new[] { 1.0, 0.0 }, new[] { 0.0, 1.0 } };
        var onAxis = new[] { 1.0, 0.0 };
        var nearby = new[] { 0.9, 0.1 };
        var other = new[] { 0.0, 1.0 };
        var kept = ReferenceDirectionThinning.OnePerDirection(
            new[] { nearby, onAxis, other },
            directions);

        Assert.Equal(2, kept.Length);
        Assert.Contains(kept, p => p[0] == 1.0 && p[1] == 0.0);
        Assert.Contains(kept, p => p[0] == 0.0 && p[1] == 1.0);
    }

    [Fact]
    public void OnePerDirection_rejects_a_short_objective_vector()
    {
        var directions = new[] { new[] { 1.0, 0.0 } };
        Assert.Throws<ArgumentException>(() =>
            ReferenceDirectionThinning.OnePerDirection(new[] { new[] { 1.0 } }, directions));
    }

    [Fact]
    public void Dtlz2_front_on_unit_sphere()
    {
        foreach (var p in ParetoFronts.Dtlz2(3, partitions: 4))
        {
            double n2 = p.Sum(v => v * v);
            Assert.InRange(n2, 1.0 - 1e-9, 1.0 + 1e-9);
        }
    }
}
