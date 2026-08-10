using Unsga3.Algorithm;
using Unsga3.Core;
using Unsga3.Utilities;

namespace Unsga3.Operators.Crossover;

public interface ICrossover
{
    /// <summary>Produce two offspring from two parents (variables only).</summary>
    (Individual Child1, Individual Child2) Crossover(Individual parent1, Individual parent2, IProblem problem, RandomProvider rng);
}
