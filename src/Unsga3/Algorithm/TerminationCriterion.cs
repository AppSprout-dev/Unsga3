namespace Unsga3.Algorithm;

/// <summary>When to stop the evolutionary loop.</summary>
public abstract class TerminationCriterion
{
    public abstract bool ShouldStop(int generation, int evaluations, Population population);

    public static TerminationCriterion MaxGenerations(int generations) =>
        new MaxGenerationsTermination(generations);

    public static TerminationCriterion MaxEvaluations(int evaluations) =>
        new MaxEvaluationsTermination(evaluations);
}

file sealed class MaxGenerationsTermination(int maxGenerations) : TerminationCriterion
{
    public override bool ShouldStop(int generation, int evaluations, Population population) =>
        generation >= maxGenerations;
}

file sealed class MaxEvaluationsTermination(int maxEvaluations) : TerminationCriterion
{
    public override bool ShouldStop(int generation, int evaluations, Population population) =>
        evaluations >= maxEvaluations;
}
