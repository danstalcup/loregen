using System.Collections.Generic;
using System.Linq;

using LoreGen.WorldGen;

namespace LoreGen.Features
{
    /// <summary>
    /// Finds the features covering a block quickly. Each feature is listed under every block that might be part of it:
    /// its footprint blocks, the blocks beside them (where a refiner may extend its edge) and the blocks containing them.
    /// A lookup gathers the candidates listed under the block and its ancestors, then asks each whether it really covers
    /// the block.
    /// </summary>
    internal class FeatureIndex
    {
        private readonly Dictionary<WorldBlock, List<GeographicFeature>> candidates = new Dictionary<WorldBlock, List<GeographicFeature>>();
        private readonly Dictionary<GeographicFeature, int> order = new Dictionary<GeographicFeature, int>();

        public FeatureIndex(IReadOnlyList<GeographicFeature> features)
        {
            for (int i = 0; i < features.Count; i++)
            {
                GeographicFeature feature = features[i];
                order[feature] = i;
                HashSet<WorldBlock> listed = new HashSet<WorldBlock>();
                foreach (WorldBlock block in feature.Footprint.Blocks)
                {
                    listed.Add(block);
                    foreach (WorldBlock beside in block.SurroundingBlocksAsList()) listed.Add(beside);
                    for (WorldBlock a = block.ParentBlock; a != null && a.ParentBlock != null; a = a.ParentBlock) listed.Add(a);
                }
                foreach (WorldBlock block in listed)
                {
                    List<GeographicFeature> list;
                    if (!candidates.TryGetValue(block, out list)) candidates[block] = list = new List<GeographicFeature>();
                    list.Add(feature);
                }
            }
        }

        /// <summary>
        /// The features covering a block, in the order they were added to the world.
        /// </summary>
        public List<GeographicFeature> At(WorldBlock block)
        {
            HashSet<GeographicFeature> found = new HashSet<GeographicFeature>();
            for (WorldBlock b = block; b != null; b = b.ParentBlock)
            {
                List<GeographicFeature> list;
                if (!candidates.TryGetValue(b, out list)) continue;
                foreach (GeographicFeature feature in list)
                    if (!found.Contains(feature) && feature.Contains(block)) found.Add(feature);
            }
            return found.OrderBy(f => order[f]).ToList();
        }
    }
}
