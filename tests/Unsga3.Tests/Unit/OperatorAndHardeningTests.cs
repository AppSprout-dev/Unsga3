using Unsga3.Algorithm;
using Unsga3.Core;
using Unsga3.Metrics;
using Unsga3.Operators.Crossover;
using Unsga3.Operators.Mutation;
using Unsga3.Operators.Selection;
using Unsga3.Operators.Survival;
using Unsga3.Problems;
using Unsga3.Results;
using Unsga3.Utilities;

namespace Unsga3.Tests.Unit;

/// <summary>
/// Unit tests covering operators, seeded determinism, termination criteria, NaN/Inf resilience,
/// custom IProblem constraints, mixed-feasibility niching, and argument validation.
/// </summary>
public class OperatorAndHardeningTests
{
    private sealed class CustomConstrainedProblem : IProblem
    {
        public int NumberOfVariables => 2;
        public int NumberOfObjectives => 2;
        public int NumberOfConstraints => 1;
        public (double Lower, double Upper)[] Bounds => new[] { (0.0, 1.0), (0.0, 1.0) };

        public void Evaluate(Individual individual)
        {
            // Intentionally does NOT call RefreshConstraintViolation() — Unsga3Algorithm must call it.
            double x0 = individual.Variables[0];
            double x1 = individual.Variables[1];
            individual.Objectives[0] = x0;
            individual.Objectives[1] = 1.0 - x0 + x1;
            // Feasible when x0 + x1 >= 0.5  <=>  0.5 - (x0 + x1) <= 0
            individual.Constraints[0] = 0.5 - (x0 + x1);
        }
    }

    [Fact]
    public void Sbx_respects_bounds_and_zero_probability_never_modifies_parents()
    {
        var problem = new Zdt1Problem(nVariables: 6);
        var p1 = new Individual(new[] { 0.1, 0.2, 0.3, 0.4, 0.5, 0.6 }, 2);
        var p2 = new Individual(new[] { 0.9, 0.8, 0.7, 0.6, 0.5, 0.4 }, 2);

        var zeroProb = new SimulatedBinaryCrossover(distributionIndex: 20.0, probability: 0.0);
        for (int seed = 0; seed < 20; seed++)
        {
            var (c1, c2) = zeroProb.Crossover(p1, p2, problem, new RandomProvider(seed));
            Assert.Equal(p1.Variables, c1.Variables);
            Assert.Equal(p2.Variables, c2.Variables);
        }

        var fullProb = new SimulatedBinaryCrossover(distributionIndex: 15.0, probability: 1.0);
        var rng = new RandomProvider(42);
        bool anyChanged = false;
        for (int trial = 0; trial < 20; trial++)
        {
            var (c1, c2) = fullProb.Crossover(p1, p2, problem, rng);
            for (int j = 0; j < problem.NumberOfVariables; j++)
            {
                (double lo, double hi) = problem.Bounds[j];
                Assert.InRange(c1.Variables[j], lo, hi);
                Assert.InRange(c2.Variables[j], lo, hi);
                if (c1.Variables[j] != p1.Variables[j] || c2.Variables[j] != p2.Variables[j])
                    anyChanged = true;
            }
        }
        Assert.True(anyChanged);
    }

    [Fact]
    public void Sbx_and_polynomial_mutation_reject_nan_and_out_of_range_parameters()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SimulatedBinaryCrossover(double.NaN, 1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SimulatedBinaryCrossover(double.PositiveInfinity, 1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SimulatedBinaryCrossover(20.0, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SimulatedBinaryCrossover(20.0, -0.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SimulatedBinaryCrossover(20.0, 1.1));

        Assert.Throws<ArgumentOutOfRangeException>(() => new PolynomialMutation(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PolynomialMutation(double.PositiveInfinity));

        var pm = new PolynomialMutation(20.0);
        var ind = new Individual(new[] { 0.5, 0.5 }, 2);
        var problem = new Zdt1Problem(nVariables: 2);
        var rng = new RandomProvider(1);
        Assert.Throws<ArgumentOutOfRangeException>(() => pm.Mutate(ind, problem, rng, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => pm.Mutate(ind, problem, rng, -0.01));
        Assert.Throws<ArgumentOutOfRangeException>(() => pm.Mutate(ind, problem, rng, 1.01));
    }

    [Fact]
    public void Polynomial_mutation_respects_bounds_and_zero_probability()
    {
        var problem = new Zdt4Problem(nVariables: 4);
        var pm = new PolynomialMutation(20.0);
        var rng = new RandomProvider(7);

        var untouched = new Individual(new[] { 0.25, -1.5, 2.0, 0.0 }, 2);
        pm.Mutate(untouched, problem, rng, 0.0);
        Assert.Equal(new[] { 0.25, -1.5, 2.0, 0.0 }, untouched.Variables);

        var ind = new Individual(new[] { 0.25, -1.5, 2.0, 0.0 }, 2);
        pm.Mutate(ind, problem, rng, 1.0);
        Assert.False(ind.Evaluated);
        for (int j = 0; j < problem.NumberOfVariables; j++)
        {
            (double lo, double hi) = problem.Bounds[j];
            Assert.InRange(ind.Variables[j], lo, hi);
        }
        Assert.NotEqual(untouched.Variables, ind.Variables);
    }

    [Fact]
    public void Random_provider_next_except_and_next_double_handle_edge_cases()
    {
        var rng = new RandomProvider(123);
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextExcept(0, 0));
        Assert.Equal(0, rng.NextExcept(1, 0));

        // Out-of-range exclude should sample uniformly across [0, n) without returning n.
        for (int i = 0; i < 100; i++)
        {
            int neg = rng.NextExcept(4, -1);
            int hi = rng.NextExcept(4, 10);
            Assert.InRange(neg, 0, 3);
            Assert.InRange(hi, 0, 3);
            int valid = rng.NextExcept(4, 2);
            Assert.InRange(valid, 0, 3);
            Assert.NotEqual(2, valid);
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextDouble(double.NaN, 1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextDouble(0.0, double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextDouble(2.0, 1.0));

        double wide = rng.NextDouble(-1e308, 1e308);
        Assert.True(double.IsFinite(wide));
    }

    [Fact]
    public void Algorithm_is_bit_deterministic_for_the_same_seed()
    {
        var problem = new Zdt1Problem(nVariables: 10);
        var dirs = ReferenceDirections.DasDennis(2, 10);

        var r1 = new Unsga3Algorithm(dirs, populationSize: 20, seed: 17).Run(problem, maxGenerations: 15);
        var r2 = new Unsga3Algorithm(dirs, populationSize: 20, seed: 17).Run(problem, maxGenerations: 15);

        Assert.Equal(r1.GenerationsExecuted, r2.GenerationsExecuted);
        Assert.Equal(r1.Evaluations, r2.Evaluations);
        Assert.Equal(r1.FinalPopulation.Count, r2.FinalPopulation.Count);
        for (int i = 0; i < r1.FinalPopulation.Count; i++)
        {
            Assert.Equal(r1.FinalPopulation[i].Variables, r2.FinalPopulation[i].Variables);
            Assert.Equal(r1.FinalPopulation[i].Objectives, r2.FinalPopulation[i].Objectives);
            Assert.True(r1.FinalPopulation[i].Evaluated);
        }
    }

    [Fact]
    public void Max_evaluations_termination_stops_at_budget()
    {
        var problem = new Zdt1Problem(nVariables: 6);
        var algo = Unsga3Algorithm.WithDasDennis(2, 5, populationSize: 12, seed: 3);
        var result = algo.Run(problem, TerminationCriterion.MaxEvaluations(36));

        Assert.Equal(36, result.Evaluations);
        Assert.Equal(2, result.GenerationsExecuted);
    }

    [Fact]
    public void Initial_population_handles_truncation_clamping_and_rejects_invalid_seeds()
    {
        var problem = new Zdt1Problem(nVariables: 3);
        var dirs = ReferenceDirections.DasDennis(2, 3);
        var algo = new Unsga3Algorithm(dirs, populationSize: 4, seed: 1);

        var seeds = new List<Individual>
        {
            new(new[] { -5.0, 2.0, 0.5 }, 2),
            new(new[] { 0.1, 0.2, 0.3 }, 2),
            new(new[] { 0.2, 0.3, 0.4 }, 2),
            new(new[] { 0.3, 0.4, 0.5 }, 2),
            new(new[] { 0.4, 0.5, 0.6 }, 2), // truncated beyond pop=4
        };
        var res = algo.Run(problem, maxGenerations: 0, initialPopulation: seeds);
        Assert.Equal(4, res.FinalPopulation.Count);
        Assert.Equal(new[] { 0.0, 1.0, 0.5 }, res.FinalPopulation[0].Variables);

        var nanSeeds = new List<Individual> { new(new[] { double.NaN, 0.5, 0.5 }, 2) };
        Assert.Throws<ArgumentException>(() => algo.Run(problem, maxGenerations: 0, initialPopulation: nanSeeds));

        var wrongLen = new List<Individual> { new(new[] { 0.5, 0.5 }, 2) };
        Assert.Throws<ArgumentException>(() => algo.Run(problem, maxGenerations: 0, initialPopulation: wrongLen));

        var nullSeeds = new List<Individual> { null! };
        Assert.Throws<ArgumentException>(() => algo.Run(problem, maxGenerations: 0, initialPopulation: nullSeeds));
    }

    [Fact]
    public void Custom_direct_IProblem_has_constraints_refreshed_automatically()
    {
        var problem = new CustomConstrainedProblem();
        var algo = Unsga3Algorithm.WithDasDennis(2, 6, populationSize: 16, seed: 5);
        var res = algo.Run(problem, maxGenerations: 15);

        Assert.NotEmpty(res.FeasibleNonDominatedSolutions);
        Assert.All(res.FeasibleNonDominatedSolutions, ind =>
        {
            Assert.True(ind.IsFeasible);
            Assert.True(ind.Variables[0] + ind.Variables[1] >= 0.5 - 1e-12);
        });
    }

    [Fact]
    public void Four_objective_many_objective_run_completes()
    {
        var problem = new Dtlz2Problem(nObjectives: 4, k: 5);
        var dirs = ReferenceDirections.DasDennis(4, 3); // C(6,3)=20
        var algo = new Unsga3Algorithm(dirs, populationSize: 20, seed: 2, tournamentMode: TournamentMode.PymooCompatible);
        var res = algo.Run(problem, maxGenerations: 10);

        Assert.Equal(10, res.GenerationsExecuted);
        Assert.Equal(20, res.FinalPopulation.Count);
        Assert.All(res.FinalPopulation, ind => Assert.Equal(4, ind.Objectives.Length));
    }

    [Fact]
    public void Normalization_reset_clears_persistent_history_and_views_are_readonly()
    {
        var norm = new Normalization(2);
        var p1 = new Individual(1, 2);
        p1.Objectives[0] = 1.0;
        p1.Objectives[1] = 2.0;
        var p2 = new Individual(1, 2);
        p2.Objectives[0] = 3.0;
        p2.Objectives[1] = 0.5;

        norm.Normalize(new[] { p1, p2 });
        Assert.Equal(1.0, norm.IdealPoint[0], 12);
        Assert.Throws<NotSupportedException>(() => ((IList<double>)norm.IdealPoint)[0] = -999.0);

        norm.Reset();
        Assert.True(double.IsPositiveInfinity(norm.IdealPoint[0]));
        Assert.True(double.IsPositiveInfinity(norm.IdealPoint[1]));

        // After Reset, an all-infeasible pool uses temporary projection without setting persistent ideal.
        var inf = new Individual(1, 2, 1);
        inf.Objectives[0] = 10.0;
        inf.Objectives[1] = 20.0;
        inf.Constraints[0] = 1.0;
        inf.RefreshConstraintViolation();
        var projected = norm.Normalize(new[] { inf });
        Assert.True(double.IsPositiveInfinity(norm.IdealPoint[0]));
        Assert.True(double.IsFinite(projected[0][0]));
    }

    [Fact]
    public void Mixed_prepare_for_selection_excludes_infeasible_fillers_from_niche_counts()
    {
        var dirs = new[] { new[] { 1.0, 0.0 }, new[] { 0.0, 1.0 } };
        var refs = new ReferencePointManager(dirs);
        var norm = new Normalization(2);

        var feas1 = Make(0.9, 0.1, cv: 0.0);
        var feas2 = Make(0.1, 0.9, cv: 0.0);
        var infeas1 = Make(0.95, 0.05, cv: 1.0);
        var infeas2 = Make(0.92, 0.08, cv: 2.0);
        var pop = new List<Individual> { feas1, feas2, infeas1, infeas2 };

        TournamentSelection.PrepareForSelection(pop, refs, norm);

        Assert.Equal(1, refs.GetNicheCount(0));
        Assert.Equal(1, refs.GetNicheCount(1));
        Assert.Equal(1, feas1.NicheCount);
        Assert.Equal(1, feas2.NicheCount);
        Assert.Equal(-1, infeas1.AssociatedReference);
        Assert.Equal(-1, infeas2.AssociatedReference);
        Assert.Equal(0, infeas1.NicheCount);
        Assert.Equal(0, infeas2.NicheCount);
        Assert.True(double.IsPositiveInfinity(infeas1.PerpendicularDistance));
    }

    [Fact]
    public void Survival_with_rng_breaks_niche_ties_and_populates_exact_boundary_associations()
    {
        var dirs = new[] { new[] { 1.0, 0.0 }, new[] { 0.0, 1.0 } };
        var refs = new ReferencePointManager(dirs);
        var norm = new Normalization(2);
        var survival = new NondominatedSortingSurvival(refs, norm);

        // Exact front boundary: front 0 has 2 points and targetSize = 2.
        var exactPool = new List<Individual>
        {
            Make(0.1, 0.9),
            Make(0.9, 0.1),
            Make(0.8, 0.8),
        };
        var exactSelected = survival.Select(exactPool, targetSize: 2, rng: new RandomProvider(1));
        Assert.Equal(2, exactSelected.Count);
        Assert.All(exactSelected, ind => Assert.InRange(ind.AssociatedReference, 0, 1));
        Assert.All(exactSelected, ind => Assert.Equal(1, ind.NicheCount));

        // Non-null rng when niche count > 0: 2 F0 points occupy ref 0 and ref 1,
        // and 3 mutually non-dominated F1 candidates all associate to ref 1.
        var pool = new List<Individual>
        {
            Make(0.05, 0.50), // F0 -> ref 1 (niche 1 already occupied)
            Make(0.90, 0.05), // F0 -> ref 0
            Make(0.06, 0.90), // F1 -> ref 1
            Make(0.07, 0.85), // F1 -> ref 1
            Make(0.08, 0.80), // F1 -> ref 1
        };
        var picks = new HashSet<double>();
        for (int seed = 0; seed < 30; seed++)
        {
            var r = new ReferencePointManager(dirs);
            var s = new NondominatedSortingSurvival(r, new Normalization(2));
            var sel = s.Select(pool, targetSize: 3, rng: new RandomProvider(seed));
            Assert.Equal(3, sel.Count);
            picks.Add(sel[2].Objectives[0]);
        }
        Assert.True(picks.Count >= 2, "Expected RNG tie-breaking across candidates in an occupied niche.");
    }

    [Fact]
    public void Nan_objectives_and_constraints_preserve_strict_partial_order_without_crashing()
    {
        // NaN constraint => maximally infeasible (+Inf)
        var nanCon = new Individual(1, 2, 1);
        nanCon.Constraints[0] = double.NaN;
        nanCon.RefreshConstraintViolation();
        Assert.False(nanCon.IsFeasible);
        Assert.True(double.IsPositiveInfinity(nanCon.ConstraintViolation));

        // NaN objectives must never form dominance cycles
        var a = Make(double.NaN, 1.0);
        var b = Make(1.0, double.NaN);
        var c = Make(0.5, 0.5);
        var d = Make(double.NaN, double.NaN);

        Assert.Equal(-1, NonDominatedSort.ComparePareto(c.Objectives, a.Objectives));
        Assert.Equal(1, NonDominatedSort.ComparePareto(a.Objectives, c.Objectives));
        Assert.Equal(0, NonDominatedSort.ComparePareto(a.Objectives, b.Objectives));
        Assert.Equal(-1, NonDominatedSort.ComparePareto(a.Objectives, d.Objectives));

        var fronts = NonDominatedSort.Sort(new[] { a, b, c, d });
        Assert.Equal(4, fronts.Sum(f => f.Count));
        Assert.Equal(new[] { 2 }, fronts[0]); // finite point c dominates all NaN points

        // ConstraintDominates without callback uses Pareto dominance
        Assert.True(c.ConstraintDominates(a));
        Assert.False(a.ConstraintDominates(c));

        // ReferencePointManager handles NaN normalized objectives without pinning to ray 0
        var refs = new ReferencePointManager(new[] { new[] { 1.0, 0.0 }, new[] { 0.0, 1.0 } });
        refs.Associate(new[] { d }, new[] { new[] { double.NaN, double.NaN } });
        Assert.Equal(-1, d.AssociatedReference);
        Assert.True(double.IsPositiveInfinity(d.PerpendicularDistance));
    }

    [Fact]
    public void Optimization_result_does_not_mutate_final_population_ranks_and_exposes_feasible_front()
    {
        var inf1 = Make(0.1, 0.9, cv: 1.0);
        var inf2 = Make(0.2, 0.8, cv: 2.0);
        inf1.Rank = 42;
        inf2.Rank = 99;

        var res = new OptimizationResult(new[] { inf1, inf2 }, generationsExecuted: 1, evaluations: 2);
        Assert.Equal(42, inf1.Rank);
        Assert.Equal(99, inf2.Rank);
        Assert.Single(res.NonDominatedSolutions);
        Assert.Empty(res.FeasibleNonDominatedSolutions);
    }

    [Fact]
    public void Decision_key_normalizes_negative_zero()
    {
        Assert.Equal(
            Unsga3Algorithm.DecisionKey(new[] { 0.0, -0.0, 1.5 }),
            Unsga3Algorithm.DecisionKey(new[] { -0.0, 0.0, 1.5 }));
    }

    [Fact]
    public void Problem_constructors_and_utilities_validate_arguments()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Zdt1Problem(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Zdt2Problem(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Zdt3Problem(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Zdt4Problem(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Zdt6Problem(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Dtlz1Problem(1, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Dtlz2Problem(3, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new C1Dtlz1Problem(1, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SphereProblem(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RosenbrockProblem(1));

        // TNK evaluated directly at x[1] = 0 stays finite via Atan2
        var tnk = new TnkProblem();
        var tnkInd = new Individual(new[] { 1.0, 0.0 }, 2, 2);
        tnk.Evaluate(tnkInd);
        Assert.True(double.IsFinite(tnkInd.Constraints[0]));

        // DasDennis removes silent p < 100 cap and validates inputs
        Assert.Equal(199, ReferenceDirections.PartitionsForMinimumDirections(2, 200));
        Assert.True(ReferenceDirections.Count(2, 199) >= 200);
        Assert.Throws<ArgumentOutOfRangeException>(() => ReferenceDirections.Count(0, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => ReferenceDirections.PartitionsForMinimumDirections(2, 0));

        // Hypervolume2D rejects 3-D points
        var threeD = new[] { new[] { 0.2, 0.3, 0.4 } };
        Assert.Throws<ArgumentException>(() => PerformanceIndicators.Hypervolume2D(threeD, new[] { 1.0, 1.0 }));

        // Constructor defensively clones reference directions
        var dirs = new[] { new[] { 1.0, 0.0 }, new[] { 0.0, 1.0 } };
        var algo = new Unsga3Algorithm(dirs, populationSize: 4, seed: 1);
        dirs[0][0] = double.NaN;
        var res = algo.Run(new Zdt1Problem(nVariables: 4), maxGenerations: 2);
        Assert.Equal(4, res.FinalPopulation.Count);
    }

    private static Individual Make(double f1, double f2, double cv = 0.0)
    {
        var ind = new Individual(1, 2, cv > 0.0 ? 1 : 0);
        ind.Objectives[0] = f1;
        ind.Objectives[1] = f2;
        if (cv > 0.0)
        {
            ind.Constraints[0] = cv;
            ind.RefreshConstraintViolation();
        }
        return ind;
    }
}
