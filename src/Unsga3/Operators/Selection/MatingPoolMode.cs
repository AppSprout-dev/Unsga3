namespace Unsga3.Operators.Selection;

/// <summary>
/// How the mating pool is built before SBX. The comparator is
/// <see cref="TournamentMode"/> and is chosen separately.
/// </summary>
public enum MatingPoolMode
{
    /// <summary>
    /// N independent binary tournaments with replacement (constructor default).
    /// Contestants inside one tournament are distinct. Across tournaments the
    /// same member can win many times and others can miss the generation.
    /// This is the pool behind the published ZDT1 Wilcoxon table.
    /// </summary>
    IndependentWithReplacement = 0,

    /// <summary>
    /// Two shuffled passes of consecutive pairs (Seada &amp; Deb Algorithm 1 and
    /// pymoo 0.6.2 <c>TournamentSelection</c> with pressure 2). Opt-in.
    /// When the number of parents equals the population size, each member is a
    /// contestant twice. This is not the published ZDT1 protocol.
    /// </summary>
    TwoShuffledPasses = 1,
}
