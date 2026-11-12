using System;
using System.Collections.Generic;
using System.Linq;

using LoreGen.Features;
using LoreGen.Lang;
using LoreGen.Simulation;

namespace LoreGen.WorldGen.Generators
{
    /// <summary>
    /// Makes geographic features of a world's water, once its sub-blocks exist:
    ///
    /// - Lakes and salt lakes (Regional blobs): the lake water the fine detail carved into each hollow the hydrology
    ///   filled (see <see cref="HydrologyModel"/>); a salt lake has no outflow.
    /// - Large rivers (Regional, Linear): the sub-blocks each river's winding centre line crosses, from its source to the
    ///   sea (the first sea water reached), the basin it ends in, or the larger river it joins. Rivers run through open
    ///   lakes (so a river can be in pieces). Below their own depth they narrow to their true width (RiverRefiner).
    /// - Oceans and seas (Continental blobs): the world's Ocean and Sea entities as features, so they can have parts.
    ///   Below the top level their edge follows the fine coastline.
    /// - Bays (Regional blobs, part of an ocean or sea): open water that land wraps around: from at least
    ///   <see cref="NoiseGeneratorRules.BayEnclosure"/> of the eight directions, land lies within
    ///   <see cref="NoiseGeneratorRules.BayReach"/> sub-blocks (but not from all eight, which is a lake-like pocket).
    /// - Straits (Regional, Linear, part of an ocean or sea): narrow water, at most
    ///   <see cref="NoiseGeneratorRules.StraitMaxWidth"/> sub-blocks between land on opposite sides, that joins two
    ///   stretches of wider water (an inlet with only one mouth is not a strait).
    ///
    /// Uses no random numbers except for naming.
    /// </summary>
    internal static class WaterFeatureDetector
    {
        public const string LakeTypeId = "Lake";
        public const string SaltLakeTypeId = "SaltLake";
        public const string RiverTypeId = "River";
        public const string OceanTypeId = "Ocean";
        public const string SeaTypeId = "Sea";
        public const string BayTypeId = "Bay";
        public const string StraitTypeId = "Strait";

        // ---- Lakes ----------------------------------------------------------------------------

        public static List<GeographicFeature> DetectLakes(SimEngine engine, World world, HydrologyModel hydrology)
        {
            List<GeographicFeature> found = new List<GeographicFeature>();
            if (hydrology == null) return found;
            FeatureType lakeType = engine.SimData.FeatureType(LakeTypeId);
            FeatureType saltType = engine.SimData.FeatureType(SaltLakeTypeId);
            WorldBlock[,] grid = world.WorldBlock.ChildBlocks;

            foreach (LakeBasin basin in hydrology.Basins)
            {
                if (basin.Cells.Count == 0) continue;
                List<WorldBlock> cells = basin.Cells.Select(c => FineCell(grid, c.X, c.Y)).Where(c => c != null).ToList();
                if (cells.Count == 0) continue;
                FeatureType type = basin.Open ? lakeType : saltType;
                GeographicFeature lake = new GeographicFeature(world, type, cells, GeographicFeature.SeedFor(engine.Rnd.Seed, type, cells[0]));

                WorldBlockStatus shore = Shore(grid, basin.Cells);
                ILanguage language = shore?.Region?.Language ?? shore?.Continent?.RootLanguage ?? world.Language;
                lake.Name = language.GetName(new NameContext(type.NameKind) { ParentName = shore?.Region?.Name });
                foreach (WorldBlock cell in cells) cell.Status.Lake = lake;
                world.AddFeature(lake);
                found.Add(lake);
            }
            return found;
        }

        // The status of a land sub-block beside the lake (the first found, in scan order)
        private static WorldBlockStatus Shore(WorldBlock[,] grid, List<(int X, int Y)> cells)
        {
            foreach ((int X, int Y) c in cells)
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        WorldBlock next = FineCell(grid, c.X + dx, c.Y + dy);
                        if (next != null && next.Status.WaterStatus != WorldBlockWaterStatus.Water && next.Status.Region != null) return next.Status;
                    }
            return null;
        }

        // ---- Rivers ---------------------------------------------------------------------------

        public static List<GeographicFeature> DetectRivers(SimEngine engine, World world, NoiseGeneratorRules rules, HydrologyModel hydrology)
        {
            List<GeographicFeature> found = new List<GeographicFeature>();
            if (hydrology == null) return found;
            FeatureType type = engine.SimData.FeatureType(RiverTypeId);
            WorldBlock[,] grid = world.WorldBlock.ChildBlocks;
            int width = grid.GetLength(0), height = grid.GetLength(1);
            double cell = (double)grid[0, 0].TrueWidth / 10;

            foreach (RiverCourse course in hydrology.Rivers)
            {
                HashSet<(int, int)> endBasin = course.End == RiverEnd.Basin && course.Basin != null
                    ? new HashSet<(int, int)>(course.Basin.Blocks) : new HashSet<(int, int)>();
                List<WorldBlock> cells = new List<WorldBlock>();
                HashSet<WorldBlock> seen = new HashSet<WorldBlock>();
                foreach ((int X, int Y) c in course.Cells)
                {
                    int bx = FloorDiv(c.X, 10), by = FloorDiv(c.Y, 10);
                    if (bx < 0 || by < 0 || bx >= width || by >= height) break; // off the map's edge
                    WorldBlock parent = grid[bx, by];
                    if (parent.ChildBlocks == null)
                    {
                        if (parent.Status.WaterStatus == WorldBlockWaterStatus.Water && course.End == RiverEnd.Sea) break;
                        continue;
                    }
                    WorldBlock block = parent.ChildBlocks[c.X - bx * 10, c.Y - by * 10];
                    if (block.Status.WaterStatus == WorldBlockWaterStatus.Water)
                    {
                        bool lake = block.Status.Ocean == null && block.Status.Sea == null;
                        if (!lake && course.End == RiverEnd.Sea) break;            // the mouth
                        if (lake && endBasin.Contains((bx, by))) break;           // into the lake it ends in
                        continue;                                                  // through an open lake
                    }
                    if (seen.Add(block)) cells.Add(block);
                }
                if (cells.Count == 0) continue;

                double scale = 10.0 * rules.RiverWidth; // km to true units (100 m)
                GeographicFeature river = new GeographicFeature(world, type, cells, course.Seed)
                {
                    Refiner = new RiverRefiner(course.Path, course.Along,
                        scale * Math.Sqrt(course.SourceFlow / rules.RiverFlowFrom), scale * Math.Sqrt(course.MouthFlow / rules.RiverFlowFrom), cell)
                };
                river.SetSpine(cells);

                // Named in the language of the region it runs through most
                Region region = cells.Where(c => c.Status.Region != null).GroupBy(c => c.Status.Region)
                    .OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault();
                Continent continent = cells.Select(c => c.Status.Continent).FirstOrDefault(c => c != null);
                ILanguage language = region?.Language ?? continent?.RootLanguage ?? world.Language;
                river.Name = language.GetName(new NameContext(type.NameKind)
                {
                    ParentName = continent?.Name,
                    Direction = continent != null ? NameHints.DirectionWithin(cells.Select(c => c.ParentBlock).Distinct(), continent.ContinentArea.Blocks()) : null
                });
                world.AddFeature(river);
                found.Add(river);
            }
            return found;
        }

        // ---- Oceans and seas, bays and straits ------------------------------------------------

        public static List<GeographicFeature> DetectBodies(SimEngine engine, World world, NoiseGeneratorRules rules)
        {
            List<GeographicFeature> found = new List<GeographicFeature>();
            if (rules.WaterFeatures == 0) return found;
            FeatureType oceanType = engine.SimData.FeatureType(OceanTypeId);
            FeatureType seaType = engine.SimData.FeatureType(SeaTypeId);

            foreach (Ocean ocean in world.Oceans)
            {
                List<WorldBlock> blocks = ocean.OceanArea.Blocks();
                if (blocks.Count == 0) continue;
                GeographicFeature feature = new GeographicFeature(world, oceanType, blocks, GeographicFeature.SeedFor(engine.Rnd.Seed, oceanType, blocks[0]))
                {
                    Name = ocean.Name,
                    Refiner = new WaterBodyRefiner(s => s.Ocean == ocean)
                };
                ocean.Feature = feature;
                world.AddFeature(feature);
                found.Add(feature);
                found.AddRange(DetectParts(engine, world, rules, feature, s => s.Ocean == ocean, world.Language));
            }
            foreach (Continent continent in world.Continents)
            {
                foreach (Sea sea in continent.Seas)
                {
                    List<WorldBlock> blocks = sea.SeaBlock.Blocks();
                    if (blocks.Count == 0) continue;
                    GeographicFeature feature = new GeographicFeature(world, seaType, blocks, GeographicFeature.SeedFor(engine.Rnd.Seed, seaType, blocks[0]))
                    {
                        Name = sea.Name,
                        Refiner = new WaterBodyRefiner(s => s.Sea == sea)
                    };
                    sea.Feature = feature;
                    world.AddFeature(feature);
                    found.Add(feature);
                    found.AddRange(DetectParts(engine, world, rules, feature, s => s.Sea == sea, continent.RootLanguage));
                }
            }
            return found;
        }

        // The bays and straits of one ocean or sea, in its fine water
        private static List<GeographicFeature> DetectParts(SimEngine engine, World world, NoiseGeneratorRules rules, GeographicFeature body,
            Func<WorldBlockStatus, bool> belongs, ILanguage fallback)
        {
            List<GeographicFeature> found = new List<GeographicFeature>();
            if (rules.BaysPerBody == 0 && rules.StraitsPerBody == 0) return found;
            WorldBlock[,] grid = world.WorldBlock.ChildBlocks;

            List<WorldBlock> water = body.Footprint.Blocks.Where(b => b.ChildBlocks != null)
                .SelectMany(b => b.ChildBlocksAsList())
                .Where(c => c.Status.WaterStatus == WorldBlockWaterStatus.Water && belongs(c.Status))
                .OrderBy(c => FeatureGeometry.Coord(c).Y).ThenBy(c => FeatureGeometry.Coord(c).X)
                .ToList();
            if (water.Count == 0) return found;

            HashSet<WorldBlock> narrow = new HashSet<WorldBlock>(water.Where(c => IsNarrow(grid, FeatureGeometry.Coord(c), rules.StraitMaxWidth)));

            // Straits: narrow water joining two stretches of wider water
            List<List<WorldBlock>> straits = new List<List<WorldBlock>>();
            foreach (List<WorldBlock> group in FeatureGeometry.TouchingGroups(water.Where(narrow.Contains).ToList()))
            {
                if (group.Count < rules.StraitMinBlocks) continue;
                HashSet<WorldBlock> inGroup = new HashSet<WorldBlock>(group);
                List<WorldBlock> mouths = new List<WorldBlock>();
                HashSet<WorldBlock> mouthSet = new HashSet<WorldBlock>();
                foreach (WorldBlock c in group)
                {
                    var at = FeatureGeometry.Coord(c);
                    foreach (WorldBlock next in Around(grid, at.X, at.Y))
                        if (next.Status.WaterStatus == WorldBlockWaterStatus.Water && !inGroup.Contains(next) && !narrow.Contains(next) && mouthSet.Add(next))
                            mouths.Add(next);
                }
                if (FeatureGeometry.TouchingGroups(mouths).Count >= 2) straits.Add(group);
            }
            straits = straits.Select((g, i) => (Group: g, Index: i, Length: FeatureGeometry.Spine(g, 1).Count))
                .OrderByDescending(x => x.Length).ThenBy(x => x.Index).Take(rules.StraitsPerBody)
                .OrderBy(x => x.Index).Select(x => x.Group).ToList();

            // Bays: wider water that land wraps around on most sides, but not all
            List<WorldBlock> enclosed = water.Where(c => !narrow.Contains(c)).Where(c =>
            {
                int count = Enclosure(grid, FeatureGeometry.Coord(c), rules.BayReach);
                return count >= rules.BayEnclosure && count < 8;
            }).ToList();
            List<List<WorldBlock>> bays = FeatureGeometry.TouchingGroups(enclosed).Where(g => g.Count >= rules.BayMinBlocks)
                .Select((g, i) => (Group: g, Index: i)).OrderByDescending(x => x.Group.Count).ThenBy(x => x.Index)
                .Take(rules.BaysPerBody).OrderBy(x => x.Index).Select(x => x.Group).ToList();

            FeatureType bayType = engine.SimData.FeatureType(BayTypeId);
            FeatureType straitType = engine.SimData.FeatureType(StraitTypeId);
            foreach (List<WorldBlock> bay in bays) found.Add(AddPart(engine, world, body, bayType, bay, water, fallback));
            foreach (List<WorldBlock> strait in straits) found.Add(AddPart(engine, world, body, straitType, strait, water, fallback));
            return found;
        }

        private static GeographicFeature AddPart(SimEngine engine, World world, GeographicFeature body, FeatureType type, List<WorldBlock> group, List<WorldBlock> within, ILanguage fallback)
        {
            GeographicFeature part = new GeographicFeature(world, type, group, GeographicFeature.SeedFor(engine.Rnd.Seed, type, group[0]));
            if (type.Shape == FeatureShape.Linear) part.SetSpine(FeatureGeometry.Spine(group, 1));

            // Named in the language of the nearest land's region
            WorldBlockStatus shore = NearestLand(world.WorldBlock.ChildBlocks, group);
            ILanguage language = shore?.Region?.Language ?? shore?.Continent?.RootLanguage ?? fallback;
            part.Name = language.GetName(new NameContext(type.NameKind)
            {
                ParentName = body.Name,
                Direction = NameHints.DirectionWithin(group, within)
            });
            body.AddChild(part);
            world.AddFeature(part);
            return part;
        }

        // ---- Fine water geometry --------------------------------------------------------------

        // The eight directions, sides first
        private static readonly (int X, int Y)[] Directions8 = { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, -1), (1, -1), (-1, 1) };

        // Whether there is land on both sides of a water sub-block, close enough across one of the four axes
        private static bool IsNarrow(WorldBlock[,] grid, (int X, int Y) at, int maxWidth)
        {
            for (int axis = 0; axis < 4; axis++)
            {
                (int X, int Y) d = Directions8[axis * 2];
                int forward = StepsToLand(grid, at, d, maxWidth), back = StepsToLand(grid, at, (-d.X, -d.Y), maxWidth);
                if (forward > 0 && back > 0 && forward + back - 1 <= maxWidth) return true;
            }
            return false;
        }

        // In how many of the eight directions land lies within reach
        private static int Enclosure(WorldBlock[,] grid, (int X, int Y) at, int reach)
        {
            int count = 0;
            foreach ((int X, int Y) d in Directions8)
                if (StepsToLand(grid, at, d, reach) > 0) count++;
            return count;
        }

        // Steps from a sub-block to the first land in a direction, or 0 if there is none within reach (off the map is water)
        private static int StepsToLand(WorldBlock[,] grid, (int X, int Y) at, (int X, int Y) d, int reach)
        {
            for (int k = 1; k <= reach; k++)
            {
                bool? land = IsLand(grid, at.X + d.X * k, at.Y + d.Y * k);
                if (land == true) return k;
            }
            return 0;
        }

        // Whether the sub-block at a position in the world's grid of sub-blocks is land: its own status if it exists,
        // otherwise its block's; off the map, null (open water)
        private static bool? IsLand(WorldBlock[,] grid, int gx, int gy)
        {
            int bx = FloorDiv(gx, 10), by = FloorDiv(gy, 10);
            if (bx < 0 || by < 0 || bx >= grid.GetLength(0) || by >= grid.GetLength(1)) return null;
            WorldBlock parent = grid[bx, by];
            WorldBlock block = parent.ChildBlocks != null ? parent.ChildBlocks[gx - bx * 10, gy - by * 10] : parent;
            return block.Status.WaterStatus != WorldBlockWaterStatus.Water;
        }

        private static WorldBlockStatus NearestLand(WorldBlock[,] grid, List<WorldBlock> group)
        {
            for (int reach = 1; reach <= 12; reach++)
                foreach (WorldBlock c in group)
                {
                    var at = FeatureGeometry.Coord(c);
                    foreach ((int X, int Y) d in Directions8)
                    {
                        WorldBlock next = FineCell(grid, at.X + d.X * reach, at.Y + d.Y * reach);
                        if (next != null && next.Status.WaterStatus != WorldBlockWaterStatus.Water && next.Status.Region != null) return next.Status;
                    }
                }
            return null;
        }

        private static IEnumerable<WorldBlock> Around(WorldBlock[,] grid, int gx, int gy)
        {
            foreach ((int X, int Y) d in Directions8)
            {
                WorldBlock next = FineCell(grid, gx + d.X, gy + d.Y);
                if (next != null) yield return next;
            }
        }

        /// <summary>
        /// The sub-block at a position in the world's grid of sub-blocks (a top-level block's X times 10 plus the sub-block's
        /// X), or null if it is off the map or its block has no sub-blocks.
        /// </summary>
        internal static WorldBlock FineCell(WorldBlock[,] grid, int gx, int gy)
        {
            int bx = FloorDiv(gx, 10), by = FloorDiv(gy, 10);
            if (bx < 0 || by < 0 || bx >= grid.GetLength(0) || by >= grid.GetLength(1)) return null;
            WorldBlock parent = grid[bx, by];
            return parent.ChildBlocks != null ? parent.ChildBlocks[gx - bx * 10, gy - by * 10] : null;
        }

        private static int FloorDiv(int a, int b)
        {
            return (int)Math.Floor((double)a / b);
        }
    }

    /// <summary>
    /// Shapes an ocean or sea below the top level: a finer block is part of it when it is water of that ocean or sea (so the
    /// edge follows the fine coastline). Blocks not yet given a status follow their coarse block.
    /// </summary>
    public sealed class WaterBodyRefiner : IFeatureRefiner
    {
        private readonly Func<WorldBlockStatus, bool> belongs;

        public WaterBodyRefiner(Func<WorldBlockStatus, bool> belongs)
        {
            this.belongs = belongs ?? throw new ArgumentNullException("belongs");
        }

        public bool Includes(GeographicFeature feature, WorldBlock block, bool coarseMember)
        {
            if (!block.Status.Elevation.HasValue) return coarseMember;
            return block.Status.WaterStatus == WorldBlockWaterStatus.Water && belongs(block.Status);
        }
    }
}
