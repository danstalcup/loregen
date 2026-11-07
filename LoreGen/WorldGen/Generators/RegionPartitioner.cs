using System;
using System.Collections.Generic;
using System.Linq;

using LoreGen.Randomizer;

namespace LoreGen.WorldGen.Generators
{
    /// <summary>
    /// Divides a continent into regions that follow its geography.
    ///
    /// Region centres are spread out across the continent. Each region then spreads outward in a race where every step
    /// has a cost, and a block goes to whichever region reaches it most cheaply. Steps are expensive across slopes, onto
    /// high ground and across changes of temperature or moisture, so regions fill valleys and plains first and meet at
    /// ridges and climate transitions, rather than at arbitrary lines. A little random unevenness keeps borders from
    /// being too regular. Regions that end up very small are merged into a neighbour.
    ///
    /// Every block is reached by following a chain of blocks from the region that won it, so each region is connected.
    /// </summary>
    internal static class RegionPartitioner
    {
        /// <summary>
        /// Splits a continent's blocks into the given number of connected groups. Each group's first block is its centre.
        /// </summary>
        /// <param name="blocks">All the continent's blocks; they must be connected</param>
        /// <param name="count">Number of regions, between 1 and the number of blocks</param>
        public static List<List<WorldBlock>> Partition(List<WorldBlock> blocks, int count, NoiseGeneratorRules rules, Rnd rnd)
        {
            List<WorldBlock> seeds = ChooseSeeds(blocks, count, rules.RegionSeedCandidates, rnd);

            // Random unevenness is decided up front, in a fixed order, so results don't depend on search order
            Dictionary<WorldBlock, double> jitter = new Dictionary<WorldBlock, double>();
            foreach (WorldBlock block in blocks) jitter[block] = rules.RegionJitter * rnd.R01();

            HashSet<WorldBlock> inside = new HashSet<WorldBlock>(blocks);
            List<List<WorldBlock>> groups = seeds.Select(s => new List<WorldBlock>()).ToList();
            HashSet<WorldBlock> claimed = new HashSet<WorldBlock>();

            // Cheapest-first search from all the centres at once. The running count breaks ties the same way every time.
            PriorityQueue<(WorldBlock Block, int Group), (double Cost, long Order)> queue = new PriorityQueue<(WorldBlock Block, int Group), (double Cost, long Order)>();
            long order = 0;
            for (int g = 0; g < seeds.Count; g++) queue.Enqueue((seeds[g], g), (0.0, order++));

            while (queue.TryDequeue(out var item, out var priority))
            {
                if (!claimed.Add(item.Block)) continue;
                groups[item.Group].Add(item.Block);

                foreach (WorldBlock next in item.Block.SurroundingBlocksAsList())
                {
                    if (!inside.Contains(next) || claimed.Contains(next)) continue;
                    queue.Enqueue((next, item.Group), (priority.Cost + StepCost(item.Block, next, jitter[next], rules), order++));
                }
            }

            if (claimed.Count != blocks.Count)
                throw new InvalidOperationException((blocks.Count - claimed.Count) + " block(s) of a continent could not be reached from any region centre.");

            MergeSmallRegions(groups, rules.RegionMinBlocks);
            return groups;
        }

        /// <summary>
        /// A centre that lands on steep or high ground can be hemmed in, leaving a region of a block or two. Merges every
        /// region smaller than the minimum into the neighbour it shares the most border with, smallest first.
        /// </summary>
        private static void MergeSmallRegions(List<List<WorldBlock>> groups, int minimum)
        {
            while (groups.Count > 1)
            {
                List<WorldBlock> smallest = null;
                foreach (List<WorldBlock> group in groups)
                    if (group.Count < minimum && (smallest == null || group.Count < smallest.Count)) smallest = group;
                if (smallest == null) return;

                // Length of shared border with each neighbouring region, in the order they are first met
                Dictionary<WorldBlock, List<WorldBlock>> groupOf = new Dictionary<WorldBlock, List<WorldBlock>>();
                foreach (List<WorldBlock> group in groups) foreach (WorldBlock block in group) groupOf[block] = group;
                List<List<WorldBlock>> neighbours = new List<List<WorldBlock>>();
                List<int> border = new List<int>();
                foreach (WorldBlock block in smallest)
                {
                    foreach (WorldBlock next in block.SurroundingBlocksAsList())
                    {
                        List<WorldBlock> other;
                        if (!groupOf.TryGetValue(next, out other) || ReferenceEquals(other, smallest)) continue;
                        int index = neighbours.IndexOf(other);
                        if (index < 0) { neighbours.Add(other); border.Add(1); }
                        else border[index]++;
                    }
                }
                if (neighbours.Count == 0) return; // nothing to merge into (a continent that is a single region)

                int best = 0;
                for (int i = 1; i < neighbours.Count; i++)
                    if (border[i] > border[best] || (border[i] == border[best] && neighbours[i].Count < neighbours[best].Count)) best = i;

                neighbours[best].AddRange(smallest);
                groups.Remove(smallest);
            }
        }

        // Cost of a region spreading from one block to the next
        private static double StepCost(WorldBlock from, WorldBlock to, double jitter, NoiseGeneratorRules rules)
        {
            WorldBlockStatus a = from.Status, b = to.Status;
            double slope = Math.Abs(a.Elevation.Value - b.Elevation.Value);
            double climateChange = Math.Abs(a.Temperature.Value - b.Temperature.Value) + Math.Abs(a.Moisture.Value - b.Moisture.Value);
            return 1.0 + rules.RegionSlopeWeight * slope + rules.RegionHeightWeight * b.Elevation.Value
                       + rules.RegionClimateWeight * climateChange + jitter;
        }

        /// <summary>
        /// Picks region centres that are spread out: each new centre is the farthest from the existing ones of a handful
        /// of random candidates (so it is well spaced but not a perfect grid).
        /// </summary>
        public static List<WorldBlock> ChooseSeeds(List<WorldBlock> blocks, int count, int candidatesPerSeed, Rnd rnd)
        {
            if (count < 1 || count > blocks.Count) throw new ArgumentOutOfRangeException("count", "Region count must be between 1 and the number of blocks.");

            List<WorldBlock> remaining = blocks.ToList();
            List<WorldBlock> seeds = new List<WorldBlock>();
            WorldBlock first = remaining[rnd.Unweighted(remaining.Count)];
            remaining.Remove(first);
            seeds.Add(first);

            while (seeds.Count < count)
            {
                WorldBlock best = null;
                double bestDistance = -1.0;
                for (int k = 0; k < candidatesPerSeed; k++)
                {
                    WorldBlock candidate = remaining[rnd.Unweighted(remaining.Count)];
                    double nearest = seeds.Min(s => (double)((s.X - candidate.X) * (s.X - candidate.X) + (s.Y - candidate.Y) * (s.Y - candidate.Y)));
                    if (nearest > bestDistance)
                    {
                        bestDistance = nearest;
                        best = candidate;
                    }
                }
                remaining.Remove(best);
                seeds.Add(best);
            }
            return seeds;
        }
    }
}
