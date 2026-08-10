using Unsga3.Core;

namespace Unsga3.Problems;

/// <summary>ZDT1 — convex front. n=30, x∈[0,1]^n (Deb et al.).</summary>
public sealed class Zdt1Problem : ProblemBase
{
    public Zdt1Problem(int nVariables = 30)
        : base(nVariables, 2, 0, Uniform(nVariables, 0, 1)) { }

    protected override void EvaluateCore(double[] x, double[] f, double[] g)
    {
        f[0] = x[0];
        double gv = GLinear(x);
        f[1] = gv * (1.0 - Math.Sqrt(x[0] / gv));
    }

    internal static double GLinear(double[] x)
    {
        double s = 0;
        for (int i = 1; i < x.Length; i++) s += x[i];
        return 1.0 + 9.0 * s / (x.Length - 1);
    }

    internal static (double Lower, double Upper)[] Uniform(int n, double lo, double hi) =>
        Enumerable.Repeat((lo, hi), n).ToArray();
}

/// <summary>ZDT2 — non-convex front.</summary>
public sealed class Zdt2Problem : ProblemBase
{
    public Zdt2Problem(int nVariables = 30)
        : base(nVariables, 2, 0, Zdt1Problem.Uniform(nVariables, 0, 1)) { }

    protected override void EvaluateCore(double[] x, double[] f, double[] g)
    {
        f[0] = x[0];
        double gv = Zdt1Problem.GLinear(x);
        double r = x[0] / gv;
        f[1] = gv * (1.0 - r * r);
    }
}

/// <summary>ZDT3 — disconnected front.</summary>
public sealed class Zdt3Problem : ProblemBase
{
    public Zdt3Problem(int nVariables = 30)
        : base(nVariables, 2, 0, Zdt1Problem.Uniform(nVariables, 0, 1)) { }

    protected override void EvaluateCore(double[] x, double[] f, double[] g)
    {
        f[0] = x[0];
        double gv = Zdt1Problem.GLinear(x);
        double r = x[0] / gv;
        f[1] = gv * (1.0 - Math.Sqrt(r) - r * Math.Sin(10.0 * Math.PI * x[0]));
    }
}

/// <summary>ZDT4 — multimodal; x0∈[0,1], x1..∈[-5,5].</summary>
public sealed class Zdt4Problem : ProblemBase
{
    public Zdt4Problem(int nVariables = 10)
        : base(nVariables, 2, 0, BuildBounds(nVariables)) { }

    private static (double Lower, double Upper)[] BuildBounds(int n)
    {
        var b = new (double, double)[n];
        b[0] = (0, 1);
        for (int i = 1; i < n; i++) b[i] = (-5, 5);
        return b;
    }

    protected override void EvaluateCore(double[] x, double[] f, double[] g)
    {
        f[0] = x[0];
        double gv = 1.0 + 10.0 * (x.Length - 1);
        for (int i = 1; i < x.Length; i++)
            gv += x[i] * x[i] - 10.0 * Math.Cos(4.0 * Math.PI * x[i]);
        f[1] = gv * (1.0 - Math.Sqrt(x[0] / gv));
    }
}

/// <summary>ZDT6 — non-uniform density, non-convex.</summary>
public sealed class Zdt6Problem : ProblemBase
{
    public Zdt6Problem(int nVariables = 10)
        : base(nVariables, 2, 0, Zdt1Problem.Uniform(nVariables, 0, 1)) { }

    protected override void EvaluateCore(double[] x, double[] f, double[] g)
    {
        f[0] = 1.0 - Math.Exp(-4.0 * x[0]) * Math.Pow(Math.Sin(6.0 * Math.PI * x[0]), 6.0);
        double s = 0;
        for (int i = 1; i < x.Length; i++) s += x[i];
        double gv = 1.0 + 9.0 * Math.Pow(s / (x.Length - 1), 0.25);
        double r = f[0] / gv;
        f[1] = gv * (1.0 - r * r);
    }
}
