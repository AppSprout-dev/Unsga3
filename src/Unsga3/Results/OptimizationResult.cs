using Unsga3.Algorithm;
using Unsga3.Core;

namespace Unsga3.Results;

/// <summary>Outcome of a U-NSGA-III run.</summary>
public sealed class OptimizationResult
{
    public OptimizationResult(
        IReadOnlyList<Individual> finalPopulation,
        int generationsExecuted,
        int evaluations)
    {
        FinalPopulation = finalPopulation ?? throw new ArgumentNullException(nameof(finalPopulation));
        GenerationsExecuted = generationsExecuted;
        Evaluations = evaluations;
        (NonDominatedSolutions, FeasibleNonDominatedSolutions) = ExtractNonDominated(finalPopulation);
    }

    public IReadOnlyList<Individual> FinalPopulation { get; }

    /// <summary>
    /// Feasible non-dominated set from the final population (Pareto front approximation).
    /// When no feasible solution exists in <see cref="FinalPopulation"/>, falls back to the
    /// least-violating first front; use <see cref="FeasibleNonDominatedSolutions"/> when an
    /// empty list is desired on all-infeasible runs.
    /// </summary>
    public IReadOnlyList<Individual> NonDominatedSolutions { get; }

    /// <summary>
    /// Strictly feasible non-dominated set from the final population (empty if no feasible
    /// solution exists).
    /// </summary>
    public IReadOnlyList<Individual> FeasibleNonDominatedSolutions { get; }

    public int GenerationsExecuted { get; }
    public int Evaluations { get; }

    private static (IReadOnlyList<Individual> AllOrFallback, IReadOnlyList<Individual> FeasibleOnly)
        ExtractNonDominated(IReadOnlyList<Individual> population)
    {
        if (population.Count == 0)
            return (Array.Empty<Individual>(), Array.Empty<Individual>());

        var fronts = NonDominatedSort.SortReadOnly(population);
        if (fronts.Count == 0)
            return (Array.Empty<Individual>(), Array.Empty<Individual>());

        var first = fronts[0];
        var feasible = new List<Individual>();
        foreach (int i in first)
        {
            if (population[i].IsFeasible)
                feasible.Add(population[i].Clone());
        }
        if (feasible.Count > 0)
            return (feasible, feasible);

        var fallback = first.Select(i => population[i].Clone()).ToList();
        return (fallback, Array.Empty<Individual>());
    }
}
