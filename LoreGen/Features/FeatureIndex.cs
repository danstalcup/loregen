using System.Collections.Generic;
using System.Linq;

using LoreGen.WorldGen;

namespace LoreGen.Features
{
    /// <summary>
    /// Finds the features covering a block quickly. Each feature is listed twice over:
    ///  - under its footprint blocks, for blocks at or below the feature's depth: such a block can only be covered if its
    ///    ancestor at the feature's depth is in the footprint or beside it (where a refiner may extend the edge), so a
    ///    lookup checks that ancestor and the blocks beside it. Blocks beside are found when looking up, not when the
    ///    feature is added, so blocks made later by zooming in are covered too;
    ///  - under the blocks containing its footprint, for blocks coarser than the feature, which count as covered if any
    ///    of the feature lies inside them.
    /// A lookup gathers the first kind of candidate from the block and its ancestors, and the second kind from the block
    /// itself, then asks each whether it really covers the block. So a lookup never wades through the many small features
    /// inside a large ancestor.
    /// </summary>
    internal class FeatureIndex
    {
        private readonly Dictionary<WorldBlock, List<GeographicFeature>> near = new Dictionary<WorldBlock, List<GeographicFeature>>();
        private readonly Dictionary<WorldBlock, List<GeographicFeature>> inside = new Dictionary<WorldBlock, List<GeographicFeature>>();
        private readonly Dictionary<GeographicFeature, int> order = new Dictionary<GeographicFeature, int>();

        public FeatureIndex(IReadOnlyList<GeographicFeature> features)
        {
            foreach (GeographicFeature feature in features) Add(feature);
        }

        /// <summary>
        /// Adds a feature (after those already in the index, in order).
        /// </summary>
        public void Add(GeographicFeature feature)
        {
            order[feature] = order.Count;
            HashSet<WorldBlock> containing = new HashSet<WorldBlock>();
            foreach (WorldBlock block in feature.Footprint.Blocks)
            {
                List(near, block).Add(feature);
                for (WorldBlock a = block.ParentBlock; a != null && a.ParentBlock != null; a = a.ParentBlock) containing.Add(a);
            }
            foreach (WorldBlock block in containing) List(inside, block).Add(feature);
        }

        private static List<GeographicFeature> List(Dictionary<WorldBlock, List<GeographicFeature>> map, WorldBlock block)
        {
            List<GeographicFeature> list;
            if (!map.TryGetValue(block, out list)) map[block] = list = new List<GeographicFeature>();
            return list;
        }

        // Features with footprint blocks at `listed` (at the depth of `level`) that cover the block
        private void Check(WorldBlock listed, WorldBlock level, WorldBlock block, HashSet<GeographicFeature> found)
        {
            List<GeographicFeature> list;
            if (!near.TryGetValue(listed, out list)) return;
            foreach (GeographicFeature feature in list)
                if (feature.Depth == level.Depth && !found.Contains(feature) && feature.Contains(block)) found.Add(feature);
        }

        /// <summary>
        /// The features covering each of a block's children, as <see cref="At"/> would give them one by one, but gathering
        /// the candidates once for them all: they share every ancestor, and features defined at the children's own depth
        /// never cover a block outside their footprint.
        /// </summary>
        public List<GeographicFeature>[,] AtChildren(WorldBlock parent)
        {
            HashSet<GeographicFeature> candidates = new HashSet<GeographicFeature>();
            for (WorldBlock b = parent; b != null && b.ParentBlock != null; b = b.ParentBlock)
            {
                Gather(b, b, candidates);
                foreach (WorldBlock beside in b.SurroundingBlocks())
                    if (beside != null) Gather(beside, b, candidates);
            }
            List<GeographicFeature> ordered = candidates.OrderBy(f => order[f]).ToList();
            WorldBlock[,] children = parent.ChildBlocks;
            var result = new List<GeographicFeature>[children.GetLength(0), children.GetLength(1)];
            for (int i = 0; i < children.GetLength(0); i++)
                for (int j = 0; j < children.GetLength(1); j++)
                {
                    WorldBlock child = children[i, j];
                    var here = new List<GeographicFeature>();
                    foreach (GeographicFeature feature in ordered) if (feature.Contains(child)) here.Add(feature);
                    // Anything at the children's own depth (their own footprints) or finer
                    List<GeographicFeature> list;
                    if (near.TryGetValue(child, out list))
                        foreach (GeographicFeature feature in list) if (feature.Depth == child.Depth && !here.Contains(feature) && feature.Contains(child)) here.Add(feature);
                    if (inside.TryGetValue(child, out list))
                        foreach (GeographicFeature feature in list) if (!here.Contains(feature) && feature.Contains(child)) here.Add(feature);
                    if (here.Count > 1) here.Sort((a, b) => order[a].CompareTo(order[b]));
                    result[i, j] = here;
                }
            return result;
        }

        private void Gather(WorldBlock listed, WorldBlock level, HashSet<GeographicFeature> found)
        {
            List<GeographicFeature> list;
            if (!near.TryGetValue(listed, out list)) return;
            foreach (GeographicFeature feature in list)
                if (feature.Depth == level.Depth) found.Add(feature);
        }

        public List<GeographicFeature> At(WorldBlock block)
        {
            HashSet<GeographicFeature> found = new HashSet<GeographicFeature>();
            List<GeographicFeature> list;
            for (WorldBlock b = block; b != null && b.ParentBlock != null; b = b.ParentBlock)
            {
                Check(b, b, block, found);
                foreach (WorldBlock beside in b.SurroundingBlocks())
                    if (beside != null) Check(beside, b, block, found);
            }
            if (inside.TryGetValue(block, out list))
                foreach (GeographicFeature feature in list)
                    if (!found.Contains(feature) && feature.Contains(block)) found.Add(feature);
            return found.OrderBy(f => order[f]).ToList();
        }
    }
}
