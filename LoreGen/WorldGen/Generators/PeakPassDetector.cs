using System;
using System.Collections.Generic;
using System.Linq;

using LoreGen.Features;
using LoreGen.Lang;
using LoreGen.Simulation;

namespace LoreGen.WorldGen.Generators
{
    /// <summary>
    /// Finds the peaks and passes of each continental mountain range, in the fine elevation of the range's sub-blocks
    /// (see CoastalDetail, which gives high ground ridged relief). Both are Regional Point features that are part of (children
    /// of) their range.
    ///
    /// Both come from the topographic prominence of the range's summits. Picture the range's land sub-blocks flooding from
    /// the top down: each summit starts its own island, and where two islands first meet is a saddle. A summit's
    /// prominence is how far it rises above the saddle where its island joins one with a higher summit (the highest
    /// summit's is its height above the range's lowest ground).
    ///
    /// Peaks are the highest summits whose prominence reaches <see cref="NoiseGeneratorRules.PeakProminenceFrom"/>, so
    /// lesser bumps on a peak's shoulder are not counted, at most <see cref="NoiseGeneratorRules.PeaksPerRange"/> of them.
    /// Passes are the saddles between two such summits (the lowest way over the crest from one to the other), the
    /// deepest first (the most the lesser summit rises above the saddle), at most
    /// <see cref="NoiseGeneratorRules.PassesPerRange"/> of them. Each is named in the language of the region it lies in.
    ///
    /// Runs after the sub-blocks exist. Uses no random numbers except for naming.
    /// </summary>
    internal static class PeakPassDetector
    {
        public const string PeakTypeId = "Peak";
        public const string PassTypeId = "Pass";

        /// <summary>Radius of a peak or pass below its own depth, as a share of its 100 km block (about 6 km).</summary>
        private const double SpotRadius = 0.06;

        public static List<GeographicFeature> Detect(SimEngine engine, World world, NoiseGeneratorRules rules)
        {
            List<GeographicFeature> found = new List<GeographicFeature>();
            if (rules.PeaksPerRange == 0 && rules.PassesPerRange == 0) return found;

            FeatureType rangeType = engine.SimData.FeatureType(MountainRangeDetector.TypeId);
            FeatureType peakType = engine.SimData.FeatureType(PeakTypeId);
            FeatureType passType = engine.SimData.FeatureType(PassTypeId);

            foreach (GeographicFeature range in world.Features.Where(f => f.Type == rangeType).ToList())
            {
                List<WorldBlock> cells = range.Footprint.Blocks
                    .Where(b => b.ChildBlocks != null)
                    .SelectMany(b => b.ChildBlocksAsList())
                    .Where(c => c.Status.WaterStatus != WorldBlockWaterStatus.Water && c.Status.Elevation.HasValue)
                    .ToList();
                if (cells.Count == 0) continue;

                Prominence prominence = Prominence.Of(cells);
                List<WorldBlock> peaks = prominence.Summits
                    .Where(s => s.Value >= rules.PeakProminenceFrom)
                    .Select(s => s.Key)
                    .OrderByDescending(c => c.Status.Elevation.Value).ThenBy(c => FeatureGeometry.Coord(c).Y).ThenBy(c => FeatureGeometry.Coord(c).X)
                    .Take(rules.PeaksPerRange).ToList();
                // A saddle where several islands meet at once is one pass
                List<WorldBlock> passes = prominence.Saddles
                    .Where(s => s.Depth >= rules.PeakProminenceFrom)
                    .GroupBy(s => s.Cell).Select(g => (Cell: g.Key, Depth: g.Max(s => s.Depth)))
                    .OrderByDescending(s => s.Depth).ThenBy(s => FeatureGeometry.Coord(s.Cell).Y).ThenBy(s => FeatureGeometry.Coord(s.Cell).X)
                    .Select(s => s.Cell).Take(rules.PassesPerRange).ToList();

                foreach (WorldBlock cell in peaks) found.Add(Add(engine, world, range, peakType, cell, cells));
                foreach (WorldBlock cell in passes) found.Add(Add(engine, world, range, passType, cell, cells));
            }
            return found;
        }

        private static GeographicFeature Add(SimEngine engine, World world, GeographicFeature parent, FeatureType type, WorldBlock cell, List<WorldBlock> within)
        {
            // Below the sub-block (when zooming in) a peak or pass is a small spot, not the whole 100 km block
            int seed = GeographicFeature.SeedFor(engine.Rnd.Seed, type, cell);
            GeographicFeature feature = new GeographicFeature(world, type, new[] { cell }, seed)
            {
                Refiner = new PointRefiner(seed, SpotRadius)
            };
            feature.SetSpine(new[] { cell });
            WorldBlockStatus status = cell.Status;
            ILanguage language = status.Region?.Language ?? status.Continent.RootLanguage;
            feature.Name = language.GetName(new NameContext(type.NameKind)
            {
                ParentName = parent.Name,
                Direction = NameHints.DirectionWithin(new[] { cell }, within)
            }.WithClimate(NameHints.Climate(new[] { cell })));
            parent.AddChild(feature);
            world.AddFeature(feature);
            return feature;
        }

        /// <summary>
        /// The summits of a set of cells (all at one depth) with their prominence, and the saddles where two islands of
        /// higher ground meet, found by flooding from the top down. Cells are neighbours when they touch at a side or a
        /// corner. Equal heights are taken in scan order, so the result does not depend on the order of the cells given.
        /// </summary>
        internal sealed class Prominence
        {
            /// <summary>Each summit and its prominence.</summary>
            public Dictionary<WorldBlock, double> Summits = new Dictionary<WorldBlock, double>();

            /// <summary>Each saddle, and how far the lower of the two summits it joins rises above it.</summary>
            public List<(WorldBlock Cell, double Depth)> Saddles = new List<(WorldBlock, double)>();

            public static Prominence Of(IReadOnlyList<WorldBlock> cells)
            {
                Prominence result = new Prominence();
                List<(WorldBlock Cell, int X, int Y, double E)> order = cells
                    .Select(c => { var at = FeatureGeometry.Coord(c); return (c, at.X, at.Y, c.Status.Elevation.Value); })
                    .OrderByDescending(c => c.Item4).ThenBy(c => c.Item3).ThenBy(c => c.Item2).ToList();

                Dictionary<(int, int), int> index = new Dictionary<(int, int), int>();
                int[] parent = new int[order.Count];
                int[] summit = new int[order.Count]; // for each root: the index of its island's summit
                Func<int, int> find = null;
                find = i => parent[i] == i ? i : (parent[i] = find(parent[i]));

                for (int i = 0; i < order.Count; i++)
                {
                    parent[i] = i;
                    summit[i] = i;
                    index[(order[i].X, order[i].Y)] = i;

                    // The islands this cell touches, highest summit first
                    List<int> islands = new List<int>();
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            int j;
                            if ((dx != 0 || dy != 0) && index.TryGetValue((order[i].X + dx, order[i].Y + dy), out j) && j != i)
                            {
                                int root = find(j);
                                if (!islands.Contains(root)) islands.Add(root);
                            }
                        }
                    }
                    if (islands.Count == 0) continue; // a new summit
                    islands.Sort((a, b) => summit[a].CompareTo(summit[b])); // earlier in the order is higher

                    // Every island but the highest ends here: this cell is the saddle joining it to higher ground
                    for (int k = 1; k < islands.Count; k++)
                    {
                        double depth = order[summit[islands[k]]].E - order[i].E;
                        result.Summits[order[summit[islands[k]]].Cell] = depth;
                        result.Saddles.Add((order[i].Cell, depth));
                    }
                    foreach (int island in islands) parent[island] = i;
                    summit[i] = summit[islands[0]];
                }

                // Islands that never joined higher ground: their prominence is their height above the lowest cell
                double lowest = order.Count > 0 ? order[order.Count - 1].E : 0;
                for (int i = 0; i < order.Count; i++)
                    if (find(i) == i) result.Summits[order[summit[i]].Cell] = order[summit[i]].E - lowest;
                return result;
            }
        }
    }
}
