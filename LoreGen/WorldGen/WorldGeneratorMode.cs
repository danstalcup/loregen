namespace LoreGen.WorldGen
{
    /// <summary>
    /// Which algorithm shapes the land and water when a world is generated.
    /// </summary>
    public enum WorldGeneratorMode
    {
        /// <summary>
        /// The original generator: continents are carved out of a randomly partitioned grid.
        /// </summary>
        Legacy,

        /// <summary>
        /// Height-field generator based on fractal noise. Not implemented yet.
        /// </summary>
        Noise
    }
}
