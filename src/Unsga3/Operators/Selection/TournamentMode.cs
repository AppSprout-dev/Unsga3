namespace Unsga3.Operators.Selection;

/// <summary>Mating tournament policy for U-NSGA-III.</summary>
public enum TournamentMode
{
    /// <summary>
    /// Rank → niche count → perpendicular distance (default).
    /// Stronger selection pressure across niches than stock pymoo.
    /// </summary>
    RankNicheDistance = 0,

    /// <summary>
    /// Matches pymoo <c>comp_by_rank_and_ref_line_dist</c>:
    /// CV first; if same niche then rank then dist-to-niche; else random.
    /// Use for oracle / equivalence runs against pymoo.
    /// </summary>
    PymooCompatible = 1,
}
