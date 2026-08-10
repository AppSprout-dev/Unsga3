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

    public double NextDouble(double minInclusive, double maxExclusive) =>
        minInclusive + (maxExclusive - minInclusive) * _rng.NextDouble();

    public int Next(int maxExclusive) => _rng.Next(maxExclusive);

    public int Next(int minInclusive, int maxExclusive) => _rng.Next(minInclusive, maxExclusive);

    /// <summary>Uniform integer in [0, n) distinct from <paramref name="exclude"/> when n &gt; 1.</summary>
    public int NextExcept(int n, int exclude)
    {
        if (n <= 1) return 0;
        int v = _rng.Next(n - 1);
        return v >= exclude ? v + 1 : v;
    }
}
