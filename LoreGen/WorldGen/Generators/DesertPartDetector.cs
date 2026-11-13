using System;
using System.Collections.Generic;
using System.Linq;

using LoreGen.Features;
using LoreGen.Lang;
using LoreGen.Noise;
using LoreGen.Simulation;

namespace LoreGen.WorldGen.Generators
{
    /// <summary>
    /// Finds the parts of each continental desert in the fine elevation of its sub-blocks (see CoastalDetail, which gives
    /// desert ground gentle rises and hollows), on its ground below mountain height. All are Regional features that are part of (children of) their desert:
    ///
    /// - Salt flats (Blob): the floors of the larger closed basins. A basin floor is ground lying at least
    ///   <see cref="NoiseGeneratorRules.BasinDepthFrom"/> below the level water would have to rise to before spilling out
    ///   of the desert (found by filling every hollow from the desert's edge inward). Floors of at least
    ///   <see cref="NoiseGeneratorRules.SaltFlatMinBlocks"/> sub-blocks are salt flats; the largest
    ///   <see cref="NoiseGeneratorRules.SaltFlatsPerDesert"/> are kept.
    /// - Oases (Point): the deepest spot of a smaller basin floor, where ground water would come nearest the surface; the
    ///   deepest <see cref="NoiseGeneratorRules.OasesPerDesert"/> are kept.
    /// - Ergs (Blob): sand seas. A smooth noise field fixed by the desert's seed marks where sand has gathered: the
    ///   highest <see cref="NoiseGeneratorRules.ErgShare"/> of the desert's ground outside salt flats. Pieces of at least
    ///   <see cref="NoiseGeneratorRules.ErgMinBlocks"/> sub-blocks are ergs; the largest
    ///   <see cref="NoiseGeneratorRules.ErgsPerDesert"/> are kept.
    ///
    /// Each is named in the language of the region it lies in. Runs after the sub-blocks exist. Uses no random numbers
    /// except for naming.
    /// </summary>
    internal static class DesertPartDetector
    {
        /// <summary>Radius of an oasis below its own depth, as a share of its 100 km block (about 4 km).</summary>
        private const double OasisRadius = 0.04;

        public const string ErgTypeId = "Erg";
        public const string SaltFlatTypeId = "SaltFlat";
        public const string OasisTypeId = "Oasis";

        // How closely sand seas are spaced, per block
        private const double ErgFrequency = 0.9;

        public static List<GeographicFeature> Detect(SimEngine engine, World world, NoiseGeneratorRules rules)
        {
            List<GeographicFeature> found = new List<GeographicFeature>();
            if (rules.DesertParts == 0) return found;

            FeatureType desertType = engine.SimData.FeatureType(DesertDetector.TypeId);
            FeatureType ergType = engine.SimData.FeatureType(ErgTypeId);
            FeatureType saltFlatType = engine.SimData.FeatureType(SaltFlatTypeId);
            FeatureType oasisType = engine.SimData.FeatureType(OasisTypeId);

            foreach (GeographicFeature desert in world.Features.Where(f => f.Type == desertType).ToList())
            {
                // The desert's land sub-blocks below mountain height (sand seas and basins lie on the lowland, not on any
                // mountains the desert spreads over), in scan order of their position in the world
                List<WorldBlock> cells = desert.Footprint.Blocks
                    .Where(b => b.ChildBlocks != null)
                    .SelectMany(b => b.ChildBlocksAsList())
                    .Where(c => c.Status.WaterStatus != WorldBlockWaterStatus.Water && c.Status.Elevation.HasValue
                        && c.Status.Elevation.Value < rules.MountainElevationFrom && desert.Contains(c))
                    .OrderBy(c => FeatureGeometry.Coord(c).Y).ThenBy(c => FeatureGeometry.Coord(c).X)
                    .ToList();
                if (cells.Count == 0) continue;

                // Basin floors, as touching pieces
                Dictionary<WorldBlock, double> depth = BasinDepths(cells);
                List<WorldBlock> floor = cells.Where(c => depth[c] >= rules.BasinDepthFrom).ToList();
                List<List<WorldBlock>> basins = FeatureGeometry.TouchingGroups(floor);

                List<List<WorldBlock>> saltFlats = Largest(basins.Where(b => b.Count >= rules.SaltFlatMinBlocks).ToList(), rules.SaltFlatsPerDesert);
                foreach (List<WorldBlock> flat in saltFlats)
                    found.Add(Add(engine, rules, world, desert, saltFlatType, flat, cells));

                List<WorldBlock> oases = basins.Where(b => b.Count < rules.SaltFlatMinBlocks)
                    .Select(b => b.OrderByDescending(c => depth[c]).First()) // ties keep scan order
                    .Select((c, i) => (Cell: c, Index: i))
                    .OrderByDescending(x => depth[x.Cell]).ThenBy(x => x.Index)
                    .Take(rules.OasesPerDesert).OrderBy(x => x.Index).Select(x => x.Cell).ToList();
                foreach (WorldBlock oasis in oases)
                {
                    GeographicFeature feature = Add(engine, rules, world, desert, oasisType, new List<WorldBlock> { oasis }, cells);
                    feature.SetSpine(new[] { oasis });
                    found.Add(feature);
                }

                // Sand seas: where the desert's own sand field is highest, outside the salt flats
                if (rules.ErgsPerDesert > 0 && rules.ErgShare > 0)
                {
                    HashSet<WorldBlock> flats = new HashSet<WorldBlock>(saltFlats.SelectMany(f => f));
                    FractalNoise sand = new FractalNoise(new SimplexNoise(desert.Seed), 3, ErgFrequency, 2.0, 0.5);
                    int n = cells[0].ParentBlock.Width;
                    Dictionary<WorldBlock, double> drift = new Dictionary<WorldBlock, double>();
                    foreach (WorldBlock c in cells)
                    {
                        var at = FeatureGeometry.Coord(c);
                        drift[c] = sand.Fbm((at.X + 0.5) / n, (at.Y + 0.5) / n);
                    }
                    List<WorldBlock> open = cells.Where(c => !flats.Contains(c)).ToList();
                    if (open.Count > 0)
                    {
                        List<double> levels = open.Select(c => drift[c]).OrderBy(v => v).ToList();
                        int at = (int)Math.Floor((1.0 - rules.ErgShare) * levels.Count);
                        if (at < levels.Count)
                        {
                            double from = levels[at];
                            List<WorldBlock> sandy = open.Where(c => drift[c] >= from).ToList();
                            List<List<WorldBlock>> ergs = Largest(FeatureGeometry.TouchingGroups(sandy).Where(g => g.Count >= rules.ErgMinBlocks).ToList(), rules.ErgsPerDesert);
                            foreach (List<WorldBlock> erg in ergs)
                                found.Add(Add(engine, rules, world, desert, ergType, erg, cells));
                        }
                    }
                }
            }
            return found;
        }

        /// <summary>
        /// How far below the spill level each cell lies: the lowest level water standing on it would have to rise to before
        /// it could run out of the set of cells. Cells on the set's edge spill straight out. Neighbours touch at a side or
        /// a corner. Ties are broken by the cells' order, so the result is fixed for a given set.
        /// </summary>
        internal static Dictionary<WorldBlock, double> BasinDepths(IReadOnlyList<WorldBlock> cells)
        {
            Dictionary<(int, int), int> index = new Dictionary<(int, int), int>();
            List<(int X, int Y)> coords = cells.Select(FeatureGeometry.Coord).ToList();
            for (int i = 0; i < cells.Count; i++) index[coords[i]] = i;

            double[] level = new double[cells.Count];
            bool[] done = new bool[cells.Count];
            PriorityQueue<int, (double, int)> pending = new PriorityQueue<int, (double, int)>();
            Func<int, IEnumerable<int>> neighbours = i =>
            {
                List<int> list = new List<int>();
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int j;
                        if ((dx != 0 || dy != 0) && index.TryGetValue((coords[i].X + dx, coords[i].Y + dy), out j)) list.Add(j);
                    }
                return list;
            };

            // Priority flood: start from the edge, and raise each cell reached to at least the level it was reached at
            for (int i = 0; i < cells.Count; i++)
            {
                if (neighbours(i).Count() < 8)
                {
                    level[i] = cells[i].Status.Elevation.Value;
                    done[i] = true;
                    pending.Enqueue(i, (level[i], i));
                }
            }
            while (pending.Count > 0)
            {
                int i = pending.Dequeue();
                foreach (int j in neighbours(i))
                {
                    if (done[j]) continue;
                    done[j] = true;
                    level[j] = Math.Max(level[i], cells[j].Status.Elevation.Value);
                    pending.Enqueue(j, (level[j], j));
                }
            }

            Dictionary<WorldBlock, double> depth = new Dictionary<WorldBlock, double>();
            for (int i = 0; i < cells.Count; i++) depth[cells[i]] = level[i] - cells[i].Status.Elevation.Value;
            return depth;
        }

        // The biggest groups, kept in the order they lie in (so names don't depend on sizes tying)
        private static List<List<WorldBlock>> Largest(List<List<WorldBlock>> groups, int count)
        {
            HashSet<List<WorldBlock>> kept = new HashSet<List<WorldBlock>>(groups
                .Select((g, i) => (Group: g, Index: i))
                .OrderByDescending(x => x.Group.Count).ThenBy(x => x.Index)
                .Take(count).Select(x => x.Group));
            return groups.Where(kept.Contains).ToList();
        }

        private static GeographicFeature Add(SimEngine engine, NoiseGeneratorRules rules, World world, GeographicFeature desert, FeatureType type, List<WorldBlock> group, List<WorldBlock> within)
        {
            // Below the sub-blocks (when zooming in) an erg's or salt flat's edge winds, and an oasis is a small spot
            int seed = GeographicFeature.SeedFor(engine.Rnd.Seed, type, group[0]);
            GeographicFeature feature = new GeographicFeature(world, type, group, seed)
            {
                Refiner = type.Shape == FeatureShape.Point ? new PointRefiner(seed, OasisRadius)
                    : new NoiseEdgeRefiner(seed, rules.FeatureEdgeStrength, rules.FeatureEdgeFrequency)
            };
            WorldBlockStatus first = group[0].Status;
            ILanguage language = first.Region?.Language ?? first.Continent.RootLanguage;
            feature.Name = language.GetName(new NameContext(type.NameKind)
            {
                ParentName = desert.Name,
                Direction = NameHints.DirectionWithin(group, within)
            }.WithClimate(NameHints.Climate(group)));
            desert.AddChild(feature);
            world.AddFeature(feature);
            return feature;
        }
    }
}
