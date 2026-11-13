using System;
using System.Collections.Generic;
using System.Linq;
using LoreGen.Features;
using LoreGen.Lang;
using LoreGen.Simulation;
using LoreGen.WorldDisplay;
using LoreGen.WorldGen;
using LoreGen.WorldGen.Generators;
using Xunit;

namespace LoreGen.Tests.Features
{
    /// <summary>
    /// Finer feature shapes (refiners and sub-blocks), deserts, and the sub-ranges within mountain ranges.
    /// </summary>
    public class FeatureDetailTests
    {
        private static readonly int[] Seeds = { 1, 2, 3, 4, 5, 6 };

        private static readonly FeatureType Blob = new FeatureType
        {
            Id = "TestBlob", Name = "Test blob", Category = FeatureCategory.Desert, Shape = FeatureShape.Blob,
            Scope = FeatureScope.Continental, NameKind = NameKind.Desert
        };

        private static WorldBlock Top(World world, int x, int y) { return world.WorldBlock.ChildBlocks[x, y]; }

        private static WorldBlock[,] Children(WorldBlock block)
        {
            if (block.ChildBlocks == null) block.InitializeChildBlocks();
            return block.ChildBlocks;
        }

        private static GeographicFeature Square(World world, int x0, int y0, int size, IFeatureRefiner refiner)
        {
            var blocks = new List<WorldBlock>();
            for (int y = y0; y < y0 + size; y++)
                for (int x = x0; x < x0 + size; x++)
                    blocks.Add(Top(world, x, y));
            return new GeographicFeature(world, Blob, blocks, 99) { Refiner = refiner };
        }

        private static IEnumerable<GeographicFeature> Of(World world, string typeId, FeatureScope scope)
        {
            return world.Features.Where(f => f.Type.Id == typeId && f.Scope == scope);
        }

        // ---- Noise edges ----------------------------------------------------------------------

        [Fact]
        public void NoiseEdgeWithNoStrengthIsTheCoarseEdge()
        {
            World world = NoiseTestWorlds.Generate(1).World;
            var feature = Square(world, 5, 5, 3, new NoiseEdgeRefiner(1, 0.0));
            Assert.True(feature.Contains(Children(Top(world, 5, 6))[0, 5]));   // edge block, outer side
            Assert.True(feature.Contains(Children(Top(world, 5, 6))[9, 5]));   // edge block, inner side
            Assert.False(feature.Contains(Children(Top(world, 4, 6))[9, 5]));  // beside block, next to the edge
        }

        [Fact]
        public void NoiseEdgeWindsBothInAndOut()
        {
            World world = NoiseTestWorlds.Generate(1).World;
            var feature = Square(world, 4, 4, 6, new NoiseEdgeRefiner(7, 0.4));
            int trimmed = 0, added = 0;
            for (int x = 3; x <= 10; x++)
            {
                for (int y = 3; y <= 10; y++)
                {
                    WorldBlock block = Top(world, x, y);
                    bool member = feature.Footprint.IsMember(block);
                    if (!feature.Footprint.IsEdge(block) && !feature.Footprint.IsBeside(block)) continue;
                    foreach (WorldBlock child in Children(block))
                    {
                        bool inside = feature.Contains(child);
                        if (member && !inside) trimmed++;
                        if (!member && inside) added++;
                    }
                }
            }
            Assert.True(trimmed > 0, "noise never pulls the edge inward");
            Assert.True(added > 0, "noise never pushes the edge outward");
        }

        [Fact]
        public void NoiseEdgeDoesNotDependOnTheOrderBlocksAreAskedAbout()
        {
            World world = NoiseTestWorlds.Generate(1).World;
            var blocks = new List<WorldBlock>();
            for (int x = 3; x <= 9; x++)
                for (int y = 3; y <= 9; y++)
                    blocks.AddRange(Children(Top(world, x, y)).Cast<WorldBlock>());

            var forward = Square(world, 4, 4, 5, new NoiseEdgeRefiner(3, 0.4));
            var backward = Square(world, 4, 4, 5, new NoiseEdgeRefiner(3, 0.4));
            var first = blocks.Select(forward.Contains).ToList();
            var second = Enumerable.Reverse(blocks).Select(backward.Contains).Reverse().ToList();
            Assert.Equal(first, second);
        }

        [Fact]
        public void NoiseEdgeStrengthMustLeaveTheEdgeRecognisable()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new NoiseEdgeRefiner(1, 0.5));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NoiseEdgeRefiner(1, -0.1));
        }

        // ---- Elevation edges ------------------------------------------------------------------

        [Fact]
        public void ElevationEdgeFollowsTheBlocksOwnHeight()
        {
            World world = NoiseTestWorlds.Generate(1).World;
            var refiner = new ElevationEdgeRefiner(0.6);
            var feature = Square(world, 5, 5, 3, refiner);
            WorldBlock child = Children(Top(world, 5, 6))[0, 0];

            child.Status.WaterStatus = WorldBlockWaterStatus.Land;
            child.Status.Elevation = 0.7;
            Assert.True(feature.Contains(child));
            child.Status.Elevation = 0.5;
            Assert.False(feature.Contains(child));
            child.Status.Elevation = 0.9;
            child.Status.WaterStatus = WorldBlockWaterStatus.Water;
            Assert.False(feature.Contains(child));
            child.Status.Elevation = null;
            Assert.True(feature.Contains(child));   // no height: follows the coarse block
        }

        [Fact]
        public void RangeEdgesFollowTheFineContours()
        {
            foreach (int seed in Seeds.Take(3))
            {
                SimEngine engine = NoiseTestWorlds.Generate(seed);
                double threshold = NoiseGeneratorRules.Load(engine.SimData).MountainElevationFrom;
                foreach (GeographicFeature range in Of(engine.World, MountainRangeDetector.TypeId, FeatureScope.Continental))
                {
                    foreach (WorldBlock block in range.Footprint.Edge())
                    {
                        Assert.NotNull(block.ChildBlocks);
                        foreach (WorldBlock child in block.ChildBlocks)
                        {
                            bool high = child.Status.WaterStatus != WorldBlockWaterStatus.Water && child.Status.Elevation >= threshold;
                            Assert.Equal(high, range.Contains(child));
                        }
                    }
                }
            }
        }

        // ---- Sub-blocks -----------------------------------------------------------------------

        [Fact]
        public void FeatureBlocksGetSubBlocks()
        {
            foreach (int seed in Seeds.Take(3))
            {
                World world = NoiseTestWorlds.Generate(seed).World;
                foreach (GeographicFeature feature in world.Features.Where(f => f.Scope == FeatureScope.Continental && f.Type.Category != FeatureCategory.Water))
                {
                    var needDetail = feature.Type.Category == FeatureCategory.Mountain ? feature.Footprint.Blocks : feature.Footprint.Edge();
                    Assert.All(needDetail, b => Assert.NotNull(b.ChildBlocks));
                }
            }
        }

        [Fact]
        public void ReliefNeverChangesWhatIsLandAndWhatIsWater()
        {
            string Surface(string relief)
            {
                // Without lakes, which are carved into the relieved ground and so do depend on it
                World world = NoiseTestWorlds.Generate(4, ("MountainRelief", relief), ("Hydrology", "0")).World;
                return string.Concat(world.WorldBlock.ChildBlocksAsList(true).Select(b => (int)b.Status.WaterStatus));
            }
            Assert.Equal(Surface("0"), Surface("0.5"));
        }

        [Fact]
        public void ReliefMakesMountainsRugged()
        {
            double Spread(string relief)
            {
                SimEngine engine = NoiseTestWorlds.Generate(4, ("MountainRelief", relief));
                var cells = Of(engine.World, MountainRangeDetector.TypeId, FeatureScope.Continental)
                    .SelectMany(r => r.Footprint.Blocks).SelectMany(b => b.ChildBlocksAsList()).Select(c => c.Status.Elevation.Value).ToList();
                double mean = cells.Average();
                return cells.Average(e => (e - mean) * (e - mean));
            }
            Assert.True(Spread("0.4") > Spread("0"));
        }

        [Fact]
        public void WithoutFeatureDetailThereAreNoSubRanges()
        {
            World world = NoiseTestWorlds.Generate(2, ("FeatureDetail", "0"), ("CoastDetail", "0"), ("RegionBorderDetail", "0"), ("Hydrology", "0")).World;
            Assert.All(world.WorldBlock.ChildBlocksAsList(), b => Assert.Null(b.ChildBlocks));
            Assert.DoesNotContain(world.Features, f => f.Scope == FeatureScope.Regional);
            Assert.NotEmpty(world.Features);
        }

        // ---- Deserts --------------------------------------------------------------------------

        [Fact]
        public void DesertsAreDryWarmLand()
        {
            int found = 0;
            foreach (int seed in Seeds)
            {
                SimEngine engine = NoiseTestWorlds.Generate(seed);
                NoiseGeneratorRules rules = NoiseGeneratorRules.Load(engine.SimData);
                var claimed = new HashSet<WorldBlock>();
                foreach (GeographicFeature desert in Of(engine.World, DesertDetector.TypeId, FeatureScope.Continental))
                {
                    found++;
                    Assert.Equal(FeatureShape.Blob, desert.Type.Shape);
                    Assert.Empty(desert.Spine);
                    Assert.IsType<NoiseEdgeRefiner>(desert.Refiner);
                    Assert.True(desert.Footprint.Count >= rules.DesertMinBlocks);
                    Assert.Single(desert.Footprint.Pieces());
                    Assert.False(string.IsNullOrWhiteSpace(desert.Name));
                    foreach (WorldBlock block in desert.Footprint.Blocks)
                    {
                        Assert.NotEqual(WorldBlockWaterStatus.Water, block.Status.WaterStatus);
                        Assert.True(block.Status.Moisture <= rules.DesertMoistureTo);
                        Assert.True(block.Status.Temperature >= rules.DesertTemperatureFrom);
                        Assert.True(claimed.Add(block), "a block is in two deserts");
                    }
                }
            }
            Assert.True(found > 0, "no deserts in any test world");
        }

        [Fact]
        public void DesertsCanBeSwitchedOff()
        {
            World world = NoiseTestWorlds.Generate(1, ("Deserts", "0")).World;
            Assert.Empty(Of(world, DesertDetector.TypeId, FeatureScope.Continental));
            Assert.NotEmpty(Of(world, MountainRangeDetector.TypeId, FeatureScope.Continental));
        }

        [Fact]
        public void DrierThresholdGivesLessDesert()
        {
            int Covered(string moisture) => Seeds.Take(4).Sum(seed =>
                Of(NoiseTestWorlds.Generate(seed, ("DesertMoistureTo", moisture)).World, DesertDetector.TypeId, FeatureScope.Continental).Sum(f => f.Footprint.Count));
            Assert.True(Covered("0.05") < Covered("0.3"));
        }

        // ---- Sub-ranges -----------------------------------------------------------------------

        [Fact]
        public void SubRangesAreCrestsWithinTheirRange()
        {
            int found = 0;
            foreach (int seed in Seeds)
            {
                SimEngine engine = NoiseTestWorlds.Generate(seed);
                NoiseGeneratorRules rules = NoiseGeneratorRules.Load(engine.SimData);
                foreach (GeographicFeature range in Of(engine.World, MountainRangeDetector.TypeId, FeatureScope.Continental))
                {
                    List<GeographicFeature> crests = range.Children.Where(c => c.Type.Id == SubRangeDetector.SubrangeTypeId || c.Type.Id == SubRangeDetector.MassifTypeId).ToList();
                    Assert.True(crests.Count <= rules.SubRangesPerRange);
                    foreach (GeographicFeature sub in crests)
                    {
                        found++;
                        Assert.Same(range, sub.Parent);
                        Assert.Equal(FeatureScope.Regional, sub.Scope);
                        Assert.Contains(sub.Type.Id, new[] { SubRangeDetector.SubrangeTypeId, SubRangeDetector.MassifTypeId });
                        Assert.Contains(sub, engine.World.Features);
                        Assert.True(sub.Footprint.Count >= rules.SubRangeMinBlocks);
                        Assert.Single(sub.Footprint.Pieces());
                        Assert.False(string.IsNullOrWhiteSpace(sub.Name));
                        foreach (WorldBlock block in sub.Footprint.Blocks)
                        {
                            Assert.True(range.Footprint.IsMember(block.ParentBlock), sub.Name + " lies outside " + range.Name);
                            Assert.True(range.Contains(block), range.Name + " does not contain its crest " + sub.Name);
                            Assert.True(block.Status.Elevation >= rules.MountainElevationFrom);
                        }
                        if (sub.Type.Shape == FeatureShape.Blob) Assert.Empty(sub.Spine);
                        else Assert.True(sub.Spine.Count * sub.Spine.Count >= rules.SubrangeElongationFrom * sub.Footprint.Count, sub.Name + " is too compact for a subrange");
                        for (int i = 1; i < sub.Spine.Count; i++)
                        {
                            var a = FeatureGeometry.Coord(sub.Spine[i - 1]);
                            var b = FeatureGeometry.Coord(sub.Spine[i]);
                            Assert.True(Math.Abs(a.X - b.X) <= 1 && Math.Abs(a.Y - b.Y) <= 1, sub.Name + ": spine steps are not neighbours");
                        }
                    }
                }
            }
            Assert.True(found > 0, "no sub-ranges in any test world");
        }

        [Fact]
        public void CrestsAreSubrangesOrMassifsByShape()
        {
            var types = new HashSet<string>();
            foreach (int seed in Seeds)
                foreach (GeographicFeature part in NoiseTestWorlds.Generate(seed).World.Features.Where(IsCrest))
                    types.Add(part.Type.Id);
            Assert.Equal(new[] { SubRangeDetector.MassifTypeId, SubRangeDetector.SubrangeTypeId }, types.OrderBy(t => t));

            World allLong = NoiseTestWorlds.Generate(2, ("SubrangeElongationFrom", "0")).World;
            Assert.All(allLong.Features.Where(IsCrest), f => Assert.Equal(SubRangeDetector.SubrangeTypeId, f.Type.Id));
            World allCompact = NoiseTestWorlds.Generate(2, ("SubrangeElongationFrom", "1000000")).World;
            Assert.All(allCompact.Features.Where(IsCrest), f => Assert.Equal(SubRangeDetector.MassifTypeId, f.Type.Id));
            Assert.Contains(allCompact.Features, IsCrest);
        }

        // A mountain range's crests: its subranges and massifs (not its peaks and passes)
        private static bool IsCrest(GeographicFeature f)
        {
            return f.Type.Category == FeatureCategory.Mountain && f.Scope == FeatureScope.Regional && f.Type.Shape != FeatureShape.Point;
        }

        [Fact]
        public void SubRangesCanBeSwitchedOff()
        {
            World world = NoiseTestWorlds.Generate(1, ("SubRanges", "0")).World;
            Assert.DoesNotContain(world.Features, IsCrest);
            Assert.Contains(world.Features, f => f.Type.Id == PeakPassDetector.PeakTypeId); // peaks don't depend on crests
        }

        // ---- Peaks and passes ---------------------------------------------------------------

        [Fact]
        public void PeaksAndPassesArePointsWithinTheirRange()
        {
            int peaks = 0, passes = 0;
            foreach (int seed in Seeds)
            {
                SimEngine engine = NoiseTestWorlds.Generate(seed);
                NoiseGeneratorRules rules = NoiseGeneratorRules.Load(engine.SimData);
                foreach (GeographicFeature range in Of(engine.World, MountainRangeDetector.TypeId, FeatureScope.Continental))
                {
                    List<GeographicFeature> rangePeaks = range.Children.Where(c => c.Type.Id == PeakPassDetector.PeakTypeId).ToList();
                    List<GeographicFeature> rangePasses = range.Children.Where(c => c.Type.Id == PeakPassDetector.PassTypeId).ToList();
                    Assert.True(rangePeaks.Count <= rules.PeaksPerRange);
                    Assert.True(rangePasses.Count <= rules.PassesPerRange);
                    peaks += rangePeaks.Count;
                    passes += rangePasses.Count;

                    foreach (GeographicFeature point in rangePeaks.Concat(rangePasses))
                    {
                        Assert.Equal(FeatureShape.Point, point.Type.Shape);
                        Assert.Equal(FeatureScope.Regional, point.Scope);
                        Assert.Equal(1, point.Footprint.Count);
                        WorldBlock cell = point.Footprint.Blocks.Single();
                        Assert.Equal(new[] { cell }, point.Spine);
                        Assert.True(range.Footprint.IsMember(cell.ParentBlock), point.Name + " lies outside " + range.Name);
                        Assert.NotEqual(WorldBlockWaterStatus.Water, cell.Status.WaterStatus);
                        Assert.False(string.IsNullOrWhiteSpace(point.Name));
                    }
                    Assert.Equal(rangePeaks.Count, rangePeaks.Select(p => p.Footprint.Blocks.Single()).Distinct().Count());
                    Assert.Equal(rangePasses.Count, rangePasses.Select(p => p.Footprint.Blocks.Single()).Distinct().Count());

                    // A peak is the highest point around it, and every pass is lower than the range's highest peak
                    foreach (GeographicFeature peak in rangePeaks)
                    {
                        WorldBlock cell = peak.Footprint.Blocks.Single();
                        foreach (WorldBlock other in range.Footprint.Blocks.SelectMany(b => b.ChildBlocksAsList()))
                        {
                            var a = FeatureGeometry.Coord(cell);
                            var b = FeatureGeometry.Coord(other);
                            if (Math.Abs(a.X - b.X) <= 1 && Math.Abs(a.Y - b.Y) <= 1)
                                Assert.True(other.Status.Elevation <= cell.Status.Elevation, peak.Name + " is not a summit");
                        }
                    }
                    if (rangePeaks.Count > 0)
                    {
                        double top = rangePeaks.Max(p => p.Footprint.Blocks.Single().Status.Elevation.Value);
                        Assert.All(rangePasses, p => Assert.True(p.Footprint.Blocks.Single().Status.Elevation.Value + rules.PeakProminenceFrom <= top + 1e-9));
                    }
                }
            }
            Assert.True(peaks > 0, "no peaks in any test world");
            Assert.True(passes > 0, "no passes in any test world");
        }

        [Fact]
        public void PeakCountsFollowTheSettings()
        {
            World none = NoiseTestWorlds.Generate(1, ("PeaksPerRange", "0"), ("PassesPerRange", "0")).World;
            Assert.DoesNotContain(none.Features, f => f.Type.Id == PeakPassDetector.PeakTypeId || f.Type.Id == PeakPassDetector.PassTypeId);

            World one = NoiseTestWorlds.Generate(1, ("PeaksPerRange", "1"), ("PassesPerRange", "0")).World;
            foreach (GeographicFeature range in Of(one, MountainRangeDetector.TypeId, FeatureScope.Continental))
            {
                GeographicFeature peak = range.Children.SingleOrDefault(c => c.Type.Id == PeakPassDetector.PeakTypeId);
                if (peak == null) continue;
                // The only peak is the range's summit
                double top = range.Footprint.Blocks.SelectMany(b => b.ChildBlocksAsList())
                    .Where(c => c.Status.WaterStatus != WorldBlockWaterStatus.Water).Max(c => c.Status.Elevation.Value);
                Assert.Equal(top, peak.Footprint.Blocks.Single().Status.Elevation.Value);
            }
        }

        // ---- Desert parts ---------------------------------------------------------------------

        [Fact]
        public void DesertPartsLieWithinTheirDesert()
        {
            var types = new HashSet<string>();
            foreach (int seed in Seeds)
            {
                SimEngine engine = NoiseTestWorlds.Generate(seed);
                NoiseGeneratorRules rules = NoiseGeneratorRules.Load(engine.SimData);
                foreach (GeographicFeature desert in Of(engine.World, DesertDetector.TypeId, FeatureScope.Continental))
                {
                    Assert.True(desert.Children.Count(c => c.Type.Id == DesertPartDetector.ErgTypeId) <= rules.ErgsPerDesert);
                    Assert.True(desert.Children.Count(c => c.Type.Id == DesertPartDetector.SaltFlatTypeId) <= rules.SaltFlatsPerDesert);
                    Assert.True(desert.Children.Count(c => c.Type.Id == DesertPartDetector.OasisTypeId) <= rules.OasesPerDesert);
                    foreach (GeographicFeature part in desert.Children)
                    {
                        types.Add(part.Type.Id);
                        Assert.Equal(FeatureScope.Regional, part.Scope);
                        Assert.Equal(FeatureCategory.Desert, part.Type.Category);
                        Assert.Contains(part, engine.World.Features);
                        Assert.Single(part.Footprint.Pieces());
                        Assert.False(string.IsNullOrWhiteSpace(part.Name));
                        foreach (WorldBlock cell in part.Footprint.Blocks)
                        {
                            Assert.True(desert.Contains(cell), part.Name + " lies outside " + desert.Name);
                            Assert.NotEqual(WorldBlockWaterStatus.Water, cell.Status.WaterStatus);
                            Assert.True(cell.Status.Elevation < rules.MountainElevationFrom, part.Name + " lies on a mountain");
                        }
                        if (part.Type.Id == DesertPartDetector.SaltFlatTypeId) Assert.True(part.Footprint.Count >= rules.SaltFlatMinBlocks);
                        if (part.Type.Id == DesertPartDetector.ErgTypeId) Assert.True(part.Footprint.Count >= rules.ErgMinBlocks);
                        if (part.Type.Id == DesertPartDetector.OasisTypeId) Assert.Equal(1, part.Footprint.Count);
                    }

                    // Ergs never cover salt flats
                    var flats = new HashSet<WorldBlock>(desert.Children.Where(c => c.Type.Id == DesertPartDetector.SaltFlatTypeId).SelectMany(c => c.Footprint.Blocks));
                    Assert.All(desert.Children.Where(c => c.Type.Id == DesertPartDetector.ErgTypeId).SelectMany(c => c.Footprint.Blocks), b => Assert.DoesNotContain(b, flats));
                }
            }
            Assert.Equal(new[] { DesertPartDetector.ErgTypeId, DesertPartDetector.OasisTypeId, DesertPartDetector.SaltFlatTypeId }, types.OrderBy(t => t));
        }

        [Fact]
        public void SaltFlatsAndOasesLieInHollows()
        {
            foreach (int seed in Seeds)
            {
                SimEngine engine = NoiseTestWorlds.Generate(seed);
                foreach (GeographicFeature part in engine.World.Features.Where(f => f.Type.Id == DesertPartDetector.SaltFlatTypeId || f.Type.Id == DesertPartDetector.OasisTypeId))
                {
                    // Every cell has a neighbour no lower than it, and the ground around the hollow rises above its floor
                    WorldBlock lowest = part.Footprint.Blocks.OrderBy(b => b.Status.Elevation.Value).First();
                    var at = FeatureGeometry.Coord(lowest);
                    List<WorldBlock> around = part.Parent.Footprint.Blocks.SelectMany(b => b.ChildBlocksAsList())
                        .Where(c => { var p = FeatureGeometry.Coord(c); return Math.Abs(p.X - at.X) <= 1 && Math.Abs(p.Y - at.Y) <= 1 && c != lowest; }).ToList();
                    Assert.All(around, c => Assert.True(c.Status.Elevation >= lowest.Status.Elevation, part.Name + " is not in a hollow"));
                }
            }
        }

        [Fact]
        public void BasinDepthIsHowFarBelowTheSpillLevel()
        {
            // A 5x5 patch: a rim at 0.5 with a notch at 0.3, around a floor at 0.1 and 0.2
            SimEngine engine = new SimEngine(1);
            World world = new World(engine);
            WorldGridBuilder.InitializeWorldBlock(world);
            WorldBlock top = world.WorldBlock.ChildBlocks[0, 0];
            top.InitializeChildBlocks();
            double[,] heights =
            {
                { 0.5, 0.5, 0.5, 0.5, 0.5 },
                { 0.5, 0.1, 0.2, 0.1, 0.5 },
                { 0.5, 0.2, 0.4, 0.2, 0.3 },
                { 0.5, 0.1, 0.2, 0.1, 0.5 },
                { 0.5, 0.5, 0.5, 0.5, 0.5 },
            };
            List<WorldBlock> cells = new List<WorldBlock>();
            for (int y = 0; y < 5; y++)
                for (int x = 0; x < 5; x++)
                {
                    top.ChildBlocks[x, y].Status.Elevation = heights[y, x];
                    cells.Add(top.ChildBlocks[x, y]);
                }

            Dictionary<WorldBlock, double> depth = DesertPartDetector.BasinDepths(cells);
            Assert.Equal(0.0, depth[top.ChildBlocks[0, 0]]);
            Assert.Equal(0.0, depth[top.ChildBlocks[4, 2]]); // the notch is on the edge, where water spills out
            Assert.Equal(0.2, depth[top.ChildBlocks[1, 1]], 9); // fills to the notch at 0.3
            Assert.Equal(0.1, depth[top.ChildBlocks[2, 1]], 9);
            Assert.Equal(0.0, depth[top.ChildBlocks[2, 2]]); // the middle rises above the spill level
        }

        [Fact]
        public void DesertPartsCanBeSwitchedOff()
        {
            SimEngine on = NoiseTestWorlds.Generate(1);
            SimEngine off = NoiseTestWorlds.Generate(1, ("DesertParts", "0"));
            Assert.All(off.World.Features.Where(f => f.Type.Id == DesertDetector.TypeId), d => Assert.Empty(d.Children));
            // The deserts themselves are unchanged
            Assert.Equal(on.World.Features.Where(f => f.Type.Id == DesertDetector.TypeId).Select(f => f.Footprint.Count),
                off.World.Features.Where(f => f.Type.Id == DesertDetector.TypeId).Select(f => f.Footprint.Count));
        }

        [Fact]
        public void MapShowsTheFinestFeature()
        {
            World world = NoiseTestWorlds.Generate(2).World;
            GeographicFeature sub = world.Features.First(f => f.Scope == FeatureScope.Regional);
            WorldBlock cell = sub.Footprint.Blocks[0];
            Assert.Contains(sub.Parent, world.FeaturesAt(cell));
            Assert.Same(sub, Map.FeatureToShow(cell));
            Assert.Equal(sub.DisplayInfo.DisplayColor.ToRgb(), Map.GetColor(cell, MapContext.Features).ToRgb());
        }

        // ---- Geometry -------------------------------------------------------------------------

        [Fact]
        public void CoordsAreGlobalAtEveryDepth()
        {
            World world = NoiseTestWorlds.Generate(1).World;
            WorldBlock top = Top(world, 3, 4);
            Assert.Equal((3, 4), FeatureGeometry.Coord(top));
            Assert.Equal((37, 42), FeatureGeometry.Coord(Children(top)[7, 2]));
            Assert.Equal((372, 429), FeatureGeometry.Coord(Children(Children(top)[7, 2])[2, 9]));
        }

        [Fact]
        public void TouchingGroupsJoinAcrossParentBlocks()
        {
            World world = NoiseTestWorlds.Generate(1).World;
            WorldBlock left = Children(Top(world, 3, 3))[9, 4], right = Children(Top(world, 4, 3))[0, 5], apart = Children(Top(world, 4, 3))[5, 5];
            var groups = FeatureGeometry.TouchingGroups(new[] { left, apart, right });
            Assert.Equal(2, groups.Count);
            Assert.Equal(new[] { left, right }, groups[0]);
            Assert.Equal(new[] { apart }, groups[1]);
        }
    }
}
