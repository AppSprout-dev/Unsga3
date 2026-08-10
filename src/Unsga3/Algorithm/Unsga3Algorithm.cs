using Unsga3.Core;
using Unsga3.Operators.Crossover;
using Unsga3.Operators.Mutation;
using Unsga3.Operators.Selection;
using Unsga3.Operators.Survival;
using Unsga3.Results;
using Unsga3.Utilities;

namespace Unsga3.Algorithm;

/// <summary>
/// U-NSGA-III (Seada &amp; Deb): unified evolutionary optimizer for single-, multi-, and many-objective problems.
/// </summary>
public sealed class Unsga3Algorithm
{
    private readonly double[][] _referenceDirections;
    private readonly int _populationSize;
    private readonly ICrossover _crossover;
    private readonly IMutation _mutation;
    private readonly double _crossoverProbability;
    private readonly double? _mutationProbability;
    private readonly int? _seed;
    private readonly TournamentMode _tournamentMode;

    /// <param name="referenceDirections">Das–Dennis (or custom) directions; one weight vector per niche.</param>
    /// <param name="populationSize">Defaults to the number of reference directions.</param>
    /// <param name="crossover">Defaults to SBX η=30.</param>
    /// <param name="mutation">Defaults to polynomial mutation η=20.</param>
    /// <param name="crossoverProbability">Probability of applying SBX to a parent pair.</param>
    /// <param name="mutationProbability">Per-variable mutation probability; default 1/nVars at run time.</param>
    /// <param name="seed">Optional RNG seed for reproducibility.</param>
    /// <param name="tournamentMode">Mating tournament policy; use <see cref="TournamentMode.PymooCompatible"/> for oracle runs.</param>
    public Unsga3Algorithm(
        double[][] referenceDirections,
        int? populationSize = null,
        ICrossover? crossover = null,
        IMutation? mutation = null,
        double crossoverProbability = 1.0,
        double? mutationProbability = null,
        int? seed = null,
        TournamentMode tournamentMode = TournamentMode.RankNicheDistance)
    {
        ArgumentNullException.ThrowIfNull(referenceDirections);
        if (referenceDirections.Length < 1)
            throw new ArgumentException("Need at least one reference direction.", nameof(referenceDirections));
        if (crossoverProbability is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(crossoverProbability));

        _referenceDirections = referenceDirections;
        _populationSize = populationSize ?? referenceDirections.Length;
        if (_populationSize < 2)
            throw new ArgumentOutOfRangeException(nameof(populationSize), "Population size must be at least 2.");

        _crossover = crossover ?? new SimulatedBinaryCrossover(distributionIndex: 30.0, probability: crossoverProbability);
        _mutation = mutation ?? new PolynomialMutation(distributionIndex: 20.0);
        _crossoverProbability = crossoverProbability;
        _mutationProbability = mutationProbability;
        _seed = seed;
        _tournamentMode = tournamentMode;
    }

    /// <summary>Convenience: build Das–Dennis directions then construct the algorithm.</summary>
    public static Unsga3Algorithm WithDasDennis(
        int numberOfObjectives,
        int partitions,
        int? populationSize = null,
        int? seed = null,
        TournamentMode tournamentMode = TournamentMode.RankNicheDistance)
    {
        var dirs = ReferenceDirections.DasDennis(numberOfObjectives, partitions);
        return new Unsga3Algorithm(dirs, populationSize, seed: seed, tournamentMode: tournamentMode);
    }

    public OptimizationResult Run(IProblem problem, int maxGenerations) =>
        Run(problem, TerminationCriterion.MaxGenerations(maxGenerations));

    public OptimizationResult Run(IProblem problem, TerminationCriterion termination)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(termination);

        if (_referenceDirections[0].Length != problem.NumberOfObjectives)
            throw new ArgumentException(
                $"Reference directions have {_referenceDirections[0].Length} objectives, problem has {problem.NumberOfObjectives}.");

        var rng = new RandomProvider(_seed);
        var refs = new ReferencePointManager(_referenceDirections);
        var normalization = new Normalization(problem.NumberOfObjectives);
        var tournament = new TournamentSelection(_tournamentMode);
        var survival = new NondominatedSortingSurvival(refs);
        double mutProb = _mutationProbability ?? (1.0 / problem.NumberOfVariables);

        // --- initialize ---
        var population = CreateInitialPopulation(problem, _populationSize, rng);
        int evaluations = EvaluateAll(problem, population);

        // Prepare ranks/niches for first selection.
        TournamentSelection.PrepareForSelection(population.Members, refs, normalization);

        int generation = 0;
        while (!termination.ShouldStop(generation, evaluations, population))
        {
            // Parents via U-NSGA-III niching tournament.
            var parents = tournament.SelectParents(population.Members, _populationSize, rng);

            // Variation → offspring.
            var offspring = new List<Individual>(_populationSize);
            for (int i = 0; i + 1 < parents.Count; i += 2)
            {
                var (c1, c2) = _crossover.Crossover(parents[i], parents[i + 1], problem, rng);
                _mutation.Mutate(c1, problem, rng, mutProb);
                _mutation.Mutate(c2, problem, rng, mutProb);
                offspring.Add(c1);
                if (offspring.Count < _populationSize)
                    offspring.Add(c2);
            }
            // Odd population: fill last slot by cloning a mutated parent.
            while (offspring.Count < _populationSize)
            {
                var extra = parents[rng.Next(parents.Count)].Clone();
                _mutation.Mutate(extra, problem, rng, mutProb);
                offspring.Add(extra);
            }

            evaluations += EvaluateAll(problem, offspring);

            // Environmental selection on R = P ∪ Q.
            var combined = new List<Individual>(population.Count + offspring.Count);
            combined.AddRange(population.Members);
            combined.AddRange(offspring);
            var next = survival.Select(combined, _populationSize, rng);

            population = new Population(next);
            TournamentSelection.PrepareForSelection(population.Members, refs, normalization);
            generation++;
        }

        return new OptimizationResult(population.Members.ToList(), generation, evaluations);
    }

    private static Population CreateInitialPopulation(IProblem problem, int size, RandomProvider rng)
    {
        var pop = new Population(size);
        for (int i = 0; i < size; i++)
        {
            var ind = new Individual(problem.NumberOfVariables, problem.NumberOfObjectives, problem.NumberOfConstraints);
            for (int j = 0; j < problem.NumberOfVariables; j++)
            {
                (double lo, double hi) = problem.Bounds[j];
                ind.Variables[j] = rng.NextDouble(lo, hi);
            }
            pop.Add(ind);
        }
        return pop;
    }

    private static int EvaluateAll(IProblem problem, Population population)
    {
        int n = 0;
        foreach (var ind in population.Members)
        {
            problem.Evaluate(ind);
            n++;
        }
        return n;
    }

    private static int EvaluateAll(IProblem problem, List<Individual> individuals)
    {
        int n = 0;
        foreach (var ind in individuals)
        {
            problem.Evaluate(ind);
            n++;
        }
        return n;
    }
}
