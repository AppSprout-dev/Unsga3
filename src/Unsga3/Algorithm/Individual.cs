namespace Unsga3.Algorithm;

/// <summary>
/// One candidate solution: decision variables, objective values, and constraint violations.
/// </summary>
public sealed class Individual
{
    public Individual(int nVariables, int nObjectives, int nConstraints = 0)
    {
        if (nVariables < 1) throw new ArgumentOutOfRangeException(nameof(nVariables));
        if (nObjectives < 1) throw new ArgumentOutOfRangeException(nameof(nObjectives));
        if (nConstraints < 0) throw new ArgumentOutOfRangeException(nameof(nConstraints));

        Variables = new double[nVariables];
        Objectives = new double[nObjectives];
        Constraints = new double[nConstraints];
    }

    public Individual(double[] variables, int nObjectives, int nConstraints = 0)
    {
        ArgumentNullException.ThrowIfNull(variables);
        if (variables.Length < 1) throw new ArgumentException("Need at least one variable.", nameof(variables));
        if (nObjectives < 1) throw new ArgumentOutOfRangeException(nameof(nObjectives));
        if (nConstraints < 0) throw new ArgumentOutOfRangeException(nameof(nConstraints));

        Variables = (double[])variables.Clone();
        Objectives = new double[nObjectives];
        Constraints = new double[nConstraints];
    }

    public double[] Variables { get; }
    public double[] Objectives { get; }
    public double[] Constraints { get; }

    /// <summary>Sum of positive constraint violations (0 = feasible).</summary>
    public double ConstraintViolation { get; private set; }

    // Internal bookkeeping for NSGA-III / U-NSGA-III selection.
    internal int Rank { get; set; } = int.MaxValue;
    internal int NicheCount { get; set; }
    internal int AssociatedReference { get; set; } = -1;
    internal double PerpendicularDistance { get; set; } = double.PositiveInfinity;
    internal bool Evaluated { get; set; }

    public Individual Clone()
    {
        var copy = new Individual(Variables.Length, Objectives.Length, Constraints.Length);
        Array.Copy(Variables, copy.Variables, Variables.Length);
        Array.Copy(Objectives, copy.Objectives, Objectives.Length);
        Array.Copy(Constraints, copy.Constraints, Constraints.Length);
        copy.ConstraintViolation = ConstraintViolation;
        copy.Evaluated = Evaluated;
        copy.Rank = Rank;
        copy.NicheCount = NicheCount;
        copy.AssociatedReference = AssociatedReference;
        copy.PerpendicularDistance = PerpendicularDistance;
        return copy;
    }

    /// <summary>
    /// Recompute aggregate violation from <see cref="Constraints"/> (g ≤ 0 form: positive values violate).
    /// </summary>
    public void RefreshConstraintViolation()
    {
        double v = 0;
        for (int i = 0; i < Constraints.Length; i++)
        {
            if (Constraints[i] > 0)
                v += Constraints[i];
        }
        ConstraintViolation = v;
    }

    public bool IsFeasible => ConstraintViolation <= 0;

    /// <summary>
    /// Constraint-domination comparison (Deb et al.): feasible beats infeasible;
    /// among infeasible, lower violation wins; among feasible, Pareto dominates.
    /// Returns true if this individual is better than <paramref name="other"/> for tournament.
    /// </summary>
    public bool ConstraintDominates(Individual other, Func<Individual, Individual, bool>? objectiveDominates = null)
    {
        ArgumentNullException.ThrowIfNull(other);
        bool aFeas = IsFeasible;
        bool bFeas = other.IsFeasible;
        if (aFeas && !bFeas) return true;
        if (!aFeas && bFeas) return false;
        if (!aFeas && !bFeas)
            return ConstraintViolation < other.ConstraintViolation;
        return objectiveDominates?.Invoke(this, other) ?? false;
    }
}
