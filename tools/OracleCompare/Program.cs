using System.Globalization;
using System.Text.Json;
using Unsga3.Algorithm;
using Unsga3.Core;
using Unsga3.Metrics;
using Unsga3.Operators.Selection;
using Unsga3.Problems;
using Unsga3.Utilities;

// Fixed-protocol C# side of the pymoo oracle (see tools/oracle/run_pymoo_oracle.py).
//
//   dotnet run --project tools/OracleCompare -- --problem zdt1 --partitions 12 --pop 52 --gens 100 --seed 1 --pymoo-mode

string problemName = "zdt1";
int partitions = 12;
int? pop = null;
int gens = 100;
int seed = 1;
bool pymooMode = false;
string? outDir = null;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--problem": problemName = args[++i]; break;
        case "--partitions": partitions = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
        case "--pop": pop = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
        case "--gens": gens = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
        case "--seed": seed = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
        case "--pymoo-mode": pymooMode = true; break;
        case "--out-dir": outDir = args[++i]; break;
    }
}

IProblem problem;
int m;
double[][] pf;
switch (problemName.ToLowerInvariant())
{
    case "zdt1":
        problem = new Zdt1Problem();
        m = 2;
        pf = ParetoFronts.Zdt1(500);
        break;
    case "zdt2":
        problem = new Zdt2Problem();
        m = 2;
        pf = ParetoFronts.Zdt2(500);
        break;
    case "dtlz2":
        problem = new Dtlz2Problem(nObjectives: 3, k: 10);
        m = 3;
        pf = ParetoFronts.Dtlz2(3, partitions);
        break;
    default:
        Console.Error.WriteLine($"Unknown problem: {problemName}");
        return 1;
}

var dirs = ReferenceDirections.DasDennis(m, partitions);
int popSize = pop ?? dirs.Length;
var mode = pymooMode ? TournamentMode.PymooCompatible : TournamentMode.RankNicheDistance;

Console.WriteLine(
    $"Unsga3 | problem={problemName} M={m} refs={dirs.Length} pop={popSize} gens={gens} seed={seed} tournament={mode}");

var algo = new Unsga3Algorithm(dirs, popSize, seed: seed, tournamentMode: mode);
var result = algo.Run(problem, gens);

var obtained = result.NonDominatedSolutions
    .Where(i => i.IsFeasible)
    .Select(i => (double[])i.Objectives.Clone())
    .ToArray();
if (obtained.Length == 0)
    obtained = result.FinalPopulation.Select(i => (double[])i.Objectives.Clone()).ToArray();

double igd = PerformanceIndicators.InvertedGenerationalDistance(obtained, pf);
double? hv = m == 2
    ? PerformanceIndicators.Hypervolume2D(obtained, new[] { 1.1, 1.1 })
    : null;

Console.WriteLine($"front_size={obtained.Length}");
Console.WriteLine($"IGD={igd.ToString("G6", CultureInfo.InvariantCulture)}");
if (hv is double h)
    Console.WriteLine($"HV2(r=1.1,1.1)={h.ToString("G6", CultureInfo.InvariantCulture)}");

string dir = outDir ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "oracle", "out");
dir = Path.GetFullPath(dir);
Directory.CreateDirectory(dir);
string stem = $"csharp_{problemName}_p{partitions}_pop{popSize}_g{gens}_s{seed}_{(pymooMode ? "pymoo" : "default")}";
string fPath = Path.Combine(dir, $"{stem}_F.csv");
using (var sw = new StreamWriter(fPath))
{
    foreach (var row in obtained)
        sw.WriteLine(string.Join(",", row.Select(v => v.ToString("G17", CultureInfo.InvariantCulture))));
}

var meta = new Dictionary<string, object?>
{
    ["source"] = "Unsga3",
    ["algorithm"] = "Unsga3Algorithm",
    ["tournament"] = mode.ToString(),
    ["problem"] = problemName,
    ["n_obj"] = m,
    ["partitions"] = partitions,
    ["n_ref_dirs"] = dirs.Length,
    ["pop_size"] = popSize,
    ["n_gen"] = gens,
    ["seed"] = seed,
    ["n_solutions"] = obtained.Length,
    ["igd"] = igd,
    ["hv2"] = hv,
    ["F_csv"] = Path.GetFileName(fPath),
};
string metaPath = Path.Combine(dir, $"{stem}_meta.json");
File.WriteAllText(metaPath, JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true }));

Console.WriteLine($"wrote {fPath}");
Console.WriteLine($"wrote {metaPath}");
return 0;
