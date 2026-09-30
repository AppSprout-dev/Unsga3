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
    /// A distance tie is a coin flip (pymoo). Algorithm 2 keeps the second parent on
    /// that tie; use <see cref="Algorithm2"/> for the paper rule. This mode's coin flip
    /// is unchanged.
    /// </summary>
    PymooCompatible = 1,

    /// <summary>
    /// Seada &amp; Deb Algorithm 2 comparator. Same rules as
    /// <see cref="PymooCompatible"/>, except a same-niche perpendicular-distance tie
    /// keeps the second parent instead of a coin flip. Opt-in. It does not change
    /// <see cref="PymooCompatible"/> or <see cref="RankNicheDistance"/>. Usable with
    /// either <see cref="MatingPoolMode"/>.
    /// </summary>
    Algorithm2 = 2,
}
