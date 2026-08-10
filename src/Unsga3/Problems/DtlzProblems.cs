using Unsga3.Core;

namespace Unsga3.Problems;

/// <summary>Shared DTLZ helpers (Deb et al.). Default k=10 ⇒ n = M+k-1.</summary>
internal static class DtlzHelper
{
    public static (double Lower, double Upper)[] UnitBounds(int n) =>
        Enumerable.Repeat((0.0, 1.0), n).ToArray();

    public static double GDtlz1(double[] x, int m)
    {
        double s = 0;
        for (int i = m - 1; i < x.Length; i++)
        {
            double d = x[i] - 0.5;
            s += d * d - Math.Cos(20.0 * Math.PI * d);
        }
        return 100.0 * ((x.Length - m + 1) + s);
    }

    public static double GDtlz2(double[] x, int m)
    {
        double s = 0;
        for (int i = m - 1; i < x.Length; i++)
        {
            double d = x[i] - 0.5;
            s += d * d;
        }
        return s;
    }
}

/// <summary>DTLZ1 — linear simplex front; highly multimodal.</summary>
public sealed class Dtlz1Problem : ProblemBase
{
    public Dtlz1Problem(int nObjectives = 3, int k = 5)
        : base(nObjectives + k - 1, nObjectives, 0, DtlzHelper.UnitBounds(nObjectives + k - 1))
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
    }
}

/// <summary>DTLZ2 — unit sphere (first orthant).</summary>
public sealed class Dtlz2Problem : ProblemBase
{
    public Dtlz2Problem(int nObjectives = 3, int k = 10)
        : base(nObjectives + k - 1, nObjectives, 0, DtlzHelper.UnitBounds(nObjectives + k - 1))
    {
        if (nObjectives < 2) throw new ArgumentOutOfRangeException(nameof(nObjectives));
        if (k < 1) throw new ArgumentOutOfRangeException(nameof(k));
    }

    protected override void EvaluateCore(double[] x, double[] f, double[] g)
    {
        int m = NumberOfObjectives;
        double gv = DtlzHelper.GDtlz2(x, m);
        for (int i = 0; i < m; i++)
        {
            double val = 1.0 + gv;
            for (int j = 0; j < m - i - 1; j++)
                val *= Math.Cos(x[j] * Math.PI / 2.0);
            if (i > 0)
                val *= Math.Sin(x[m - i - 1] * Math.PI / 2.0);
            f[i] = val;
        }
    }
}

/// <summary>DTLZ3 — sphere + multimodal g (like DTLZ1's cosine term).</summary>
public sealed class Dtlz3Problem : ProblemBase
{
    public Dtlz3Problem(int nObjectives = 3, int k = 10)
        : base(nObjectives + k - 1, nObjectives, 0, DtlzHelper.UnitBounds(nObjectives + k - 1))
    {
        if (nObjectives < 2) throw new ArgumentOutOfRangeException(nameof(nObjectives));
        if (k < 1) throw new ArgumentOutOfRangeException(nameof(k));
    }

    protected override void EvaluateCore(double[] x, double[] f, double[] g)
    {
        int m = NumberOfObjectives;
        double gv = DtlzHelper.GDtlz1(x, m); // same g as DTLZ1
        for (int i = 0; i < m; i++)
        {
            double val = 1.0 + gv;
            for (int j = 0; j < m - i - 1; j++)
                val *= Math.Cos(x[j] * Math.PI / 2.0);
            if (i > 0)
                val *= Math.Sin(x[m - i - 1] * Math.PI / 2.0);
            f[i] = val;
        }
    }
}

/// <summary>DTLZ4 — biased density toward f_M = 0 (α=100).</summary>
public sealed class Dtlz4Problem : ProblemBase
{
    private readonly double _alpha;

    public Dtlz4Problem(int nObjectives = 3, int k = 10, double alpha = 100.0)
        : base(nObjectives + k - 1, nObjectives, 0, DtlzHelper.UnitBounds(nObjectives + k - 1))
    {
        if (nObjectives < 2) throw new ArgumentOutOfRangeException(nameof(nObjectives));
        if (k < 1) throw new ArgumentOutOfRangeException(nameof(k));
        _alpha = alpha;
    }

    protected override void EvaluateCore(double[] x, double[] f, double[] g)
    {
        int m = NumberOfObjectives;
        double gv = DtlzHelper.GDtlz2(x, m);
        for (int i = 0; i < m; i++)
        {
            double val = 1.0 + gv;
            for (int j = 0; j < m - i - 1; j++)
                val *= Math.Cos(Math.Pow(x[j], _alpha) * Math.PI / 2.0);
            if (i > 0)
                val *= Math.Sin(Math.Pow(x[m - i - 1], _alpha) * Math.PI / 2.0);
            f[i] = val;
        }
    }
}

/// <summary>DTLZ7 — disconnected Pareto-optimal regions.</summary>
public sealed class Dtlz7Problem : ProblemBase
{
    public Dtlz7Problem(int nObjectives = 3, int k = 20)
        : base(nObjectives + k - 1, nObjectives, 0, DtlzHelper.UnitBounds(nObjectives + k - 1))
    {
        if (nObjectives < 2) throw new ArgumentOutOfRangeException(nameof(nObjectives));
        if (k < 1) throw new ArgumentOutOfRangeException(nameof(k));
    }

    protected override void EvaluateCore(double[] x, double[] f, double[] g)
    {
        int m = NumberOfObjectives;
        for (int i = 0; i < m - 1; i++)
            f[i] = x[i];

        double gVal = 0;
        for (int i = m - 1; i < x.Length; i++)
            gVal += x[i];
        gVal = 1.0 + 9.0 * gVal / (x.Length - m + 1);

        double h = m;
        for (int i = 0; i < m - 1; i++)
            h -= f[i] / (1.0 + gVal) * (1.0 + Math.Sin(3.0 * Math.PI * f[i]));
        f[m - 1] = (1.0 + gVal) * h;
    }
}
