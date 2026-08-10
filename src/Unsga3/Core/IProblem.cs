using Unsga3.Algorithm;

namespace Unsga3.Core;

/// <summary>
/// Optimization problem: bounds, objectives (minimize), and optional inequality constraints g(x) ≤ 0.
/// </summary>
public interface IProblem
{
    int NumberOfVariables { get; }
    int NumberOfObjectives { get; }

    /// <summary>Number of inequality constraints (0 if unconstrained).</summary>
    int NumberOfConstraints { get; }

    /// <summary>Per-variable [lower, upper] bounds.</summary>
    (double Lower, double Upper)[] Bounds { get; }

    /// <summary>
    /// Evaluate decision variables already stored on <paramref name="individual"/>;
    /// write objectives (minimize) and constraint values (positive = violation).
    /// </summary>
    void Evaluate(Individual individual);
}
