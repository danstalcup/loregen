using LoreGen.Lang;
using LoreGen.Simulation;

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

    /// <summary>Raised after a new world has been generated.</summary>
    public event Action Changed;

    public bool HasWorld => Engine?.World != null;

    public WorldSession()
    {
        Generate(Random.Shared.Next(1, 1_000_000));
    }

    /// <summary>Generates a brand new world from the given seed.</summary>
    public void Generate(int seed)
    {
        Seed = seed;
        Engine = new SimEngine(seed);
        Engine.GenerateWorld();
        TestLanguage = null;
        Map = new MapView(this);
        Changed?.Invoke();
    }

    public void GenerateRandom() => Generate(Random.Shared.Next(1, 1_000_000));

    public Language GetTestLanguage() => TestLanguage ??= Engine.Language.GetRandomLanguage();
}
