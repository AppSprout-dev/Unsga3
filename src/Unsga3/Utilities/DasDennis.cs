namespace Unsga3.Utilities;

/// <summary>
/// Das–Dennis structured reference directions on the unit simplex (NSGA-III / U-NSGA-III).
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

    /// <summary>Number of Das–Dennis points: C(p + M - 1, M - 1).</summary>
    public static int Count(int numberOfObjectives, int partitions)
    {
        if (numberOfObjectives == 1) return 1;
        return Binomial(partitions + numberOfObjectives - 1, numberOfObjectives - 1);
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

    private static int Binomial(int n, int k)
    {
        if (k < 0 || k > n) return 0;
        if (k == 0 || k == n) return 1;
        k = Math.Min(k, n - k);
        long result = 1;
        for (int i = 1; i <= k; i++)
        {
            result *= n - k + i;
            result /= i;
        }
        return (int)result;
    }
}
