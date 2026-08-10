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
        NonDominatedSolutions = ExtractNonDominated(finalPopulation);
    }

    public IReadOnlyList<Individual> FinalPopulation { get; }

    /// <summary>Feasible non-dominated set from the final population (Pareto front approximation).</summary>
    public IReadOnlyList<Individual> NonDominatedSolutions { get; }

    public int GenerationsExecuted { get; }
    public int Evaluations { get; }

    private static IReadOnlyList<Individual> ExtractNonDominated(IReadOnlyList<Individual> population)
    {
        if (population.Count == 0)
            return Array.Empty<Individual>();

        var fronts = NonDominatedSort.Sort(population);
        if (fronts.Count == 0)
            return Array.Empty<Individual>();

        // Prefer feasible members of the first front; if none, return whole first front.
        var first = fronts[0];
        var feasible = new List<Individual>();
        foreach (int i in first)
        {
            if (population[i].IsFeasible)
                feasible.Add(population[i].Clone());
        }
        if (feasible.Count > 0)
            return feasible;

        return first.Select(i => population[i].Clone()).ToList();
    }
}
