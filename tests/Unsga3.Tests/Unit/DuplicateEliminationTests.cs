using System.Globalization;
using Unsga3.Algorithm;
using Unsga3.Core;
using Unsga3.Operators.Crossover;
using Unsga3.Operators.Mutation;
using Unsga3.Utilities;

namespace Unsga3.Tests.Unit;

public class DuplicateEliminationTests
{
    [Fact]
    public void Decision_key_uses_G12_significant_digits()
    {
        double tiny = 1e-20;
        string key = Unsga3Algorithm.DecisionKey(new[] { tiny });
        string significant = tiny.ToString("G12", CultureInfo.InvariantCulture);
        string twelveDecimalPlaces = tiny.ToString("F12", CultureInfo.InvariantCulture);

        Assert.Equal(significant, key);
        Assert.Equal("0.000000000000", twelveDecimalPlaces);
        Assert.NotEqual(twelveDecimalPlaces, key);
    }

    [Fact]
    public void Elimination_returns_when_mutation_cannot_diversify()
    {
        var dirs = ReferenceDirections.DasDennis(1, 1);
        var algo = new Unsga3Algorithm(
            dirs,
            populationSize: 6,
            crossover: new CopyParentsCrossover(),
            mutation: new NoopMutation(),
            seed: 1,
            eliminateDuplicates: true);

        var result = algo.Run(new IdentityProblem(), maxGenerations: 1);
        Assert.Equal(6, result.FinalPopulation.Count);
    }

    private sealed class IdentityProblem : ProblemBase
    {
        public IdentityProblem()
            : base(1, 1, 0, new[] { (0.0, 1.0) })
        {
        }

        protected override void EvaluateCore(double[] x, double[] f, double[] g) => f[0] = x[0];
    }

    private sealed class CopyParentsCrossover : ICrossover
    {
        public (Individual Child1, Individual Child2) Crossover(
            Individual parent1, Individual parent2, IProblem problem, RandomProvider rng)
        {
            return (parent1.Clone(), parent2.Clone());
        }
    }

    private sealed class NoopMutation : IMutation
    {
        public void Mutate(Individual individual, IProblem problem, RandomProvider rng, double probabilityPerVariable)
        {
        }
    }
}
