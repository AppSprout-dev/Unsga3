using Unsga3.Algorithm;
using Unsga3.Core;
using Unsga3.Operators.Survival;
using Unsga3.Problems;
using Unsga3.Utilities;

namespace Unsga3.Tests.Unit;

/// <summary>
/// Formulation checks and short runs for OSY, TNK, and C1-DTLZ1.
/// No IGD numbers: those wait on measured oracle runs.
/// </summary>
public class ConstrainedProblemTests
{
    [Fact]
    public void Osy_matches_the_pymoo_constraint_scaling()
    {
        var feasible = Eval(new OsyProblem(), 5, 1, 5, 0, 5, 0);
        Assert.Equal(-274.0, feasible.Objectives[0], 12);
        Assert.Equal(76.0, feasible.Objectives[1], 12);
        Assert.Equal(new[] { -2.0, 0.0, -3.0, 0.0, 0.0, 0.0 }, feasible.Constraints.ToList());
        Assert.Equal(0.0, feasible.ConstraintViolation);
        Assert.True(feasible.IsFeasible);

        var infeasible = Eval(new OsyProblem(), 0, 0, 1, 0, 1, 0);
        Assert.Equal(-120.0, infeasible.Objectives[0], 12);
        Assert.Equal(2.0, infeasible.Objectives[1], 12);
        Assert.Equal(1.0, infeasible.Constraints[0], 12);
        Assert.Equal(1.0, infeasible.ConstraintViolation, 12);
        Assert.False(infeasible.IsFeasible);
    }

    [Fact]
    public void Tnk_feasible_corner_and_interior_violation()
    {
        var feasible = Eval(new TnkProblem(), 1, 1);
        Assert.Equal(1.0, feasible.Objectives[0], 12);
        Assert.Equal(1.0, feasible.Objectives[1], 12);
        Assert.Equal(-0.9, feasible.Constraints[0], 12);
        Assert.Equal(0.0, feasible.Constraints[1], 12);
        Assert.True(feasible.IsFeasible);

        var infeasible = Eval(new TnkProblem(), 0.1, 0.1);
        Assert.Equal(1.08, infeasible.Constraints[0], 12);
        Assert.True(infeasible.Constraints[1] < 0);
        Assert.Equal(1.08, infeasible.ConstraintViolation, 12);
        Assert.False(infeasible.IsFeasible);
    }

    [Fact]
    public void C1_dtlz1_center_is_feasible_and_the_origin_is_not()
    {
        var problem = new C1Dtlz1Problem(nObjectives: 3, k: 5);
        Assert.Equal(7, problem.NumberOfVariables);
        Assert.Equal(1, problem.NumberOfConstraints);

        var center = Eval(problem, Enumerable.Repeat(0.5, 7).ToArray());
        Assert.Equal(0.125, center.Objectives[0], 12);
        Assert.Equal(0.125, center.Objectives[1], 12);
        Assert.Equal(0.25, center.Objectives[2], 12);
        Assert.Equal(-1.0 / 12.0, center.Constraints[0], 12);
        Assert.True(center.IsFeasible);

        var origin = Eval(problem, new double[7]);
        Assert.False(origin.IsFeasible);
        Assert.True(origin.ConstraintViolation > 1);
    }

    [Fact]
    public void Osy_survival_uses_the_feasible_hyperplane_and_cv_fill()
    {
        var problem = new OsyProblem();
        var feasible = Eval(problem, 5, 1, 5, 0, 5, 0);
        var lowerCv = Eval(problem, 0, 0, 1, 0, 1, 0);
        var higherCv = Eval(problem, 0, 0, 1, 6, 1, 0);
        Assert.True(lowerCv.ConstraintViolation < higherCv.ConstraintViolation);
        // Both infeasible points have a smaller f2 than the feasible point.
        Assert.True(lowerCv.Objectives[1] < feasible.Objectives[1]);

        var norm = new Normalization(2);
        var survival = new NondominatedSortingSurvival(
            new ReferencePointManager(ReferenceDirections.DasDennis(2, 1)), norm);
        higherCv.AssociatedReference = 4;

        var selected = survival.Select(
            new List<Individual> { higherCv, feasible, lowerCv },
            targetSize: 2,
            rng: null);

        Assert.True(selected[0].IsFeasible);
        Assert.Equal(lowerCv.ConstraintViolation, selected[1].ConstraintViolation, 12);
        Assert.Equal(-1, selected[1].AssociatedReference);
        Assert.Equal(feasible.Objectives[0], norm.IdealPoint[0], 9);
        Assert.Equal(feasible.Objectives[1], norm.IdealPoint[1], 9);
    }

    [Fact]
    public void Tnk_survival_does_not_take_the_infeasible_ideal()
    {
        var problem = new TnkProblem();
        var feasible = Eval(problem, 1, 1);
        var closerToOrigin = Eval(problem, 0.1, 0.1);
        var far = Eval(problem, 3, 3);
        Assert.True(closerToOrigin.ConstraintViolation < far.ConstraintViolation);

        var norm = new Normalization(2);
        var survival = new NondominatedSortingSurvival(
            new ReferencePointManager(ReferenceDirections.DasDennis(2, 1)), norm);
        var selected = survival.Select(
            new List<Individual> { far, closerToOrigin, feasible },
            targetSize: 2,
            rng: null);

        Assert.True(selected[0].IsFeasible);
        Assert.Equal(closerToOrigin.ConstraintViolation, selected[1].ConstraintViolation, 9);
        Assert.Equal(1.0, norm.IdealPoint[0], 9);
        Assert.Equal(1.0, norm.IdealPoint[1], 9);
    }

    [Fact]
    public void C1_dtlz1_survival_fills_from_the_smaller_violation()
    {
        var problem = new C1Dtlz1Problem();
        var feasible = Eval(problem, Enumerable.Repeat(0.5, 7).ToArray());
        var mild = Eval(problem, Enumerable.Repeat(0.2, 7).ToArray());
        var severe = Eval(problem, new double[7]);
        Assert.True(mild.ConstraintViolation < severe.ConstraintViolation);
        Assert.True(severe.Objectives[0] < feasible.Objectives[0]);

        var norm = new Normalization(3);
        var survival = new NondominatedSortingSurvival(
            new ReferencePointManager(ReferenceDirections.DasDennis(3, 1)), norm);
        var selected = survival.Select(
            new List<Individual> { severe, feasible, mild },
            targetSize: 2,
            rng: null);

        Assert.True(selected[0].IsFeasible);
        Assert.Equal(mild.ConstraintViolation, selected[1].ConstraintViolation, 8);
        Assert.Equal(feasible.Objectives[0], norm.IdealPoint[0], 9);
        Assert.Equal(feasible.Objectives[1], norm.IdealPoint[1], 9);
        Assert.Equal(feasible.Objectives[2], norm.IdealPoint[2], 9);
    }

    [Theory]
    [InlineData("osy")]
    [InlineData("tnk")]
    [InlineData("c1")]
    public void Seeded_run_keeps_a_feasible_member(string name)
    {
        IProblem problem = name switch
        {
            "osy" => new OsyProblem(),
            "tnk" => new TnkProblem(),
            "c1" => new C1Dtlz1Problem(),
            _ => throw new ArgumentOutOfRangeException(nameof(name)),
        };
        Individual seed = name switch
        {
            "osy" => new Individual(new[] { 5.0, 1, 5, 0, 5, 0 }, problem.NumberOfObjectives, problem.NumberOfConstraints),
            "tnk" => new Individual(new[] { 1.0, 1.0 }, problem.NumberOfObjectives, problem.NumberOfConstraints),
            "c1" => new Individual(Enumerable.Repeat(0.5, problem.NumberOfVariables).ToArray(), problem.NumberOfObjectives, problem.NumberOfConstraints),
            _ => throw new ArgumentOutOfRangeException(nameof(name)),
        };

        var dirs = ReferenceDirections.DasDennis(problem.NumberOfObjectives, problem.NumberOfObjectives == 2 ? 4 : 2);
        var algo = new Unsga3Algorithm(dirs, populationSize: 12, seed: 1);
        var result = algo.Run(problem, maxGenerations: 3, initialPopulation: new[] { seed });

        Assert.Equal(12, result.FinalPopulation.Count);
        Assert.Contains(result.FinalPopulation, ind => ind.IsFeasible);
        Assert.All(result.FinalPopulation, ind => Assert.Equal(problem.NumberOfObjectives, ind.Objectives.Length));
    }

    private static Individual Eval(IProblem problem, params double[] x)
    {
        var ind = new Individual(x, problem.NumberOfObjectives, problem.NumberOfConstraints);
        problem.Evaluate(ind);
        return ind;
    }
}
