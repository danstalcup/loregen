using System;
using System.Collections.Generic;
using System.Linq;

using LoreGen.Features;
using LoreGen.Lang;
using LoreGen.Simulation;

namespace LoreGen.WorldGen.Generators
{
    /// <summary>
    /// Finds the features ice makes (when GlacialFeatures is on):
    ///  - Ice sheets: connected land of top-level blocks at IceSheetTemperature or colder, at least IceSheetMinBlocks of
    ///    them. Below that, the edge follows each block's own temperature (<see cref="ColdEdgeRefiner"/>).
    ///  - Glaciers (part of a mountain range): the range's 100 km blocks above the snowline, those at GlacierTemperature or
    ///    colder once the coarse temperature is cooled by how much higher they stand than the coarse ground around them, and
    ///    not already under an ice sheet. The largest few pieces of each range, each at most its coldest 25 blocks.
    ///  - Fjords (part of an ocean or sea): sea too narrow to have open water of its own, reaching in from the open sea,
    ///    on a coast at FjordTemperature or colder with high ground on its shores.
    /// </summary>
    internal static class GlacialDetector
    {
        public const string IceSheetTypeId = "IceSheet";
        public const string GlacierTypeId = "Glacier";
        public const string FjordTypeId = "Fjord";

        private const int GlaciersPerRange = 3;
        private const int GlacierMinBlocks = 2, GlacierMaxBlocks = 25;
        // Fjords: open water lies at least this many 100 km blocks from land; a fjord is at least this big and its shores
        // stand at least this high on average
        private const int FjordOpenWater = 3;
        private const int FjordMinBlocks = 4;
        private const double FjordShoreElevation = 0.08;

        private static readonly (int X, int Y)[] Sides = { (1, 0), (-1, 0), (0, 1), (0, -1) };

        private static FeatureType Type(SimEngine engine, string id) => engine.SimData.FeatureTypes.FirstOrDefault(t => t.Id == id);

        // ---- Ice sheets ---------------------------------------------------------------------------

        public static List<GeographicFeature> DetectIceSheets(SimEngine engine, World world, NoiseGeneratorRules rules, bool[,] land, double[,] temperature)
        {
            var found = new List<GeographicFeature>();
            FeatureType type = Type(engine, IceSheetTypeId);
            if (rules.GlacialFeatures == 0 || type == null) return found;
            WorldBlock[,] grid = world.WorldBlock.ChildBlocks;
            int width = grid.GetLength(0), height = grid.GetLength(1);
            bool[,] seen = new bool[width, height];
            Func<int, int, bool> frozen = (x, y) => land[x, y] && temperature[x, y] <= rules.IceSheetTemperature;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    if (seen[x, y] || !frozen(x, y)) continue;
                    var blocks = Piece(x, y, width, height, seen, frozen).Select(p => grid[p.X, p.Y]).ToList();
                    if (blocks.Count < rules.IceSheetMinBlocks) continue;
                    int seed = GeographicFeature.SeedFor(engine.Rnd.Seed, type, blocks[0]);
                    var sheet = new GeographicFeature(world, type, blocks, seed) { Refiner = new ColdEdgeRefiner(rules.IceSheetTemperature) };
                    WorldBlockStatus first = blocks[0].Status;
                    ILanguage language = first.Continent?.RootLanguage ?? world.Language;
                    sheet.Name = language.GetName(new NameContext(type.NameKind) { ParentName = first.Continent?.Name }.WithClimate(NameHints.Climate(blocks)));
                    world.AddFeature(sheet);
                    found.Add(sheet);
                }
            return found;
        }

        // ---- Glaciers -----------------------------------------------------------------------------

        public static List<GeographicFeature> DetectGlaciers(SimEngine engine, World world, NoiseGeneratorRules rules, double[,] elevation, double[,] temperature)
        {
            var found = new List<GeographicFeature>();
            FeatureType type = Type(engine, GlacierTypeId);
            if (rules.GlacialFeatures == 0 || type == null) return found;
            foreach (GeographicFeature range in world.Features.Where(f => f.Type.Id == MountainRangeDetector.TypeId).ToList())
            {
                var cells = range.Footprint.Blocks.Where(t => t.ChildBlocks != null).SelectMany(t => t.ChildBlocks.Cast<WorldBlock>())
                    .Where(c => c.Status.WaterStatus != WorldBlockWaterStatus.Water && c.Status.Elevation.HasValue && range.Contains(c)
                        && Temperature(c, elevation, temperature, rules) <= rules.GlacierTemperature
                        && !world.FeaturesAt(c).Any(f => f.Type.Id == IceSheetTypeId)) // already under the ice sheet
                    .ToList();
                var inside = new HashSet<WorldBlock>(cells);
                var seen = new HashSet<WorldBlock>();
                var pieces = new List<List<WorldBlock>>();
                foreach (WorldBlock start in cells.OrderBy(c => c.TrueY).ThenBy(c => c.TrueX))
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
                    if (piece.Count >= GlacierMinBlocks) pieces.Add(piece.OrderBy(c => c.TrueY).ThenBy(c => c.TrueX).ToList());
                }
                foreach (var whole in pieces.OrderByDescending(p => p.Count).ThenBy(p => p[0].TrueY).ThenBy(p => p[0].TrueX).Take(GlaciersPerRange))
                {
                    // A glacier is a river of ice, not a whole frozen range: at most its coldest GlacierMaxBlocks blocks
                    var piece = whole.Count <= GlacierMaxBlocks ? whole : Coldest(whole, elevation, temperature, rules);
                    int seed = GeographicFeature.SeedFor(engine.Rnd.Seed, type, piece[0]);
                    var glacier = new GeographicFeature(world, type, piece, seed) { Refiner = new NoiseEdgeRefiner(seed, 0.3) };
                    WorldBlockStatus status = piece[0].Status;
                    ILanguage language = status.Region?.Language ?? status.Continent?.RootLanguage ?? world.Language;
                    glacier.Name = language.GetName(new NameContext(type.NameKind) { ParentName = range.Name }.WithClimate(NameHints.Climate(piece)));
                    range.AddChild(glacier);
                    world.AddFeature(glacier);
                    found.Add(glacier);
                }
            }
            return found;
        }

        // The largest connected piece of a piece's coldest GlacierMaxBlocks blocks
        private static List<WorldBlock> Coldest(List<WorldBlock> piece, double[,] elevation, double[,] temperature, NoiseGeneratorRules rules)
        {
            var coldest = new HashSet<WorldBlock>(piece.OrderBy(c => Temperature(c, elevation, temperature, rules)).ThenBy(c => c.TrueY).ThenBy(c => c.TrueX).Take(GlacierMaxBlocks));
            var seen = new HashSet<WorldBlock>();
            List<WorldBlock> best = null;
            foreach (WorldBlock start in piece.Where(coldest.Contains))
            {
                if (!seen.Add(start)) continue;
                var part = new List<WorldBlock>();
                var queue = new Queue<WorldBlock>();
                queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    WorldBlock block = queue.Dequeue();
                    part.Add(block);
                    foreach (WorldBlock next in block.SurroundingBlocksAsList())
                        if (coldest.Contains(next) && seen.Add(next)) queue.Enqueue(next);
                }
                if (best == null || part.Count > best.Count) best = part;
            }
            return best.OrderBy(c => c.TrueY).ThenBy(c => c.TrueX).ToList();
        }

        // A 100 km block's temperature: the coarse temperature where it lies, cooled by how much higher it stands than the
        // coarse ground there (the same lapse as the climate's)
        private static double Temperature(WorldBlock cell, double[,] elevation, double[,] temperature, NoiseGeneratorRules rules)
        {
            double size = cell.ParentBlock.TrueWidth;
            double gx = (cell.TrueX + cell.TrueWidth / 2.0) / size - 0.5, gy = (cell.TrueY + cell.TrueHeight / 2.0) / size - 0.5;
            double above = cell.Status.Elevation.Value - Math.Max(0, Interpolate(elevation, gx, gy));
            return Interpolate(temperature, gx, gy) - rules.TemperatureLapse * Math.Max(0, above);
        }

        // ---- Fjords -------------------------------------------------------------------------------

        public static List<GeographicFeature> DetectFjords(SimEngine engine, World world, NoiseGeneratorRules rules, double[,] temperature)
        {
            var found = new List<GeographicFeature>();
            FeatureType type = Type(engine, FjordTypeId);
            if (rules.GlacialFeatures == 0 || type == null) return found;
            WorldBlock[,] grid = world.WorldBlock.ChildBlocks;
            int width = grid.GetLength(0) * 10, height = grid.GetLength(1) * 10;
            bool[,] sea = new bool[width, height];
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    WorldBlock top = grid[x / 10, y / 10];
                    WorldBlock cell = top.ChildBlocks != null ? top.ChildBlocks[x % 10, y % 10] : top;
                    sea[x, y] = cell.Status.WaterStatus == WorldBlockWaterStatus.Water && cell.Status.Lake == null;
                }
            int[,] toLand = Distances(width, height, (x, y) => !sea[x, y], (x, y) => sea[x, y]);
            int[,] toOpen = Distances(width, height, (x, y) => sea[x, y] && toLand[x, y] >= FjordOpenWater, (x, y) => sea[x, y]);
            Func<int, int, bool> narrow = (x, y) => sea[x, y] && toOpen[x, y] >= FjordOpenWater;

            bool[,] seen = new bool[width, height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    if (seen[x, y] || !narrow(x, y)) continue;
                    var piece = Piece(x, y, width, height, seen, narrow);
                    if (piece.Count < FjordMinBlocks) continue;
                    // Joined to the open sea, on a cold coast with high shores
                    bool joined = piece.Any(p => Sides.Any(d => In(p.X + d.X, p.Y + d.Y, width, height) && sea[p.X + d.X, p.Y + d.Y] && !narrow(p.X + d.X, p.Y + d.Y)));
                    if (!joined) continue;
                    double cold = piece.Average(p => temperature[p.X / 10, p.Y / 10]);
                    if (cold > rules.FjordTemperature) continue;
                    var shores = piece.SelectMany(p => Sides.Select(d => (p.X + d.X, p.Y + d.Y))).Where(s => In(s.Item1, s.Item2, width, height) && !sea[s.Item1, s.Item2])
                        .Select(s => WaterFeatureDetector.FineCell(grid, s.Item1, s.Item2)).Where(c => c?.Status.Elevation != null).ToList();
                    if (shores.Count == 0 || shores.Average(c => c.Status.Elevation.Value) < FjordShoreElevation) continue;
                    var blocks = piece.Select(p => WaterFeatureDetector.FineCell(grid, p.X, p.Y)).Where(c => c != null)
                        .OrderBy(c => c.TrueY).ThenBy(c => c.TrueX).ToList();
                    if (blocks.Count < FjordMinBlocks) continue;
                    WorldBlockStatus water = blocks[0].Status;
                    GeographicFeature body = water.Ocean?.Feature ?? water.Sea?.Feature;
                    if (body == null) continue;
                    int seed = GeographicFeature.SeedFor(engine.Rnd.Seed, type, blocks[0]);
                    var fjord = new GeographicFeature(world, type, blocks, seed)
                    {
                        Refiner = new InsideRefiner(new WaterBodyRefiner(s => s.Ocean != null || s.Sea != null))
                    };
                    WorldBlockStatus shore = shores[0].Status;
                    ILanguage language = shore.Region?.Language ?? shore.Continent?.RootLanguage ?? world.Language;
                    fjord.Name = language.GetName(new NameContext(type.NameKind) { ParentName = shore.Region?.Name }.WithClimate(NameHints.Climate(blocks)));
                    body.AddChild(fjord);
                    world.AddFeature(fjord);
                    found.Add(fjord);
                }
            return found;
        }

        // ---- Helpers ------------------------------------------------------------------------------

        private static bool In(int x, int y, int width, int height) => x >= 0 && y >= 0 && x < width && y < height;

        // The connected (side by side) cells passing a test, from a start
        private static List<(int X, int Y)> Piece(int x, int y, int width, int height, bool[,] seen, Func<int, int, bool> test)
        {
            var piece = new List<(int X, int Y)>();
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
                    if (!In(a, b, width, height) || seen[a, b] || !test(a, b)) continue;
                    seen[a, b] = true;
                    queue.Enqueue((a, b));
                }
            }
            return piece;
        }

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
                    if (!In(a, b, width, height) || distance[a, b] != int.MaxValue || !through(a, b)) continue;
                    distance[a, b] = distance[x, y] + 1;
                    queue.Enqueue((a, b));
                }
            }
            return distance;
        }

        // A top-level value at a position in top-level blocks, centres at whole numbers, blended between the four nearest
        private static double Interpolate(double[,] field, double gx, double gy)
        {
            int width = field.GetLength(0), height = field.GetLength(1);
            int x0 = (int)Math.Floor(gx), y0 = (int)Math.Floor(gy);
            double tx = gx - x0, ty = gy - y0;
            Func<int, int, double> at = (x, y) => field[Math.Max(0, Math.Min(width - 1, x)), Math.Max(0, Math.Min(height - 1, y))];
            return (1 - tx) * (1 - ty) * at(x0, y0) + tx * (1 - ty) * at(x0 + 1, y0) + (1 - tx) * ty * at(x0, y0 + 1) + tx * ty * at(x0 + 1, y0 + 1);
        }
    }

    /// <summary>
    /// For features defined by cold, such as ice sheets: below the feature's depth, a block is part of it when its own
    /// temperature is at the threshold or colder, so the edge follows the climate. Blocks without a temperature follow their
    /// coarse ancestor. Pure, as block temperatures depend only on the world and position.
    /// </summary>
    public sealed class ColdEdgeRefiner : IFeatureRefiner
    {
        /// <summary>Warmest temperature that is part of the feature.</summary>
        public double Threshold { get; }

        public ColdEdgeRefiner(double threshold)
        {
            Threshold = threshold;
        }

        public bool Includes(GeographicFeature feature, WorldBlock block, bool coarseMember)
        {
            if (!block.Status.Temperature.HasValue || block.Status.WaterStatus == WorldBlockWaterStatus.Water) return coarseMember && block.Status.WaterStatus != WorldBlockWaterStatus.Water;
            return block.Status.Temperature.Value <= Threshold;
        }
    }
}
