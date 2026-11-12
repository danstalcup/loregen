using System;
using System.Collections.Generic;
using System.Linq;

using LoreGen.Drawing;
using LoreGen.Randomizer;
using LoreGen.WorldDisplay;
using LoreGen.WorldGen;

namespace LoreGen.Features
{
    /// <summary>
    /// A named piece of geography: a mountain range, a great desert, a lake, a volcano.
    ///
    /// A feature has a type, which fixes its scope (its size: the block depth its shape is defined at), and a footprint
    /// of blocks at that depth. Blocks may belong to any number of features: a desert can lie on a plateau, and a range
    /// can cross region borders. Features nest by type: a mountain range holds subranges and massifs.
    ///
    /// Features are found by world generators and added with <see cref="World.AddFeature"/>; look them up by block with
    /// <see cref="World.FeaturesAt"/>.
    /// </summary>
    public class GeographicFeature : WorldEntity
    {
        private readonly List<GeographicFeature> children = new List<GeographicFeature>();
        private List<WorldBlock> spine = new List<WorldBlock>();

        public FeatureType Type { get; }

        /// <summary>How big the feature is (set by its type), which is also the depth its footprint is defined at.</summary>
        public FeatureScope Scope { get { return Type.Scope; } }

        /// <summary>The block depth of the footprint.</summary>
        public int Depth { get { return Scope.Depth(); } }

        /// <summary>The blocks the feature covers.</summary>
        public FeatureFootprint Footprint { get; }

        /// <summary>
        /// Shapes the feature below its own depth. The default adds no detail. See <see cref="IFeatureRefiner"/>.
        /// </summary>
        public IFeatureRefiner Refiner { get; set; } = InheritRefiner.Instance;

        /// <summary>
        /// Fixed per feature, for anything that needs randomness about it later (finer shapes, colour) without drawing on
        /// the world's random number generators. Derived from the world seed and where the feature is, so it does not
        /// depend on the order features are created in.
        /// </summary>
        public int Seed { get; }

        /// <summary>
        /// For Linear features, the blocks along the feature from one end to the other (a range's crest line, a river's
        /// course); for Point features, the single block it sits on; otherwise empty. Every spine block is in the footprint.
        /// Consecutive blocks need not touch: a range's spine can jump a pass.
        /// </summary>
        public IReadOnlyList<WorldBlock> Spine { get { return spine; } }

        /// <summary>The larger feature this one is part of, if any.</summary>
        public GeographicFeature Parent { get; private set; }

        /// <summary>The smaller features that are part of this one.</summary>
        public IReadOnlyList<GeographicFeature> Children { get { return children; } }

        /// <summary>
        /// Creates a feature. It is not part of the world until passed to <see cref="World.AddFeature"/>.
        /// </summary>
        /// <param name="world">The world it is in</param>
        /// <param name="type">Its type, which sets its scope</param>
        /// <param name="blocks">Its footprint: blocks at the depth of the type's scope</param>
        /// <param name="seed">Its seed (see <see cref="SeedFor"/>)</param>
        /// <exception cref="ArgumentException">The blocks are not a valid footprint</exception>
        public GeographicFeature(World world, FeatureType type, IEnumerable<WorldBlock> blocks, int seed)
        {
            if (world == null) throw new ArgumentNullException("world");
            if (type == null) throw new ArgumentNullException("type");

            World = world;
            WorldEntityType = WorldEntityType.GeographicFeature;
            Type = type;
            Seed = seed;
            Name = string.Empty;
            Footprint = new FeatureFootprint(blocks, type.Scope.Depth()) { Feature = this };

            Rnd colour = new Rnd(seed);
            DisplayInfo = new WorldEntityDisplayInfo
            {
                Entity = this,
                DisplayColor = Color.FromArgb(64 + colour.Unweighted(192), 64 + colour.Unweighted(192), 64 + colour.Unweighted(192))
            };
        }

        /// <summary>
        /// Sets the spine (see <see cref="Spine"/>).
        /// </summary>
        /// <exception cref="ArgumentException">A block is not in the footprint, or a Point feature is given other than one block</exception>
        public void SetSpine(IEnumerable<WorldBlock> blocks)
        {
            List<WorldBlock> list = (blocks ?? Enumerable.Empty<WorldBlock>()).ToList();
            if (list.Any(b => !Footprint.IsMember(b))) throw new ArgumentException("Every spine block must be in the feature's footprint.", "blocks");
            if (Type.Shape == FeatureShape.Point && list.Count != 1) throw new ArgumentException("A point feature's spine is exactly one block.", "blocks");
            spine = list;
        }

        /// <summary>
        /// Makes a smaller feature part of this one.
        /// </summary>
        /// <exception cref="ArgumentException">The child's type is not a part of this feature's type, it is in another
        /// world, or it already has a parent</exception>
        public void AddChild(GeographicFeature child)
        {
            if (child == null) throw new ArgumentNullException("child");
            if (child.World != World) throw new ArgumentException("A feature's parts must be in the same world.", "child");
            if (!child.Type.IsPartOf(Type))
                throw new ArgumentException(child.Type.Name + " features cannot be part of " + Type.Name.ToLowerInvariant() + " features.", "child");
            if (child.Parent != null) throw new ArgumentException("That feature is already part of " + child.Parent.Name + ".", "child");
            child.Parent = this;
            children.Add(child);
        }

        /// <summary>
        /// Whether the feature covers a block, at any depth (see <see cref="FeatureFootprint.Contains"/>).
        /// </summary>
        public bool Contains(WorldBlock block)
        {
            return Footprint.Contains(block);
        }

        /// <summary>
        /// A feature seed from the world seed, the feature type and a block that identifies where the feature is (for
        /// example its first block), so the seed does not depend on the order features are found in.
        /// </summary>
        public static int SeedFor(int worldSeed, FeatureType type, WorldBlock anchor)
        {
            unchecked
            {
                // FNV-1a: stable across runs and machines, unlike string.GetHashCode
                uint hash = 2166136261;
                Action<int> mix = value =>
                {
                    for (int i = 0; i < 4; i++)
                    {
                        hash ^= (uint)(value >> (8 * i)) & 0xFF;
                        hash *= 16777619;
                    }
                };
                mix(worldSeed);
                foreach (char c in type.Id) mix(c);
                mix(anchor.Depth);
                mix(anchor.TrueX);
                mix(anchor.TrueY);
                return (int)hash;
            }
        }

        public override string ToString() { return Name + " (" + Type.Name + ")"; }
    }
}
