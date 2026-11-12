using LoreGen.Lang;
using LoreGen.Simulation;
using LoreGen.WorldGen.Generators;

namespace Worldbuilder.Web.Services;

/// <summary>
/// Holds the current simulation (engine + generated world) and the map view for the app.
/// </summary>
public class WorldSession
{
    public SimEngine Engine { get; private set; }
    public int Seed { get; private set; }
    public MapView Map { get; private set; }
    /// <summary>How the current world's places were named.</summary>
    public LanguageStyle LanguageStyle { get; private set; } = LanguageStyle.Common;

    /// <summary>
    /// Id of the generator that produced the current world. The app starts on the tectonic generator (the library's own
    /// default stays the legacy one).
    /// </summary>
    public string GeneratorId { get; private set; } = TectonicWorldGenerator.GeneratorId;

    /// <summary>The generators the user can choose from.</summary>
    public IReadOnlyList<IWorldGenerator> Generators { get; } = WorldGeneratorRegistry.CreateDefault().All
        .OrderBy(g => g.Id == TectonicWorldGenerator.GeneratorId ? 0 : 1).ToList();

    /// <summary>The generator settings the current world was made with (only those that differ from the defaults).</summary>
    public IReadOnlyDictionary<string, double> Settings { get; private set; } = new Dictionary<string, double>();

    // Any engine will do for asking a generator what it offers; this one is made the first time it is needed
    private SimEngine optionsEngine;
    private readonly Dictionary<string, IReadOnlyList<GeneratorOption>> optionsCache = new(StringComparer.OrdinalIgnoreCase);

    private SimEngine OptionsEngine => optionsEngine ??= new SimEngine(1);

    /// <summary>The loaded data files. Available before any world has been generated.</summary>
    public SimData Data => (Engine ?? OptionsEngine).SimData;

    /// <summary>The options a generator offers, with defaults read from the loaded data.</summary>
    public IReadOnlyList<GeneratorOption> OptionsFor(string generatorId)
    {
        if (generatorId == null) return Array.Empty<GeneratorOption>();
        if (!optionsCache.TryGetValue(generatorId, out var options))
            optionsCache[generatorId] = options = OptionsEngine.Generators.Get(generatorId).GetOptions(OptionsEngine.SimData);
        return options;
    }

    /// <summary>Raised after a new world has been generated.</summary>
    public event Action Changed;

    /// <summary>Raised whenever a generation starts, moves on to its next step, or ends.</summary>
    public event Action Progressed;

    /// <summary>The generation in progress, or the last one to finish (null before the first).</summary>
    public GenerationRun Run { get; private set; }

    /// <summary>The last generation that produced the current world, for the generation log.</summary>
    public GenerationRun LastCompletedRun { get; private set; }

    public bool IsGenerating => Run?.IsRunning == true;

    public bool HasWorld => Engine?.World != null;

    private CancellationTokenSource cancel;

    /// <summary>
    /// Generates a brand new world from the given seed, using the given generator, settings and language style (or the
    /// current ones).
    /// </summary>
    public void Generate(int seed, string generatorId = null, IReadOnlyDictionary<string, double> settings = null, LanguageStyle? languageStyle = null)
    {
        generatorId ??= GeneratorId;
        settings ??= Settings;
        var style = languageStyle ?? LanguageStyle;
        var engine = new SimEngine(seed);
        engine.Configuration.GeneratorId = generatorId;
        engine.Configuration.LanguageStyle = style;
        foreach (var setting in settings) engine.Configuration.GeneratorSettings[setting.Key] = setting.Value;
        engine.GenerateWorld(); // if this throws, the previous world, seed, generator and settings stay in place
        Seed = seed;
        GeneratorId = generatorId;
        Settings = new Dictionary<string, double>(settings);
        LanguageStyle = style;
        Engine = engine;
        Map = new MapView(this);
        Changed?.Invoke();
    }

    public void GenerateRandom(string generatorId = null, IReadOnlyDictionary<string, double> settings = null, LanguageStyle? languageStyle = null) =>
        Generate(Random.Shared.Next(1, 1_000_000), generatorId, settings, languageStyle);

    /// <summary>
    /// Generates a brand new world one step at a time, pausing between steps so the page can redraw and show progress
    /// (the browser runs everything on one thread, so without the pauses nothing would update until the end). Gives the
    /// same world as <see cref="Generate"/>. If it fails or is cancelled, the previous world stays in place.
    /// </summary>
    /// <returns>True if a new world was made, false if the generation was cancelled</returns>
    /// <exception cref="Exception">The settings were rejected or a step failed</exception>
    public async Task<bool> GenerateAsync(int seed, string generatorId = null, IReadOnlyDictionary<string, double> settings = null, LanguageStyle? languageStyle = null)
    {
        if (IsGenerating) throw new InvalidOperationException("A world is already being generated.");
        generatorId ??= GeneratorId;
        settings ??= Settings;
        var style = languageStyle ?? LanguageStyle;
        var engine = new SimEngine(seed);
        engine.Configuration.GeneratorId = generatorId;
        engine.Configuration.LanguageStyle = style;
        foreach (var setting in settings) engine.Configuration.GeneratorSettings[setting.Key] = setting.Value;

        var generation = engine.BeginWorldGeneration(); // rejects bad settings before any work is done
        var name = Generators.FirstOrDefault(g => string.Equals(g.Id, generatorId, StringComparison.OrdinalIgnoreCase))?.DisplayName ?? generatorId;
        var run = Run = new GenerationRun(seed, name, generation);
        using var source = cancel = new CancellationTokenSource();
        try
        {
            while (!generation.IsComplete)
            {
                Progressed?.Invoke();
                await Task.Delay(1); // a real timer, so the browser paints the step that is about to run
                if (source.IsCancellationRequested)
                {
                    run.Finish(GenerationState.Cancelled);
                    return false;
                }
                generation.RunNextStep();
            }
        }
        catch (Exception ex)
        {
            run.Finish(GenerationState.Failed, ex.Message);
            throw;
        }
        finally
        {
            cancel = null;
            Progressed?.Invoke();
        }
        run.Finish(GenerationState.Done);
        LastCompletedRun = run;
        Seed = seed;
        GeneratorId = generatorId;
        Settings = new Dictionary<string, double>(settings);
        LanguageStyle = style;
        Engine = engine;
        Map = new MapView(this);
        Progressed?.Invoke();
        Changed?.Invoke();
        return true;
    }

    public Task<bool> GenerateRandomAsync(string generatorId = null, IReadOnlyDictionary<string, double> settings = null, LanguageStyle? languageStyle = null) =>
        GenerateAsync(Random.Shared.Next(1, 1_000_000), generatorId, settings, languageStyle);

    /// <summary>Stops the generation in progress after its current step. The previous world stays.</summary>
    public void CancelGeneration() => cancel?.Cancel();
}
