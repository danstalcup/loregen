using LoreGen.Simulation;

namespace LoreGen.WorldGen.Generators
{
    /// <summary>
    /// An algorithm that builds a complete <see cref="World"/>: where the land and water are, and the continents,
    /// oceans, seas, regions, biomes, terrain and languages on it. Implement this to add a new way of generating worlds,
    /// then register it with <see cref="WorldGeneratorRegistry"/> (or assign it to <see cref="SimEngine.WorldGenerator"/>).
    ///
    /// The rest of the system (the map, the world info, region neighbours, naming) relies on the contract below, and
    /// every registered generator is checked against it by the contract tests in LoreGen.Tests.
    ///
    /// Rules for implementers:
    ///  - Be stateless between calls. All working state belongs in locals or in the World being built, so a single
    ///    instance can be reused for any number of worlds.
    ///  - Take all randomness from engine.Rnd (or generators seeded from it), never from static or shared state.
    ///    The same engine seed must always give the same world.
    ///  - Read settings from engine.SimData. Put settings specific to your generator in a keyed rules file of your own
    ///    (see SimData.LoadRules) rather than adding keys to the shared ones.
    ///  - Do not assign engine.World. Return the finished world and the engine assigns it; if you throw, the engine
    ///    keeps whatever world it had before.
    ///  - Bump <see cref="Version"/> whenever a seed would start producing a different world.
    ///
    /// What a returned world must contain:
    ///  - WorldBlock with a populated top-level ChildBlocks grid. Every block has a Status. Blocks may optionally have
    ///    finer ChildBlocks of their own.
    ///  - At least one Continent with a name, a root language, a ContinentArea, and at least one Region.
    ///  - Every Region has a name, a language, a biome and a RegionArea, and belongs to the Continent that lists it.
    ///  - Every non-water block (Land or Coastline) belongs to a Continent and a Region and has a Terrain.
    ///  - Every top-level Water block belongs to an Ocean or a Sea (an enclosed body of water may be a Sea with no Ocean),
    ///    and every Ocean and Sea in a block's Status is registered on the World (World.Oceans) or its Continent
    ///    (Continent.Seas).
    /// </summary>
    public interface IWorldGenerator
    {
        /// <summary>
        /// Short stable identifier used in configuration and links, for example "legacy". Letters, digits, '-' and '_'.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Name shown to people.
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// One or two sentences describing how worlds from this generator differ.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Changes whenever the same seed would produce a different world. A world is identified by its seed,
        /// generator Id and generator Version.
        /// </summary>
        string Version { get; }

        /// <summary>
        /// Generates a complete world.
        /// </summary>
        /// <param name="engine">The engine providing the random number generator and data</param>
        /// <returns>The finished world</returns>
        World Generate(SimEngine engine);
    }
}
