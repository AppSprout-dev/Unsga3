using Unsga3.Algorithm;
using Unsga3.Core;
using Unsga3.Utilities;

namespace Unsga3.Operators.Survival;

/// <summary>
/// NSGA-III environmental selection: fill complete fronts, then niche-preserve the last front.
/// When any member is infeasible, niching follows pymoo <c>Survival.filter_infeasible</c>
/// (Jain &amp; Deb, NSGA-III Part II): niche the feasible subset, and fill a shortfall by
/// ascending constraint violation with no niche assignment. The hyperplane is updated
/// from feasible objectives whenever that subset is non-empty.
/// </summary>
public sealed class NondominatedSortingSurvival
{
    private readonly ReferencePointManager _references;
    private readonly Normalization _normalization;

    /// <param name="references">Reference-direction / niche manager.</param>
    /// <param name="normalization">
    /// Optional shared normalizer. Pass the algorithm-owned instance so ideal / extreme
    /// points persist across generations (pymoo). When null, a fresh normalizer is created.
    /// </param>
    public NondominatedSortingSurvival(ReferencePointManager references, Normalization? normalization = null)
    {
        _references = references ?? throw new ArgumentNullException(nameof(references));
        _normalization = normalization ?? new Normalization(references.NumberOfObjectives);
    }

    /// <summary>
    /// Select <paramref name="targetSize"/> individuals from the combined parent+offspring pool.
    /// When a niche already has members, a random candidate in that niche is chosen (pymoo-style)
    /// if <paramref name="rng"/> is provided; otherwise lowest index (deterministic).
    /// Normalization uses the first front only for extreme points (pymoo HyperplaneNormalization).
    /// An all-feasible pool follows that path unchanged. A mixed or infeasible pool niches
    /// only feasible members and fills any shortfall by ascending constraint violation
    /// (original index breaks CV ties). Those fillers are not associated with a niche.
    /// </summary>
    public List<Individual> Select(
        IReadOnlyList<Individual> combined,
        int targetSize,
        RandomProvider? rng = null)
    {
        ArgumentNullException.ThrowIfNull(combined);
        if (targetSize < 1) throw new ArgumentOutOfRangeException(nameof(targetSize));
        if (combined.Count <= targetSize)
            return combined.Select(i => i.Clone()).ToList();

        if (!AnyInfeasible(combined))
            return SelectByNiching(combined, targetSize, rng);

        var feasible = new List<Individual>();
        var infeasibleIdx = new List<int>();
        for (int i = 0; i < combined.Count; i++)
        {
            if (combined[i].IsFeasible)
                feasible.Add(combined[i]);
            else
                infeasibleIdx.Add(i);
        }

        // All infeasible: truncate by CV. Do not move the hyperplane.
        if (feasible.Count == 0)
            return TakeByAscendingCv(combined, infeasibleIdx, targetSize);

        int nFeasibleSurvive = Math.Min(feasible.Count, targetSize);
        var selected = SelectByNiching(feasible, nFeasibleSurvive, rng);

        int need = targetSize - selected.Count;
        if (need <= 0)
            return selected;

        SortByAscendingCv(combined, infeasibleIdx);

        int take = Math.Min(need, infeasibleIdx.Count);
        for (int k = 0; k < take; k++)
            selected.Add(CloneWithoutNiche(combined[infeasibleIdx[k]]));

        return selected;
    }

    private List<Individual> SelectByNiching(
        IReadOnlyList<Individual> population,
        int targetSize,
        RandomProvider? rng)
    {
        var fronts = NonDominatedSort.Sort(population);
        // Extreme points from the ND front only — matches pymoo ReferenceDirectionSurvival.
        IReadOnlyList<int>? nd = fronts.Count > 0 ? fronts[0] : null;
        var normalized = _normalization.Normalize(population, nd);
        if (population.Count <= targetSize)
            return population.Select(i => i.Clone()).ToList();

        return SelectWithIndices(population, fronts, targetSize, normalized, rng);
    }

    private static bool AnyInfeasible(IReadOnlyList<Individual> population)
    {
        for (int i = 0; i < population.Count; i++)
        {
            if (!population[i].IsFeasible)
                return true;
        }

        return false;
    }

    private static void SortByAscendingCv(IReadOnlyList<Individual> combined, List<int> indices)
    {
        indices.Sort((i, j) =>
        {
            int cmp = combined[i].ConstraintViolation.CompareTo(combined[j].ConstraintViolation);
            return cmp != 0 ? cmp : i.CompareTo(j);
        });
    }

    private static List<Individual> TakeByAscendingCv(
        IReadOnlyList<Individual> combined,
        List<int> infeasibleIdx,
        int targetSize)
    {
        SortByAscendingCv(combined, infeasibleIdx);
        int take = Math.Min(targetSize, infeasibleIdx.Count);
        var selected = new List<Individual>(take);
        for (int k = 0; k < take; k++)
            selected.Add(CloneWithoutNiche(combined[infeasibleIdx[k]]));

        return selected;
    }

    /// <summary>CV fill does not assign a niche (pymoo merges these after <c>_do</c>).</summary>
    private static Individual CloneWithoutNiche(Individual source)
    {
        var clone = source.Clone();
        clone.AssociatedReference = -1;
        clone.PerpendicularDistance = double.PositiveInfinity;
        return clone;
    }

    private List<Individual> SelectWithIndices(
        IReadOnlyList<Individual> combined,
        List<List<int>> fronts,
        int targetSize,
        double[][] normalized,
        RandomProvider? rng)
    {
        var selectedIdx = new List<int>(targetSize);
        int fi = 0;
        while (fi < fronts.Count && selectedIdx.Count + fronts[fi].Count <= targetSize)
        {
            selectedIdx.AddRange(fronts[fi]);
            fi++;
        }

        if (selectedIdx.Count == targetSize || fi >= fronts.Count)
            return selectedIdx.Select(i => combined[i].Clone()).ToList();

        int remaining = targetSize - selectedIdx.Count;
        var candidates = new List<int>(fronts[fi]);

        _references.Associate(combined, normalized, indicesToCount: null);
        _references.ResetNicheCounts();
        foreach (int i in selectedIdx)
            _references.IncrementNiche(combined[i].AssociatedReference);

        while (remaining > 0 && candidates.Count > 0)
        {
            int minNiche = int.MaxValue;
            var refsWithCandidates = new HashSet<int>();
            foreach (int i in candidates)
                refsWithCandidates.Add(combined[i].AssociatedReference);

            foreach (int r in refsWithCandidates)
            {
                int nc = _references.GetNicheCount(r);
                if (nc < minNiche) minNiche = nc;
            }

            var minRefs = new List<int>();
            foreach (int r in refsWithCandidates)
            {
                if (_references.GetNicheCount(r) == minNiche)
                    minRefs.Add(r);
            }

            // Random among min-niche refs when rng present (pymoo); else stable sort.
            int chosenRef;
            if (rng is not null && minRefs.Count > 1)
                chosenRef = minRefs[rng.Next(minRefs.Count)];
            else
            {
                minRefs.Sort();
                chosenRef = minRefs[0];
            }

            var inNiche = new List<int>();
            foreach (int i in candidates)
            {
                if (combined[i].AssociatedReference == chosenRef)
                    inNiche.Add(i);
            }

            int pick;
            if (_references.GetNicheCount(chosenRef) == 0)
            {
                pick = inNiche[0];
                double best = combined[pick].PerpendicularDistance;
                for (int k = 1; k < inNiche.Count; k++)
                {
                    double d = combined[inNiche[k]].PerpendicularDistance;
                    if (d < best)
                    {
                        best = d;
                        pick = inNiche[k];
                    }
                }
            }
            else if (rng is not null)
            {
                pick = inNiche[rng.Next(inNiche.Count)];
            }
            else
            {
                pick = inNiche[0];
                for (int k = 1; k < inNiche.Count; k++)
                {
                    if (inNiche[k] < pick)
                        pick = inNiche[k];
                }
            }

            selectedIdx.Add(pick);
            candidates.Remove(pick);
            _references.IncrementNiche(chosenRef);
            remaining--;
        }

        return selectedIdx.Select(i => combined[i].Clone()).ToList();
    }
}
