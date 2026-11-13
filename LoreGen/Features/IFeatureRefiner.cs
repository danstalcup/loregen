using LoreGen.WorldGen;

namespace LoreGen.Features
{
    /// <summary>
    /// Decides a feature's shape below the depth it is defined at, so that zooming in shows a finer outline (a winding
    /// range edge, a ragged lake shore) rather than the coarse blocks.
    ///
    /// A refiner is only asked about the feature's edge: blocks inside a coarse block on the edge of the footprint, and
    /// blocks inside a coarse block just outside it. Blocks deep inside the footprint are always part of the feature and
    /// blocks well away from it never are, so finer detail can only reshape edges.
    ///
    /// Implementations must be pure: the answer may depend only on the feature (its <see cref="GeographicFeature.Seed"/>
    /// and footprint) and the block's position (TrueX, TrueY, TrueWidth, TrueHeight, Depth). Never draw from the
    /// engine's random number generators or keep state that depends on which blocks were asked about first. Then the
    /// same seed gives the same shapes however and in whatever order the world is explored, and detail can be worked out
    /// lazily, only for the blocks someone zooms in on. Noise sampled at the block's position, seeded from the feature's
    /// Seed (as CoastalDetail does for coastlines), meets these rules.
    /// </summary>
    public interface IFeatureRefiner
    {
        /// <summary>
        /// Whether a block finer than the feature's scope is part of the feature.
        /// </summary>
        /// <param name="feature">The feature</param>
        /// <param name="block">A block deeper than the feature's scope, within or next to the edge of its footprint</param>
        /// <param name="coarseMember">Whether the block's ancestor at the feature's depth is in the footprint</param>
        bool Includes(GeographicFeature feature, WorldBlock block, bool coarseMember);
    }

    /// <summary>
    /// The default refiner: no finer detail. A block is part of the feature exactly when its coarse ancestor is.
    /// </summary>
    public sealed class InheritRefiner : IFeatureRefiner
    {
        public static readonly InheritRefiner Instance = new InheritRefiner();

        private InheritRefiner() { }

        public bool Includes(GeographicFeature feature, WorldBlock block, bool coarseMember)
        {
            return coarseMember;
        }
    }

    /// <summary>
    /// Wraps another refiner so the feature never reaches beyond its own coarse blocks: a finer block is part of it only
    /// if its coarse ancestor is in the footprint and the inner refiner agrees. For features found while exploring, whose
    /// edges must not spill into blocks whose detail may already have been made.
    /// </summary>
    public sealed class InsideRefiner : IFeatureRefiner
    {
        private readonly IFeatureRefiner inner;

        public InsideRefiner(IFeatureRefiner inner)
        {
            this.inner = inner ?? throw new System.ArgumentNullException("inner");
        }

        /// <summary>The refiner it wraps.</summary>
        public IFeatureRefiner Inner { get { return inner; } }

        public bool Includes(GeographicFeature feature, WorldBlock block, bool coarseMember)
        {
            return coarseMember && inner.Includes(feature, block, true);
        }
    }
}
