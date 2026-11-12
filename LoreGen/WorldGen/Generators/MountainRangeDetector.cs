using System;
using System.Collections.Generic;
using System.Linq;

using LoreGen.Features;
using LoreGen.Lang;
using LoreGen.Simulation;

namespace LoreGen.WorldGen.Generators
{
    /// <summary>
    /// Finds continental mountain ranges in the elevation of a world's top-level blocks.
    ///
    /// High ground is land at or above <see cref="NoiseGeneratorRules.MountainElevationFrom"/>. High blocks on the same
    /// continent belong to the same range when they touch (at a side or a corner), or when they are joined by a pass: a
    /// chain of no more than <see cref="NoiseGeneratorRules.MountainRangeGap"/> lower blocks that are still at least
    /// <see cref="NoiseGeneratorRules.MountainPassShare"/> of the mountain height (a saddle, not a valley). Groups smaller than
    /// <see cref="NoiseGeneratorRules.MountainRangeMinBlocks"/> are left out. Each range's footprint is its high blocks
    /// (passes are not part of it, so a range can be in several pieces), and its spine runs between its two farthest
    /// apart ends through the range. Below the top level a range's edge follows the fine elevation (ElevationEdgeRefiner).
    ///
    /// Uses no random numbers except for naming (which draws on the separate naming generator), so it does not change
    /// anything else about the world.
    /// </summary>
    internal static class MountainRangeDetector
    {
        public const string TypeId = "MountainRange";

        public static List<GeographicFeature> Detect(SimEngine engine, World world, NoiseGeneratorRules rules)
        {
            List<GeographicFeature> found = new List<GeographicFeature>();
            if (rules.MountainRanges == 0) return found;

            FeatureType type = engine.SimData.FeatureType(TypeId);
            WorldBlock[,] grid = world.WorldBlock.ChildBlocks;
            int width = grid.GetLength(0), height = grid.GetLength(1);

            // High blocks, in scan order (top row first, left to right) so results don't depend on anything else
            List<WorldBlock> high = new List<WorldBlock>();
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    if (IsHigh(grid[x, y], rules)) high.Add(grid[x, y]);

            int reach = 1 + rules.MountainRangeGap;
            double passFrom = rules.MountainElevationFrom * rules.MountainPassShare;
            foreach (List<WorldBlock> group in Group(high, grid, rules.MountainRangeGap, b => IsPass(b, passFrom)))
            {
                if (group.Count < rules.MountainRangeMinBlocks) continue;

                Continent continent = group[0].Status.Continent;
                GeographicFeature range = new GeographicFeature(world, type, group, GeographicFeature.SeedFor(engine.Rnd.Seed, type, group[0]));
                range.SetSpine(FeatureGeometry.Spine(group, reach));
                // Below the top level, the range's edge follows the fine elevation's contours
                range.Refiner = new ElevationEdgeRefiner(rules.MountainElevationFrom);
                range.Name = continent.RootLanguage.GetName(new NameContext(type.NameKind)
                {
                    ParentName = continent.Name,
                    Direction = NameHints.DirectionWithin(group, continent.ContinentArea.Blocks())
                });
                world.AddFeature(range);
                found.Add(range);
            }
            return found;
        }

        private static bool IsHigh(WorldBlock block, NoiseGeneratorRules rules)
        {
            WorldBlockStatus s = block.Status;
            return s.WaterStatus != WorldBlockWaterStatus.Water && s.Continent != null
                && s.Elevation.HasValue && s.Elevation.Value >= rules.MountainElevationFrom;
        }

        private static bool IsPass(WorldBlock block, double passFrom)
        {
            WorldBlockStatus s = block.Status;
            return s.WaterStatus != WorldBlockWaterStatus.Water && s.Elevation.HasValue && s.Elevation.Value >= passFrom;
        }

        // Groups high blocks of the same continent that touch, or are joined through at most `gap` pass blocks, in order
        // of each group's first block. Blocks keep scan order within a group. Neighbours include corners.
        private static List<List<WorldBlock>> Group(List<WorldBlock> high, WorldBlock[,] grid, int gap, Func<WorldBlock, bool> isPass)
        {
            Dictionary<WorldBlock, int> index = new Dictionary<WorldBlock, int>();
            for (int i = 0; i < high.Count; i++) index[high[i]] = i;

            int[] parent = Enumerable.Range(0, high.Count).ToArray();
            Func<int, int> find = null;
            find = i => parent[i] == i ? i : (parent[i] = find(parent[i]));

            int width = grid.GetLength(0), height = grid.GetLength(1);
            for (int i = 0; i < high.Count; i++)
            {
                // Search outward from this block through pass blocks, at most `gap` of them in a row; every high block
                // reached on the same continent joins this block's range
                WorldBlock block = high[i];
                Dictionary<WorldBlock, int> steps = new Dictionary<WorldBlock, int> { { block, 0 } };
                Queue<WorldBlock> pending = new Queue<WorldBlock>();
                pending.Enqueue(block);
                while (pending.Count > 0)
                {
                    WorldBlock current = pending.Dequeue();
                    int passesSoFar = steps[current];
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            int nx = current.X + dx, ny = current.Y + dy;
                            if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                            WorldBlock next = grid[nx, ny];
                            if (steps.ContainsKey(next) || next.Status.Continent != block.Status.Continent) continue;
                            int j;
                            if (index.TryGetValue(next, out j))
                            {
                                steps[next] = passesSoFar; // reached; don't search on from it (its own search does that)
                                int a = find(i), b = find(j);
                                if (a != b) parent[Math.Max(a, b)] = Math.Min(a, b); // the earlier root wins, so order is stable
                            }
                            else if (passesSoFar < gap && isPass(next))
                            {
                                steps[next] = passesSoFar + 1;
                                pending.Enqueue(next);
                            }
                        }
                    }
                }
            }

            Dictionary<int, List<WorldBlock>> groups = new Dictionary<int, List<WorldBlock>>();
            List<List<WorldBlock>> ordered = new List<List<WorldBlock>>();
            for (int i = 0; i < high.Count; i++)
            {
                int root = find(i);
                List<WorldBlock> group;
                if (!groups.TryGetValue(root, out group))
                {
                    groups[root] = group = new List<WorldBlock>();
                    ordered.Add(group);
                }
                group.Add(high[i]);
            }
            return ordered;
        }
    }
}
