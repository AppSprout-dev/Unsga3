namespace Unsga3.Metrics;

/// <summary>
/// Industry-standard MOEA performance indicators aligned with pymoo / Coello / Ishibuchi:
/// GD, IGD (p=2 Euclidean), IGD+, and 2-D hypervolume.
/// Formulas match pymoo docs (https://pymoo.org/misc/indicators.html).
/// </summary>
public static class PerformanceIndicators
{
    /// <summary>
    /// Generational Distance: average distance from each point in A to nearest PF reference in Z.
    /// GD(A) = (1/|A|) * (Σ d_i^p)^{1/p}, p=2.
    /// </summary>
    public static double GenerationalDistance(IReadOnlyList<double[]> obtained, IReadOnlyList<double[]> referenceFront)
    {
        ValidateFronts(obtained, referenceFront);
        if (obtained.Count == 0) return double.PositiveInfinity;

        double sumSq = 0;
        for (int i = 0; i < obtained.Count; i++)
        {
            double d = NearestDistance(obtained[i], referenceFront);
            sumSq += d * d;
        }
        return Math.Sqrt(sumSq) / obtained.Count;
    }

    /// <summary>
    /// Inverted Generational Distance: average distance from each PF point in Z to nearest in A.
    /// Primary metric for many-objective papers (lower is better).
    /// IGD(A) = (1/|Z|) * (Σ d̂_i^p)^{1/p}, p=2.
    /// </summary>
    public static double InvertedGenerationalDistance(
        IReadOnlyList<double[]> obtained,
        IReadOnlyList<double[]> referenceFront)
    {
        ValidateFronts(obtained, referenceFront);
        if (referenceFront.Count == 0) return double.PositiveInfinity;
        if (obtained.Count == 0) return double.PositiveInfinity;

        double sumSq = 0;
        for (int i = 0; i < referenceFront.Count; i++)
        {
            double d = NearestDistance(referenceFront[i], obtained);
            sumSq += d * d;
        }
        return Math.Sqrt(sumSq) / referenceFront.Count;
    }

    /// <summary>
    /// IGD+ (Ishibuchi et al.): weakly Pareto-compliant; uses max(a_j - z_j, 0) style distance
    /// from each reference z to nearest obtained a (minimization).
    /// </summary>
    public static double InvertedGenerationalDistancePlus(
        IReadOnlyList<double[]> obtained,
        IReadOnlyList<double[]> referenceFront)
    {
        ValidateFronts(obtained, referenceFront);
        if (referenceFront.Count == 0 || obtained.Count == 0)
            return double.PositiveInfinity;

        double sumSq = 0;
        for (int i = 0; i < referenceFront.Count; i++)
        {
            double best = double.PositiveInfinity;
            var z = referenceFront[i];
            for (int j = 0; j < obtained.Count; j++)
            {
                double d = ModifiedDistance(obtained[j], z);
                if (d < best) best = d;
            }
            sumSq += best * best;
        }
        return Math.Sqrt(sumSq) / referenceFront.Count;
    }

    /// <summary>
    /// 2-objective hypervolume for a minimization front vs reference point r (Fonseca et al. sweep).
    /// Higher is better. Front need not be pre-sorted; dominated points are ignored after sort.
    /// </summary>
    public static double Hypervolume2D(IReadOnlyList<double[]> front, double[] referencePoint)
    {
        ArgumentNullException.ThrowIfNull(front);
        ArgumentNullException.ThrowIfNull(referencePoint);
        if (referencePoint.Length != 2)
            throw new ArgumentException("Hypervolume2D requires a 2-D reference point.");

        // Filter points that dominate nothing beyond ref and sort by f1 ascending.
        var pts = front
            .Where(p => p.Length >= 2 && p[0] < referencePoint[0] && p[1] < referencePoint[1])
            .OrderBy(p => p[0])
            .ThenBy(p => p[1])
            .ToList();

        if (pts.Count == 0) return 0;

        // Keep non-dominated only (minimization).
        var nd = new List<double[]>();
        double bestF2 = double.PositiveInfinity;
        foreach (var p in pts)
        {
            if (p[1] < bestF2)
            {
                nd.Add(p);
                bestF2 = p[1];
            }
        }

        double hv = 0;
        double prevF1 = referencePoint[0];
        // Walk from right (large f1) to left using reverse.
        for (int i = nd.Count - 1; i >= 0; i--)
        {
            double width = prevF1 - nd[i][0];
            double height = referencePoint[1] - nd[i][1];
            if (width > 0 && height > 0)
                hv += width * height;
            prevF1 = nd[i][0];
        }
        return hv;
    }

    private static double ModifiedDistance(double[] a, double[] z)
    {
        // d+ from z toward a for minimization: Euclidean of max(a_j - z_j, 0)
        double s = 0;
        for (int k = 0; k < z.Length; k++)
        {
            double d = a[k] - z[k];
            if (d < 0) d = 0;
            s += d * d;
        }
        return Math.Sqrt(s);
    }

    private static double NearestDistance(double[] point, IReadOnlyList<double[]> set)
    {
        double best = double.PositiveInfinity;
        for (int i = 0; i < set.Count; i++)
        {
            double d = Euclidean(point, set[i]);
            if (d < best) best = d;
        }
        return best;
    }

    private static double Euclidean(double[] a, double[] b)
    {
        double s = 0;
        int n = Math.Min(a.Length, b.Length);
        for (int i = 0; i < n; i++)
        {
            double d = a[i] - b[i];
            s += d * d;
        }
        return Math.Sqrt(s);
    }

    private static void ValidateFronts(IReadOnlyList<double[]> a, IReadOnlyList<double[]> z)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(z);
    }
}
