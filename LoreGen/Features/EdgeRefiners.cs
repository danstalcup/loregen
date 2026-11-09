using System;

using LoreGen.Noise;
using LoreGen.WorldGen;

namespace LoreGen.Features
{
    /// <summary>
    /// Gives a feature a natural, fractal edge below its own depth. The feature's coarse footprint is blurred into a smooth
    /// field (1 inside, 0 outside, blending across the edge), and fractal noise is added; a block is in the feature where
    /// the result is above one half. More octaves of noise show as blocks get smaller, so the more you zoom in, the finer
    /// the edge.
    ///
    /// Pure: the answer depends only on the feature's seed and footprint and the block's position, so it is the same
    /// whatever order blocks are asked about in (see <see cref="IFeatureRefiner"/>).
    /// </summary>
    public sealed class NoiseEdgeRefiner : IFeatureRefiner
    {
        private readonly FractalNoise noise;

        /// <summary>How far noise can push the edge, from 0 (a smooth curve) to below 0.5.</summary>
        public double Strength { get; }

        /// <param name="seed">The feature's seed</param>
        /// <param name="strength">How far noise can push the edge: from 0 (smooth) up to, but not including, 0.5</param>
        /// <param name="frequency">Wiggles per coarse block</param>
        /// <param name="octaves">Layers of ever finer wiggle</param>
        public NoiseEdgeRefiner(int seed, double strength, double frequency = 2.0, int octaves = 5)
        {
            if (strength < 0 || strength >= 0.5) throw new ArgumentOutOfRangeException("strength", "Strength must be at least 0 and below 0.5.");
            Strength = strength;
            noise = new FractalNoise(new SimplexNoise(seed), octaves, frequency, 2.0, 0.55);
        }

        public bool Includes(GeographicFeature feature, WorldBlock block, bool coarseMember)
        {
            WorldBlock coarse = block.AncestorAt(feature.Depth);
            double x, y;
            CentreIn(coarse, block, out x, out y);
            double field = BlurredMembership(feature.Footprint, coarse, x, y);
            // Sample the noise in world units of the coarse block size, so it is continuous across coarse blocks
            double gx = (block.TrueX + block.TrueWidth / 2.0) / coarse.TrueWidth, gy = (block.TrueY + block.TrueHeight / 2.0) / coarse.TrueHeight;
            return field + Strength * noise.Fbm(gx, gy) > 0.5;
        }

        // Centre of `block` relative to the centre of `coarse`, in coarse-block units (-0.5 to 0.5)
        internal static void CentreIn(WorldBlock coarse, WorldBlock block, out double x, out double y)
        {
            x = (block.TrueX + block.TrueWidth / 2.0 - coarse.TrueX) / coarse.TrueWidth - 0.5;
            y = (block.TrueY + block.TrueHeight / 2.0 - coarse.TrueY) / coarse.TrueHeight - 0.5;
        }

        /// <summary>
        /// Bilinear blend of footprint membership (1 or 0) between the centres of the coarse block and its neighbours on
        /// the side the point lies toward. Missing neighbours count as outside.
        /// </summary>
        internal static double BlurredMembership(FeatureFootprint footprint, WorldBlock coarse, double x, double y)
        {
            WorldBlock sideX = x >= 0 ? coarse.East() : coarse.West();
            WorldBlock sideY = y >= 0 ? coarse.South() : coarse.North();
            WorldBlock corner = sideX == null ? null : (y >= 0 ? sideX.South() : sideX.North());
            double tx = Math.Abs(x), ty = Math.Abs(y);
            double a = Member(footprint, coarse), b = Member(footprint, sideX), c = Member(footprint, sideY), d = Member(footprint, corner);
            return (a * (1 - tx) + b * tx) * (1 - ty) + (c * (1 - tx) + d * tx) * ty;
        }

        private static double Member(FeatureFootprint footprint, WorldBlock block)
        {
            return footprint.IsMember(block) ? 1.0 : 0.0;
        }
    }

    /// <summary>
    /// For features that are defined by height, such as mountain ranges: below the feature's depth, a block is in the
    /// feature when its own elevation reaches the threshold, so the edge follows the land's real contours. Blocks without
    /// an elevation follow their coarse ancestor.
    ///
    /// Pure as <see cref="IFeatureRefiner"/> requires: block elevations are fixed when the world is generated, and blocks
    /// created later copy their parent's.
    /// </summary>
    public sealed class ElevationEdgeRefiner : IFeatureRefiner
    {
        /// <summary>Lowest elevation that is part of the feature.</summary>
        public double Threshold { get; }

        public ElevationEdgeRefiner(double threshold)
        {
            Threshold = threshold;
        }

        public bool Includes(GeographicFeature feature, WorldBlock block, bool coarseMember)
        {
            double? elevation = block.Status.Elevation;
            if (!elevation.HasValue) return coarseMember;
            if (block.Status.WaterStatus == WorldBlockWaterStatus.Water) return false;
            return elevation.Value >= Threshold;
        }
    }
}
