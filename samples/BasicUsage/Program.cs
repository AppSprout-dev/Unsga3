using Unsga3.Algorithm;
using Unsga3.Problems;
using Unsga3.Utilities;

// Minimal ZDT1 run — bi-objective front approximation.
var problem = new Zdt1Problem();
var dirs = ReferenceDirections.DasDennis(numberOfObjectives: 2, partitions: 12); // 13 directions
var algo = new Unsga3Algorithm(dirs, populationSize: 40, seed: 42);

Console.WriteLine($"U-NSGA-III on ZDT1 | pop={40} refs={dirs.Length} gens=50");
var result = algo.Run(problem, maxGenerations: 50);

Console.WriteLine($"Generations: {result.GenerationsExecuted}");
Console.WriteLine($"Evaluations: {result.Evaluations}");
Console.WriteLine($"Final pop:   {result.FinalPopulation.Count}");
Console.WriteLine($"Front size:  {result.NonDominatedSolutions.Count}");
Console.WriteLine();
Console.WriteLine("First 10 non-dominated (f1, f2):");
foreach (var ind in result.NonDominatedSolutions.Take(10))
    Console.WriteLine($"  {ind.Objectives[0]:F4}  {ind.Objectives[1]:F4}");
