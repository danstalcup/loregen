using System;
using System.Collections.Generic;
using System.Linq;

using LoreGen.Features;
using LoreGen.Lang;
using LoreGen.Simulation;

namespace LoreGen.WorldGen.Generators
{
    /// <summary>
    /// Finds the regional ranges within each continental mountain range: its main crests, in the fine elevation of the
    /// range's sub-blocks (see CoastalDetail, which gives high ground ridged relief).
    ///
    /// Crest is ranked locally: within each of the range's blocks, the highest <see cref="NoiseGeneratorRules.SubRangeShare"/>
    /// of its land sub-blocks (never any below the range's own height threshold). Ranking per block rather than across the
    /// whole range takes out the broad rise toward the range's summit, leaving the ridge lines of the relief, so crests come
    /// out long and narrow rather than as one blob around the highest point. Crest sub-blocks that touch form one
    /// sub-range; those smaller than <see cref="NoiseGeneratorRules.SubRangeMinBlocks"/> are left out, and only the largest
    /// <see cref="NoiseGeneratorRules.SubRangesPerRange"/> are kept. Each becomes a Regional mountain range that is part of
    /// (a child of) its continental range, named in the language of the region it lies in.
    ///
    /// Runs after the sub-blocks exist. Uses no random numbers except for naming.
    /// </summary>
    internal static class SubRangeDetector
    {
        public static List<GeographicFeature> Detect(SimEngine engine, World world, NoiseGeneratorRules rules)
        {
            List<GeographicFeature> found = new List<GeographicFeature>();
            if (rules.SubRanges == 0) return found;

            FeatureType type = engine.SimData.FeatureType(MountainRangeDetector.TypeId);
            List<GeographicFeature> ranges = world.Features
                .Where(f => f.Type == type && f.Scope == FeatureScope.Continental).ToList();

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
                    // Blocks below the sub-blocks copy their parent's elevation, so the default refiner (no finer detail) is exact
                    GeographicFeature sub = new GeographicFeature(world, type, FeatureScope.Regional, group,
                        GeographicFeature.SeedFor(engine.Rnd.Seed, type, group[0]));
                    sub.SetSpine(FeatureGeometry.Spine(group, 1));

                    WorldBlockStatus first = group[0].Status;
                    ILanguage language = first.Region?.Language ?? first.Continent.RootLanguage;
                    sub.Name = language.GetName(new NameContext(type.NameKind)
                    {
                        ParentName = range.Name,
                        Direction = NameHints.DirectionWithin(group, cells)
                    });

                    range.AddChild(sub);
                    world.AddFeature(sub);
                    found.Add(sub);
                }
            }
            return found;
        }
    }
}
