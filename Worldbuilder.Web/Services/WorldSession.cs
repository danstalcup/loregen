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
    public Language TestLanguage { get; set; }

    /// <summary>Id of the generator that produced the current world.</summary>
    public string GeneratorId { get; private set; } = WorldGeneratorRegistry.DefaultId;

    /// <summary>The generators the user can choose from.</summary>
    public IReadOnlyList<IWorldGenerator> Generators { get; } = WorldGeneratorRegistry.CreateDefault().All;

    /// <summary>Raised after a new world has been generated.</summary>
    public event Action Changed;

    public bool HasWorld => Engine?.World != null;

    public WorldSession()
    {
        Generate(Random.Shared.Next(1, 1_000_000));
    }

    /// <summary>Generates a brand new world from the given seed, using the given generator (or the current one).</summary>
    public void Generate(int seed, string generatorId = null)
    {
        generatorId ??= GeneratorId;
        var engine = new SimEngine(seed);
        engine.Configuration.GeneratorId = generatorId;
        engine.GenerateWorld(); // if this throws, the previous world, seed and generator stay in place
        Seed = seed;
        GeneratorId = generatorId;
        Engine = engine;
        TestLanguage = null;
        Map = new MapView(this);
        Changed?.Invoke();
    }

    public void GenerateRandom(string generatorId = null) => Generate(Random.Shared.Next(1, 1_000_000), generatorId);

    public Language GetTestLanguage() => TestLanguage ??= Engine.Language.GetRandomLanguage();
}
