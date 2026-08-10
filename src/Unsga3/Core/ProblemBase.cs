using Unsga3.Algorithm;

namespace Unsga3.Core;

/// <summary>Convenience base for analytic test problems with fixed bounds.</summary>
public abstract class ProblemBase : IProblem
{
    protected ProblemBase(int nVariables, int nObjectives, int nConstraints, (double Lower, double Upper)[] bounds)
    {
        if (nVariables < 1) throw new ArgumentOutOfRangeException(nameof(nVariables));
        if (nObjectives < 1) throw new ArgumentOutOfRangeException(nameof(nObjectives));
        if (nConstraints < 0) throw new ArgumentOutOfRangeException(nameof(nConstraints));
        ArgumentNullException.ThrowIfNull(bounds);
        if (bounds.Length != nVariables)
            throw new ArgumentException("Bounds length must equal number of variables.", nameof(bounds));

        NumberOfVariables = nVariables;
        NumberOfObjectives = nObjectives;
        NumberOfConstraints = nConstraints;
        Bounds = bounds;
    }

    public int NumberOfVariables { get; }
    public int NumberOfObjectives { get; }
    public int NumberOfConstraints { get; }
    public (double Lower, double Upper)[] Bounds { get; }

    public void Evaluate(Individual individual)
    {
        ArgumentNullException.ThrowIfNull(individual);
        if (individual.Variables.Length != NumberOfVariables)
            throw new ArgumentException("Individual variable count does not match the problem.");
        if (individual.Objectives.Length != NumberOfObjectives)
            throw new ArgumentException("Individual objective count does not match the problem.");
        if (individual.Constraints.Length != NumberOfConstraints)
            throw new ArgumentException("Individual constraint count does not match the problem.");

        EvaluateCore(individual.Variables, individual.Objectives, individual.Constraints);
        individual.RefreshConstraintViolation();
        individual.Evaluated = true;
    }

    /// <summary>Implement objective (and constraint) evaluation. Minimize all objectives.</summary>
    protected abstract void EvaluateCore(double[] x, double[] f, double[] g);
}
