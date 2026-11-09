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
    public ILanguage TestLanguage { get; set; }

    /// <summary>How the current world's places were named.</summary>
    public LanguageStyle LanguageStyle { get; private set; } = LanguageStyle.Common;

    /// <summary>Id of the generator that produced the current world.</summary>
    public string GeneratorId { get; private set; } = WorldGeneratorRegistry.DefaultId;

    /// <summary>The generators the user can choose from.</summary>
    public IReadOnlyList<IWorldGenerator> Generators { get; } = WorldGeneratorRegistry.CreateDefault().All;

    /// <summary>The generator settings the current world was made with (only those that differ from the defaults).</summary>
    public IReadOnlyDictionary<string, double> Settings { get; private set; } = new Dictionary<string, double>();

    // Any engine will do for asking a generator what it offers; this one is made the first time it is needed
    private SimEngine optionsEngine;
    private readonly Dictionary<string, IReadOnlyList<GeneratorOption>> optionsCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The options a generator offers, with defaults read from the loaded data.</summary>
    public IReadOnlyList<GeneratorOption> OptionsFor(string generatorId)
    {
        if (generatorId == null) return Array.Empty<GeneratorOption>();
        if (!optionsCache.TryGetValue(generatorId, out var options))
        {
            optionsEngine ??= new SimEngine(1);
            optionsCache[generatorId] = options = optionsEngine.Generators.Get(generatorId).GetOptions(optionsEngine.SimData);
        }
        return options;
    }

    /// <summary>Raised after a new world has been generated.</summary>
    public event Action Changed;

    public bool HasWorld => Engine?.World != null;

    public WorldSession()
    {
        Generate(Random.Shared.Next(1, 1_000_000));
    }

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
        TestLanguage = null;
        Map = new MapView(this);
        Changed?.Invoke();
    }

    public void GenerateRandom(string generatorId = null, IReadOnlyDictionary<string, double> settings = null, LanguageStyle? languageStyle = null) =>
        Generate(Random.Shared.Next(1, 1_000_000), generatorId, settings, languageStyle);

    public ILanguage GetTestLanguage() => TestLanguage ??= Engine.Language.GetRandomLanguage();
}
