using Unsga3.Algorithm;

namespace Unsga3.Core;

/// <summary>Fast non-dominated sorting (Deb et al.) with constraint-domination.</summary>
public static class NonDominatedSort
{
    /// <summary>
    /// Assign ranks (0 = best front) and return fronts as lists of indices into <paramref name="population"/>.
    /// </summary>
    public static List<List<int>> Sort(IReadOnlyList<Individual> population)
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
            population[i].Rank = int.MaxValue;
        }

        var first = new List<int>();
        for (int p = 0; p < n; p++)
        {
            for (int q = 0; q < n; q++)
            {
                if (p == q) continue;
                int cmp = CompareConstraintDominated(population[p], population[q]);
                if (cmp < 0)
                    S[p].Add(q);
                else if (cmp > 0)
                    nDom[p]++;
            }
            if (nDom[p] == 0)
            {
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
            if (a.ConstraintViolation < b.ConstraintViolation) return -1;
            if (a.ConstraintViolation > b.ConstraintViolation) return 1;
            // equal violation → fall through to objective comparison
        }

        return ComparePareto(a.Objectives, b.Objectives);
    }

    /// <summary>
    /// Pareto comparison for minimization: &lt; 0 if a dominates b, &gt; 0 if b dominates a, else 0.
    /// </summary>
    public static int ComparePareto(double[] a, double[] b)
    {
        if (a.Length != b.Length)
            throw new ArgumentException("Objective vectors must have the same length.");

        bool aBetter = false;
        bool bBetter = false;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] < b[i]) aBetter = true;
            else if (a[i] > b[i]) bBetter = true;
        }

        if (aBetter && !bBetter) return -1;
        if (bBetter && !aBetter) return 1;
        return 0;
    }

    /// <summary>True if a Pareto-dominates b (minimization, ignores constraints).</summary>
    public static bool Dominates(Individual a, Individual b) =>
        ComparePareto(a.Objectives, b.Objectives) < 0;
}
