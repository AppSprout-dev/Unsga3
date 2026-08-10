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
        // Single PF point (0,0); obtained (1,1) → IGD = √2 (pymoo p=2 form: (1/|Z|)·(Σ d²)^{1/2}).
        var pf = new[] { new[] { 0.0, 0.0 } };
        var obtained = new[] { new[] { 1.0, 1.0 } };
        double igd = PerformanceIndicators.InvertedGenerationalDistance(obtained, pf);
        Assert.InRange(igd, Math.Sqrt(2) - 1e-9, Math.Sqrt(2) + 1e-9);
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
    public void ParetoFronts_Zdt1_on_curve()
    {
        foreach (var p in ParetoFronts.Zdt1(20))
            Assert.InRange(p[1], 1.0 - Math.Sqrt(p[0]) - 1e-9, 1.0 - Math.Sqrt(p[0]) + 1e-9);
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
