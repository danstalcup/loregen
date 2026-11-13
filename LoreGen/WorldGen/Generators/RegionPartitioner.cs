using System;
using System.Collections.Generic;
using System.Linq;

using LoreGen.Randomizer;

namespace LoreGen.WorldGen.Generators
{
    /// <summary>
    /// Divides a continent into regions defined by its terrain and features.
    ///
    /// First the continent is split into zones: connected stretches of one landscape (the climate, ruggedness and cover of
    /// their terrain, see <see cref="Landscape"/>) under one set of features (inside a mountain range, a desert, both or
    /// neither). Each zone big enough to matter gets region centres in proportion to its size, the first at its heart (the
    /// block farthest from its edge) and the rest spread out within it.
    ///
    /// Each region then spreads outward in a race where every step has a cost, and a block goes to whichever region reaches
    /// it most cheaply. Stepping into a different landscape or across a feature's edge is expensive, as are steps across
    /// slopes, onto high ground and across changes of temperature or moisture, so regions fill their own zone first and
    /// meet at the zone's edges, ridges and climate transitions, rather than at arbitrary lines. Zones too small for a
    /// region of their own go to whichever neighbour reaches them first. A little random unevenness keeps borders from
    /// being too regular. Regions that end up very small are merged into a neighbour.
    ///
    /// Every block is reached by following a chain of blocks from the region that won it, so each region is connected.
    /// </summary>
    internal static class RegionPartitioner
    {
        /// <summary>
        /// The landscape a terrain belongs to, as one comparable value: the broad kind of its climate (cold, temperate and
        /// green, temperate and dry, hot and green, or hot and dry, from its primary climate type's ideal), how rugged it is
        /// (low ground, hills or mountains) and whether it is wooded. Neighbouring blocks with different values lie in
        /// different landscapes.
        /// </summary>
        public static int Landscape(Terrains.Terrain terrain)
        {
            if (terrain == null) return 0;
            Terrains.ClimateType climate = terrain.Climate.PrimaryClimateType;
            int kind = climate.IdealTemperature < 0.25 ? 0 : 1 + (climate.IdealTemperature >= 0.6 ? 2 : 0) + (climate.IdealMoisture < 0.25 ? 1 : 0);
            Terrains.TerrainType type = terrain.TerrainType;
            int rugged = type.Relief <= 1 ? 0 : type.Relief == 2 ? 1 : 2;
            int wooded = type.Trees >= 0.5 ? 1 : 0;
            return 1 + kind * 6 + rugged * 2 + wooded;
        }

        /// <summary>
        /// Splits a continent's blocks into connected groups, about the given number. Each group's first block is its centre.
        /// </summary>
        /// <param name="blocks">All the continent's blocks; they must be connected</param>
        /// <param name="count">Number of regions to aim for, between 1 and the number of blocks</param>
        /// <param name="featureSignatures">Optional: for each block, a value saying which features cover it (blocks on two
        /// sides of a feature's edge have different values). Crossing from one value to another costs RegionFeatureWeight.</param>
        /// <param name="landscapes">Optional: for each block, its landscape (see <see cref="Landscape"/>). Crossing from one
        /// to another costs RegionTerrainWeight.</param>
        /// <remarks>Without features or landscapes the centres are simply spread out and exactly <paramref name="count"/> are
        /// used (fewer after small regions merge). With them, each zone gets its share, so the number can differ a little.</remarks>
        public static List<List<WorldBlock>> Partition(List<WorldBlock> blocks, int count, NoiseGeneratorRules rules, Rnd rnd,
            IReadOnlyDictionary<WorldBlock, int> featureSignatures = null, IReadOnlyDictionary<WorldBlock, int> landscapes = null)
        {
            bool zoned = (featureSignatures != null && rules.RegionFeatureWeight > 0) || (landscapes != null && rules.RegionTerrainWeight > 0);
            List<WorldBlock> seeds = zoned
                ? ZoneSeeds(blocks, count, rules, rnd, featureSignatures, landscapes)
                : ChooseSeeds(blocks, count, rules.RegionSeedCandidates, rnd);

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
                    queue.Enqueue((next, item.Group), (priority.Cost + StepCost(item.Block, next, jitter[next], rules, featureSignatures, landscapes), order++));
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
        internal static void MergeSmallRegions(List<List<WorldBlock>> groups, int minimum)
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
        private static double StepCost(WorldBlock from, WorldBlock to, double jitter, NoiseGeneratorRules rules,
            IReadOnlyDictionary<WorldBlock, int> featureSignatures, IReadOnlyDictionary<WorldBlock, int> landscapes)
        {
            double featureEdge = featureSignatures != null && featureSignatures[from] != featureSignatures[to] ? rules.RegionFeatureWeight : 0.0;
            if (landscapes != null) featureEdge += rules.RegionTerrainWeight * LandscapeDifference(landscapes[from], landscapes[to]);
            WorldBlockStatus a = from.Status, b = to.Status;
            double slope = Math.Abs(a.Elevation.Value - b.Elevation.Value);
            double climateChange = Math.Abs(a.Temperature.Value - b.Temperature.Value) + Math.Abs(a.Moisture.Value - b.Moisture.Value);
            return 1.0 + rules.RegionSlopeWeight * slope + rules.RegionHeightWeight * b.Elevation.Value
                       + rules.RegionClimateWeight * climateChange + featureEdge + jitter;
        }

        /// <summary>
        /// How different two landscapes are: 1 for a different kind of climate, 0.5 for each step of ruggedness and 0.5 for
        /// wooded against open, so 0 for the same landscape and at most 2.5.
        /// </summary>
        public static double LandscapeDifference(int a, int b)
        {
            if (a == b) return 0;
            if (a <= 0 || b <= 0) return 1;
            a--; b--;
            return (a / 6 != b / 6 ? 1.0 : 0.0) + 0.5 * Math.Abs((a % 6) / 2 - (b % 6) / 2) + (a % 2 != b % 2 ? 0.5 : 0.0);
        }

        /// <summary>
        /// The connected stretches of blocks with the same landscape and features, largest first.
        /// </summary>
        public static List<List<WorldBlock>> Zones(List<WorldBlock> blocks, IReadOnlyDictionary<WorldBlock, int> featureSignatures,
            IReadOnlyDictionary<WorldBlock, int> landscapes)
        {
            Func<WorldBlock, (int, int)> key = b => (featureSignatures != null ? featureSignatures[b] : 0, landscapes != null ? landscapes[b] : 0);
            HashSet<WorldBlock> inside = new HashSet<WorldBlock>(blocks);
            HashSet<WorldBlock> seen = new HashSet<WorldBlock>();
            List<List<WorldBlock>> zones = new List<List<WorldBlock>>();
            foreach (WorldBlock start in blocks)
            {
                if (!seen.Add(start)) continue;
                List<WorldBlock> zone = new List<WorldBlock> { start };
                var value = key(start);
                for (int i = 0; i < zone.Count; i++)
                    foreach (WorldBlock next in zone[i].SurroundingBlocksAsList())
                        if (inside.Contains(next) && !seen.Contains(next) && key(next).Equals(value)) { seen.Add(next); zone.Add(next); }
                zones.Add(zone);
            }
            // Stable: equal sizes keep the order they were found in
            return zones.Select((z, i) => (z, i)).OrderByDescending(p => p.z.Count).ThenBy(p => p.i).Select(p => p.z).ToList();
        }

        /// <summary>
        /// Region centres for zones, as many as asked for (or as there are blocks):
        ///  - every zone of at least RegionZoneMinBlocks gets one, largest first;
        ///  - if more are wanted, smaller zones of at least RegionMinBlocks get one too, largest first;
        ///  - the rest are shared out among the zones that have one, by size (largest remainder first), at first never more
        ///    than a zone has room for at RegionMinBlocks each, and beyond that only if there is no other way.
        /// The first centre of a zone is at the block farthest from its edge and the rest are spread out within it.
        /// </summary>
        private static List<WorldBlock> ZoneSeeds(List<WorldBlock> blocks, int count, NoiseGeneratorRules rules, Rnd rnd,
            IReadOnlyDictionary<WorldBlock, int> featureSignatures, IReadOnlyDictionary<WorldBlock, int> landscapes)
        {
            if (count < 1 || count > blocks.Count) throw new ArgumentOutOfRangeException("count", "Region count must be between 1 and the number of blocks.");
            List<List<WorldBlock>> zones = Zones(blocks, featureSignatures, landscapes);
            int minimum = Math.Max(1, rules.RegionMinBlocks);

            List<List<WorldBlock>> chosen = zones.Where((z, i) => i == 0 || z.Count >= rules.RegionZoneMinBlocks).Take(count).ToList();
            foreach (List<WorldBlock> zone in zones)
            {
                if (chosen.Count >= count) break;
                if (zone.Count >= minimum && !chosen.Contains(zone)) chosen.Add(zone);
            }

            int total = chosen.Sum(z => z.Count);
            double[] quota = chosen.Select(z => (double)z.Count * count / total).ToArray();
            int[] share = chosen.Select(z => 1).ToArray();
            foreach (bool roomy in new[] { true, false })
            {
                while (share.Sum() < count)
                {
                    int best = -1;
                    for (int i = 0; i < chosen.Count; i++)
                    {
                        int room = roomy ? Math.Max(1, chosen[i].Count / minimum) : chosen[i].Count;
                        if (share[i] < room && (best < 0 || quota[i] - share[i] > quota[best] - share[best])) best = i;
                    }
                    if (best < 0) break;
                    share[best]++;
                }
            }

            List<WorldBlock> seeds = new List<WorldBlock>();
            for (int i = 0; i < chosen.Count; i++)
            {
                List<WorldBlock> zone = chosen[i];
                WorldBlock heart = Heart(zone);
                seeds.Add(heart);
                if (share[i] > 1)
                    seeds.AddRange(SpreadFrom(new List<WorldBlock> { heart }, zone.Where(b => !ReferenceEquals(b, heart)).ToList(), share[i] - 1, rules.RegionSeedCandidates, rnd));
            }
            return seeds;
        }

        // The block of a zone farthest (in steps) from any block outside it, the first such in the zone's order
        private static WorldBlock Heart(List<WorldBlock> zone)
        {
            HashSet<WorldBlock> inside = new HashSet<WorldBlock>(zone);
            Dictionary<WorldBlock, int> depth = new Dictionary<WorldBlock, int>();
            List<WorldBlock> frontier = new List<WorldBlock>();
            foreach (WorldBlock block in zone)
            {
                List<WorldBlock> around = block.SurroundingBlocksAsList();
                if (around.Count < 4 || around.Any(b => !inside.Contains(b))) { depth[block] = 0; frontier.Add(block); }
            }
            for (int i = 0; i < frontier.Count; i++)
                foreach (WorldBlock next in frontier[i].SurroundingBlocksAsList())
                    if (inside.Contains(next) && !depth.ContainsKey(next)) { depth[next] = depth[frontier[i]] + 1; frontier.Add(next); }
            WorldBlock heart = zone[0];
            foreach (WorldBlock block in zone)
                if (depth.ContainsKey(block) && depth[block] > (depth.ContainsKey(heart) ? depth[heart] : -1)) heart = block;
            return heart;
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
            seeds.AddRange(SpreadFrom(seeds, remaining, count - 1, candidatesPerSeed, rnd));
            return seeds;
        }

        // Adds centres from the remaining blocks, each the farthest from those already chosen of a handful of random candidates
        private static List<WorldBlock> SpreadFrom(List<WorldBlock> existing, List<WorldBlock> remaining, int count, int candidatesPerSeed, Rnd rnd)
        {
            List<WorldBlock> chosen = existing.ToList();
            List<WorldBlock> added = new List<WorldBlock>();
            remaining = remaining.ToList();
            while (added.Count < count && remaining.Count > 0)
            {
                WorldBlock best = null;
                double bestDistance = -1.0;
                for (int k = 0; k < candidatesPerSeed; k++)
                {
                    WorldBlock candidate = remaining[rnd.Unweighted(remaining.Count)];
                    double nearest = chosen.Min(s => (double)((s.X - candidate.X) * (s.X - candidate.X) + (s.Y - candidate.Y) * (s.Y - candidate.Y)));
                    if (nearest > bestDistance)
                    {
                        bestDistance = nearest;
                        best = candidate;
                    }
                }
                remaining.Remove(best);
                chosen.Add(best);
                added.Add(best);
            }
            return added;
        }
    }
}
