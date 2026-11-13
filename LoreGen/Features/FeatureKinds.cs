using System;

namespace LoreGen.Features
{
    /// <summary>
    /// How big a geographic feature is. Each scope is the block depth its shape is defined at: a Continental feature is
    /// a set of top-level blocks (about 1,000 km across), a Regional one a set of their sub-blocks (about 100 km), and so
    /// on. A feature has exactly one scope.
    /// </summary>
    public enum FeatureScope
    {
        /// <summary>Defined on top-level blocks (depth 1, about 1,000 km): great ranges, great deserts, inland seas.</summary>
        Continental = 1,
        /// <summary>Defined on depth-2 blocks (about 100 km): lesser ranges, large lakes, plateaus.</summary>
        Regional = 2,
        /// <summary>Defined on depth-3 blocks (about 10 km): single mountains, lakes, valleys.</summary>
        Local = 3,
        /// <summary>Defined on depth-4 blocks (about 1 km): hills, ponds, springs.</summary>
        Minor = 4
    }

    /// <summary>
    /// The broad outline of a feature.
    /// </summary>
    public enum FeatureShape
    {
        /// <summary>A single spot, such as a volcano or a spring.</summary>
        Point,
        /// <summary>A long chain, such as a mountain range or a river. Linear features have an ordered <see cref="GeographicFeature.Spine"/>.</summary>
        Linear,
        /// <summary>A rounded area, such as a sea, a lake or a desert.</summary>
        Blob
    }

    /// <summary>
    /// What kind of geography a feature is.
    /// </summary>
    public enum FeatureCategory
    {
        Mountain, Water, Desert, Forest, Plain, Valley, Volcanic, Ice, Wetland,
        /// <summary>Shapes of the shore: peninsulas, beaches, cliffs.</summary>
        Coast,
        /// <summary>Caves and cave systems, underground.</summary>
        Cave,
        /// <summary>Craters left by impacts from the sky.</summary>
        Impact
    }

    /// <summary>
    /// Helpers for <see cref="FeatureScope"/>.
    /// </summary>
    public static class FeatureScopes
    {
        /// <summary>
        /// The block depth a scope's shapes are defined at.
        /// </summary>
        public static int Depth(this FeatureScope scope)
        {
            return (int)scope;
        }

        /// <summary>
        /// The scope whose shapes are defined at the given depth.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">No scope is defined at that depth</exception>
        public static FeatureScope AtDepth(int depth)
        {
            if (!Enum.IsDefined(typeof(FeatureScope), depth))
                throw new ArgumentOutOfRangeException("depth", "No feature scope is defined at depth " + depth + ".");
            return (FeatureScope)depth;
        }
    }
}
