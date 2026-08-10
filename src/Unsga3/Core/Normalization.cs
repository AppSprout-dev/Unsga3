using Unsga3.Algorithm;

namespace Unsga3.Core;

/// <summary>
/// NSGA-III style adaptive normalization: ideal point + intercept-based hyperplane scaling.
/// </summary>
public sealed class Normalization
{
    private readonly int _m;
    private readonly double[] _ideal;
    private readonly double[] _intercepts;
    private readonly double[] _nadir;

    public Normalization(int nObjectives)
    {
        if (nObjectives < 1) throw new ArgumentOutOfRangeException(nameof(nObjectives));
        _m = nObjectives;
        _ideal = new double[_m];
        _intercepts = new double[_m];
        _nadir = new double[_m];
        for (int i = 0; i < _m; i++)
        {
            _ideal[i] = double.PositiveInfinity;
            _intercepts[i] = 1.0;
            _nadir[i] = 1.0;
        }
    }

    public IReadOnlyList<double> IdealPoint => _ideal;
    public IReadOnlyList<double> Intercepts => _intercepts;

    /// <summary>
    /// Update ideal/nadir/intercepts from the current population and return
    /// normalized objective vectors (one per individual, same order).
    /// </summary>
    public double[][] Normalize(IReadOnlyList<Individual> population)
    {
        ArgumentNullException.ThrowIfNull(population);
        int n = population.Count;
        if (n == 0) return Array.Empty<double[]>();

        for (int j = 0; j < _m; j++)
        {
            _ideal[j] = double.PositiveInfinity;
            _nadir[j] = double.NegativeInfinity;
        }

        for (int i = 0; i < n; i++)
        {
            var f = population[i].Objectives;
            for (int j = 0; j < _m; j++)
            {
                if (f[j] < _ideal[j]) _ideal[j] = f[j];
                if (f[j] > _nadir[j]) _nadir[j] = f[j];
            }
        }

        // Translate.
        var translated = new double[n][];
        for (int i = 0; i < n; i++)
        {
            translated[i] = new double[_m];
            for (int j = 0; j < _m; j++)
                translated[i][j] = population[i].Objectives[j] - _ideal[j];
        }

        // Extreme points via ASF (Deb & Jain NSGA-III).
        var extreme = new double[_m][];
        for (int j = 0; j < _m; j++)
        {
            extreme[j] = (double[])translated[0].Clone();
            double bestAsf = Asf(translated[0], j);
            for (int i = 1; i < n; i++)
            {
                double asf = Asf(translated[i], j);
                if (asf < bestAsf)
                {
                    bestAsf = asf;
                    extreme[j] = (double[])translated[i].Clone();
                }
            }
        }

        // Intercepts from hyperplane through extreme points; fall back to nadir-ideal.
        if (!TryIntercepts(extreme, _intercepts))
        {
            for (int j = 0; j < _m; j++)
            {
                double span = _nadir[j] - _ideal[j];
                _intercepts[j] = span > 1e-12 ? span : 1.0;
            }
        }

        for (int j = 0; j < _m; j++)
        {
            if (_intercepts[j] < 1e-12)
                _intercepts[j] = 1.0;
        }

        var normalized = new double[n][];
        for (int i = 0; i < n; i++)
        {
            normalized[i] = new double[_m];
            for (int j = 0; j < _m; j++)
                normalized[i][j] = translated[i][j] / _intercepts[j];
        }

        return normalized;
    }

    private static double Asf(double[] f, int axis)
    {
        double max = double.NegativeInfinity;
        for (int j = 0; j < f.Length; j++)
        {
            double w = j == axis ? 1e-6 : 1.0;
            double v = f[j] / w;
            if (v > max) max = v;
        }
        return max;
    }

    private static bool TryIntercepts(double[][] extreme, double[] intercepts)
    {
        int m = intercepts.Length;
        // Solve extreme^T * b = 1, intercept_j = 1/b_j
        var a = new double[m, m];
        var b = new double[m];
        for (int i = 0; i < m; i++)
        {
            b[i] = 1.0;
            for (int j = 0; j < m; j++)
                a[i, j] = extreme[i][j];
        }

        if (!SolveLinear(a, b, out var x))
            return false;

        for (int j = 0; j < m; j++)
        {
            if (Math.Abs(x[j]) < 1e-12 || x[j] < 0)
                return false;
            intercepts[j] = 1.0 / x[j];
            if (double.IsNaN(intercepts[j]) || double.IsInfinity(intercepts[j]) || intercepts[j] <= 0)
                return false;
        }
        return true;
    }

    /// <summary>Gaussian elimination for A x = b (square).</summary>
    private static bool SolveLinear(double[,] a, double[] b, out double[] x)
    {
        int n = b.Length;
        x = new double[n];
        var m = new double[n, n + 1];
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
                m[i, j] = a[i, j];
            m[i, n] = b[i];
        }

        for (int col = 0; col < n; col++)
        {
            int pivot = col;
            double best = Math.Abs(m[col, col]);
            for (int row = col + 1; row < n; row++)
            {
                double v = Math.Abs(m[row, col]);
                if (v > best)
                {
                    best = v;
                    pivot = row;
                }
            }
            if (best < 1e-14) return false;

            if (pivot != col)
            {
                for (int j = col; j <= n; j++)
                    (m[col, j], m[pivot, j]) = (m[pivot, j], m[col, j]);
            }

            double div = m[col, col];
            for (int j = col; j <= n; j++)
                m[col, j] /= div;

            for (int row = 0; row < n; row++)
            {
                if (row == col) continue;
                double factor = m[row, col];
                for (int j = col; j <= n; j++)
                    m[row, j] -= factor * m[col, j];
            }
        }

        for (int i = 0; i < n; i++)
            x[i] = m[i, n];
        return true;
    }
}
