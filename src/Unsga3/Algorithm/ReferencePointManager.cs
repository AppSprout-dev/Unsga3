namespace Unsga3.Algorithm;

/// <summary>
/// Associates normalized individuals to Das–Dennis (or custom) reference directions
/// and tracks niche counts for U-NSGA-III tournament and NSGA-III survival.
/// </summary>
public sealed class ReferencePointManager
{
    private readonly double[][] _directions;
    private readonly int[] _nicheCount;

    public ReferencePointManager(double[][] referenceDirections)
    {
        ArgumentNullException.ThrowIfNull(referenceDirections);
        if (referenceDirections.Length < 1)
            throw new ArgumentException("Need at least one reference direction.", nameof(referenceDirections));

        _directions = referenceDirections;
        _nicheCount = new int[referenceDirections.Length];
        NumberOfObjectives = referenceDirections[0].Length;
        for (int i = 1; i < referenceDirections.Length; i++)
        {
            if (referenceDirections[i].Length != NumberOfObjectives)
                throw new ArgumentException("All reference directions must have the same dimension.");
        }
    }

    public int Count => _directions.Length;
    public int NumberOfObjectives { get; }
    public IReadOnlyList<double[]> Directions => _directions;

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
            int bestRef = 0;
            double bestDist = double.PositiveInfinity;
            for (int r = 0; r < _directions.Length; r++)
            {
                double d = PerpendicularDistance(normalizedObjectives[i], _directions[r]);
                if (d < bestDist)
                {
                    bestDist = d;
                    bestRef = r;
                }
            }
            population[i].AssociatedReference = bestRef;
            population[i].PerpendicularDistance = bestDist;
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
        double ww = 0, fw = 0;
        for (int i = 0; i < f.Length; i++)
        {
            ww += w[i] * w[i];
            fw += f[i] * w[i];
        }
        if (ww < 1e-16)
        {
            double norm = 0;
            for (int i = 0; i < f.Length; i++)
                norm += f[i] * f[i];
            return Math.Sqrt(norm);
        }

        double t = fw / ww;
        double distSq = 0;
        for (int i = 0; i < f.Length; i++)
        {
            double d = f[i] - t * w[i];
            distSq += d * d;
        }
        return Math.Sqrt(distSq);
    }
}
