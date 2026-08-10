using Unsga3.Algorithm;
using Unsga3.Core;
using Unsga3.Utilities;

namespace Unsga3.Operators.Selection;

/// <summary>
/// U-NSGA-III niching-based binary tournament (Seada &amp; Deb):
/// prefer better non-domination rank, then lower niche count (associated reference).
/// Feasible solutions beat infeasible; among infeasible, lower CV wins.
/// </summary>
public sealed class TournamentSelection
{
    /// <summary>
    /// Select <paramref name="count"/> parents (with replacement tournaments) from the population.
    /// Population must already have Rank and NicheCount set.
    /// </summary>
    public List<Individual> SelectParents(
        IReadOnlyList<Individual> population,
        int count,
        RandomProvider rng)
    {
        ArgumentNullException.ThrowIfNull(population);
        ArgumentNullException.ThrowIfNull(rng);
        if (population.Count == 0)
            throw new ArgumentException("Population is empty.", nameof(population));
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));

        var parents = new List<Individual>(count);
        for (int i = 0; i < count; i++)
            parents.Add(Tournament(population, rng).Clone());
        return parents;
    }

    public Individual Tournament(IReadOnlyList<Individual> population, RandomProvider rng)
    {
        int n = population.Count;
        int a = rng.Next(n);
        int b = rng.NextExcept(n, a);
        return Winner(population[a], population[b], rng);
    }

    internal static Individual Winner(Individual a, Individual b, RandomProvider rng)
    {
        // Constraint first.
        bool aFeas = a.IsFeasible;
        bool bFeas = b.IsFeasible;
        if (aFeas && !bFeas) return a;
        if (!aFeas && bFeas) return b;
        if (!aFeas && !bFeas)
        {
            if (a.ConstraintViolation < b.ConstraintViolation) return a;
            if (b.ConstraintViolation < a.ConstraintViolation) return b;
        }

        // Rank (lower better).
        if (a.Rank < b.Rank) return a;
        if (b.Rank < a.Rank) return b;

        // Niche count (lower better — less crowded reference direction).
        if (a.NicheCount < b.NicheCount) return a;
        if (b.NicheCount < a.NicheCount) return b;

        // Perpendicular distance to reference as tie-break (closer better).
        if (a.PerpendicularDistance < b.PerpendicularDistance) return a;
        if (b.PerpendicularDistance < a.PerpendicularDistance) return b;

        return rng.NextDouble() < 0.5 ? a : b;
    }

    /// <summary>Recompute ranks + niche counts for tournament (normalize + associate).</summary>
    public static void PrepareForSelection(
        IReadOnlyList<Individual> population,
        ReferencePointManager references,
        Normalization normalization)
    {
        ArgumentNullException.ThrowIfNull(population);
        ArgumentNullException.ThrowIfNull(references);
        ArgumentNullException.ThrowIfNull(normalization);

        NonDominatedSort.Sort(population);
        var normalized = normalization.Normalize(population);
        references.ResetNicheCounts();
        references.Associate(population, normalized);
    }
}
