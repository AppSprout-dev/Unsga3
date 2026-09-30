using Unsga3.Algorithm;
using Unsga3.Core;
using Unsga3.Utilities;

namespace Unsga3.Operators.Selection;

/// <summary>
/// Binary mating tournament. <see cref="TournamentMode.PymooCompatible"/> follows the
/// Seada &amp; Deb Algorithm 2 split (same niche: rank then distance; different niches: random)
/// and pymoo's coin flip on equal distance. <see cref="TournamentMode.Algorithm2"/> keeps the
/// second parent on that distance tie. <see cref="TournamentMode.RankNicheDistance"/>
/// is the constructor default and also prefers the smaller niche count across niches.
/// The pool is independent of the comparator: the default is N draws with replacement,
/// and <see cref="MatingPoolMode.TwoShuffledPasses"/> is the paper / pymoo 0.6.2 pool.
/// </summary>
public sealed class TournamentSelection
{
    public TournamentSelection(
        TournamentMode mode = TournamentMode.RankNicheDistance,
        MatingPoolMode matingPool = MatingPoolMode.IndependentWithReplacement)
    {
        Mode = mode;
        MatingPool = matingPool;
    }

    public TournamentMode Mode { get; }

    /// <summary>How parents are drawn. Default is independent tournaments with replacement.</summary>
    public MatingPoolMode MatingPool { get; }

    /// <summary>
    /// Select <paramref name="count"/> parents.
    /// <see cref="MatingPoolMode.IndependentWithReplacement"/> draws each tournament
    /// independently (with replacement across tournaments).
    /// <see cref="MatingPoolMode.TwoShuffledPasses"/> shuffles the population twice
    /// and competes consecutive pairs. Population must already have Rank / niche
    /// association set via <see cref="PrepareForSelection"/>.
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

        switch (MatingPool)
        {
            case MatingPoolMode.IndependentWithReplacement:
            {
                var parents = new List<Individual>(count);
                for (int i = 0; i < count; i++)
                    parents.Add(Tournament(population, rng).Clone());
                return parents;
            }
            case MatingPoolMode.TwoShuffledPasses:
                return SelectTwoShuffledPasses(population, count, rng);
            default:
                return UnexpectedMatingPool(MatingPool);
        }
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
        switch (mode)
        {
            case TournamentMode.RankNicheDistance:
                return WinnerRankNicheDistance(a, b, rng);
            case TournamentMode.PymooCompatible:
                return WinnerPymoo(a, b, rng, keepSecondParentOnDistanceTie: false);
            case TournamentMode.Algorithm2:
                return WinnerPymoo(a, b, rng, keepSecondParentOnDistanceTie: true);
            default:
                return UnexpectedMode(mode);
        }
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
    /// When <paramref name="keepSecondParentOnDistanceTie"/> is set, a same-niche distance
    /// tie returns <paramref name="b"/> (Seada &amp; Deb Algorithm 2) and does not draw.
    /// </summary>
    internal static Individual WinnerPymoo(
        Individual a,
        Individual b,
        RandomProvider rng,
        bool keepSecondParentOnDistanceTie = false)
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
            if (keepSecondParentOnDistanceTie)
                return b;
        }

        // Different niches (or no association, or a pymoo distance tie) → random.
        return rng.NextDouble() < 0.5 ? a : b;
    }

    /// <summary>
    /// Contestant indices for <see cref="MatingPoolMode.TwoShuffledPasses"/>.
    /// Length is <c>2 * parentCount</c>, grouped as consecutive pairs
    /// <c>(0,1), (2,3), …</c>. When <paramref name="parentCount"/> equals
    /// <paramref name="populationSize"/>, this is exactly two permutations and
    /// each index appears twice.
    /// </summary>
    internal static int[] BuildTwoShuffleContestants(int populationSize, int parentCount, RandomProvider rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        if (populationSize < 2)
            throw new ArgumentOutOfRangeException(nameof(populationSize), "Need at least two members to form a tournament.");
        if (parentCount < 1)
            throw new ArgumentOutOfRangeException(nameof(parentCount));

        int slots = checked(parentCount * 2);
        int nPerms = (slots + populationSize - 1) / populationSize;
        var shuffled = new int[nPerms * populationSize];
        for (int pass = 0; pass < nPerms; pass++)
        {
            int start = pass * populationSize;
            for (int i = 0; i < populationSize; i++)
                shuffled[start + i] = i;
            ShuffleRange(shuffled, start, populationSize, rng);
        }

        var contests = new int[slots];
        Array.Copy(shuffled, contests, slots);
        // Even N: permutation boundaries fall between pairs, so every contest is
        // two distinct members of one pass. Odd N: one pair crosses a boundary
        // and can repeat an index; separate that pair without dropping a member.
        if ((populationSize & 1) == 1)
            SeparateOddBoundaryPairs(contests, populationSize);
        return contests;
    }

    private List<Individual> SelectTwoShuffledPasses(
        IReadOnlyList<Individual> population,
        int count,
        RandomProvider rng)
    {
        if (count == 0)
            return new List<Individual>();

        int[] contests = BuildTwoShuffleContestants(population.Count, count, rng);
        var parents = new List<Individual>(count);
        for (int i = 0; i < count; i++)
        {
            int a = contests[2 * i];
            int b = contests[2 * i + 1];
            parents.Add(Winner(population[a], population[b], rng, Mode).Clone());
        }
        return parents;
    }

    /// <summary>
    /// Recompute ranks and niche association for mating. This is a second normalization
    /// of the survivors. pymoo keeps the niche ids from environmental selection.
    /// </summary>
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

    private static void ShuffleRange(int[] values, int start, int length, RandomProvider rng)
    {
        for (int i = length - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (values[start + i], values[start + j]) = (values[start + j], values[start + i]);
        }
    }

    /// <summary>
    /// An odd population puts a permutation boundary inside one contest
    /// (<c>last of pass k</c>, <c>first of pass k+1</c>). Those two entries can be
    /// the same index. Swap with the next slot when that slot is still in the
    /// contest list: the pass is a permutation, so the next slot is a different
    /// member and the following pair stays two distinct members. If the boundary
    /// is the last slot (a short parent list), replace the repeated index.
    /// </summary>
    private static void SeparateOddBoundaryPairs(int[] contests, int populationSize)
    {
        // Step across passes. Only an odd boundary index sits inside a pair
        // (the right-hand contestant). An even boundary falls between pairs.
        for (int junction = populationSize; junction < contests.Length; junction += populationSize * 2)
        {
            int left = junction - 1;
            if (contests[left] != contests[junction])
                continue;

            if (junction + 1 < contests.Length)
            {
                // Both slots are inside the later pass, which is a permutation.
                (contests[junction], contests[junction + 1]) = (contests[junction + 1], contests[junction]);
                continue;
            }

            contests[junction] = (contests[left] + 1) % populationSize;
        }
    }

    private static List<Individual> UnexpectedMatingPool(MatingPoolMode matingPool) =>
        throw new ArgumentOutOfRangeException(nameof(matingPool), matingPool, "Unhandled mating pool.");

    private static Individual UnexpectedMode(TournamentMode mode) =>
        throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unhandled tournament mode.");
}
