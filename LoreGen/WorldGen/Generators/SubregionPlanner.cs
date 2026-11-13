using System;
using System.Collections.Generic;
using System.Linq;

using LoreGen.Drawing;
using LoreGen.Features;
using LoreGen.Lang;
using LoreGen.Randomizer;
using LoreGen.WorldDisplay;

namespace LoreGen.WorldGen.Generators
{
    /// <summary>
    /// Divides a region into subregions the first time they are asked for (see <see cref="World.SubregionsOf"/>).
    ///
    /// The region's 100 km land blocks are made (zooming in on its 1000 km blocks and those around them, which is pure,
    /// see <see cref="ZoomDetail"/>) and split the way continents are split into regions (<see cref="RegionPartitioner"/>):
    /// along landscapes and the edges of the world's larger features, about one subregion per SubregionBlocks blocks, at
    /// most SubregionsMax. Pieces of the region cut off by water are divided separately; tiny ones join the largest
    /// subregion. Each subregion is named after its land ("Hills", "Fens", "Weald"...) in the region's language.
    ///
    /// Everything depends only on the world seed and the region, never on what was asked for first.
    /// </summary>
    internal sealed class SubregionPlanner
    {
        private readonly World world;
        private readonly NoiseGeneratorRules rules;
        private readonly int worldSeed;
        private readonly HashSet<Region> dividing = new HashSet<Region>();

        private static readonly Dictionary<SubregionKind, string[]> Landforms = new Dictionary<SubregionKind, string[]>
        {
            { SubregionKind.Highlands, new[] { "Heights", "Highlands", "Crags", "Fells" } },
            { SubregionKind.Hills, new[] { "Hills", "Downs", "Uplands", "Wolds" } },
            { SubregionKind.Woods, new[] { "Woods", "Weald", "Forest", "Woodlands" } },
            { SubregionKind.Wetlands, new[] { "Fens", "Marshes", "Mires", "Wetlands" } },
            { SubregionKind.Frozen, new[] { "Barrens", "Snowfields", "Tundra", "Wastes" } },
            { SubregionKind.Drylands, new[] { "Wastes", "Barrens", "Flats", "Badlands" } },
            { SubregionKind.Plains, new[] { "Plains", "Vale", "Meadows", "Lowlands", "Fields" } },
        };

        public SubregionPlanner(World world, NoiseGeneratorRules rules, int worldSeed)
        {
            this.world = world;
            this.rules = rules;
            this.worldSeed = worldSeed;
        }

        public void Divide(Region region)
        {
            if (region.Subregions != null || !dividing.Add(region)) return;
            try
            {
                region.Subregions = Make(region);
            }
            finally
            {
                dividing.Remove(region);
            }
        }

        private List<Subregion> Make(Region region)
        {
            // Every 100 km block that could belong to the region: those in its 1000 km blocks and the ones around them
            // (region borders wander up to half a block)
            WorldBlock[,] top = world.WorldBlock.ChildBlocks;
            int width = top.GetLength(0), height = top.GetLength(1);
            var tops = new HashSet<WorldBlock>();
            foreach (WorldBlock block in region.RegionArea.Blocks())
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int x = block.X + dx, y = block.Y + dy;
                        if (x >= 0 && y >= 0 && x < width && y < height) tops.Add(top[x, y]);
                    }
            foreach (WorldBlock block in tops.OrderBy(b => b.Y).ThenBy(b => b.X)) world.EnsureChildren(block);

            List<WorldBlock> cells = tops.Where(t => t.ChildBlocks != null).SelectMany(t => t.ChildBlocks.Cast<WorldBlock>())
                .Where(c => c.Status.Region == region && c.Status.WaterStatus != WorldBlockWaterStatus.Water && c.Status.Terrain != null)
                .OrderBy(c => c.TrueY).ThenBy(c => c.TrueX).ToList();
            var result = new List<Subregion>();
            if (cells.Count == 0) return result;

            int seed = Seed(cells[0], 0x5b);
            Rnd rnd = new Rnd(seed);
            var pieces = Pieces(cells);
            int target = Math.Max(1, Math.Min(rules.SubregionsMax, (int)Math.Round(cells.Count / (double)rules.SubregionBlocks)));
            var landscapes = cells.ToDictionary(c => c, c => RegionPartitioner.Landscape(c.Status.Terrain));
            var signatures = Signatures(cells);

            var groups = new List<List<WorldBlock>>();
            var leftovers = new List<List<WorldBlock>>();
            // Pieces cut off by water that are smaller than half a subregion (offshore islands) join the nearest subregion
            int islandMax = Math.Max(Math.Max(1, rules.RegionMinBlocks), rules.SubregionBlocks / 2);
            foreach (var piece in pieces)
            {
                if (piece.Count < islandMax && pieces.Count > 1 && groups.Count + leftovers.Count > 0) { leftovers.Add(piece); continue; }
                int count = Math.Max(1, Math.Min(piece.Count, (int)Math.Round(target * piece.Count / (double)cells.Count)));
                groups.AddRange(RegionPartitioner.Partition(piece, count, rules, rnd, signatures, landscapes));
            }
            // Slivers join the neighbour they share the most border with
            RegionPartitioner.MergeSmallRegions(groups, Math.Max(rules.RegionMinBlocks, rules.SubregionBlocks / 4));
            foreach (var island in leftovers) Nearest(groups, island).AddRange(island);
            // Never more than the most allowed: the smallest joins the nearest
            while (groups.Count > Math.Max(1, rules.SubregionsMax))
            {
                var smallest = groups.OrderBy(g => g.Count).ThenBy(g => g[0].TrueY).ThenBy(g => g[0].TrueX).First();
                groups.Remove(smallest);
                Nearest(groups, smallest).AddRange(smallest);
            }

            foreach (var group in groups)
            {
                var blocks = group.OrderBy(c => c.TrueY).ThenBy(c => c.TrueX).ToList();
                int subSeed = Seed(blocks[0], 0x5c);
                Rnd own = new Rnd(subSeed);
                SubregionKind kind = KindOf(blocks);
                string[] words = Landforms[kind];
                string landform = words[own.Unweighted(words.Length)];
                Subregion subregion = new Subregion(world, region, kind, landform, blocks, subSeed);
                // Names within a region differ (tried again from the next seed if not)
                ILanguage language = region.Language ?? region.Continent.RootLanguage ?? world.Language;
                for (int attempt = 0; attempt < 10; attempt++)
                {
                    subregion.Name = world.SimEngine.Language.NameFromSeed(language,
                        new NameContext(NameKind.Subregion) { Landform = landform, ParentName = region.Name }.WithClimate(NameHints.Climate(blocks)), unchecked(subSeed + attempt * 7919));
                    if (!result.Any(s => s.Name == subregion.Name)) break;
                }
                // A shade of the region's colour, so subregions read as parts of it
                Color baseColor = region.DisplayInfo?.DisplayColor ?? Color.FromArgb(128, 128, 128);
                Func<int, int> shade = c => Math.Max(0, Math.Min(255, c + own.Unweighted(-75, 75)));
                subregion.DisplayInfo = new WorldEntityDisplayInfo { Entity = subregion, DisplayColor = Color.FromArgb(shade(baseColor.R), shade(baseColor.G), shade(baseColor.B)) };
                foreach (WorldBlock block in blocks) block.Status.Subregion = subregion;
                result.Add(subregion);
            }
            return result;
        }

        // The group whose blocks come nearest to any of the given blocks (ties to the first)
        private static List<WorldBlock> Nearest(List<List<WorldBlock>> groups, List<WorldBlock> blocks)
        {
            List<WorldBlock> best = groups[0];
            double bestDistance = double.MaxValue;
            foreach (var group in groups)
            {
                foreach (WorldBlock a in group)
                    foreach (WorldBlock b in blocks)
                    {
                        double dx = a.TrueX - b.TrueX, dy = a.TrueY - b.TrueY, distance = dx * dx + dy * dy;
                        if (distance < bestDistance) { bestDistance = distance; best = group; }
                    }
            }
            return best;
        }

        private int Seed(WorldBlock block, int salt)
        {
            unchecked
            {
                int h = worldSeed * 486187739 + salt;
                h = h * 31 + block.TrueX;
                h = h * 31 + block.TrueY;
                h = h * 31 + block.Depth;
                return h;
            }
        }

        // Connected pieces (side by side) of the blocks, largest first
        private static List<List<WorldBlock>> Pieces(List<WorldBlock> cells)
        {
            var inside = new HashSet<WorldBlock>(cells);
            var seen = new HashSet<WorldBlock>();
            var pieces = new List<List<WorldBlock>>();
            foreach (WorldBlock start in cells)
            {
                if (!seen.Add(start)) continue;
                var piece = new List<WorldBlock>();
                var queue = new Queue<WorldBlock>();
                queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    WorldBlock block = queue.Dequeue();
                    piece.Add(block);
                    foreach (WorldBlock next in block.SurroundingBlocksAsList())
                        if (inside.Contains(next) && seen.Add(next)) queue.Enqueue(next);
                }
                pieces.Add(piece.OrderBy(c => c.TrueY).ThenBy(c => c.TrueX).ToList());
            }
            return pieces.OrderByDescending(p => p.Count).ThenBy(p => p[0].TrueY).ThenBy(p => p[0].TrueX).ToList();
        }

        // Which of the world's own features (100 km or larger, made with the world) cover each block
        private Dictionary<WorldBlock, int> Signatures(List<WorldBlock> cells)
        {
            var number = new Dictionary<GeographicFeature, int>();
            for (int i = 0; i < world.Features.Count; i++) number[world.Features[i]] = i + 1;
            var signatures = new Dictionary<WorldBlock, int>();
            foreach (WorldBlock cell in cells)
            {
                int signature = 0;
                foreach (GeographicFeature feature in world.FeaturesAt(cell))
                    if (feature.Depth <= 2) signature = unchecked(signature * 31 + number[feature]);
                signatures[cell] = signature;
            }
            return signatures;
        }

        // The kind of land most of the blocks are
        private static SubregionKind KindOf(List<WorldBlock> blocks)
        {
            int count = blocks.Count;
            Func<Func<Terrains.TerrainType, bool>, double> share = test => blocks.Count(b => test(b.Status.Terrain.TerrainType)) / (double)count;
            Func<Func<int, bool>, double> climate = test => blocks.Count(b => test((RegionPartitioner.Landscape(b.Status.Terrain) - 1) / 6)) / (double)count;
            if (share(t => t.Relief >= 3) >= 0.4) return SubregionKind.Highlands;
            if (share(t => t.Wetness >= 0.7) >= 0.35) return SubregionKind.Wetlands;
            if (share(t => t.Relief >= 2) >= 0.4) return SubregionKind.Hills;
            if (share(t => t.Trees >= 0.5) >= 0.5) return SubregionKind.Woods;
            if (climate(k => k == 0) >= 0.5) return SubregionKind.Frozen;
            if (climate(k => k == 2 || k == 4) >= 0.5) return SubregionKind.Drylands;
            return SubregionKind.Plains;
        }
    }
}
