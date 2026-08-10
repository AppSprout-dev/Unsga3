using Unsga3.Algorithm;
using Unsga3.Core;
using Unsga3.Utilities;

namespace Unsga3.Operators.Mutation;

public interface IMutation
{
    void Mutate(Individual individual, IProblem problem, RandomProvider rng, double probabilityPerVariable);
}
