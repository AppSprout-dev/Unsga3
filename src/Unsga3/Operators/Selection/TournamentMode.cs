namespace Unsga3.Operators.Selection;

/// <summary>Mating tournament policy for U-NSGA-III.</summary>
public enum TournamentMode
{
    /// <summary>
    /// Rank, then niche count, then perpendicular distance (constructor default).
    /// Niche count is compared even when the two parents sit on different reference
    /// directions. That is not Seada &amp; Deb Algorithm 2, which picks at random
    /// across directions. The default is intentionally unchanged.
    /// </summary>
    RankNicheDistance = 0,

    /// <summary>
    /// Same-niche / different-niche split from Seada &amp; Deb Algorithm 2 and from
    /// pymoo <c>comp_by_rank_and_ref_line_dist</c>: constraint violation first; if the
    /// parents share a niche then rank, then distance-to-niche; otherwise random.
    /// A distance tie is a coin flip (pymoo). Algorithm 2 keeps the second parent on that tie.
    /// </summary>
    PymooCompatible = 1,
}
