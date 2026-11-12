using System.Diagnostics;
using LoreGen.Simulation;

namespace Worldbuilder.Web.Services;

public enum GenerationState { Running, Done, Cancelled, Failed }

/// <summary>
/// Progress of one world generation, for the progress display and the generation log.
/// </summary>
public class GenerationRun
{
    private readonly WorldGeneration generation;
    private readonly Stopwatch watch = Stopwatch.StartNew();

    public GenerationRun(int seed, string generatorName, WorldGeneration generation)
    {
        Seed = seed;
        GeneratorName = generatorName;
        this.generation = generation;
    }

    public int Seed { get; }
    public string GeneratorName { get; }
    public GenerationState State { get; private set; } = GenerationState.Running;
    public string Error { get; private set; }

    public IReadOnlyList<string> StepNames => generation.StepNames;
    public IReadOnlyList<TimeSpan> Durations => generation.Durations;
    public int StepCount => generation.StepCount;
    public int CompletedSteps => generation.CompletedSteps;
    public string CurrentStep => generation.NextStep;
    public double Fraction => generation.Fraction;

    /// <summary>Wall-clock time since the run started, or its total once finished (including time spent redrawing).</summary>
    public TimeSpan Elapsed => watch.Elapsed;

    /// <summary>Time spent in the steps themselves.</summary>
    public TimeSpan WorkTime => TimeSpan.FromTicks(generation.Durations.Sum(d => d.Ticks));

    public bool IsRunning => State == GenerationState.Running;

    internal void Finish(GenerationState state, string error = null)
    {
        State = state;
        Error = error;
        watch.Stop();
    }
}
