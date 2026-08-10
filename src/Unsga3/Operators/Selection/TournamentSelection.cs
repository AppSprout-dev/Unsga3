using Unsga3.Algorithm;
using Unsga3.Core;
using Unsga3.Utilities;

namespace Unsga3.Operators.Selection;

/// <summary>
/// U-NSGA-III niching-based binary tournament (Seada &amp; Deb / pymoo variants).
/// </summary>
public sealed class TournamentSelection
{
    public TournamentSelection(TournamentMode mode = TournamentMode.RankNicheDistance)
    {
        Mode = mode;
    }

    public TournamentMode Mode { get; }

    /// <summary>
    /// Select <paramref name="count"/> parents (with replacement tournaments) from the population.
    /// Population must already have Rank / niche association set via <see cref="PrepareForSelection"/>.
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
        return Winner(population[a], population[b], rng, Mode);
    }

    internal static Individual Winner(
        Individual a,
        Individual b,
        RandomProvider rng,
        TournamentMode mode = TournamentMode.RankNicheDistance)
    {
        return mode switch
        {
            TournamentMode.PymooCompatible => WinnerPymoo(a, b, rng),
            _ => WinnerRankNicheDistance(a, b, rng),
        };
    }

    /// <summary>Default: rank → niche count → perpendicular distance.</summary>
    internal static Individual WinnerRankNicheDistance(Individual a, Individual b, RandomProvider rng)
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

        if (a.Rank < b.Rank) return a;
        if (b.Rank < a.Rank) return b;

        if (a.NicheCount < b.NicheCount) return a;
        if (b.NicheCount < a.NicheCount) return b;

        if (a.PerpendicularDistance < b.PerpendicularDistance) return a;
        if (b.PerpendicularDistance < a.PerpendicularDistance) return b;

        return rng.NextDouble() < 0.5 ? a : b;
    }

    /// <summary>
    /// pymoo <c>comp_by_rank_and_ref_line_dist</c>:
    /// CV → if same associated reference (niche) then rank → dist-to-niche; else random.
    /// </summary>
    internal static Individual WinnerPymoo(Individual a, Individual b, RandomProvider rng)
    {
        bool aInfeas = !a.IsFeasible;
        bool bInfeas = !b.IsFeasible;
        if (aInfeas || bInfeas)
        {
            if (a.ConstraintViolation < b.ConstraintViolation) return a;
            if (b.ConstraintViolation < a.ConstraintViolation) return b;
            return rng.NextDouble() < 0.5 ? a : b;
        }

        // Same niche (associated reference index) → rank, else dist_to_niche.
        if (a.AssociatedReference == b.AssociatedReference && a.AssociatedReference >= 0)
        {
            if (a.Rank != b.Rank)
                return a.Rank < b.Rank ? a : b;
            if (a.PerpendicularDistance < b.PerpendicularDistance) return a;
            if (b.PerpendicularDistance < a.PerpendicularDistance) return b;
        }

        // Different niches (or no association) → random (pymoo).
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

        var fronts = NonDominatedSort.Sort(population);
        IReadOnlyList<int>? nd = fronts.Count > 0 ? fronts[0] : null;
        var normalized = normalization.Normalize(population, nd);
        references.ResetNicheCounts();
        references.Associate(population, normalized);
    }
}
