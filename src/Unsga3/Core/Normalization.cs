using Unsga3.Algorithm;

namespace Unsga3.Core;

/// <summary>
/// NSGA-III adaptive hyperplane normalization (Deb &amp; Jain), aligned with pymoo
/// <c>HyperplaneNormalization</c>:
/// persistent ideal / worst points, ASF extreme points (optionally from the ND front),
/// intercept-based nadir with front/population fallbacks.
/// </summary>
public sealed class Normalization
{
    private readonly int _m;
    private readonly double[] _ideal;
    private readonly double[] _worst;
    private readonly double[] _nadir;
    private readonly double[] _intercepts;
    private double[][]? _extremePoints;

    /// <summary>
    /// Values below this (after ideal translation) are treated as 0 when scoring ASF,
    /// matching pymoo's <c>__F[__F &lt; 1e-3] = 0</c> numerical guard.
    /// </summary>
    private const double AsfFloor = 1e-3;

    public Normalization(int nObjectives)
    {
        if (nObjectives < 1) throw new ArgumentOutOfRangeException(nameof(nObjectives));
        _m = nObjectives;
        _ideal = new double[_m];
        _worst = new double[_m];
        _nadir = new double[_m];
        _intercepts = new double[_m];
        for (int i = 0; i < _m; i++)
        {
            _ideal[i] = double.PositiveInfinity;
            _worst[i] = double.NegativeInfinity;
            _nadir[i] = 1.0;
            _intercepts[i] = 1.0;
        }
    }

    public IReadOnlyList<double> IdealPoint => _ideal;
    public IReadOnlyList<double> NadirPoint => _nadir;
    public IReadOnlyList<double> Intercepts => _intercepts;

    /// <summary>
    /// Update ideal / nadir from <paramref name="population"/> and return normalized
    /// objective vectors (same order). When <paramref name="nonDominatedIndices"/> is
    /// provided, extreme points are sought only among that subset (pymoo / NSGA-III);
    /// otherwise the whole population is used.
    /// </summary>
    public double[][] Normalize(
        IReadOnlyList<Individual> population,
        IReadOnlyList<int>? nonDominatedIndices = null)
    {
        ArgumentNullException.ThrowIfNull(population);
        int n = population.Count;
        if (n == 0) return Array.Empty<double[]>();

        // Persistent ideal / worst over the run (pymoo: never loses the best ideal).
        for (int i = 0; i < n; i++)
        {
            var f = population[i].Objectives;
            for (int j = 0; j < _m; j++)
            {
                if (f[j] < _ideal[j]) _ideal[j] = f[j];
                if (f[j] > _worst[j]) _worst[j] = f[j];
            }
        }

        // Extreme-point search set: ND front when supplied, else all.
        int[] ndIdx;
        if (nonDominatedIndices is { Count: > 0 })
        {
            ndIdx = new int[nonDominatedIndices.Count];
            for (int i = 0; i < nonDominatedIndices.Count; i++)
                ndIdx[i] = nonDominatedIndices[i];
        }
        else
        {
            ndIdx = new int[n];
            for (int i = 0; i < n; i++) ndIdx[i] = i;
        }

        UpdateExtremePoints(population, ndIdx);
        UpdateNadir(population, ndIdx);

        var normalized = new double[n][];
        for (int i = 0; i < n; i++)
        {
            normalized[i] = new double[_m];
            var f = population[i].Objectives;
            for (int j = 0; j < _m; j++)
            {
                double denom = _nadir[j] - _ideal[j];
                if (denom < 1e-12) denom = 1.0;
                normalized[i][j] = (f[j] - _ideal[j]) / denom;
            }
        }

        // Expose intercepts as (nadir - ideal) for diagnostics / older call sites.
        for (int j = 0; j < _m; j++)
        {
            double span = _nadir[j] - _ideal[j];
            _intercepts[j] = span > 1e-12 ? span : 1.0;
        }

        return normalized;
    }

    /// <summary>Reset persistent state (tests / fresh run). Algorithm creates a new instance per run.</summary>
    public void Reset()
    {
        for (int i = 0; i < _m; i++)
        {
            _ideal[i] = double.PositiveInfinity;
            _worst[i] = double.NegativeInfinity;
            _nadir[i] = 1.0;
            _intercepts[i] = 1.0;
        }
        _extremePoints = null;
    }

    private void UpdateExtremePoints(IReadOnlyList<Individual> population, int[] ndIdx)
    {
        // Build candidate matrix = previous extremes ∪ current ND (raw objectives).
        int nCand = ndIdx.Length + (_extremePoints?.Length ?? 0);
        var cand = new double[nCand][];
        int k = 0;
        if (_extremePoints is not null)
        {
            for (int i = 0; i < _extremePoints.Length; i++)
                cand[k++] = (double[])_extremePoints[i].Clone();
        }
        for (int i = 0; i < ndIdx.Length; i++)
            cand[k++] = (double[])population[ndIdx[i]].Objectives.Clone();

        var extreme = new double[_m][];
        for (int axis = 0; axis < _m; axis++)
        {
            int best = 0;
            // ASF on ideal-translated objectives (pymoo __F = F - ideal).
            double bestAsf = Asf(cand[0], axis, _ideal);
            for (int i = 1; i < cand.Length; i++)
            {
                double asf = Asf(cand[i], axis, _ideal);
                if (asf < bestAsf)
                {
                    bestAsf = asf;
                    best = i;
                }
            }
            extreme[axis] = (double[])cand[best].Clone();
        }

        _extremePoints = extreme;
    }

    private void UpdateNadir(IReadOnlyList<Individual> population, int[] ndIdx)
    {
        // Worst of front / population this generation.
        var worstOfFront = new double[_m];
        var worstOfPop = new double[_m];
        for (int j = 0; j < _m; j++)
        {
            worstOfFront[j] = double.NegativeInfinity;
            worstOfPop[j] = double.NegativeInfinity;
        }
        for (int i = 0; i < population.Count; i++)
        {
            var f = population[i].Objectives;
            for (int j = 0; j < _m; j++)
                if (f[j] > worstOfPop[j]) worstOfPop[j] = f[j];
        }
        for (int i = 0; i < ndIdx.Length; i++)
        {
            var f = population[ndIdx[i]].Objectives;
            for (int j = 0; j < _m; j++)
                if (f[j] > worstOfFront[j]) worstOfFront[j] = f[j];
        }

        // Hyperplane through extreme points → intercepts; nadir = ideal + intercepts.
        if (_extremePoints is not null && TryIntercepts(_extremePoints, _ideal, out var intercepts))
        {
            for (int j = 0; j < _m; j++)
            {
                _nadir[j] = _ideal[j] + intercepts[j];
                // pymoo: if computed nadir exceeds global worst, clamp to worst.
                if (_nadir[j] > _worst[j])
                    _nadir[j] = _worst[j];
            }
        }
        else
        {
            for (int j = 0; j < _m; j++)
                _nadir[j] = worstOfFront[j];
        }

        // Degenerate range → fall back to worst of population.
        for (int j = 0; j < _m; j++)
        {
            if (_nadir[j] - _ideal[j] <= 1e-6)
                _nadir[j] = worstOfPop[j];
            if (_nadir[j] - _ideal[j] <= 1e-6)
                _nadir[j] = _ideal[j] + 1.0;
        }
    }

    /// <summary>
    /// Achievement scalarizing function for extreme-point selection.
    /// Preferred axis weight = 1, other axes weight = 1e-6 (divide form), equivalent to
    /// pymoo's multiply form with preferred=1 / others=1e6. Finds the point nearest the
    /// preferred objective axis (other objectives near zero).
    /// </summary>
    internal static double Asf(double[] f, int axis, double[]? ideal = null)
    {
        double max = double.NegativeInfinity;
        for (int j = 0; j < f.Length; j++)
        {
            double v = ideal is null ? f[j] : f[j] - ideal[j];
            if (v < AsfFloor) v = 0.0;
            // Preferred axis: large weight when dividing → f/1; others: f/1e-6.
            double w = j == axis ? 1.0 : 1e-6;
            double scaled = v / w;
            if (scaled > max) max = scaled;
        }
        return max;
    }

    /// <summary>Solve (E − ideal)·b = 1 → intercept_j = 1/b_j.</summary>
    private static bool TryIntercepts(double[][] extreme, double[] ideal, out double[] intercepts)
    {
        int m = ideal.Length;
        intercepts = new double[m];
        var a = new double[m, m];
        var b = new double[m];
        for (int i = 0; i < m; i++)
        {
            b[i] = 1.0;
            for (int j = 0; j < m; j++)
                a[i, j] = extreme[i][j] - ideal[j];
        }

        if (!SolveLinear(a, b, out var x))
            return false;

        for (int j = 0; j < m; j++)
        {
            if (Math.Abs(x[j]) < 1e-12 || x[j] < 0)
                return false;
            intercepts[j] = 1.0 / x[j];
            if (double.IsNaN(intercepts[j]) || double.IsInfinity(intercepts[j]) || intercepts[j] <= 1e-6)
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
