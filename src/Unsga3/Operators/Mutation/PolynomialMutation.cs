using Unsga3.Algorithm;
using Unsga3.Core;
using Unsga3.Utilities;

namespace Unsga3.Operators.Mutation;

/// <summary>Polynomial mutation (Deb) for real-coded decision variables.</summary>
public sealed class PolynomialMutation : IMutation
{
    public PolynomialMutation(double distributionIndex = 20.0)
    {
        if (distributionIndex <= 0) throw new ArgumentOutOfRangeException(nameof(distributionIndex));
        DistributionIndex = distributionIndex;
    }

    public double DistributionIndex { get; }

    public void Mutate(Individual individual, IProblem problem, RandomProvider rng, double probabilityPerVariable)
    {
        ArgumentNullException.ThrowIfNull(individual);
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(rng);
        if (probabilityPerVariable is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(probabilityPerVariable));

        double eta = DistributionIndex;
        for (int i = 0; i < individual.Variables.Length; i++)
        {
            if (rng.NextDouble() > probabilityPerVariable)
                continue;

            (double lo, double hi) = problem.Bounds[i];
            double x = individual.Variables[i];
            double deltaMax = hi - lo;
            if (deltaMax < 1e-14)
                continue;

            double delta1 = (x - lo) / deltaMax;
            double delta2 = (hi - x) / deltaMax;
            double rand = rng.NextDouble();
            double mutPow = 1.0 / (eta + 1.0);
            double deltaq;

            if (rand < 0.5)
            {
                double xy = 1.0 - delta1;
                double val = 2.0 * rand + (1.0 - 2.0 * rand) * Math.Pow(xy, eta + 1.0);
                deltaq = Math.Pow(val, mutPow) - 1.0;
            }
            else
            {
                double xy = 1.0 - delta2;
                double val = 2.0 * (1.0 - rand) + 2.0 * (rand - 0.5) * Math.Pow(xy, eta + 1.0);
                deltaq = 1.0 - Math.Pow(val, mutPow);
            }

            double y = x + deltaq * deltaMax;
            if (y < lo) y = lo;
            if (y > hi) y = hi;
            individual.Variables[i] = y;
            individual.Evaluated = false;
        }
    }
}
