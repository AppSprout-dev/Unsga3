using Unsga3.Core;

namespace Unsga3.Problems;

/// <summary>Sphere: f = Σ x_i², bounds [-5.12, 5.12]^n.</summary>
public sealed class SphereProblem : ProblemBase
{
    public SphereProblem(int nVariables = 10)
        : base(nVariables, 1, 0, Enumerable.Repeat((-5.12, 5.12), nVariables).ToArray()) { }

    protected override void EvaluateCore(double[] x, double[] f, double[] g)
    {
        double s = 0;
        for (int i = 0; i < x.Length; i++) s += x[i] * x[i];
        f[0] = s;
    }
}

/// <summary>Ackley (standard continuous SO test). Used by pymoo UNSGA3 demo.</summary>
public sealed class AckleyProblem : ProblemBase
{
    public AckleyProblem(int nVariables = 30)
        : base(nVariables, 1, 0, Enumerable.Repeat((-32.768, 32.768), nVariables).ToArray()) { }

    protected override void EvaluateCore(double[] x, double[] f, double[] g)
    {
        int n = x.Length;
        double s1 = 0, s2 = 0;
        for (int i = 0; i < n; i++)
        {
            s1 += x[i] * x[i];
            s2 += Math.Cos(2.0 * Math.PI * x[i]);
        }
        f[0] = -20.0 * Math.Exp(-0.2 * Math.Sqrt(s1 / n))
               - Math.Exp(s2 / n)
               + 20.0 + Math.E;
    }
}

/// <summary>Rosenbrock valley; global min at (1,…,1).</summary>
public sealed class RosenbrockProblem : ProblemBase
{
    public RosenbrockProblem(int nVariables = 10)
        : base(nVariables, 1, 0, Enumerable.Repeat((-2.048, 2.048), nVariables).ToArray()) { }

    protected override void EvaluateCore(double[] x, double[] f, double[] g)
    {
        double s = 0;
        for (int i = 0; i < x.Length - 1; i++)
        {
            double a = x[i + 1] - x[i] * x[i];
            double b = 1.0 - x[i];
            s += 100.0 * a * a + b * b;
        }
        f[0] = s;
    }
}
