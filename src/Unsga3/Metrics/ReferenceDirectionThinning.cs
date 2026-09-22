using Unsga3.Algorithm;

namespace Unsga3.Metrics;

/// <summary>
/// Optional comparison aid. pymoo <c>UNSGA3</c> reports <c>res.F</c> as the survival
/// niche set (about one member per filled reference direction). This library scores
/// the full non-dominated front. <see cref="OnePerDirection"/> keeps the raw objective
/// vector with the smallest perpendicular distance to each direction so a caller can
/// score a similar cardinality.
/// </summary>
/// <remarks>
/// The result is not pymoo <c>res.F</c>. It does not repeat hyperplane normalization
/// or survival, and it is not evidence that the two algorithms match.
/// </remarks>
public static class ReferenceDirectionThinning
{
    /// <summary>
    /// Keep at most one point per reference direction: the member with the smallest
    /// perpendicular distance to that direction. Directions with no assigned point are omitted.
    /// </summary>
    public static double[][] OnePerDirection(
        IReadOnlyList<double[]> front,
        IReadOnlyList<double[]> directions)
    {
        ArgumentNullException.ThrowIfNull(front);
        ArgumentNullException.ThrowIfNull(directions);
        if (directions.Count == 0)
            throw new ArgumentException("Need at least one direction.", nameof(directions));

        int m = directions[0].Length;
        for (int r = 1; r < directions.Count; r++)
        {
            if (directions[r].Length != m)
                throw new ArgumentException("All directions must have the same length.", nameof(directions));
        }

        var bestDist = new double[directions.Count];
        var bestIdx = new int[directions.Count];
        Array.Fill(bestDist, double.PositiveInfinity);
        Array.Fill(bestIdx, -1);

        for (int i = 0; i < front.Count; i++)
        {
            var f = front[i];
            if (f.Length != m)
                throw new ArgumentException(
                    "Each front point must have the same length as the reference directions.",
                    nameof(front));

            int bestRef = 0;
            double best = double.PositiveInfinity;
            for (int r = 0; r < directions.Count; r++)
            {
                double d = ReferencePointManager.PerpendicularDistance(f, directions[r]);
                if (d < best)
                {
                    best = d;
                    bestRef = r;
                }
            }

            if (best < bestDist[bestRef])
            {
                bestDist[bestRef] = best;
                bestIdx[bestRef] = i;
            }
        }

        var kept = new List<double[]>();
        for (int r = 0; r < bestIdx.Length; r++)
        {
            if (bestIdx[r] >= 0)
                kept.Add(front[bestIdx[r]]);
        }
        return kept.ToArray();
    }
}
