using System;
using System.Collections.Generic;
using System.Linq;

using LoreGen.Features;
using LoreGen.Lang;
using LoreGen.Simulation;

namespace LoreGen.WorldGen.Generators
{
    /// <summary>
    /// Finds volcanoes, for worlds with tectonic plates: where ocean floor dives under another plate (a converging boundary
    /// where at least one of the two plates is oceanic: continental margins and island arcs), the mountain ranges are
    /// volcanic. In each such range, the highest points on that kind of boundary, at least VolcanoSpacing blocks apart,
    /// become volcanoes (up to VolcanoesPerRange), as parts of the range. Each is a cone about 30 km across when zoomed in.
    /// </summary>
    internal static class VolcanoDetector
    {
        public const string VolcanoTypeId = "Volcano";

        private const int VolcanoSpacing = 5;
        private const double VolcanoRadius = 0.15;

        public static List<GeographicFeature> Detect(SimEngine engine, World world, NoiseGeneratorRules rules)
        {
            var found = new List<GeographicFeature>();
            FeatureType type = engine.SimData.FeatureTypes.FirstOrDefault(t => t.Id == VolcanoTypeId);
            if (type == null || world.Plates.Count == 0 || rules.VolcanoesPerRange <= 0) return found;

            WorldBlock[,] top = world.WorldBlock.ChildBlocks;
            int width = top.GetLength(0), height = top.GetLength(1);
            bool[,] subducting = new bool[width, height];
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    subducting[x, y] = Subducting(top, x, y);
            // Volcanoes rise on the boundary and just behind it
            Func<WorldBlock, bool> volcanic = b =>
            {
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int x = b.X + dx, y = b.Y + dy;
                        if (x >= 0 && y >= 0 && x < width && y < height && subducting[x, y]) return true;
                    }
                return false;
            };

            foreach (GeographicFeature range in world.Features.Where(f => f.Type.Id == MountainRangeDetector.TypeId).ToList())
            {
                var cells = range.Footprint.Blocks.Where(volcanic).Where(t => t.ChildBlocks != null)
                    .SelectMany(t => t.ChildBlocks.Cast<WorldBlock>())
                    .Where(c => c.Status.WaterStatus != WorldBlockWaterStatus.Water && c.Status.Elevation.HasValue && range.Contains(c))
                    .OrderByDescending(c => c.Status.Elevation.Value).ThenBy(c => c.TrueY).ThenBy(c => c.TrueX).ToList();
                var chosen = new List<WorldBlock>();
                foreach (WorldBlock cell in cells)
                {
                    if (chosen.Count >= rules.VolcanoesPerRange) break;
                    if (chosen.Any(c => Math.Max(Math.Abs(c.TrueX - cell.TrueX), Math.Abs(c.TrueY - cell.TrueY)) < VolcanoSpacing * cell.TrueWidth)) continue;
                    chosen.Add(cell);
                }
                foreach (WorldBlock cell in chosen)
                {
                    int seed = GeographicFeature.SeedFor(engine.Rnd.Seed, type, cell);
                    var volcano = new GeographicFeature(world, type, new[] { cell }, seed) { Refiner = new PointRefiner(seed, VolcanoRadius) };
                    volcano.SetSpine(new[] { cell });
                    WorldBlockStatus status = cell.Status;
                    ILanguage language = status.Region?.Language ?? status.Continent?.RootLanguage ?? world.Language;
                    volcano.Name = language.GetName(new NameContext(type.NameKind) { ParentName = range.Name }.WithClimate(NameHints.Climate(new[] { cell })));
                    range.AddChild(volcano);
                    world.AddFeature(volcano);
                    found.Add(volcano);
                }
            }
            return found;
        }

        // A converging boundary block beside a different plate, where one of the two is ocean floor
        private static bool Subducting(WorldBlock[,] top, int x, int y)
        {
            WorldBlock block = top[x, y];
            TectonicPlate plate = block.Status.Plate;
            if (plate == null || plate.BoundaryAt(block) != PlateBoundaryKind.Convergent) return false;
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    int a = x + dx, b = y + dy;
                    if (a < 0 || b < 0 || a >= top.GetLength(0) || b >= top.GetLength(1)) continue;
                    TectonicPlate other = top[a, b].Status.Plate;
                    if (other != null && other != plate && (!other.Continental || !plate.Continental)) return true;
                }
            return false;
        }
    }
}
