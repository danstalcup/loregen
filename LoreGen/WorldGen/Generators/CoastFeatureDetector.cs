using System;
using System.Collections.Generic;
using System.Linq;

using LoreGen.Features;
using LoreGen.Lang;
using LoreGen.Simulation;

namespace LoreGen.WorldGen.Generators
{
    /// <summary>
    /// Finds the shapes of the coast made with the world, on the 100 km blocks:
    ///  - Peninsulas: land reaching out into the sea. The land that lies at least PeninsulaCore blocks from the sea is its
    ///    core; growing the core back out by as much covers the mainland and its ordinary coasts, so what it doesn't cover
    ///    is land too narrow to have a core of its own, sticking out from the rest. Each such piece of at least
    ///    PeninsulaMinBlocks blocks still joined to the mainland is a peninsula.
    ///  - Deltas: where a long river meets the sea over flat lowland, the low land around its mouth (named after the river;
    ///    as big as the river's own blocks, so not one of its parts).
    /// </summary>
    internal static class CoastFeatureDetector
    {
        public const string PeninsulaTypeId = "Peninsula";
        public const string DeltaTypeId = "Delta";

        // How far from the sea land must be to be part of a coast's core, in 100 km blocks
        private const int PeninsulaCore = 4;
        // A delta: land within this many blocks of the river's mouth, below this elevation
        private const int DeltaReach = 3;
        private const double DeltaElevation = 0.06;
        private const int DeltaMinBlocks = 4;
        // Only rivers at least this long (in 100 km blocks) build deltas
        private const int DeltaRiverMinBlocks = 6;

        public static List<GeographicFeature> Detect(SimEngine engine, World world, NoiseGeneratorRules rules)
        {
            var found = new List<GeographicFeature>();
            WorldBlock[,] grid = world.WorldBlock.ChildBlocks;
            int width = grid.GetLength(0) * 10, height = grid.GetLength(1) * 10;

            // The sea (ocean and sea water, not lakes) at 100 km, from the detail where there is some and the 1000 km blocks
            // where there isn't
            bool[,] sea = new bool[width, height];
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    WorldBlock top = grid[x / 10, y / 10];
                    WorldBlock cell = top.ChildBlocks != null ? top.ChildBlocks[x % 10, y % 10] : top;
                    sea[x, y] = cell.Status.WaterStatus == WorldBlockWaterStatus.Water && cell.Status.Lake == null;
                }

            FeatureType peninsula = Type(engine, PeninsulaTypeId);
            if (peninsula != null && rules.PeninsulaMinBlocks > 0) found.AddRange(Peninsulas(world, peninsula, grid, sea, rules.PeninsulaMinBlocks));
            FeatureType delta = Type(engine, DeltaTypeId);
            if (delta != null) found.AddRange(Deltas(world, delta, grid, sea));
            return found;
        }

        private static FeatureType Type(SimEngine engine, string id)
        {
            return engine.SimData.FeatureTypes.FirstOrDefault(t => t.Id == id);
        }

        private static readonly (int X, int Y)[] Sides = { (1, 0), (-1, 0), (0, 1), (0, -1) };

        private static List<GeographicFeature> Peninsulas(World world, FeatureType type, WorldBlock[,] grid, bool[,] sea, int minBlocks)
        {
            int width = sea.GetLength(0), height = sea.GetLength(1);
            int[,] toSea = Distances(width, height, (x, y) => sea[x, y], (x, y) => !sea[x, y]);
            int[,] toCore = Distances(width, height, (x, y) => !sea[x, y] && toSea[x, y] >= PeninsulaCore, (x, y) => !sea[x, y]);
            // Land the grown-back core doesn't reach
            Func<int, int, bool> outlying = (x, y) => !sea[x, y] && toCore[x, y] >= PeninsulaCore;

            var found = new List<GeographicFeature>();
            bool[,] seen = new bool[width, height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (seen[x, y] || !outlying(x, y)) continue;
                    var piece = new List<(int X, int Y)>();
                    bool joined = false;
                    var queue = new Queue<(int, int)>();
                    queue.Enqueue((x, y));
                    seen[x, y] = true;
                    while (queue.Count > 0)
                    {
                        var (cx, cy) = queue.Dequeue();
                        piece.Add((cx, cy));
                        foreach (var (dx, dy) in Sides)
                        {
                            int a = cx + dx, b = cy + dy;
                            if (a < 0 || b < 0 || a >= width || b >= height || sea[a, b]) continue;
                            if (!outlying(a, b)) { joined = true; continue; }
                            if (seen[a, b]) continue;
                            seen[a, b] = true;
                            queue.Enqueue((a, b));
                        }
                    }
                    if (!joined || piece.Count < minBlocks) continue;
                    var blocks = piece.Select(p => WaterFeatureDetector.FineCell(grid, p.X, p.Y)).Where(b => b != null && b.Status.Continent != null)
                        .OrderBy(b => b.TrueY).ThenBy(b => b.TrueX).ToList();
                    if (blocks.Count < minBlocks) continue;
                    var feature = new GeographicFeature(world, type, blocks, GeographicFeature.SeedFor(world.SimEngine.Rnd.Seed, type, blocks[0]))
                    {
                        Refiner = new NoiseEdgeRefiner(GeographicFeature.SeedFor(world.SimEngine.Rnd.Seed, type, blocks[0]), 0.3)
                    };
                    feature.Name = Name(world, type, blocks);
                    world.AddFeature(feature);
                    found.Add(feature);
                }
            }
            return found;
        }

        private static List<GeographicFeature> Deltas(World world, FeatureType type, WorldBlock[,] grid, bool[,] sea)
        {
            int width = sea.GetLength(0), height = sea.GetLength(1);
            var found = new List<GeographicFeature>();
            foreach (GeographicFeature river in world.Features.Where(f => f.Type.Id == WaterFeatureDetector.RiverTypeId && f.Parent == null).ToList())
            {
                if (river.Spine.Count < DeltaRiverMinBlocks) continue;
                WorldBlock mouth = river.Spine[river.Spine.Count - 1];
                int mx = mouth.TrueX / mouth.TrueWidth, my = mouth.TrueY / mouth.TrueWidth;
                if (!Sides.Any(d => mx + d.X >= 0 && my + d.Y >= 0 && mx + d.X < width && my + d.Y < height && sea[mx + d.X, my + d.Y])) continue;

                // Low land joined to the mouth, close to it
                var cells = new List<WorldBlock>();
                var seen = new HashSet<(int, int)> { (mx, my) };
                var queue = new Queue<(int X, int Y)>();
                queue.Enqueue((mx, my));
                while (queue.Count > 0)
                {
                    var (cx, cy) = queue.Dequeue();
                    WorldBlock cell = WaterFeatureDetector.FineCell(grid, cx, cy);
                    if (cell == null) continue;
                    cells.Add(cell);
                    foreach (var (dx, dy) in Sides)
                    {
                        int a = cx + dx, b = cy + dy;
                        if (Math.Max(Math.Abs(a - mx), Math.Abs(b - my)) > DeltaReach || !seen.Add((a, b))) continue;
                        WorldBlock next = WaterFeatureDetector.FineCell(grid, a, b);
                        if (next == null || next.Status.WaterStatus == WorldBlockWaterStatus.Water || next.Status.Continent == null) continue;
                        if ((next.Status.Elevation ?? 1) > DeltaElevation) continue;
                        queue.Enqueue((a, b));
                    }
                }
                if (cells.Count < DeltaMinBlocks || (mouth.Status.Elevation ?? 1) > DeltaElevation) continue;
                cells = cells.OrderBy(b => b.TrueY).ThenBy(b => b.TrueX).ToList();
                int seed = GeographicFeature.SeedFor(world.SimEngine.Rnd.Seed, type, mouth);
                var delta = new GeographicFeature(world, type, cells, seed) { Refiner = new NoiseEdgeRefiner(seed, 0.3) };
                WorldBlockStatus status = mouth.Status;
                ILanguage language = status.Region?.Language ?? status.Continent?.RootLanguage ?? world.Language;
                delta.Name = language.GetName(new NameContext(type.NameKind) { ParentName = river.Name }.WithClimate(NameHints.Climate(cells)));
                world.AddFeature(delta);
                found.Add(delta);
            }
            return found;
        }

        private static string Name(World world, FeatureType type, List<WorldBlock> blocks)
        {
            WorldBlockStatus first = blocks.Select(b => b.Status).FirstOrDefault(s => s.Region != null) ?? blocks[0].Status;
            ILanguage language = first.Region?.Language ?? first.Continent?.RootLanguage ?? world.Language;
            return language.GetName(new NameContext(type.NameKind) { ParentName = first.Continent?.Name }.WithClimate(NameHints.Climate(blocks)));
        }

        // Steps (side by side) from the nearest cell where `from` holds, moving only through cells where `through` holds
        private static int[,] Distances(int width, int height, Func<int, int, bool> from, Func<int, int, bool> through)
        {
            int[,] distance = new int[width, height];
            var queue = new Queue<(int X, int Y)>();
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    distance[x, y] = from(x, y) ? 0 : int.MaxValue;
                    if (distance[x, y] == 0) queue.Enqueue((x, y));
                }
            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();
                foreach (var (dx, dy) in Sides)
                {
                    int a = x + dx, b = y + dy;
                    if (a < 0 || b < 0 || a >= width || b >= height || distance[a, b] != int.MaxValue || !through(a, b)) continue;
                    distance[a, b] = distance[x, y] + 1;
                    queue.Enqueue((a, b));
                }
            }
            return distance;
        }
    }
}
