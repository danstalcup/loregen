using System;
using System.Collections.Generic;
using System.Linq;

using LoreGen.Features;
using LoreGen.Lang;
using LoreGen.Simulation;

namespace LoreGen.WorldGen.Generators
{
    /// <summary>
    /// Finds the parts of each continental mountain range: its main crests, in the fine elevation of the range's
    /// sub-blocks (see CoastalDetail, which gives high ground ridged relief). Each crest is a Regional feature of one of
    /// two types, by its shape: a Subrange if it is long for its size (a chain of peaks, with a spine), or a Massif if it
    /// is compact (a knot of peaks).
    ///
    /// Crest is ranked locally: within each of the range's blocks, the highest <see cref="NoiseGeneratorRules.SubRangeShare"/>
    /// of its land sub-blocks (never any below the range's own height threshold). Ranking per block rather than across the
    /// whole range takes out the broad rise toward the range's summit, leaving the ridge lines of the relief, so crests come
    /// out long and narrow rather than as one blob around the highest point. Crest sub-blocks that touch form one
    /// sub-range; those smaller than <see cref="NoiseGeneratorRules.SubRangeMinBlocks"/> are left out, and only the largest
    /// <see cref="NoiseGeneratorRules.SubRangesPerRange"/> are kept. A crest is a subrange when the square of its spine's
    /// length over its size reaches <see cref="NoiseGeneratorRules.SubrangeElongationFrom"/> (a compact knot is about 1, a
    /// line of n sub-blocks n), and a massif otherwise. Each is part of (a child of) its range, and is named in the
    /// language of the region it lies in.
    ///
    /// Runs after the sub-blocks exist. Uses no random numbers except for naming.
    /// </summary>
    internal static class SubRangeDetector
    {
        public const string SubrangeTypeId = "Subrange";
        public const string MassifTypeId = "Massif";

        public static List<GeographicFeature> Detect(SimEngine engine, World world, NoiseGeneratorRules rules)
        {
            List<GeographicFeature> found = new List<GeographicFeature>();
            if (rules.SubRanges == 0) return found;

            FeatureType rangeType = engine.SimData.FeatureType(MountainRangeDetector.TypeId);
            FeatureType subrangeType = engine.SimData.FeatureType(SubrangeTypeId);
            FeatureType massifType = engine.SimData.FeatureType(MassifTypeId);
            List<GeographicFeature> ranges = world.Features.Where(f => f.Type == rangeType).ToList();

            foreach (GeographicFeature range in ranges)
            {
                // The range's land sub-blocks, in scan order of their position in the world
                List<WorldBlock> cells = range.Footprint.Blocks
                    .Where(b => b.ChildBlocks != null)
                    .SelectMany(b => b.ChildBlocksAsList())
                    .Where(c => c.Status.WaterStatus != WorldBlockWaterStatus.Water && c.Status.Elevation.HasValue)
                    .OrderBy(c => FeatureGeometry.Coord(c).Y).ThenBy(c => FeatureGeometry.Coord(c).X)
                    .ToList();
                if (cells.Count == 0) continue;

                // Each block's own crest height: the elevation its highest SubRangeShare of sub-blocks reach
                Dictionary<WorldBlock, double> crestFrom = new Dictionary<WorldBlock, double>();
                foreach (IGrouping<WorldBlock, WorldBlock> inBlock in cells.GroupBy(c => c.ParentBlock))
                {
                    List<double> heights = inBlock.Select(c => c.Status.Elevation.Value).OrderBy(e => e).ToList();
                    int at = Math.Min(heights.Count - 1, (int)Math.Floor((1.0 - rules.SubRangeShare) * heights.Count));
                    crestFrom[inBlock.Key] = Math.Max(rules.MountainElevationFrom, heights[at]);
                }

                List<WorldBlock> crest = cells.Where(c => c.Status.Elevation.Value >= crestFrom[c.ParentBlock]).ToList();
                List<List<WorldBlock>> groups = FeatureGeometry.TouchingGroups(crest)
                    .Where(g => g.Count >= rules.SubRangeMinBlocks).ToList();
                // Keep the biggest, but create them in the order they lie in, so names don't depend on sizes tying
                HashSet<List<WorldBlock>> kept = new HashSet<List<WorldBlock>>(groups
                    .Select((g, i) => (Group: g, Index: i))
                    .OrderByDescending(x => x.Group.Count).ThenBy(x => x.Index)
                    .Take(rules.SubRangesPerRange).Select(x => x.Group));

                foreach (List<WorldBlock> group in groups.Where(kept.Contains))
                {
                    List<WorldBlock> spine = FeatureGeometry.Spine(group, 1);
                    double elongation = (double)spine.Count * spine.Count / group.Count;
                    FeatureType type = elongation >= rules.SubrangeElongationFrom ? subrangeType : massifType;

                    // Below the sub-blocks (when zooming in) the edge winds rather than following block edges
                    int seed = GeographicFeature.SeedFor(engine.Rnd.Seed, type, group[0]);
                    GeographicFeature sub = new GeographicFeature(world, type, group, seed)
                    {
                        Refiner = new NoiseEdgeRefiner(seed, rules.FeatureEdgeStrength, rules.FeatureEdgeFrequency)
                    };
                    if (type.Shape == FeatureShape.Linear) sub.SetSpine(spine);

                    WorldBlockStatus first = group[0].Status;
                    ILanguage language = first.Region?.Language ?? first.Continent.RootLanguage;
                    sub.Name = language.GetName(new NameContext(type.NameKind)
                    {
                        ParentName = range.Name,
                        Direction = NameHints.DirectionWithin(group, cells)
                    }.WithClimate(NameHints.Climate(group)));

                    range.AddChild(sub);
                    world.AddFeature(sub);
                    found.Add(sub);
                }
            }
            return found;
        }
    }
}
