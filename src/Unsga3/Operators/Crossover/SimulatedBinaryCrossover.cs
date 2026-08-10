using Unsga3.Algorithm;
using Unsga3.Core;
using Unsga3.Utilities;

namespace Unsga3.Operators.Crossover;

/// <summary>Simulated Binary Crossover (SBX) for real-coded GAs (Deb &amp; Agrawal).</summary>
public sealed class SimulatedBinaryCrossover : ICrossover
{
    public SimulatedBinaryCrossover(double distributionIndex = 30.0, double probability = 1.0)
    {
        if (distributionIndex <= 0) throw new ArgumentOutOfRangeException(nameof(distributionIndex));
        if (probability is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(probability));
        DistributionIndex = distributionIndex;
        Probability = probability;
    }

    public double DistributionIndex { get; }
    public double Probability { get; }

    public (Individual Child1, Individual Child2) Crossover(
        Individual parent1, Individual parent2, IProblem problem, RandomProvider rng)
    {
        ArgumentNullException.ThrowIfNull(parent1);
        ArgumentNullException.ThrowIfNull(parent2);
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(rng);

        int n = problem.NumberOfVariables;
        var c1 = new Individual(n, problem.NumberOfObjectives, problem.NumberOfConstraints);
        var c2 = new Individual(n, problem.NumberOfObjectives, problem.NumberOfConstraints);
        Array.Copy(parent1.Variables, c1.Variables, n);
        Array.Copy(parent2.Variables, c2.Variables, n);

        if (rng.NextDouble() > Probability)
            return (c1, c2);

        double eta = DistributionIndex;
        for (int i = 0; i < n; i++)
        {
            double x1 = parent1.Variables[i];
            double x2 = parent2.Variables[i];
            (double lo, double hi) = problem.Bounds[i];

            if (rng.NextDouble() > 0.5)
                continue;
            if (Math.Abs(x1 - x2) < 1e-14)
                continue;

            if (x1 > x2)
                (x1, x2) = (x2, x1);

            double rand = rng.NextDouble();
            double beta;
            double delta = x2 - x1;

            double beta1 = 1.0 + 2.0 * (x1 - lo) / delta;
            double alpha1 = 2.0 - Math.Pow(beta1, -(eta + 1.0));
            if (rand <= 1.0 / alpha1)
                beta = Math.Pow(rand * alpha1, 1.0 / (eta + 1.0));
            else
                beta = Math.Pow(1.0 / (2.0 - rand * alpha1), 1.0 / (eta + 1.0));

            double c1v = 0.5 * ((x1 + x2) - beta * delta);

            double beta2 = 1.0 + 2.0 * (hi - x2) / delta;
            double alpha2 = 2.0 - Math.Pow(beta2, -(eta + 1.0));
            if (rand <= 1.0 / alpha2)
                beta = Math.Pow(rand * alpha2, 1.0 / (eta + 1.0));
            else
                beta = Math.Pow(1.0 / (2.0 - rand * alpha2), 1.0 / (eta + 1.0));

            double c2v = 0.5 * ((x1 + x2) + beta * delta);

            c1.Variables[i] = Clamp(c1v, lo, hi);
            c2.Variables[i] = Clamp(c2v, lo, hi);

            if (rng.NextDouble() > 0.5)
                (c1.Variables[i], c2.Variables[i]) = (c2.Variables[i], c1.Variables[i]);
        }

        return (c1, c2);
    }

    private static double Clamp(double v, double lo, double hi) =>
        v < lo ? lo : v > hi ? hi : v;
}
