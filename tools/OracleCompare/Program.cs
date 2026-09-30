using System.Globalization;
using System.Text.Json;
using Unsga3.Algorithm;
using Unsga3.Core;
using Unsga3.Metrics;
using Unsga3.Operators.Selection;
using Unsga3.Problems;
using Unsga3.Utilities;

// Fixed-protocol C# side of the pymoo oracle (see tools/oracle/run_pymoo_oracle.py
// and tools/oracle/run_new_surfaces.py).
// IGD is scored on the full feasible non-dominated front when this process has a
// reference front. pymoo's script scores res.F unless the new-surfaces harness
// re-scores both populations itself.
//
//   dotnet run --project tools/OracleCompare -- --problem zdt1 --partitions 12 --pop 52 --gens 100 --seed 1
//   # ZDT2 quality protocol (matches unsga3-bend A/B): gens=250 + --pymoo-mode.
//   # Omitted --gens on --problem zdt2 is 250. gens=100 is an early-stress snapshot.
//   dotnet run --project tools/OracleCompare -- --problem zdt2 --partitions 12 --pop 52 --seed 1 --pymoo-mode
//
// Mating opt-ins (package defaults stay rank-niche + independent):
//   --tournament algorithm2 --mating two-shuffled --seed 1 --seed-count 15

string problemName = "zdt1";
int partitions = 12;
int? pop = null;
int gens = 100;
bool gensExplicit = false;
int seedStart = 1;
int seedCount = 1;
bool pymooMode = false;
string? tournamentArg = null;
string? matingArg = null;
string? outDir = null;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--problem": problemName = args[++i]; break;
        case "--partitions": partitions = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
        case "--pop": pop = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
        case "--gens":
            gens = int.Parse(args[++i], CultureInfo.InvariantCulture);
            gensExplicit = true;
            break;
        case "--seed": seedStart = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
        case "--seed-count": seedCount = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
        case "--pymoo-mode": pymooMode = true; break;
        case "--tournament": tournamentArg = args[++i]; break;
        case "--mating": matingArg = args[++i]; break;
        case "--out-dir": outDir = args[++i]; break;
        default:
            Console.Error.WriteLine($"Unknown argument: {args[i]}");
            return 1;
    }
}

if (seedCount < 1)
{
    Console.Error.WriteLine("--seed-count must be at least 1.");
    return 1;
}

// Quality protocol: ZDT2 A/B default is gens=250 (unsga3-bend honesty).
// Constrained bi-objective uses that same 250. C1-DTLZ1 uses the DTLZ2 budget (150).
// ZDT1 stays 100. Explicit --gens always wins.
if (!gensExplicit)
{
    if (problemName.Equals("zdt2", StringComparison.OrdinalIgnoreCase)
        || problemName.Equals("osy", StringComparison.OrdinalIgnoreCase)
        || problemName.Equals("tnk", StringComparison.OrdinalIgnoreCase))
        gens = 250;
    else if (problemName.Equals("c1dtlz1", StringComparison.OrdinalIgnoreCase))
        gens = 150;
}

TournamentMode mode = tournamentArg is not null
    ? ParseTournament(tournamentArg)
    : pymooMode ? TournamentMode.PymooCompatible : TournamentMode.RankNicheDistance;
MatingPoolMode mating = matingArg is not null
    ? ParseMating(matingArg)
    : MatingPoolMode.IndependentWithReplacement;

if (!TryCreateProblem(problemName, partitions, out IProblem problem, out int m, out double[][]? pf))
{
    Console.Error.WriteLine($"Unknown problem: {problemName}");
    return 1;
}

var dirs = ReferenceDirections.DasDennis(m, partitions);
int popSize = pop ?? dirs.Length;
string tournamentSlug = TournamentSlug(mode);
string matingSlug = MatingSlug(mating);

Console.WriteLine(
    $"Unsga3 | problem={problemName} M={m} n_var={problem.NumberOfVariables} refs={dirs.Length} pop={popSize} gens={gens} seed={seedStart} seed_count={seedCount} tournament={mode} mating={mating}");

string dir = outDir ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "oracle", "out");
dir = Path.GetFullPath(dir);
Directory.CreateDirectory(dir);

for (int s = 0; s < seedCount; s++)
{
    int seed = seedStart + s;
    var algo = new Unsga3Algorithm(
        dirs,
        popSize,
        seed: seed,
        tournamentMode: mode,
        matingPool: mating);
    var result = algo.Run(problem, gens);

    var population = result.FinalPopulation;
    int feasibleCount = 0;
    var feasibleNd = new List<double[]>();
    foreach (var individual in result.NonDominatedSolutions)
    {
        if (!individual.IsFeasible)
            continue;
        feasibleNd.Add((double[])individual.Objectives.Clone());
    }

    foreach (var individual in population)
    {
        if (individual.IsFeasible)
            feasibleCount++;
    }

    // Score only when a local reference front exists. OSY and TNK have none here;
    // tools/oracle/run_new_surfaces.py scores those from the population file.
    double? igd = null;
    if (pf is not null && feasibleNd.Count > 0)
        igd = PerformanceIndicators.InvertedGenerationalDistance(feasibleNd, pf);

    // The (1.1, 1.1) reference is the locked ZDT point. OSY and TNK are not on that scale.
    bool zdtHv = problemName.Equals("zdt1", StringComparison.OrdinalIgnoreCase)
        || problemName.Equals("zdt2", StringComparison.OrdinalIgnoreCase);
    double? hv = zdtHv && feasibleNd.Count > 0
        ? PerformanceIndicators.Hypervolume2D(feasibleNd, new[] { 1.1, 1.1 })
        : null;

    string stem = $"csharp_{problemName}_p{partitions}_pop{popSize}_g{gens}_s{seed}_{tournamentSlug}_{matingSlug}";
    string popPath = Path.Combine(dir, $"{stem}_pop.csv");
    using (var sw = new StreamWriter(popPath))
    {
        foreach (var individual in population)
        {
            var cells = new string[individual.Objectives.Length + 1];
            for (int j = 0; j < individual.Objectives.Length; j++)
                cells[j] = individual.Objectives[j].ToString("G17", CultureInfo.InvariantCulture);
            cells[^1] = individual.ConstraintViolation.ToString("G17", CultureInfo.InvariantCulture);
            sw.WriteLine(string.Join(",", cells));
        }
    }

    string fPath = Path.Combine(dir, $"{stem}_F.csv");
    using (var sw = new StreamWriter(fPath))
    {
        foreach (var row in feasibleNd)
            sw.WriteLine(string.Join(",", row.Select(v => v.ToString("G17", CultureInfo.InvariantCulture))));
    }

    var meta = new Dictionary<string, object?>
    {
        ["source"] = "Unsga3",
        ["algorithm"] = "Unsga3Algorithm",
        ["tournament"] = mode.ToString(),
        ["mating"] = mating.ToString(),
        ["problem"] = problemName,
        ["n_obj"] = m,
        ["n_var"] = problem.NumberOfVariables,
        ["partitions"] = partitions,
        ["n_ref_dirs"] = dirs.Length,
        ["pop_size"] = popSize,
        ["n_gen"] = gens,
        ["seed"] = seed,
        ["n_population"] = population.Count,
        ["n_feasible"] = feasibleCount,
        ["n_solutions"] = feasibleNd.Count,
        ["front_definition"] = "non_dominated_feasible",
        ["igd"] = igd,
        ["igd_reference"] = pf is null ? "none_local" : "library_pareto_front",
        ["hv2"] = hv,
        ["F_csv"] = Path.GetFileName(fPath),
        ["pop_csv"] = Path.GetFileName(popPath),
    };
    string metaPath = Path.Combine(dir, $"{stem}_meta.json");
    File.WriteAllText(metaPath, JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true }));

    string igdText = igd is double measured
        ? measured.ToString("G17", CultureInfo.InvariantCulture)
        : "skip";
    string igdG6 = igd is double measuredG6
        ? measuredG6.ToString("G6", CultureInfo.InvariantCulture)
        : "skip";
    if (seedCount == 1)
    {
        Console.WriteLine($"front=non_dominated n={feasibleNd.Count}");
        Console.WriteLine($"front_size={feasibleNd.Count}");
        Console.WriteLine($"feasible={feasibleCount}");
        Console.WriteLine(igd is null ? "IGD=skip" : $"IGD={igdG6}");
        if (hv is double h)
            Console.WriteLine($"HV2(r=1.1,1.1)={h.ToString("G6", CultureInfo.InvariantCulture)}");
        Console.WriteLine($"wrote {fPath}");
        Console.WriteLine($"wrote {popPath}");
        Console.WriteLine($"wrote {metaPath}");
    }

    Console.WriteLine(
        $"RESULT seed={seed.ToString(CultureInfo.InvariantCulture)} igd={igdText} igd_g6={igdG6} n={feasibleNd.Count.ToString(CultureInfo.InvariantCulture)} feasible={feasibleCount.ToString(CultureInfo.InvariantCulture)} pop_csv={Path.GetFileName(popPath)}");
}

return 0;

static bool TryCreateProblem(string problemName, int partitions, out IProblem problem, out int m, out double[][]? pf)
{
    switch (problemName.ToLowerInvariant())
    {
        case "zdt1":
            problem = new Zdt1Problem();
            m = 2;
            pf = ParetoFronts.Zdt1(500);
            return true;
        case "zdt2":
            problem = new Zdt2Problem();
            m = 2;
            pf = ParetoFronts.Zdt2(500);
            return true;
        case "dtlz2":
            problem = new Dtlz2Problem(nObjectives: 3, k: 10);
            m = 3;
            pf = ParetoFronts.Dtlz2(3, partitions);
            return true;
        case "osy":
            problem = new OsyProblem();
            m = 2;
            pf = null;
            return true;
        case "tnk":
            problem = new TnkProblem();
            m = 2;
            pf = null;
            return true;
        case "c1dtlz1":
            // k=5 matches C1Dtlz1Problem / Dtlz1Problem (n_var = 7 when M=3).
            // pymoo get_problem("c1dtlz1") defaults to n_var=12. The new-surfaces
            // harness passes n_var=7. The reference front is the DTLZ1 simplex;
            // the C1 inequality does not remove that front.
            problem = new C1Dtlz1Problem(nObjectives: 3, k: 5);
            m = 3;
            pf = ParetoFronts.Dtlz1(3, partitions);
            return true;
        default:
            problem = null!;
            m = 0;
            pf = null;
            return false;
    }
}

static TournamentMode ParseTournament(string value)
{
    switch (value.ToLowerInvariant())
    {
        case "default":
        case "rank-niche":
        case "rankniche":
            return TournamentMode.RankNicheDistance;
        case "pymoo":
        case "pymoo-compatible":
            return TournamentMode.PymooCompatible;
        case "algorithm2":
        case "algorithm-2":
            return TournamentMode.Algorithm2;
        default:
            throw new ArgumentException(
                $"Unknown --tournament '{value}'. Expected rank-niche, pymoo, or algorithm2.");
    }
}

static MatingPoolMode ParseMating(string value)
{
    switch (value.ToLowerInvariant())
    {
        case "default":
        case "independent":
            return MatingPoolMode.IndependentWithReplacement;
        case "two-shuffled":
        case "two-shuffle":
        case "twoshuffle":
            return MatingPoolMode.TwoShuffledPasses;
        default:
            throw new ArgumentException(
                $"Unknown --mating '{value}'. Expected independent or two-shuffled.");
    }
}

static string TournamentSlug(TournamentMode mode) => mode switch
{
    TournamentMode.RankNicheDistance => "rankniche",
    TournamentMode.PymooCompatible => "pymoo",
    TournamentMode.Algorithm2 => "algorithm2",
    _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unhandled tournament mode."),
};

static string MatingSlug(MatingPoolMode mode) => mode switch
{
    MatingPoolMode.IndependentWithReplacement => "independent",
    MatingPoolMode.TwoShuffledPasses => "twoshuffle",
    _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unhandled mating pool."),
};
