using System;
using System.Collections.Generic;
using System.Linq;
using LoreGen.Features;
using LoreGen.Simulation;
using LoreGen.WorldGen;
using LoreGen.WorldGen.Generators;
using Xunit;

namespace LoreGen.Tests.Features
{
    /// <summary>
    /// Rain shadows, the hydrology (drainage, lakes and river courses), and the water features made from it: lakes, large
    /// rivers, and oceans and seas with their bays and straits.
    /// </summary>
    public class WaterFeatureTests
    {
        private static readonly int[] Seeds = { 1, 2, 3, 4, 5, 6 };

        // ---- Rain shadow ----------------------------------------------------------------------

        [Fact]
        public void WindsBlowFromTheEastInTheTropicsAndFromTheWestInMidLatitudes()
        {
            const int height = 90; // one row per degree from pole to pole... twice over
            Assert.Equal(-1, ClimateModel.WindTowardX(height / 2, height));      // equator
            Assert.Equal(1, ClimateModel.WindTowardX(height / 4, height));       // 45 degrees north
            Assert.Equal(1, ClimateModel.WindTowardX(3 * height / 4, height));   // 45 degrees south
            Assert.Equal(-1, ClimateModel.WindTowardX(0, height));               // pole
        }

        [Fact]
        public void HighGroundCastsARainShadowDownwind()
        {
            // A strip of land along the equator, with a ridge in the middle and the sea at both ends
            const int width = 30, height = 9, row = 4;
            var land = new bool[width, height];
            var elevation = new double[width, height];
            for (int x = 1; x < width - 1; x++)
            {
                land[x, row] = true;
                elevation[x, row] = x == 15 ? 0.9 : 0.1;
            }
            double[,] shadow = ClimateModel.RainShadow(land, elevation, 6);

            // At the equator the wind comes from the east (+x), so the shadow falls to the west of the ridge
            Assert.True(shadow[12, row] > 0.5);
            Assert.Equal(0, shadow[18, row]);
            Assert.Equal(0, shadow[15, row]);
            Assert.Equal(0, shadow[5, row]); // beyond the shadow's reach
        }

        // ---- Hydrology on made-up land --------------------------------------------------------

        // Land sloping down to the sea on the west, with a valley along the middle row
        private static (World World, HydrologyModel Model, bool[,] Land) Valley(double moisture, Action<NoiseGeneratorRules> tune = null)
        {
            var engine = new SimEngine(1);
            var world = new World(engine);
            WorldGridBuilder.InitializeWorldBlock(world);
            WorldBlock[,] grid = world.WorldBlock.ChildBlocks;
            int width = grid.GetLength(0), height = grid.GetLength(1), middle = height / 2;
            var land = new bool[width, height];
            var elevation = new double[width, height];
            var wet = new double[width, height];
            var warmth = new double[width, height];
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    land[x, y] = x >= 3 && y >= 2 && y < height - 2 && x < width - 2;
                    elevation[x, y] = land[x, y] ? 0.02 * x + 0.03 * Math.Abs(y - middle) : -0.5;
                    wet[x, y] = moisture;
                    warmth[x, y] = 0.5;
                }
            NoiseGeneratorRules rules = NoiseGeneratorRules.Load(engine.SimData);
            tune?.Invoke(rules);
            var model = HydrologyModel.Build(rules, grid, land, elevation, wet, warmth, 1234, engine.SimData.FeatureType(WaterFeatureDetector.RiverTypeId));
            return (world, model, land);
        }

        [Fact]
        public void AllLandDrainsToTheSeaAndNoWaterIsLost()
        {
            var (_, model, land) = Valley(0.8);
            double runoff = 0, reachingSea = 0;
            for (int x = 0; x < model.Width; x++)
            {
                for (int y = 0; y < model.Height; y++)
                {
                    if (!land[x, y]) continue;
                    runoff += model.Runoff[x, y];
                    // Following the drainage always ends
                    (int X, int Y) c = (x, y);
                    int steps = 0;
                    while (model.Receiver[c.X, c.Y].X >= 0 && steps++ < model.Width * model.Height) c = model.Receiver[c.X, c.Y];
                    Assert.True(steps < model.Width * model.Height, "drainage goes round in circles");
                    if (model.Receiver[x, y].X < 0) reachingSea += model.Flow[x, y];
                }
            }
            Assert.Equal(runoff, reachingSea, 6);
        }

        [Fact]
        public void TheValleyCarriesARiverToTheSea()
        {
            var (_, model, _) = Valley(0.8, r => r.RiverFlowFrom = 2);
            Assert.NotEmpty(model.Rivers);
            RiverCourse main = model.Rivers.OrderByDescending(r => r.MouthFlow).First();
            Assert.Equal(RiverEnd.Sea, main.End);
            Assert.True(main.MouthFlow >= main.SourceFlow);

            foreach (RiverCourse river in model.Rivers)
            {
                for (int i = 1; i < river.Blocks.Count; i++)
                    Assert.True(Math.Max(Math.Abs(river.Blocks[i].X - river.Blocks[i - 1].X), Math.Abs(river.Blocks[i].Y - river.Blocks[i - 1].Y)) == 1, "river blocks do not follow on");
                for (int i = 1; i < river.Cells.Count; i++)
                    Assert.Equal(1, Math.Abs(river.Cells[i].X - river.Cells[i - 1].X) + Math.Abs(river.Cells[i].Y - river.Cells[i - 1].Y));
                for (int i = 1; i < river.Along.Count; i++)
                    Assert.True(river.Along[i] >= river.Along[i - 1]);
                Assert.All(river.Blocks, b => Assert.True(model.DetailBlocks[b.X, b.Y], "a river block gets no sub-blocks"));

                // A river that joins another meets it exactly, at a point on the other's course
                if (river.End == RiverEnd.River)
                {
                    Assert.NotNull(river.Joins);
                    Assert.Contains(river.Path[river.Path.Count - 1], river.Joins.Path);
                }
            }
        }

        [Fact]
        public void RiverCoursesAreFixedByTheWorldSeed()
        {
            var first = Valley(0.8, r => r.RiverFlowFrom = 2).Model;
            var second = Valley(0.8, r => r.RiverFlowFrom = 2).Model;
            Assert.Equal(first.Rivers.Count, second.Rivers.Count);
            for (int i = 0; i < first.Rivers.Count; i++)
                Assert.Equal(first.Rivers[i].Path, second.Rivers[i].Path);
        }

        // A dome of land in a sea, with a pit in its middle
        private static HydrologyModel Pit(double moisture)
        {
            var engine = new SimEngine(1);
            var world = new World(engine);
            WorldGridBuilder.InitializeWorldBlock(world);
            WorldBlock[,] grid = world.WorldBlock.ChildBlocks;
            int width = grid.GetLength(0), height = grid.GetLength(1);
            int cx = width / 2, cy = height / 2;
            var land = new bool[width, height];
            var elevation = new double[width, height];
            var wet = new double[width, height];
            var warmth = new double[width, height];
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    int toSea = Math.Min(Math.Min(x, y), Math.Min(width - 1 - x, height - 1 - y));
                    land[x, y] = toSea >= 2;
                    elevation[x, y] = land[x, y] ? 0.05 + 0.03 * toSea : -0.5;
                    wet[x, y] = moisture;
                    warmth[x, y] = 0.5;
                }
            elevation[cx, cy] = 0.1;
            NoiseGeneratorRules rules = NoiseGeneratorRules.Load(engine.SimData);
            return HydrologyModel.Build(rules, grid, land, elevation, wet, warmth, 1, engine.SimData.FeatureType(WaterFeatureDetector.RiverTypeId));
        }

        [Fact]
        public void APitHoldsALakeThatOverflowsOnlyWhenWellFed()
        {
            LakeBasin wet = Pit(1.0).Basins.Single();
            Assert.True(wet.HasLake);
            Assert.True(wet.Open);

            LakeBasin dry = Pit(0.25).Basins.Single();
            Assert.True(dry.HasLake);
            Assert.False(dry.Open); // a salt lake
            Assert.True(dry.TargetCells < wet.TargetCells);

            Assert.False(Pit(0.02).Basins.Single().HasLake);
        }

        [Fact]
        public void HydrologyCanBeSwitchedOff()
        {
            World world = NoiseTestWorlds.Generate(1, ("Hydrology", "0")).World;
            Assert.DoesNotContain(world.Features, f => f.Type.Id == WaterFeatureDetector.RiverTypeId
                || f.Type.Id == WaterFeatureDetector.LakeTypeId || f.Type.Id == WaterFeatureDetector.SaltLakeTypeId);
        }

        // ---- Lakes and rivers in generated worlds ---------------------------------------------

        [Fact]
        public void LakesAreWaterThatBelongsToNoOceanOrSea()
        {
            int lakes = 0;
            foreach (int seed in Seeds)
            {
                SimEngine engine = NoiseTestWorlds.Generate(seed);
                NoiseGeneratorRules rules = NoiseGeneratorRules.Load(engine.SimData);
                foreach (GeographicFeature lake in engine.World.Features.Where(f => f.Type.Id == WaterFeatureDetector.LakeTypeId || f.Type.Id == WaterFeatureDetector.SaltLakeTypeId))
                {
                    lakes++;
                    Assert.Equal(FeatureScope.Regional, lake.Scope);
                    Assert.Null(lake.Parent);
                    Assert.True(lake.Footprint.Count >= rules.LakeMinBlocks);
                    Assert.Single(lake.Footprint.Pieces());
                    Assert.False(string.IsNullOrWhiteSpace(lake.Name));
                    foreach (WorldBlock cell in lake.Footprint.Blocks)
                    {
                        Assert.Equal(WorldBlockWaterStatus.Water, cell.Status.WaterStatus);
                        Assert.Same(lake, cell.Status.Lake);
                        Assert.Null(cell.Status.Ocean);
                        Assert.Null(cell.Status.Sea);
                        Assert.NotEqual(WorldBlockWaterStatus.Water, cell.ParentBlock.Status.WaterStatus); // inland
                    }
                }
            }
            Assert.True(lakes > 0, "no lakes in any test world");
        }

        [Fact]
        public void RiversRunOverLandAndNarrowBelowTheirOwnDepth()
        {
            int rivers = 0;
            foreach (int seed in Seeds.Take(3))
            {
                World world = NoiseTestWorlds.Generate(seed).World;
                foreach (GeographicFeature river in world.Features.Where(f => f.Type.Id == WaterFeatureDetector.RiverTypeId))
                {
                    rivers++;
                    Assert.Equal(FeatureScope.Regional, river.Scope);
                    Assert.Equal(FeatureShape.Linear, river.Type.Shape);
                    Assert.Equal(river.Footprint.Blocks, river.Spine);
                    Assert.IsType<RiverRefiner>(river.Refiner);
                    Assert.All(river.Footprint.Blocks, c => Assert.NotEqual(WorldBlockWaterStatus.Water, c.Status.WaterStatus));

                    // Most steps go to a sub-block beside the last (the exceptions cross a lake)
                    int touching = 0;
                    for (int i = 1; i < river.Spine.Count; i++)
                    {
                        var a = FeatureGeometry.Coord(river.Spine[i - 1]);
                        var b = FeatureGeometry.Coord(river.Spine[i]);
                        if (Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) == 1) touching++;
                    }
                    Assert.True(touching >= (river.Spine.Count - 1) * 0.9, river.Name + " is broken up");

                    // One level down, the river is a narrow ribbon through each of its sub-blocks
                    foreach (WorldBlock cell in river.Footprint.Blocks.Take(12))
                    {
                        if (cell.ChildBlocks == null) cell.InitializeChildBlocks();
                        int inside = cell.ChildBlocksAsList().Count(river.Contains);
                        Assert.InRange(inside, 1, 40);
                    }
                }
            }
            Assert.True(rivers > 0, "no rivers in any test world");
        }

        [Fact]
        public void RiversAreDrawnOnTheMap()
        {
            World world = NoiseTestWorlds.Generate(1).World;
            GeographicFeature river = world.Features.First(f => f.Type.Id == WaterFeatureDetector.RiverTypeId);
            Assert.True(LoreGen.WorldDisplay.Map.IsRiver(river.Footprint.Blocks[0]));
            Assert.False(LoreGen.WorldDisplay.Map.IsRiver(river.Footprint.Blocks[0].ParentBlock)); // too narrow to show at the top level
        }

        // ---- Oceans and seas, bays and straits ------------------------------------------------

        [Fact]
        public void OceansAndSeasAreFeatures()
        {
            foreach (int seed in Seeds.Take(3))
            {
                World world = NoiseTestWorlds.Generate(seed).World;
                foreach (Ocean ocean in world.Oceans)
                {
                    Assert.NotNull(ocean.Feature);
                    Assert.Equal(ocean.Name, ocean.Feature.Name);
                    Assert.Equal(WaterFeatureDetector.OceanTypeId, ocean.Feature.Type.Id);
                    Assert.Equal(ocean.OceanArea.Blocks().OrderBy(b => b.Y).ThenBy(b => b.X), ocean.Feature.Footprint.Blocks.OrderBy(b => b.Y).ThenBy(b => b.X));
                }
                foreach (Sea sea in world.Continents.SelectMany(c => c.Seas))
                {
                    Assert.NotNull(sea.Feature);
                    Assert.Equal(sea.Name, sea.Feature.Name);
                    Assert.Equal(WaterFeatureDetector.SeaTypeId, sea.Feature.Type.Id);
                }
            }
        }

        [Fact]
        public void BaysAndStraitsLieInTheirOceanOrSea()
        {
            var types = new HashSet<string>();
            foreach (int seed in Seeds)
            {
                World world = NoiseTestWorlds.Generate(seed).World;
                foreach (GeographicFeature part in world.Features.Where(f => f.Type.Id == WaterFeatureDetector.BayTypeId || f.Type.Id == WaterFeatureDetector.StraitTypeId))
                {
                    types.Add(part.Type.Id);
                    Assert.NotNull(part.Parent);
                    Assert.Contains(part.Parent.Type.Id, new[] { WaterFeatureDetector.OceanTypeId, WaterFeatureDetector.SeaTypeId });
                    Assert.Single(part.Footprint.Pieces());
                    foreach (WorldBlock cell in part.Footprint.Blocks)
                    {
                        Assert.Equal(WorldBlockWaterStatus.Water, cell.Status.WaterStatus);
                        Assert.True(part.Parent.Footprint.IsMember(cell.ParentBlock));
                        Assert.True(part.Parent.Contains(cell), part.Name + " lies outside " + part.Parent.Name);
                    }
                    if (part.Type.Shape == FeatureShape.Linear) Assert.NotEmpty(part.Spine);
                }
            }
            Assert.Equal(new[] { WaterFeatureDetector.BayTypeId, WaterFeatureDetector.StraitTypeId }, types.OrderBy(t => t));
        }

        [Fact]
        public void WaterFeaturesCanBeSwitchedOff()
        {
            World world = NoiseTestWorlds.Generate(1, ("WaterFeatures", "0")).World;
            Assert.All(world.Oceans, o => Assert.Null(o.Feature));
            Assert.DoesNotContain(world.Features, f => f.Type.Id == WaterFeatureDetector.OceanTypeId || f.Type.Id == WaterFeatureDetector.BayTypeId);
        }
    }
}
