using Unsga3.Algorithm;
using Unsga3.Core;

namespace Unsga3.Operators.Survival;

/// <summary>
/// NSGA-III environmental selection: fill complete fronts, then niche-preserve the last front.
/// </summary>
public sealed class NondominatedSortingSurvival
{
    private readonly ReferencePointManager _references;
    private readonly Normalization _normalization;

    public NondominatedSortingSurvival(ReferencePointManager references)
    {
        _references = references ?? throw new ArgumentNullException(nameof(references));
        _normalization = new Normalization(references.NumberOfObjectives);
    }

    /// <summary>
    /// Select <paramref name="targetSize"/> individuals from the combined parent+offspring pool.
    /// </summary>
    public List<Individual> Select(IReadOnlyList<Individual> combined, int targetSize)
    {
        ArgumentNullException.ThrowIfNull(combined);
        if (targetSize < 1) throw new ArgumentOutOfRangeException(nameof(targetSize));
        if (combined.Count <= targetSize)
            return combined.Select(i => i.Clone()).ToList();

        var fronts = NonDominatedSort.Sort(combined);
        var next = new List<Individual>(targetSize);
        int fi = 0;

        while (fi < fronts.Count && next.Count + fronts[fi].Count <= targetSize)
        {
            foreach (int idx in fronts[fi])
                next.Add(combined[idx].Clone());
            fi++;
        }

        if (next.Count == targetSize || fi >= fronts.Count)
            return next;

        // Last front — niche-based selection.
        var lastFrontIdx = fronts[fi];
        int remaining = targetSize - next.Count;

        // Normalize whole combined population for consistent association.
        var normalized = _normalization.Normalize(combined);

        // Niche counts from already accepted individuals.
        _references.ResetNicheCounts();
        for (int i = 0; i < next.Count; i++)
        {
            // Find original index of cloned individual is awkward; re-associate accepted + last front.
        }

        // Re-associate everyone; count niches only for members already in next (by matching variables is fragile).
        // Better approach: work with indices only until the end.
        return SelectWithIndices(combined, fronts, fi, targetSize, normalized);
    }

    private List<Individual> SelectWithIndices(
        IReadOnlyList<Individual> combined,
        List<List<int>> fronts,
        int lastFront,
        int targetSize,
        double[][] normalized)
    {
        var selectedIdx = new List<int>(targetSize);
        for (int f = 0; f < lastFront; f++)
            selectedIdx.AddRange(fronts[f]);

        int remaining = targetSize - selectedIdx.Count;
        var candidates = new List<int>(fronts[lastFront]);

        // Associate all combined individuals.
        _references.Associate(combined, normalized, indicesToCount: null);
        // Niche counts currently include everyone — reset and count only selected so far.
        _references.ResetNicheCounts();
        foreach (int i in selectedIdx)
            _references.IncrementNiche(combined[i].AssociatedReference);

        // For last-front candidates, we pick by minimum niche of their associated ref,
        // then minimum perpendicular distance (NSGA-III).
        while (remaining > 0 && candidates.Count > 0)
        {
            // Find reference points with minimum niche among those that have candidates.
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

            // Pick a random min-niche reference among candidates' refs (deterministic: smallest id for reproducibility).
            minRefs.Sort();
            int chosenRef = minRefs[0];

            // Candidates associated with chosenRef.
            var inNiche = new List<int>();
            foreach (int i in candidates)
            {
                if (combined[i].AssociatedReference == chosenRef)
                    inNiche.Add(i);
            }

            int pick;
            if (_references.GetNicheCount(chosenRef) == 0)
            {
                // Closest to the reference direction.
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
            else
            {
                // Random among niche — use lowest index for determinism with fixed seed pipelines.
                // (A pure random pick needs rng; pass later. For now stable min-index.)
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
