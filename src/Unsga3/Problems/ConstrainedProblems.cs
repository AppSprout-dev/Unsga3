using Unsga3.Core;

namespace Unsga3.Problems;

/// <summary>
/// OSY (Osyczka and Kundu). Six variables, two objectives, six inequalities.
/// Constraint scaling matches pymoo 0.6: each g ≥ 0 form is divided by its
/// constant, then negated so a positive value is a violation (g ≤ 0).
/// </summary>
public sealed class OsyProblem : ProblemBase
{
    public OsyProblem()
        : base(6, 2, 6, new (double, double)[]
        {
            (0.0, 10.0),
            (0.0, 10.0),
            (1.0, 5.0),
            (0.0, 6.0),
            (1.0, 5.0),
            (0.0, 10.0),
        })
    {
    }

    protected override void EvaluateCore(double[] x, double[] f, double[] g)
    {
        f[0] = -(25.0 * Sq(x[0] - 2.0)
                 + Sq(x[1] - 2.0)
                 + Sq(x[2] - 1.0)
                 + Sq(x[3] - 4.0)
                 + Sq(x[4] - 1.0));
        f[1] = Sq(x[0]) + Sq(x[1]) + Sq(x[2]) + Sq(x[3]) + Sq(x[4]) + Sq(x[5]);

        // pymoo stores G = -g_feas after the same divisors.
        g[0] = -(x[0] + x[1] - 2.0) / 2.0;
        g[1] = -(6.0 - x[0] - x[1]) / 6.0;
        g[2] = -(2.0 - x[1] + x[0]) / 2.0;
        g[3] = -(2.0 - x[0] + 3.0 * x[1]) / 2.0;
        g[4] = -(4.0 - Sq(x[2] - 3.0) - x[3]) / 4.0;
        g[5] = -(Sq(x[4] - 3.0) + x[5] - 4.0) / 4.0;
    }

    private static double Sq(double v) => v * v;
}

/// <summary>
/// TNK (Tanaka). Two variables on [0, π] × [1e-30, π], two inequalities in g ≤ 0 form.
/// The disk constraint uses pymoo's factor of two:
/// <c>2((x−0.5)² + (y−0.5)²) − 1 ≤ 0</c>.
/// </summary>
public sealed class TnkProblem : ProblemBase
{
    public TnkProblem()
        : base(2, 2, 2, new (double, double)[]
        {
            (0.0, Math.PI),
            (1e-30, Math.PI),
        })
    {
    }

    protected override void EvaluateCore(double[] x, double[] f, double[] g)
    {
        f[0] = x[0];
        f[1] = x[1];
        double circle = x[0] * x[0] + x[1] * x[1] - 1.0
            - 0.1 * Math.Cos(16.0 * Math.Atan(x[0] / x[1]));
        g[0] = -circle;
        double dx = x[0] - 0.5;
        double dy = x[1] - 0.5;
        g[1] = 2.0 * (dx * dx + dy * dy) - 1.0;
    }
}

/// <summary>
/// C1-DTLZ1 (Jain &amp; Deb, NSGA-III Part II). DTLZ1 objectives plus one inequality.
/// Feasible when <c>1 − f_M/0.6 − Σ_{i&lt;M} f_i/0.5 ≥ 0</c>. Stored in g ≤ 0 form,
/// so a positive value is the violation. Default <c>k = 5</c> matches
/// <see cref="Dtlz1Problem"/> (<c>n = M + k − 1</c>). The constraint is a barrier
/// in front of the linear front; it does not replace that front.
/// </summary>
public sealed class C1Dtlz1Problem : ProblemBase
{
    public C1Dtlz1Problem(int nObjectives = 3, int k = 5)
        : base(nObjectives + k - 1, nObjectives, 1, DtlzHelper.UnitBounds(nObjectives + k - 1))
    {
        if (nObjectives < 2) throw new ArgumentOutOfRangeException(nameof(nObjectives));
        if (k < 1) throw new ArgumentOutOfRangeException(nameof(k));
    }

    protected override void EvaluateCore(double[] x, double[] f, double[] g)
    {
        int m = NumberOfObjectives;
        double gv = DtlzHelper.GDtlz1(x, m);
        for (int i = 0; i < m; i++)
        {
            double val = 0.5 * (1.0 + gv);
            for (int j = 0; j < m - i - 1; j++)
                val *= x[j];
            if (i > 0)
                val *= 1.0 - x[m - i - 1];
            f[i] = val;
        }

        double weighted = f[m - 1] / 0.6;
        for (int i = 0; i < m - 1; i++)
            weighted += f[i] / 0.5;
        g[0] = weighted - 1.0;
    }
}
