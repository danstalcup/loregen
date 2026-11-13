using System.Collections.Generic;

namespace LoreGen.WorldGen
{
    /// <summary>
    /// The broad kind of land a subregion is.
    /// </summary>
    public enum SubregionKind
    {
        /// <summary>Mountainous ground: crags and peaks.</summary>
        Highlands,
        /// <summary>Rolling high ground.</summary>
        Hills,
        /// <summary>Wooded country.</summary>
        Woods,
        /// <summary>Marsh and standing water.</summary>
        Wetlands,
        /// <summary>Cold, open ground.</summary>
        Frozen,
        /// <summary>Dry, open ground.</summary>
        Drylands,
        /// <summary>Open, green lowland.</summary>
        Plains
    }

    /// <summary>
    /// A named stretch of one kind of land within a region, such as "the Ashen Hills" or "Greywater Fens": a purely
    /// geographic division (political ones may be built on top later). Each region is divided into subregions along its
    /// landscapes and features, at the 100 km level, when they are first asked for (see <see cref="World.SubregionsOf"/>);
    /// finer blocks belong to the subregion of the 100 km block they mostly carry on from, with winding borders.
    /// </summary>
    public class Subregion : WorldEntity
    {
        internal Subregion(World world, Region region, SubregionKind kind, string landform, IReadOnlyList<WorldBlock> blocks, int seed)
        {
            World = world;
            WorldEntityType = WorldEntityType.Subregion;
            Region = region;
            Kind = kind;
            Landform = landform;
            Blocks = blocks;
            Seed = seed;
        }

        /// <summary>The region it is part of.</summary>
        public Region Region { get; }

        /// <summary>The broad kind of land it is.</summary>
        public SubregionKind Kind { get; }

        /// <summary>The word its name uses for its land, such as "Hills", "Fens" or "Weald".</summary>
        public string Landform { get; }

        /// <summary>Its 100 km blocks (depth 2).</summary>
        public IReadOnlyList<WorldBlock> Blocks { get; }

        /// <summary>Fixed by the world seed and where it is, for anything random about it.</summary>
        public int Seed { get; }
    }
}
