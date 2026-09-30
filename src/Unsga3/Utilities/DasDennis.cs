namespace Unsga3.Utilities;

/// <summary>
/// Das–Dennis structured reference directions on the unit simplex (NSGA-III / U-NSGA-III).
/// <see cref="DasDennis"/> is a single layer and remains the default
/// (<c>WithDasDennis</c>, and the M≤3 oracles). <see cref="TwoLayerDasDennis"/> adds
/// Deb &amp; Jain Part I's inside layer for larger M. Survival accepts either array.
/// </summary>
public static class ReferenceDirections
{
    /// <summary>
    /// Generate Das–Dennis reference directions for <paramref name="numberOfObjectives"/> objectives
    /// with <paramref name="partitions"/> divisions along each axis.
    /// Count = C(partitions + M - 1, M - 1).
    /// </summary>
    public static double[][] DasDennis(int numberOfObjectives, int partitions)
    {
        if (numberOfObjectives < 1)
            throw new ArgumentOutOfRangeException(nameof(numberOfObjectives));
        if (partitions < 1)
            throw new ArgumentOutOfRangeException(nameof(partitions));

        if (numberOfObjectives == 1)
            return new[] { new[] { 1.0 } };

        var points = new List<double[]>();
        var current = new double[numberOfObjectives];
        Recurse(points, current, numberOfObjectives, partitions, partitions, 0);
        return points.ToArray();
    }

    /// <summary>
    /// Outer Das–Dennis layer plus an inside layer scaled toward the centroid
    /// (Deb &amp; Jain, NSGA-III Part I). The inside point is
    /// <c>w' = s·w + (1−s)/M</c>, which stays on the simplex.
    /// <paramref name="innerScaling"/> defaults to 0.5. Points that repeat an
    /// earlier direction (max-norm at most 1e-12), including the shared centroid,
    /// are dropped. Single-layer <see cref="DasDennis"/> stays the default;
    /// current M≤3 oracles do not need a second layer.
    /// </summary>
    /// <param name="numberOfObjectives">Simplex dimension M.</param>
    /// <param name="outerPartitions">Divisions on the boundary layer.</param>
    /// <param name="innerPartitions">Divisions on the inside layer.</param>
    /// <param name="innerScaling">Scale of the inside layer toward the centroid, in (0, 1].</param>
    public static double[][] TwoLayerDasDennis(
        int numberOfObjectives,
        int outerPartitions,
        int innerPartitions,
        double innerScaling = 0.5)
    {
        if (!(innerScaling > 0.0) || innerScaling > 1.0)
            throw new ArgumentOutOfRangeException(
                nameof(innerScaling), innerScaling, "Inner scaling must be in (0, 1].");

        var outer = DasDennis(numberOfObjectives, outerPartitions);
        var inner = DasDennis(numberOfObjectives, innerPartitions);
        var scaled = new double[inner.Length][];
        for (int i = 0; i < inner.Length; i++)
            scaled[i] = ScaleTowardCentroid(inner[i], innerScaling);

        var kept = new List<double[]>(outer.Length + scaled.Length);
        kept.AddRange(outer);
        for (int i = 0; i < scaled.Length; i++)
        {
            if (!IsDuplicate(scaled[i], kept))
                kept.Add(scaled[i]);
        }

        return kept.ToArray();
    }

    /// <summary><c>w' = s·w + (1−s)/M</c>. Sum is unchanged when <paramref name="w"/> is on the simplex.</summary>
    internal static double[] ScaleTowardCentroid(double[] w, double scaling)
    {
        int m = w.Length;
        double shift = (1.0 - scaling) / m;
        var scaled = new double[m];
        for (int j = 0; j < m; j++)
            scaled[j] = w[j] * scaling + shift;
        return scaled;
    }

    private static bool IsDuplicate(double[] point, List<double[]> kept)
    {
        for (int i = 0; i < kept.Count; i++)
        {
            var other = kept[i];
            bool near = true;
            for (int j = 0; j < point.Length; j++)
            {
                if (Math.Abs(point[j] - other[j]) > 1e-12)
                {
                    near = false;
                    break;
                }
            }

            if (near)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Choose partitions so the number of directions is at least <paramref name="minDirections"/>
    /// (useful when population size is chosen first).
    /// </summary>
    public static int PartitionsForMinimumDirections(int numberOfObjectives, int minDirections)
    {
        if (numberOfObjectives <= 1) return 1;
        int p = 1;
        while (Count(numberOfObjectives, p) < minDirections && p < 100)
            p++;
        return p;
    }

    /// <summary>
    /// Number of Das–Dennis points: C(p + M - 1, M - 1).
    /// The combination is computed in a <see cref="long"/> and checked into <see cref="int"/>.
    /// <see cref="OverflowException"/> is thrown when the value does not fit in <see cref="int"/>
    /// (for example M = 11, p = 34, C(44, 10) = 2,481,256,778).
    /// </summary>
    public static int Count(int numberOfObjectives, int partitions)
    {
        if (numberOfObjectives == 1) return 1;
        return checked((int)Binomial(partitions + numberOfObjectives - 1, numberOfObjectives - 1));
    }

    private static void Recurse(List<double[]> points, double[] current, int m, int p, int left, int index)
    {
        if (index == m - 1)
        {
            current[index] = left / (double)p;
            points.Add((double[])current.Clone());
            return;
        }

        for (int i = 0; i <= left; i++)
        {
            current[index] = i / (double)p;
            Recurse(points, current, m, p, left - i, index + 1);
        }
    }

    private static long Binomial(int n, int k)
    {
        if (k < 0 || k > n) return 0;
        if (k == 0 || k == n) return 1;
        k = Math.Min(k, n - k);
        long result = 1;
        for (int i = 1; i <= k; i++)
        {
            result = checked(result * (n - k + i));
            result /= i;
        }
        return result;
    }
}
