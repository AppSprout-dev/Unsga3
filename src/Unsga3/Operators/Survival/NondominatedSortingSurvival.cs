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
        {
            _references.ResetNicheCounts();
            _references.Associate(population, normalized);
            return population.Select(i => i.Clone()).ToList();
        }

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
        clone.NicheCount = 0;
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

        _references.ResetNicheCounts();
        _references.Associate(combined, normalized, indicesToCount: selectedIdx);

        if (selectedIdx.Count == targetSize || fi >= fronts.Count)
            return CloneWithUpdatedNicheCounts(combined, selectedIdx);

        int remaining = targetSize - selectedIdx.Count;
        var front = fronts[fi];
        var buckets = new List<int>?[_references.Count];
        List<int>? unassociated = null;

        for (int idx = 0; idx < front.Count; idx++)
        {
            int candidate = front[idx];
            int r = combined[candidate].AssociatedReference;
            if ((uint)r < (uint)buckets.Length)
            {
                (buckets[r] ??= new List<int>()).Add(candidate);
            }
            else
            {
                (unassociated ??= new List<int>()).Add(candidate);
            }
        }

        // Ascending reference-index order keeps minRefs deterministic across runtimes
        // without HashSet enumeration-order dependence or per-pick sorting.
        var activeRefs = new List<int>(_references.Count);
        for (int r = 0; r < buckets.Length; r++)
        {
            if (buckets[r] is { Count: > 0 })
                activeRefs.Add(r);
        }

        var minRefs = new List<int>(activeRefs.Count);

        while (remaining > 0 && activeRefs.Count > 0)
        {
            int minNiche = int.MaxValue;
            for (int a = 0; a < activeRefs.Count; a++)
            {
                int nc = _references.GetNicheCount(activeRefs[a]);
                if (nc < minNiche) minNiche = nc;
            }

            minRefs.Clear();
            for (int a = 0; a < activeRefs.Count; a++)
            {
                int r = activeRefs[a];
                if (_references.GetNicheCount(r) == minNiche)
                    minRefs.Add(r);
            }

            // Random among min-niche refs when rng present (pymoo); else smallest ref index.
            int chosenRef = rng is not null && minRefs.Count > 1
                ? minRefs[rng.Next(minRefs.Count)]
                : minRefs[0];

            var inNiche = buckets[chosenRef]!;
            int pickSlot;
            if (minNiche == 0)
            {
                pickSlot = 0;
                double best = combined[inNiche[0]].PerpendicularDistance;
                for (int k = 1; k < inNiche.Count; k++)
                {
                    double d = combined[inNiche[k]].PerpendicularDistance;
                    if (d < best)
                    {
                        best = d;
                        pickSlot = k;
                    }
                }
            }
            else if (rng is not null)
            {
                pickSlot = rng.Next(inNiche.Count);
            }
            else
            {
                pickSlot = 0;
                int bestIndex = inNiche[0];
                for (int k = 1; k < inNiche.Count; k++)
                {
                    if (inNiche[k] < bestIndex)
                    {
                        bestIndex = inNiche[k];
                        pickSlot = k;
                    }
                }
            }

            int pick = inNiche[pickSlot];
            inNiche.RemoveAt(pickSlot);
            if (inNiche.Count == 0)
                activeRefs.Remove(chosenRef);

            selectedIdx.Add(pick);
            _references.IncrementNiche(chosenRef);
            remaining--;
        }

        if (remaining > 0 && unassociated is not null)
        {
            int take = Math.Min(remaining, unassociated.Count);
            for (int k = 0; k < take; k++)
                selectedIdx.Add(unassociated[k]);
        }

        return CloneWithUpdatedNicheCounts(combined, selectedIdx);
    }

    private List<Individual> CloneWithUpdatedNicheCounts(IReadOnlyList<Individual> combined, List<int> selectedIdx)
    {
        var result = new List<Individual>(selectedIdx.Count);
        for (int i = 0; i < selectedIdx.Count; i++)
        {
            var clone = combined[selectedIdx[i]].Clone();
            int r = clone.AssociatedReference;
            clone.NicheCount = r >= 0 ? _references.GetNicheCount(r) : 0;
            result.Add(clone);
        }
        return result;
    }
}
