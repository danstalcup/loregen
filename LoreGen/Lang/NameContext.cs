using LoreGen.Terrains;
using LoreGen.WorldGen;

namespace LoreGen.Lang
{
    /// <summary>
    /// The kind of thing being named.
    /// </summary>
    public enum NameKind
    {
        /// <summary>A language (its own name).</summary>
        Language,
        /// <summary>A whole world.</summary>
        World,
        /// <summary>A continent.</summary>
        Continent,
        /// <summary>A region of a continent.</summary>
        Region,
        /// <summary>A sea belonging to a continent.</summary>
        Sea,
        /// <summary>An ocean.</summary>
        Ocean,
        /// <summary>A mountain range.</summary>
        MountainRange,
        /// <summary>A desert.</summary>
        Desert,
        /// <summary>A subrange: a long crest within a mountain range.</summary>
        Subrange,
        /// <summary>A massif: a compact knot of peaks within a mountain range.</summary>
        Massif,
        /// <summary>A peak: a summit of a mountain range.</summary>
        Peak,
        /// <summary>A pass: the lowest way over a mountain range between two of its peaks.</summary>
        Pass,
        /// <summary>An erg: a sea of sand dunes within a desert.</summary>
        Erg,
        /// <summary>A salt flat: a dry, salt-crusted basin floor within a desert.</summary>
        SaltFlat,
        /// <summary>An oasis: a hollow in a desert where ground water comes near the surface.</summary>
        Oasis,
        /// <summary>A bay: water of an ocean or sea that land wraps around.</summary>
        Bay,
        /// <summary>A strait: a narrow passage of water between two pieces of land.</summary>
        Strait,
        /// <summary>A lake.</summary>
        Lake,
        /// <summary>A salt lake: a lake with no outflow.</summary>
        SaltLake,
        /// <summary>A large river.</summary>
        River
    }

    /// <summary>
    /// What is being named, plus optional hints about it. Languages use the hints when they can and ignore them otherwise,
    /// so every hint may be left unset. A bare <see cref="NameKind"/> converts to a context with no hints.
    /// </summary>
    public sealed class NameContext
    {
        /// <summary>
        /// The kind of thing being named.
        /// </summary>
        public NameKind Kind { get; }

        /// <summary>
        /// The biome of the thing being named (regions), so names can describe the land: "the Ashfen" for a swamp.
        /// </summary>
        public Biome Biome { get; init; }

        /// <summary>
        /// Where the thing lies within its parent (a region within its continent, an ocean within the world), if it lies
        /// clearly to one side: "the Western Ocean".
        /// </summary>
        public Directions? Direction { get; init; }

        /// <summary>
        /// Name of the thing's parent (a region's continent), for names such as "North Valoria".
        /// </summary>
        public string ParentName { get; init; }

        /// <summary>
        /// A context with no hints.
        /// </summary>
        public NameContext(NameKind kind)
        {
            Kind = kind;
        }

        /// <summary>
        /// A context with no hints.
        /// </summary>
        public static implicit operator NameContext(NameKind kind)
        {
            return new NameContext(kind);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return Kind.ToString();
        }
    }
}
