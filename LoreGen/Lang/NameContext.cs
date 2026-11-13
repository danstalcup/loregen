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
        River,
        /// <summary>Hills: a stretch of rolling high ground about 10 km across.</summary>
        Hills,
        /// <summary>A forest or wood about 10 km across.</summary>
        Forest,
        /// <summary>A fen: a stretch of marsh about 10 km across.</summary>
        Fen,
        /// <summary>A gorge: where a river cuts through rugged ground.</summary>
        Gorge,
        /// <summary>A headland: land jutting out into the water.</summary>
        Headland,
        /// <summary>A cove: a small sheltered inlet of the sea.</summary>
        Cove,
        /// <summary>An isle: a small island.</summary>
        Isle,
        /// <summary>A knoll: a small hill about 1 km across.</summary>
        Knoll,
        /// <summary>A waterfall or rapids on a river.</summary>
        Waterfall,
        /// <summary>A grove: a small wood in open country.</summary>
        Grove,
        /// <summary>A clearing: a small open space in a forest.</summary>
        Clearing,
        /// <summary>A pond or mere.</summary>
        Pond,
        /// <summary>A subregion: a named stretch of one kind of land within a region, such as hills or woods.</summary>
        Subregion,
        /// <summary>A peninsula.</summary>
        Peninsula,
        /// <summary>A river's delta.</summary>
        Delta,
        /// <summary>A volcano.</summary>
        Volcano,
        /// <summary>A beach.</summary>
        Beach,
        /// <summary>Sea cliffs.</summary>
        Cliffs,
        /// <summary>A lagoon.</summary>
        Lagoon,
        /// <summary>A volcano's caldera.</summary>
        Caldera,
        /// <summary>An ice sheet.</summary>
        IceSheet,
        /// <summary>A glacier.</summary>
        Glacier,
        /// <summary>A fjord.</summary>
        Fjord,
        /// <summary>A cave system.</summary>
        CaveSystem,
        /// <summary>A cave.</summary>
        Cave,
        /// <summary>An impact crater.</summary>
        Crater,
        /// <summary>A lake filling a crater.</summary>
        CraterLake
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
        /// The word for the kind of land being named (subregions), such as "Hills", "Fens" or "Weald", for names such as
        /// "the Ashen Hills". Templates use it as {Landform}.
        /// </summary>
        public string Landform { get; init; }

        /// <summary>
        /// How warm the place being named is (0 coldest to 1 hottest), if known, so names suit it: no "Burning" glacier
        /// or "Frozen" jungle. See CommonNameKindThemes.csv.
        /// </summary>
        public double? Temperature { get; init; }

        /// <summary>How wet the place being named is (0 driest to 1 wettest), if known.</summary>
        public double? Moisture { get; init; }

        /// <summary>A copy of this context with the place's climate added (see <see cref="Temperature"/>).</summary>
        public NameContext WithClimate((double? Temperature, double? Moisture) climate)
        {
            return new NameContext(Kind)
            {
                Biome = Biome, Direction = Direction, ParentName = ParentName, Landform = Landform,
                Temperature = climate.Temperature, Moisture = climate.Moisture
            };
        }

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
