using Unsga3.Algorithm;

namespace Unsga3.Core;

/// <summary>Fast non-dominated sorting (Deb et al.) with constraint-domination.</summary>
public static class NonDominatedSort
{
    /// <summary>
    /// Assign ranks (0 = best front) and return fronts as lists of indices into <paramref name="population"/>.
    /// </summary>
    public static List<List<int>> Sort(IReadOnlyList<Individual> population) =>
        SortCore(population, assignRanks: true);

    /// <summary>
    /// Return fronts as lists of indices into <paramref name="population"/> without mutating <see cref="Individual.Rank"/>.
    /// </summary>
    internal static List<List<int>> SortReadOnly(IReadOnlyList<Individual> population) =>
        SortCore(population, assignRanks: false);

    private static List<List<int>> SortCore(IReadOnlyList<Individual> population, bool assignRanks)
    {
        ArgumentNullException.ThrowIfNull(population);
        int n = population.Count;
        var fronts = new List<List<int>>();
        if (n == 0) return fronts;

        var S = new List<int>[n];
        var nDom = new int[n];
        for (int i = 0; i < n; i++)
        {
            S[i] = new List<int>();
            nDom[i] = 0;
            if (assignRanks)
                population[i].Rank = int.MaxValue;
        }

        for (int p = 0; p < n; p++)
        {
            for (int q = p + 1; q < n; q++)
            {
                int cmp = CompareConstraintDominated(population[p], population[q]);
                if (cmp < 0)
                {
                    S[p].Add(q);
                    nDom[q]++;
                }
                else if (cmp > 0)
                {
                    S[q].Add(p);
                    nDom[p]++;
                }
            }
        }

        var first = new List<int>();
        for (int p = 0; p < n; p++)
        {
            if (nDom[p] == 0)
            {
                if (assignRanks)
                    population[p].Rank = 0;
                first.Add(p);
            }
        }

        fronts.Add(first);
        int k = 0;
        while (fronts[k].Count > 0)
        {
            var next = new List<int>();
            foreach (int p in fronts[k])
            {
                foreach (int q in S[p])
                {
                    nDom[q]--;
                    if (nDom[q] == 0)
                    {
                        if (assignRanks)
                            population[q].Rank = k + 1;
                        next.Add(q);
                    }
                }
            }
            k++;
            fronts.Add(next);
        }

        // Drop trailing empty front.
        if (fronts.Count > 0 && fronts[^1].Count == 0)
            fronts.RemoveAt(fronts.Count - 1);

        return fronts;
    }

    /// <summary>
    /// Returns &lt; 0 if a dominates b, &gt; 0 if b dominates a, 0 if mutual non-domination.
    /// </summary>
    public static int CompareConstraintDominated(Individual a, Individual b)
    {
        bool aFeas = a.IsFeasible;
        bool bFeas = b.IsFeasible;

        if (aFeas && !bFeas) return -1;
        if (!aFeas && bFeas) return 1;
        if (!aFeas && !bFeas)
        {
            bool aNan = double.IsNaN(a.ConstraintViolation);
            bool bNan = double.IsNaN(b.ConstraintViolation);
            if (aNan != bNan) return aNan ? 1 : -1;
            if (a.ConstraintViolation < b.ConstraintViolation) return -1;
            if (a.ConstraintViolation > b.ConstraintViolation) return 1;
            // Deb constraint-domination: equal violation ⇒ neither dominates.
            // Objectives are not consulted. Tournament comparators are separate
            // and still break an equal-CV tie their own way.
            return 0;
        }

        return ComparePareto(a.Objectives, b.Objectives);
    }

    /// <summary>
    /// Pareto comparison for minimization: &lt; 0 if a dominates b, &gt; 0 if b dominates a, else 0.
    /// <see cref="double.NaN"/> coordinates are ordered strictly worse than any non-NaN value
    /// (and tie with each other) to preserve a strict partial order.
    /// </summary>
    public static int ComparePareto(double[] a, double[] b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        if (a.Length != b.Length)
            throw new ArgumentException("Objective vectors must have the same length.");

        bool aBetter = false;
        bool bBetter = false;
        for (int i = 0; i < a.Length; i++)
        {
            double ai = a[i];
            double bi = b[i];
            bool aNan = double.IsNaN(ai);
            bool bNan = double.IsNaN(bi);
            if (aNan || bNan)
            {
                if (!aNan) aBetter = true;
                else if (!bNan) bBetter = true;
                continue;
            }
            if (ai < bi) aBetter = true;
            else if (ai > bi) bBetter = true;
        }

        if (aBetter && !bBetter) return -1;
        if (bBetter && !aBetter) return 1;
        return 0;
    }

    /// <summary>True if a Pareto-dominates b (minimization, ignores constraints).</summary>
    public static bool Dominates(Individual a, Individual b) =>
        ComparePareto(a.Objectives, b.Objectives) < 0;
}
