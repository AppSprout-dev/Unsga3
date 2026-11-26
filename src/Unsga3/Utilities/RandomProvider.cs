namespace Unsga3.Utilities;

/// <summary>Seeded random source for reproducible evolutionary runs.</summary>
public sealed class RandomProvider
{
    private readonly Random _rng;

    public RandomProvider(int? seed = null)
    {
        _rng = seed.HasValue ? new Random(seed.Value) : new Random();
        Seed = seed;
    }

    public int? Seed { get; }

    public double NextDouble() => _rng.NextDouble();

    public double NextDouble(double minInclusive, double maxExclusive)
    {
        if (!double.IsFinite(minInclusive))
            throw new ArgumentOutOfRangeException(nameof(minInclusive), "Lower bound must be finite.");
        if (!double.IsFinite(maxExclusive))
            throw new ArgumentOutOfRangeException(nameof(maxExclusive), "Upper bound must be finite.");
        if (minInclusive > maxExclusive)
            throw new ArgumentOutOfRangeException(nameof(maxExclusive), "Upper bound must be >= lower bound.");

        double span = maxExclusive - minInclusive;
        double u = _rng.NextDouble();
        if (double.IsInfinity(span))
            return minInclusive * (1.0 - u) + maxExclusive * u;
        return minInclusive + span * u;
    }

    public int Next(int maxExclusive) => _rng.Next(maxExclusive);

    public int Next(int minInclusive, int maxExclusive) => _rng.Next(minInclusive, maxExclusive);

    /// <summary>Uniform integer in [0, n) distinct from <paramref name="exclude"/> when n &gt; 1.</summary>
    public int NextExcept(int n, int exclude)
    {
        if (n <= 0)
            throw new ArgumentOutOfRangeException(nameof(n), "Upper bound must be positive.");
        if (n == 1) return 0;
        if (exclude < 0 || exclude >= n)
            return _rng.Next(n);
        int v = _rng.Next(n - 1);
        return v >= exclude ? v + 1 : v;
    }
}
