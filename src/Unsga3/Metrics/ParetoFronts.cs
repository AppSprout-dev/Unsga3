using Unsga3.Utilities;

namespace Unsga3.Metrics;

/// <summary>
/// Analytic / sampled true Pareto fronts for standard benchmarks (for IGD/GD).
/// Sampling density follows common MOEA practice (hundreds of points on continuous fronts).
/// </summary>
public static class ParetoFronts
{
    /// <summary>ZDT1: f2 = 1 - sqrt(f1), f1 ∈ [0,1].</summary>
    public static double[][] Zdt1(int nPoints = 500)
    {
        var pf = new double[nPoints][];
        for (int i = 0; i < nPoints; i++)
        {
            double f1 = i / (double)(nPoints - 1);
            pf[i] = new[] { f1, 1.0 - Math.Sqrt(f1) };
        }
        return pf;
    }

    /// <summary>ZDT2: f2 = 1 - f1².</summary>
    public static double[][] Zdt2(int nPoints = 500)
    {
        var pf = new double[nPoints][];
        for (int i = 0; i < nPoints; i++)
        {
            double f1 = i / (double)(nPoints - 1);
            pf[i] = new[] { f1, 1.0 - f1 * f1 };
        }
        return pf;
    }

    /// <summary>
    /// ZDT3 disconnected segments (standard discrete sampling of the known intervals).
    /// </summary>
    public static double[][] Zdt3(int pointsPerSegment = 100)
    {
        // Known f1 intervals for ZDT3 Pareto set (Deb).
        double[][] intervals =
        {
            new[] { 0.0, 0.0830015349 },
            new[] { 0.1822287280, 0.2577623634 },
            new[] { 0.4093136748, 0.4538821041 },
            new[] { 0.6183967944, 0.6525117038 },
            new[] { 0.8233317983, 0.8518328654 },
        };
        var list = new List<double[]>();
        foreach (var iv in intervals)
        {
            for (int i = 0; i < pointsPerSegment; i++)
            {
                double t = i / (double)(pointsPerSegment - 1);
                double f1 = iv[0] + t * (iv[1] - iv[0]);
                double f2 = 1.0 - Math.Sqrt(f1) - f1 * Math.Sin(10.0 * Math.PI * f1);
                list.Add(new[] { f1, f2 });
            }
        }
        return list.ToArray();
    }

    /// <summary>ZDT4 same geometry as ZDT1.</summary>
    public static double[][] Zdt4(int nPoints = 500) => Zdt1(nPoints);

    /// <summary>ZDT6: f1 from ~0.280775 to 1, f2 = 1 - f1².</summary>
    public static double[][] Zdt6(int nPoints = 500)
    {
        // f1* = 1 - exp(-4x) sin^6(6πx) for x in [0,1]; min ≈ 0.280775
        double f1Min = 0.280775;
        var pf = new double[nPoints][];
        for (int i = 0; i < nPoints; i++)
        {
            double f1 = f1Min + (1.0 - f1Min) * i / (nPoints - 1);
            pf[i] = new[] { f1, 1.0 - f1 * f1 };
        }
        return pf;
    }

    /// <summary>
    /// DTLZ1 true front: linear simplex Σ f_i = 0.5, f_i ≥ 0.
    /// Sampled via Das–Dennis on the simplex scaled by 0.5.
    /// </summary>
    public static double[][] Dtlz1(int nObjectives, int partitions = 12)
    {
        var dirs = ReferenceDirections.DasDennis(nObjectives, partitions);
        var pf = new double[dirs.Length][];
        for (int i = 0; i < dirs.Length; i++)
        {
            pf[i] = new double[nObjectives];
            for (int j = 0; j < nObjectives; j++)
                pf[i][j] = 0.5 * dirs[i][j];
        }
        return pf;
    }

    /// <summary>
    /// DTLZ2/3/4 true front: unit sphere first orthant Σ f_i² = 1, f_i ≥ 0.
    /// </summary>
    public static double[][] Dtlz2(int nObjectives, int partitions = 12)
    {
        var dirs = ReferenceDirections.DasDennis(nObjectives, partitions);
        var pf = new double[dirs.Length][];
        for (int i = 0; i < dirs.Length; i++)
        {
            // Normalize direction to unit L2.
            double norm = 0;
            for (int j = 0; j < nObjectives; j++)
                norm += dirs[i][j] * dirs[i][j];
            norm = Math.Sqrt(norm);
            pf[i] = new double[nObjectives];
            for (int j = 0; j < nObjectives; j++)
                pf[i][j] = dirs[i][j] / norm;
        }
        return pf;
    }
}
