namespace Unsga3.Algorithm;

/// <summary>
/// Associates normalized individuals to Das–Dennis (or custom) reference directions
/// and tracks niche counts for U-NSGA-III tournament and NSGA-III survival.
/// </summary>
public sealed class ReferencePointManager
{
    private readonly double[][] _directions;
    private readonly IReadOnlyList<double[]> _directionsView;
    private readonly double[] _normSq;
    private readonly int[] _nicheCount;

    public ReferencePointManager(double[][] referenceDirections)
    {
        ArgumentNullException.ThrowIfNull(referenceDirections);
        if (referenceDirections.Length < 1)
            throw new ArgumentException("Need at least one reference direction.", nameof(referenceDirections));
        if (referenceDirections[0] is null || referenceDirections[0].Length < 1)
            throw new ArgumentException("Reference directions must be non-empty.", nameof(referenceDirections));

        NumberOfObjectives = referenceDirections[0].Length;
        _directions = new double[referenceDirections.Length][];
        _normSq = new double[referenceDirections.Length];
        _nicheCount = new int[referenceDirections.Length];

        for (int i = 0; i < referenceDirections.Length; i++)
        {
            var row = referenceDirections[i];
            if (row is null || row.Length != NumberOfObjectives)
                throw new ArgumentException("All reference directions must be non-null and have the same dimension.", nameof(referenceDirections));

            var copy = (double[])row.Clone();
            _directions[i] = copy;
            double ww = 0;
            for (int j = 0; j < NumberOfObjectives; j++)
                ww += copy[j] * copy[j];
            _normSq[i] = ww;
        }

        _directionsView = Array.AsReadOnly(_directions);
    }

    public int Count => _directions.Length;
    public int NumberOfObjectives { get; }
    public IReadOnlyList<double[]> Directions => _directionsView;

    public void ResetNicheCounts()
    {
        Array.Clear(_nicheCount, 0, _nicheCount.Length);
    }

    public int GetNicheCount(int referenceIndex) => _nicheCount[referenceIndex];

    public void IncrementNiche(int referenceIndex)
    {
        if ((uint)referenceIndex < (uint)_nicheCount.Length)
            _nicheCount[referenceIndex]++;
    }

    /// <summary>
    /// Associate each individual to the nearest reference direction (perpendicular distance)
    /// and optionally increment niche counts for the supplied index set.
    /// </summary>
    public void Associate(
        IReadOnlyList<Individual> population,
        double[][] normalizedObjectives,
        IReadOnlyList<int>? indicesToCount = null)
    {
        ArgumentNullException.ThrowIfNull(population);
        ArgumentNullException.ThrowIfNull(normalizedObjectives);
        if (population.Count != normalizedObjectives.Length)
            throw new ArgumentException("Population and normalized objective counts must match.");

        for (int i = 0; i < population.Count; i++)
        {
            var f = normalizedObjectives[i];
            ArgumentNullException.ThrowIfNull(f);
            if (f.Length != NumberOfObjectives)
                throw new ArgumentException("Normalized objective vector dimension does not match reference directions.");

            int bestRef = -1;
            double bestDistSq = double.PositiveInfinity;
            for (int r = 0; r < _directions.Length; r++)
            {
                double dSq = PerpendicularDistanceSquared(f, _directions[r], _normSq[r]);
                if (dSq < bestDistSq)
                {
                    bestDistSq = dSq;
                    bestRef = r;
                }
            }
            population[i].AssociatedReference = bestRef;
            population[i].PerpendicularDistance = bestRef >= 0 ? Math.Sqrt(bestDistSq) : double.PositiveInfinity;
        }

        if (indicesToCount is not null)
        {
            foreach (int i in indicesToCount)
            {
                int r = population[i].AssociatedReference;
                if (r >= 0)
                    _nicheCount[r]++;
            }
        }
        else
        {
            ResetNicheCounts();
            for (int i = 0; i < population.Count; i++)
            {
                int r = population[i].AssociatedReference;
                if (r >= 0)
                    _nicheCount[r]++;
            }
        }

        for (int i = 0; i < population.Count; i++)
        {
            int r = population[i].AssociatedReference;
            population[i].NicheCount = r >= 0 ? _nicheCount[r] : 0;
        }
    }

    /// <summary>
    /// Perpendicular distance from point f to the ray through reference direction w (NSGA-III).
    /// </summary>
    public static double PerpendicularDistance(double[] f, double[] w)
    {
        ArgumentNullException.ThrowIfNull(f);
        ArgumentNullException.ThrowIfNull(w);
        if (f.Length != w.Length)
            throw new ArgumentException("Point and reference direction must have the same length.");

        double ww = 0;
        for (int i = 0; i < w.Length; i++)
            ww += w[i] * w[i];

        return Math.Sqrt(PerpendicularDistanceSquared(f, w, ww));
    }

    private static double PerpendicularDistanceSquared(double[] f, double[] w, double ww)
    {
        if (ww < 1e-16)
        {
            double norm = 0;
            for (int i = 0; i < f.Length; i++)
                norm += f[i] * f[i];
            return norm;
        }

        double fw = 0;
        for (int i = 0; i < f.Length; i++)
            fw += f[i] * w[i];

        double t = fw / ww;
        double distSq = 0;
        for (int i = 0; i < f.Length; i++)
        {
            double d = f[i] - t * w[i];
            distSq += d * d;
        }
        return distSq;
    }
}
