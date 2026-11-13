using System;
using System.Collections.Generic;
using System.Linq;

using LoreGen.Features;
using LoreGen.Lang;
using LoreGen.Simulation;

namespace LoreGen.WorldGen.Generators
{
    /// <summary>
    /// Finds small features in the detail made when zooming in, among the 10x10 children of one block, and adds them to the
    /// world. Called by <see cref="ZoomDetail"/> right after it makes the children.
    ///
    /// When a 100 km block's 10 km children are made (Local features):
    ///  - Hills: connected rugged ground (terrain relief 2 or more) outside mountain ranges.
    ///  - Forest: connected wooded ground.
    ///  - Fen: connected marsh.
    ///  - Gorge: a river through a mountain range or between rugged ground (part of the river).
    ///  - Headland: coastal land with water on most sides.
    ///  - Cove: sea or ocean water with land on most sides.
    ///  - Isle: a small piece of land with water all round, inside the block.
    ///  - Beach and Cliffs: stretches of flat and of rugged coast.
    ///  - Lagoon: sea water almost shut in by land, with a narrow mouth.
    ///  - Caldera: the collapsed summit of some volcanoes (part of the volcano).
    ///  - Crater (rare, by CraterChance) and, in wet country, its crater lake (see Craters).
    ///  - Cave system (part of a mountain range): under rugged, wet ground in some blocks (CaveChance).
    /// When a 10 km block's 1 km children are made (Minor features):
    ///  - Knoll: the highest spot within two blocks, standing well above them.
    ///  - Waterfall: a river where it drops steeply, in a mountain range or rugged ground (part of the river).
    ///  - Grove: a small wood in mostly open country.
    ///  - Clearing: a small open space in mostly wooded country.
    ///  - Pond: a small patch of standing water on flat ground, where such ground is scarce.
    ///  - Cave (part of a cave system): its ways in, on slopes: one in each of two of the system's blocks, picked by seed.
    ///
    /// Each feature lies within the one block whose children it was found in (a forest running on into the next block
    /// is found there again, as another forest), and never reaches past its own blocks when zoomed into further. Its seed,
    /// shape and name depend only on the world seed and where it is, so it is the same whatever order blocks are zoomed
    /// into. Features found in a block shape the terrain of the blocks made inside them later (TerrainInfluences.csv).
    /// </summary>
    internal static class FineFeatureDetector
    {
        private const int N = 10;

        // How many of each kind one block can hold, and how big they must be (in children)
        private const int MaxPerKind = 2;
        // Areas that fill most of the block are the character of the land there (see subregions), not a feature in it
        private const int AreaMax = 70;
        private const int HillsMin = 4, ForestMin = 8, FenMin = 3, GorgeMin = 2, CoveMax = 8, IsleMax = 6;
        private const int GroveMin = 2, GroveMax = 10, ClearingMax = 8, PondMax = 6;
        // A knoll is the highest point within two blocks, and stands at least this much above their average (elevation)
        private const double KnollProminence = 0.005;
        // Ponds are named only where pond ground is scarce, at most this share of the block's land
        private const double PondShareMax = 0.1;
        // Beaches and cliffs: shortest stretch of coast (in children); lagoons: largest
        private const int BeachMin = 4, CliffsMin = 3, LagoonMax = 15;
        // Coast at least this high (elevation) stands as cliffs rather than beach
        private const double CliffElevation = 0.08;
        // Share of volcanoes with a caldera
        private const double CalderaShare = 0.6;
        // Cave systems: how wet the ground must be, and how big (in children); cave mouths: chance per block and most
        private const double CaveHydration = 0.45;
        private const int CaveMin = 3, CaveMax = 12, CaveMouthsMax = 2;
        // Crater lakes: in craters at least this wide (radius, in children) in ground at least this wet
        private const double CraterLakeRadius = 1.8, CraterLakeHydration = 0.55;
        // A waterfall is where neighbouring river blocks differ in height by at least this much (elevation)
        private const double WaterfallDrop = 0.006;

        /// <summary>Feature types found when zooming in, in the order they are looked for.</summary>
        public static readonly string[] TypeIds =
        {
            "Crater", "CraterLake", "CaveSystem",
            "Hills", "Forest", "Fen", "Gorge", "Headland", "Cove", "Isle", "Beach", "Cliffs", "Lagoon", "Caldera", "Cave",
            "Knoll", "Waterfall", "Grove", "Clearing", "Pond"
        };

        /// <param name="world">The world</param>
        /// <param name="parent">The block whose children were just made</param>
        /// <param name="statusAt">The status of any block at the children's depth, by position in blocks of that depth
        /// (made or worked out); null off the map. Used only for land and water across the parent's edge.</param>
        /// <returns>The features found</returns>
        /// <param name="rules">Settings (cave and crater chances)</param>
        /// <param name="covering">The features covering each child, if already known (see World.FeaturesAtChildren)</param>
        public static List<GeographicFeature> Detect(World world, WorldBlock parent, Func<int, int, WorldBlockStatus> statusAt, NoiseGeneratorRules rules,
            IReadOnlyList<GeographicFeature>[,] covering = null)
        {
            int before = world.Features.Count;
            DetectIn(world, parent, statusAt, rules, covering ?? world.FeaturesAtChildren(parent));
            return world.Features.Skip(before).ToList();
        }

        private static void DetectIn(World world, WorldBlock parent, Func<int, int, WorldBlockStatus> statusAt, NoiseGeneratorRules rules,
            IReadOnlyList<GeographicFeature>[,] covering)
        {
            int depth = parent.Depth + 1;
            if (depth != FeatureScope.Local.Depth() && depth != FeatureScope.Minor.Depth()) return;
            SimData data = world.SimEngine.SimData;
            Func<string, FeatureType> type = id => data.FeatureTypes.FirstOrDefault(t => t.Id == id);

            WorldBlock[,] cells = parent.ChildBlocks;
            int size = cells[0, 0].TrueWidth;
            int ox = parent.TrueX / size, oy = parent.TrueY / size; // position of cells[0, 0] in blocks of this depth
            Func<int, int, WorldBlockStatus> at = (i, j) =>
                i >= 0 && j >= 0 && i < N && j < N ? cells[i, j].Status : statusAt(ox + i, oy + j);

            var features = new Dictionary<WorldBlock, IReadOnlyList<GeographicFeature>>();
            foreach (WorldBlock cell in cells) features[cell] = covering[cell.X, cell.Y];
            Func<WorldBlock, bool> land = c => c.Status.WaterStatus != WorldBlockWaterStatus.Water && c.Status.Terrain != null;
            Func<WorldBlock, GeographicFeature> riverOf = c => features[c].FirstOrDefault(f => f.Type.Id == WaterFeatureDetector.RiverTypeId);
            Func<WorldBlock, bool> inRange = c => features[c].Any(f => f.Type.Category == FeatureCategory.Mountain);

            if (depth == FeatureScope.Local.Depth())
            {
                // Craters first: a crater lake turns land to water, which the rest should see
                Craters(world, parent, cells, features, rules, type);

                // Cave systems: under rugged, wet ground in a mountain range, in some blocks
                FeatureType caves = type("CaveSystem");
                if (caves != null && Chance(world, caves, parent) < rules.CaveChance)
                {
                    var ground = Pieces(cells, c => land(c) && inRange(c) && c.Status.Terrain.TerrainType.Relief >= 2
                        && (c.Status.Hydration ?? 0) >= CaveHydration);
                    var piece = Largest(ground.Where(p => p.Count >= CaveMin), 1).FirstOrDefault();
                    if (piece != null)
                    {
                        double mx = piece.Average(c => c.X), my = piece.Average(c => c.Y);
                        piece = piece.OrderBy(c => (c.X - mx) * (c.X - mx) + (c.Y - my) * (c.Y - my)).ThenBy(c => c.TrueY).ThenBy(c => c.TrueX)
                            .Take(CaveMax).OrderBy(c => c.TrueY).ThenBy(c => c.TrueX).ToList();
                        GeographicFeature range = features[piece[0]].FirstOrDefault(f => f.Type.Id == MountainRangeDetector.TypeId);
                        if (range != null) Add(world, caves, piece, range);
                    }
                }

                // Hills, forests and fens
                AddAreas(world, type("Hills"), Pieces(cells, c => land(c) && c.Status.Terrain.TerrainType.Relief >= 2 && !inRange(c)), HillsMin, AreaMax);
                AddAreas(world, type("Forest"), Pieces(cells, c => land(c) && c.Status.Terrain.TerrainType.Trees >= 0.5), ForestMin, AreaMax);
                AddAreas(world, type("Fen"), Pieces(cells, c => land(c) && c.Status.Terrain.TerrainType.Wetness >= 0.7), FenMin, AreaMax);

                // Gorges: a river through rugged ground, as part of that river
                FeatureType gorge = type("Gorge");
                if (gorge != null)
                {
                    // River banks are kept gentle, so the ruggedness is judged by the ground beside the river
                    Func<WorldBlock, bool> walled = c =>
                    {
                        for (int dx = -1; dx <= 1; dx++)
                            for (int dy = -1; dy <= 1; dy++)
                            {
                                int a = c.X + dx, b = c.Y + dy;
                                if (a < 0 || b < 0 || a >= N || b >= N) continue;
                                WorldBlock d = cells[a, b];
                                if (land(d) && riverOf(d) == null && d.Status.Terrain.TerrainType.Relief >= 3) return true;
                            }
                        return false;
                    };
                    var rugged = Pieces(cells, c => land(c) && riverOf(c) != null && (inRange(c) || walled(c)));
                    foreach (var piece in Largest(rugged.Where(p => p.Count >= GorgeMin), 1))
                        Add(world, gorge, piece, riverOf(piece[0]));
                }

                // Headlands and coves, judged by the eight blocks around (looking across the parent's edge)
                Func<int, int, bool> water = (i, j) => { var s = at(i, j); return s != null && s.WaterStatus == WorldBlockWaterStatus.Water; };
                Func<int, int, int> waterAround = (i, j) =>
                {
                    int count = 0;
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dy = -1; dy <= 1; dy++)
                            if ((dx != 0 || dy != 0) && water(i + dx, j + dy)) count++;
                    return count;
                };
                FeatureType headland = type("Headland");
                if (headland != null)
                {
                    var points = new List<(WorldBlock Cell, int Water)>();
                    for (int i = 0; i < N; i++)
                        for (int j = 0; j < N; j++)
                            if (land(cells[i, j]) && cells[i, j].Status.WaterStatus == WorldBlockWaterStatus.Coastline && waterAround(i, j) >= 5)
                                points.Add((cells[i, j], waterAround(i, j)));
                    foreach (var point in Spread(points.OrderByDescending(p => p.Water).ThenBy(p => p.Cell.TrueY).ThenBy(p => p.Cell.TrueX).Select(p => p.Cell), 1, 3))
                        Add(world, headland, new List<WorldBlock> { point }, null);
                }
                FeatureType cove = type("Cove");
                if (cove != null)
                {
                    var sheltered = Pieces(cells, c =>
                    {
                        var s = c.Status;
                        return s.WaterStatus == WorldBlockWaterStatus.Water && s.Lake == null && (s.Ocean != null || s.Sea != null)
                            && 8 - waterAround(c.X, c.Y) >= 5;
                    });
                    AddAreas(world, cove, sheltered, 1, CoveMax, 1);
                }

                // Isles: land with water all round, wholly inside the parent
                FeatureType isle = type("Isle");
                if (isle != null)
                {
                    var islands = Pieces(cells, c => c.Status.WaterStatus != WorldBlockWaterStatus.Water)
                        .Where(p => p.Count <= IsleMax && p.All(c => c.X > 0 && c.Y > 0 && c.X < N - 1 && c.Y < N - 1 && land(c)))
                        .ToList();
                    foreach (var piece in Largest(islands, MaxPerKind)) Add(world, isle, piece, null);
                }

                // Beaches and cliffs: stretches of flat and of rugged coast
                Func<WorldBlock, bool> shore = c => land(c) && c.Status.WaterStatus == WorldBlockWaterStatus.Coastline;
                AddAreas(world, type("Beach"), Pieces(cells, c => shore(c) && c.Status.Terrain.TerrainType.Relief <= 1
                    && c.Status.Terrain.TerrainType.Wetness < 0.7 && !inRange(c) && c.Status.Elevation < CliffElevation), BeachMin, int.MaxValue, 1);
                // Cliffs: rugged terrain at the shore, or land that stands high right beside the sea
                AddAreas(world, type("Cliffs"), Pieces(cells, c => shore(c) && (c.Status.Terrain.TerrainType.Relief >= 2 || inRange(c)
                    || c.Status.Elevation >= CliffElevation)), CliffsMin, int.MaxValue, 1);

                // Lagoons: sea water almost shut in by land, with a narrow mouth to the rest of the sea
                FeatureType lagoon = type("Lagoon");
                if (lagoon != null)
                {
                    Func<WorldBlockStatus, bool> sea = st => st != null && st.WaterStatus == WorldBlockWaterStatus.Water && st.Lake == null && (st.Ocean != null || st.Sea != null);
                    var pools = new List<List<WorldBlock>>();
                    foreach (var piece in Pieces(cells, c => sea(c.Status)).Where(p => p.Count >= 2 && p.Count <= LagoonMax))
                    {
                        var inside = new HashSet<WorldBlock>(piece);
                        int mouth = 0, shoreline = 0;
                        foreach (WorldBlock c in piece)
                            foreach (var (dx, dy) in new[] { (1, 0), (0, 1), (-1, 0), (0, -1) })
                            {
                                int a = c.X + dx, b = c.Y + dy;
                                bool within = a >= 0 && b >= 0 && a < N && b < N;
                                if (within && inside.Contains(cells[a, b])) continue;
                                var st = at(a, b);
                                if (sea(st)) mouth++;
                                else if (st != null && st.WaterStatus != WorldBlockWaterStatus.Water) shoreline++;
                            }
                        if (mouth >= 1 && mouth <= 2 && shoreline >= 3 * mouth + 3) pools.Add(piece);
                    }
                    foreach (var piece in Largest(pools, 1)) Add(world, lagoon, piece, null);
                }

                // Calderas: the collapsed summit of some volcanoes, where the volcano's spot lies in this block
                FeatureType caldera = type("Caldera");
                if (caldera != null)
                {
                    foreach (GeographicFeature volcano in world.FeaturesAt(parent).Where(f => f.Type.Id == VolcanoDetector.VolcanoTypeId && f.Spine.Count > 0 && f.Spine[0] == parent).ToList())
                    {
                        if (new Randomizer.Rnd(unchecked(volcano.Seed * 31 + 0xca1d)).R01() >= CalderaShare) continue;
                        var summit = cells.Cast<WorldBlock>().Where(c => land(c) && features[c].Contains(volcano)).OrderBy(c => c.TrueY).ThenBy(c => c.TrueX).ToList();
                        if (summit.Count > 0) Add(world, caldera, summit, volcano);
                    }
                }
            }
            else
            {
                // Knolls: higher than all eight blocks around, and well above their average
                FeatureType knoll = type("Knoll");
                if (knoll != null)
                {
                    var tops = new List<(WorldBlock Cell, double Prominence)>();
                    for (int i = 0; i < N; i++)
                    {
                        for (int j = 0; j < N; j++)
                        {
                            WorldBlock c = cells[i, j];
                            if (!land(c) || c.Status.Terrain.TerrainType.Wetness >= 0.5) continue;
                            double e = c.Status.Elevation.Value, sum = 0;
                            bool highest = true;
                            int count = 0;
                            for (int dx = -2; dx <= 2 && highest; dx++)
                                for (int dy = -2; dy <= 2; dy++)
                                {
                                    if (dx == 0 && dy == 0) continue;
                                    var s = at(i + dx, j + dy);
                                    if (s?.Elevation == null) continue;
                                    if (s.Elevation.Value >= e) { highest = false; break; }
                                    sum += s.Elevation.Value;
                                    count++;
                                }
                            if (highest && count == 24 && e - sum / 24 >= KnollProminence) tops.Add((c, e - sum / 24));
                        }
                    }
                    foreach (var (cell, _) in tops.OrderByDescending(t => t.Prominence).ThenBy(t => t.Cell.TrueY).ThenBy(t => t.Cell.TrueX).Take(1))
                        Add(world, knoll, new List<WorldBlock> { cell }, null);
                }

                // Waterfalls: the steepest drop between neighbouring blocks of a river, as part of that river
                FeatureType waterfall = type("Waterfall");
                if (waterfall != null)
                {
                    WorldBlock best = null;
                    double drop = WaterfallDrop;
                    for (int i = 0; i < N; i++)
                        for (int j = 0; j < N; j++)
                        {
                            WorldBlock c = cells[i, j];
                            if (!land(c) || riverOf(c) == null) continue;
                            // Falls need steep country: a mountain range, or rugged ground
                            if (!inRange(c) && c.Status.Terrain.TerrainType.Relief < 2) continue;
                            foreach (var (dx, dy) in new[] { (1, 0), (0, 1), (-1, 0), (0, -1) })
                            {
                                int a = i + dx, b = j + dy;
                                if (a < 0 || b < 0 || a >= N || b >= N) continue;
                                WorldBlock d = cells[a, b];
                                if (!land(d) || riverOf(d) != riverOf(c)) continue;
                                double fall = c.Status.Elevation.Value - d.Status.Elevation.Value;
                                if (fall > drop) { drop = fall; best = c; }
                            }
                        }
                    if (best != null) Add(world, waterfall, new List<WorldBlock> { best }, riverOf(best));
                }

                // Cave mouths: on slopes within a cave system
                FeatureType cave = type("Cave");
                if (cave != null)
                {
                    foreach (GeographicFeature system in cells.Cast<WorldBlock>().SelectMany(c => features[c]).Where(f => f.Type.Id == "CaveSystem").Distinct().ToList())
                    {
                        // A system has a few ways in: in the blocks of it its seed picks, one each
                        var chosen = system.Footprint.Blocks.OrderBy(b => Chance(world, cave, b)).ThenBy(b => b.TrueY).ThenBy(b => b.TrueX).Take(CaveMouthsMax);
                        if (!chosen.Contains(parent)) continue;
                        var mouth = cells.Cast<WorldBlock>().Where(c => land(c) && features[c].Contains(system) && c.Status.Terrain.TerrainType.Relief >= 2)
                            .OrderBy(c => Chance(world, cave, c)).ThenBy(c => c.TrueY).ThenBy(c => c.TrueX).FirstOrDefault();
                        if (mouth != null) Add(world, cave, new List<WorldBlock> { mouth }, system);
                    }
                }

                // Groves in open country, clearings in wooded country, and ponds
                var landCells = cells.Cast<WorldBlock>().Where(land).ToList();
                double wooded = landCells.Count == 0 ? 0 : landCells.Count(c => c.Status.Terrain.TerrainType.Trees >= 0.5) / (double)landCells.Count;
                if (wooded < 0.35)
                    AddAreas(world, type("Grove"), Pieces(cells, c => land(c) && c.Status.Terrain.TerrainType.Trees >= 0.5), GroveMin, GroveMax);
                if (wooded > 0.65)
                    AddAreas(world, type("Clearing"), Pieces(cells, c => land(c) && c.Status.Terrain.TerrainType.Trees < 0.3), 1, ClearingMax);
                Func<WorldBlock, bool> pond = c => land(c) && c.Status.Terrain.TerrainType.Wetness >= 0.5
                    && c.Status.Terrain.TerrainType.Wetness < 0.9 && c.Status.Terrain.TerrainType.Relief <= 1;
                if (landCells.Count > 0 && landCells.Count(pond) <= PondShareMax * landCells.Count)
                    AddAreas(world, type("Pond"), Pieces(cells, pond), 2, PondMax, 1);
            }
        }

        // Connected pieces (side by side) of the children that pass a test, each in a fixed order
        private static List<List<WorldBlock>> Pieces(WorldBlock[,] cells, Func<WorldBlock, bool> test)
        {
            var pieces = new List<List<WorldBlock>>();
            var seen = new bool[N, N];
            for (int j = 0; j < N; j++)
            {
                for (int i = 0; i < N; i++)
                {
                    if (seen[i, j] || !test(cells[i, j])) continue;
                    var piece = new List<WorldBlock>();
                    var queue = new Queue<(int, int)>();
                    queue.Enqueue((i, j));
                    seen[i, j] = true;
                    while (queue.Count > 0)
                    {
                        var (x, y) = queue.Dequeue();
                        piece.Add(cells[x, y]);
                        foreach (var (dx, dy) in new[] { (1, 0), (0, 1), (-1, 0), (0, -1) })
                        {
                            int a = x + dx, b = y + dy;
                            if (a < 0 || b < 0 || a >= N || b >= N || seen[a, b] || !test(cells[a, b])) continue;
                            seen[a, b] = true;
                            queue.Enqueue((a, b));
                        }
                    }
                    pieces.Add(piece.OrderBy(c => c.TrueY).ThenBy(c => c.TrueX).ToList());
                }
            }
            return pieces;
        }

        // The largest pieces, ties broken by position
        private static IEnumerable<List<WorldBlock>> Largest(IEnumerable<List<WorldBlock>> pieces, int count)
        {
            return pieces.OrderByDescending(p => p.Count).ThenBy(p => p[0].TrueY).ThenBy(p => p[0].TrueX).Take(count);
        }

        // Up to count blocks from a list in order of preference, no two within `gap` blocks of each other
        private static IEnumerable<WorldBlock> Spread(IEnumerable<WorldBlock> ranked, int count, int gap)
        {
            var chosen = new List<WorldBlock>();
            foreach (WorldBlock block in ranked)
            {
                if (chosen.Count >= count) break;
                if (chosen.Any(c => Math.Abs(c.X - block.X) < gap && Math.Abs(c.Y - block.Y) < gap)) continue;
                chosen.Add(block);
            }
            return chosen;
        }

        // The blocks of a connected piece in order from one end to the other, as near as can be: from the block farthest from
        // the first, always on to the nearest one not yet visited
        private static List<WorldBlock> Chain(List<WorldBlock> blocks)
        {
            Func<WorldBlock, WorldBlock, long> gap = (a, b) => (long)(a.TrueX - b.TrueX) * (a.TrueX - b.TrueX) + (long)(a.TrueY - b.TrueY) * (a.TrueY - b.TrueY);
            WorldBlock end = blocks.OrderByDescending(b => gap(b, blocks[0])).ThenBy(b => b.TrueY).ThenBy(b => b.TrueX).First();
            var chain = new List<WorldBlock> { end };
            var left = new List<WorldBlock>(blocks);
            left.Remove(end);
            while (left.Count > 0)
            {
                WorldBlock last = chain[chain.Count - 1];
                WorldBlock next = left.OrderBy(b => gap(b, last)).ThenBy(b => b.TrueY).ThenBy(b => b.TrueX).First();
                chain.Add(next);
                left.Remove(next);
            }
            return chain;
        }

        private static void AddAreas(World world, FeatureType type, List<List<WorldBlock>> pieces, int min, int max, int most = MaxPerKind)
        {
            if (type == null) return;
            foreach (var piece in Largest(pieces.Where(p => p.Count >= min && p.Count <= max), most))
                Add(world, type, piece, null);
        }

        private static GeographicFeature Add(World world, FeatureType type, List<WorldBlock> blocks, GeographicFeature parent,
            IFeatureRefiner shape = null, string parentName = null)
        {
            if (parent != null && !type.IsPartOf(parent.Type)) parent = null;
            if (parent == null && type.ParentTypes.Count > 0) return null; // a part needs the feature it is part of
            int seed = GeographicFeature.SeedFor(world.SimEngine.Rnd.Seed, type, blocks[0]);
            IFeatureRefiner inner = shape ?? (type.Shape == FeatureShape.Point
                ? (IFeatureRefiner)new PointRefiner(seed, 0.3)
                : new NoiseEdgeRefiner(seed, 0.3, 2.0));
            GeographicFeature feature = new GeographicFeature(world, type, blocks, seed) { Refiner = new InsideRefiner(inner) };
            if (type.Shape != FeatureShape.Blob) feature.SetSpine(type.Shape == FeatureShape.Point ? blocks.Take(1) : Chain(blocks));

            WorldBlockStatus first = blocks.Select(b => b.Status).FirstOrDefault(s => s.Region != null) ?? blocks[0].Status;
            ILanguage language = first.Region?.Language ?? first.Continent?.RootLanguage ?? world.Language;
            feature.Name = world.SimEngine.Language.NameFromSeed(language, new NameContext(type.NameKind)
            {
                ParentName = parentName ?? parent?.Name ?? first.Region?.Name
            }.WithClimate(NameHints.Climate(blocks)), seed);
            parent?.AddChild(feature);
            world.AddFeature(feature);
            return feature;
        }

        // A number in [0, 1) fixed by the world seed, a feature type and a block: for rare features placed by chance
        private static double Chance(World world, FeatureType type, WorldBlock block)
        {
            ulong h = unchecked((ulong)GeographicFeature.SeedFor(world.SimEngine.Rnd.Seed, type, block) * 0x9E3779B97F4A7C15UL);
            h ^= h >> 30; h *= 0xbf58476d1ce4e5b9UL;
            h ^= h >> 27; h *= 0x94d049bb133111ebUL;
            h ^= h >> 31;
            return (h >> 11) * (1.0 / (1UL << 53));
        }

        /// <summary>
        /// Impact craters, placed by chance (CraterChance per 100 km block of land): a round bowl 20 to 60 km across at a
        /// position in the block fixed by the seed, where the ground is mostly land and not under an ice sheet. In wet
        /// country a larger crater's middle fills with water: a crater lake, named after the crater. The lake keeps clear
        /// of the block's outer row of children, so the detail beside it, which may already have been made, never meets
        /// it.
        /// </summary>
        private static void Craters(World world, WorldBlock parent, WorldBlock[,] cells, Dictionary<WorldBlock, IReadOnlyList<GeographicFeature>> features,
            NoiseGeneratorRules rules, Func<string, FeatureType> type)
        {
            FeatureType crater = type("Crater");
            if (crater == null || rules.CraterChance <= 0 || Chance(world, crater, parent) >= rules.CraterChance) return;
            var rnd = new Randomizer.Rnd(unchecked(GeographicFeature.SeedFor(world.SimEngine.Rnd.Seed, crater, parent) * 31 + 0x5ca7));
            double radius = 1.0 + 2.0 * rnd.R01();                 // in children: 10 to 30 km
            double cx = 2.5 + 5.0 * rnd.R01(), cy = 2.5 + 5.0 * rnd.R01();
            var disc = cells.Cast<WorldBlock>().Where(c => (c.X + 0.5 - cx) * (c.X + 0.5 - cx) + (c.Y + 0.5 - cy) * (c.Y + 0.5 - cy) <= radius * radius).ToList();
            var ground = disc.Where(c => c.Status.WaterStatus != WorldBlockWaterStatus.Water && c.Status.Terrain != null
                && !features[c].Any(f => f.Type.Id == GlacialDetector.IceSheetTypeId)).OrderBy(c => c.TrueY).ThenBy(c => c.TrueX).ToList();
            if (ground.Count < 3 || ground.Count < 0.7 * disc.Count) return;
            double size = cells[0, 0].TrueWidth;
            double tx = parent.TrueX + cx * size, ty = parent.TrueY + cy * size;
            GeographicFeature bowl = Add(world, crater, ground, null, new CircleRefiner(tx, ty, radius * size));
            if (bowl == null) return;

            FeatureType lakeType = type("CraterLake");
            double wet = ground.Average(c => c.Status.Hydration ?? 0);
            if (lakeType == null || radius < CraterLakeRadius || wet < CraterLakeHydration) return;
            var water = ground.Where(c => c.X > 0 && c.Y > 0 && c.X < N - 1 && c.Y < N - 1
                && (c.X + 0.5 - cx) * (c.X + 0.5 - cx) + (c.Y + 0.5 - cy) * (c.Y + 0.5 - cy) <= (radius - 1) * (radius - 1)).ToList();
            if (water.Count == 0) return;
            GeographicFeature lake = null;
            lake = Add(world, lakeType, water, null, new WaterBodyRefiner(s => s.Lake == lake), bowl.Name);
            if (lake == null) return;
            foreach (WorldBlock c in water)
            {
                WorldBlockStatus s = c.Status;
                s.WaterStatus = WorldBlockWaterStatus.Water;
                s.Elevation = -0.001;
                s.Lake = lake;
                s.Continent = null;
                s.Region = null;
                s.Subregion = null;
                s.Terrain = null;
            }
        }
    }
}
