using Unsga3.Algorithm;
using Unsga3.Core;
using Unsga3.Utilities;

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
    /// When a niche already has members, a random candidate in that niche is chosen (pymoo-style)
    /// if <paramref name="rng"/> is provided; otherwise lowest index (deterministic).
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

        var fronts = NonDominatedSort.Sort(combined);
        var normalized = _normalization.Normalize(combined);
        return SelectWithIndices(combined, fronts, targetSize, normalized, rng);
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
