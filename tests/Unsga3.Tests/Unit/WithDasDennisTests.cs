using Unsga3.Algorithm;
using Unsga3.Problems;

namespace Unsga3.Tests.Unit;

public class WithDasDennisTests
{
    [Fact]
    public void Single_objective_default_population_throws()
    {
        // Das–Dennis for M=1 is one direction. N defaults to |H| and must be at least 2.
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Unsga3Algorithm.WithDasDennis(1, 1));
        Assert.Equal("populationSize", ex.ParamName);
    }

    [Fact]
    public void Single_objective_runs_when_caller_chooses_population()
    {
        var algo = Unsga3Algorithm.WithDasDennis(1, 1, populationSize: 8, seed: 1);
        var result = algo.Run(new SphereProblem(nVariables: 2), maxGenerations: 2);
        Assert.Equal(8, result.FinalPopulation.Count);
        Assert.Equal(2, result.GenerationsExecuted);
    }
}
