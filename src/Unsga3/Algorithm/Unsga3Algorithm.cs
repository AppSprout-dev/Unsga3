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
    private readonly bool _eliminateDuplicates;

    /// <param name="referenceDirections">Das–Dennis (or custom) directions; one weight vector per niche.</param>
    /// <param name="populationSize">Defaults to the number of reference directions.</param>
    /// <param name="crossover">Defaults to SBX η=30.</param>
    /// <param name="mutation">Defaults to polynomial mutation η=20.</param>
    /// <param name="crossoverProbability">Probability of applying SBX to a parent pair.</param>
    /// <param name="mutationProbability">Per-variable mutation probability; default 1/nVars at run time.</param>
    /// <param name="seed">Optional RNG seed for reproducibility.</param>
    /// <param name="tournamentMode">Mating tournament policy; use <see cref="TournamentMode.PymooCompatible"/> for oracle runs.</param>
    /// <param name="eliminateDuplicates">
    /// Drop offspring whose decision vector matches an existing parent or earlier offspring
    /// (pymoo <c>eliminate_duplicates=True</c>). Default true.
    /// </param>
    public Unsga3Algorithm(
        double[][] referenceDirections,
        int? populationSize = null,
        ICrossover? crossover = null,
        IMutation? mutation = null,
        double crossoverProbability = 1.0,
        double? mutationProbability = null,
        int? seed = null,
        TournamentMode tournamentMode = TournamentMode.RankNicheDistance,
        bool eliminateDuplicates = true)
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
        _eliminateDuplicates = eliminateDuplicates;
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

    /// <param name="initialPopulation">
    /// Optional seed individuals (decision variables only; re-evaluated). Used to inject a known
    /// feasible set (e.g. Torquon grid oracle). When null or empty, a uniform random init is used.
    /// Extra seeds beyond <see cref="_populationSize"/> are truncated; fewer are pad-filled randomly.
    /// </param>
    public OptimizationResult Run(
        IProblem problem,
        int maxGenerations,
        IReadOnlyList<Individual>? initialPopulation) =>
        Run(problem, TerminationCriterion.MaxGenerations(maxGenerations), initialPopulation);

    public OptimizationResult Run(IProblem problem, TerminationCriterion termination) =>
        Run(problem, termination, initialPopulation: null);

    public OptimizationResult Run(
        IProblem problem,
        TerminationCriterion termination,
        IReadOnlyList<Individual>? initialPopulation)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(termination);

        if (_referenceDirections[0].Length != problem.NumberOfObjectives)
            throw new ArgumentException(
                $"Reference directions have {_referenceDirections[0].Length} objectives, problem has {problem.NumberOfObjectives}.");

        var rng = new RandomProvider(_seed);
        var refs = new ReferencePointManager(_referenceDirections);
        // One Normalization instance for the whole run so ideal / extreme points persist
        // across generations (pymoo HyperplaneNormalization). Survival reuses the same
        // instance so tournament prep and environmental selection stay consistent.
        var normalization = new Normalization(problem.NumberOfObjectives);
        var tournament = new TournamentSelection(_tournamentMode);
        var survival = new NondominatedSortingSurvival(refs, normalization);
        double mutProb = _mutationProbability ?? (1.0 / problem.NumberOfVariables);

        // --- initialize ---
        var population = CreateInitialPopulation(problem, _populationSize, rng, initialPopulation);
        int evaluations = EvaluateAll(problem, population);

        // Prepare ranks/niches for first selection.
        TournamentSelection.PrepareForSelection(population.Members, refs, normalization);

        int generation = 0;
        while (!termination.ShouldStop(generation, evaluations, population))
        {
            // Parents via U-NSGA-III niching tournament.
            var parents = tournament.SelectParents(population.Members, _populationSize, rng);

            // Variation → offspring (optionally de-duplicated vs parents + siblings).
            var offspring = CreateOffspring(problem, population.Members, parents, rng, mutProb);

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

    private List<Individual> CreateOffspring(
        IProblem problem,
        IReadOnlyList<Individual> currentPop,
        IReadOnlyList<Individual> parents,
        RandomProvider rng,
        double mutProb)
    {
        var offspring = new List<Individual>(_populationSize);
        // Hash of decision vectors already present (parents + accepted offspring).
        HashSet<string>? seen = null;
        if (_eliminateDuplicates)
        {
            seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < currentPop.Count; i++)
                seen.Add(DecisionKey(currentPop[i].Variables));
        }

        int safety = 0;
        int maxAttempts = _populationSize * 40;
        int pair = 0;
        while (offspring.Count < _populationSize && safety < maxAttempts)
        {
            safety++;
            if (pair + 1 >= parents.Count)
                pair = 0;
            var (c1, c2) = _crossover.Crossover(parents[pair], parents[pair + 1], problem, rng);
            pair += 2;
            _mutation.Mutate(c1, problem, rng, mutProb);
            _mutation.Mutate(c2, problem, rng, mutProb);

            TryAddOffspring(offspring, c1, seen);
            if (offspring.Count < _populationSize)
                TryAddOffspring(offspring, c2, seen);
        }

        // Fallback: mutated clones if de-dup exhausted attempts (should be rare).
        while (offspring.Count < _populationSize)
        {
            var extra = parents[rng.Next(parents.Count)].Clone();
            _mutation.Mutate(extra, problem, rng, mutProb);
            // Always accept in the hard-fallback path so we never deadlock.
            if (seen is null || seen.Add(DecisionKey(extra.Variables)) || offspring.Count + 1 >= _populationSize)
                offspring.Add(extra);
        }

        return offspring;
    }

    private static void TryAddOffspring(List<Individual> offspring, Individual child, HashSet<string>? seen)
    {
        if (seen is null)
        {
            offspring.Add(child);
            return;
        }
        if (seen.Add(DecisionKey(child.Variables)))
            offspring.Add(child);
    }

    /// <summary>Stable decision-vector key for duplicate elimination (rounded to 12 dp).</summary>
    private static string DecisionKey(double[] x)
    {
        // Invariant culture, fixed decimals — enough for continuous SBX without false collisions.
        var sb = new System.Text.StringBuilder(x.Length * 18);
        for (int i = 0; i < x.Length; i++)
        {
            if (i > 0) sb.Append('|');
            sb.Append(x[i].ToString("G12", System.Globalization.CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }

    private static Population CreateInitialPopulation(
        IProblem problem,
        int size,
        RandomProvider rng,
        IReadOnlyList<Individual>? seed = null)
    {
        var pop = new Population(size);
        int nVar = problem.NumberOfVariables;
        int nObj = problem.NumberOfObjectives;
        int nCon = problem.NumberOfConstraints;

        if (seed is { Count: > 0 })
        {
            int nTake = Math.Min(size, seed.Count);
            for (int i = 0; i < nTake; i++)
            {
                var src = seed[i];
                if (src.Variables.Length != nVar)
                    throw new ArgumentException(
                        $"Seed individual {i} has {src.Variables.Length} variables; problem expects {nVar}.",
                        nameof(seed));
                var ind = new Individual(nVar, nObj, nCon);
                Array.Copy(src.Variables, ind.Variables, nVar);
                // Clamp into bounds (categorical genes already live in [0,1]).
                for (int j = 0; j < nVar; j++)
                {
                    (double lo, double hi) = problem.Bounds[j];
                    double v = ind.Variables[j];
                    if (v < lo) v = lo;
                    if (v > hi) v = hi;
                    ind.Variables[j] = v;
                }
                pop.Add(ind);
            }
        }

        while (pop.Count < size)
        {
            var ind = new Individual(nVar, nObj, nCon);
            for (int j = 0; j < nVar; j++)
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
