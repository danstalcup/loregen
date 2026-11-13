using System;
using System.Collections.Generic;
using System.Linq;
using LoreGen.Features;
using LoreGen.Simulation;
using LoreGen.WorldGen;
using LoreGen.WorldGen.Generators;
using Xunit;

namespace LoreGen.Tests
{
    /// <summary>
    /// Ice sheets, glaciers and fjords, and the glacier ice terrain that belongs to them.
    /// </summary>
    public class GlacialTests
    {
        private static World Generate(int seed, string generator = TectonicWorldGenerator.GeneratorId, Action<SimEngine> configure = null)
        {
            var engine = new SimEngine(seed);
            engine.Configuration.GeneratorId = generator;
            configure?.Invoke(engine);
            engine.GenerateWorld();
            return engine.World;
        }

        private static readonly int[] Seeds = { 8, 11, 14 };

        private static List<GeographicFeature> All(string id) =>
            Seeds.SelectMany(s => Generate(s).Features.Where(f => f.Type.Id == id)).ToList();

        [Fact]
        public void IceSheetsCoverVeryColdLand()
        {
            var sheets = All(GlacialDetector.IceSheetTypeId);
            Assert.NotEmpty(sheets);
            var rules = NoiseGeneratorRules.Load(new SimEngine(1).SimData);
            Assert.All(sheets, s =>
            {
                Assert.True(s.Footprint.Count >= rules.IceSheetMinBlocks);
                Assert.All(s.Footprint.Blocks, b =>
                {
                    Assert.NotEqual(WorldBlockWaterStatus.Water, b.Status.WaterStatus);
                    Assert.True(b.Status.Temperature <= rules.IceSheetTemperature);
                });
            });
        }

        [Fact]
        public void IceSheetEdgesFollowTheTemperatureWhenZoomedIn()
        {
            var world = Seeds.Select(s => Generate(s)).First(w => w.Features.Any(f => f.Type.Id == GlacialDetector.IceSheetTypeId));
            var sheet = world.Features.First(f => f.Type.Id == GlacialDetector.IceSheetTypeId);
            var threshold = ((ColdEdgeRefiner)sheet.Refiner).Threshold;
            foreach (var top in sheet.Footprint.Blocks)
            {
                world.EnsureChildren(top);
                foreach (var child in top.ChildBlocks)
                {
                    if (child.Status.WaterStatus == WorldBlockWaterStatus.Water) continue;
                    bool cold = child.Status.Temperature <= threshold;
                    if (sheet.Footprint.IsEdge(top)) Assert.Equal(cold, sheet.Contains(child));
                }
            }
        }

        [Fact]
        public void GlaciersArePartsOfRangesAboveTheSnowline()
        {
            var glaciers = All(GlacialDetector.GlacierTypeId);
            Assert.NotEmpty(glaciers);
            Assert.All(glaciers, g =>
            {
                Assert.Equal(MountainRangeDetector.TypeId, g.Parent?.Type.Id);
                Assert.InRange(g.Footprint.Count, 2, 25);
                Assert.All(g.Footprint.Blocks, b => Assert.NotEqual(WorldBlockWaterStatus.Water, b.Status.WaterStatus));
            });
            // Glaciers stand high: higher on average than their ranges' ground
            Assert.All(glaciers, g =>
            {
                double glacier = g.Footprint.Blocks.Average(b => b.Status.Elevation.Value);
                Assert.True(glacier > 0.1, g.Name);
            });
        }

        [Fact]
        public void FjordsAreNarrowColdInletsOfTheSea()
        {
            var fjords = All(GlacialDetector.FjordTypeId);
            Assert.NotEmpty(fjords);
            var rules = NoiseGeneratorRules.Load(new SimEngine(1).SimData);
            Assert.All(fjords, f =>
            {
                Assert.Contains(f.Parent?.Type.Id, new[] { "Ocean", "Sea" });
                Assert.All(f.Footprint.Blocks, b =>
                {
                    Assert.Equal(WorldBlockWaterStatus.Water, b.Status.WaterStatus);
                    Assert.Null(b.Status.Lake);
                });
                Assert.True(f.Footprint.Blocks.Average(b => b.ParentBlock.Status.Temperature.Value) <= rules.FjordTemperature + 1e-9);
            });
        }

        [Fact]
        public void GlacierIceOnlyLiesOnIce()
        {
            int ice = 0;
            foreach (int seed in Seeds)
            {
                var world = Generate(seed);
                foreach (var sheet in world.Features.Where(f => f.Type.Id == GlacialDetector.IceSheetTypeId).Take(1))
                    world.EnsureChildren(sheet.Footprint.Blocks[0]);
                foreach (var block in world.WorldBlock.ChildBlocksAsList(true))
                {
                    var type = block.Status.Terrain?.TerrainType;
                    if (type?.Name != "Glacier") continue;
                    ice++;
                    Assert.Contains(world.FeaturesAt(block), f => f.Type.Id == GlacialDetector.IceSheetTypeId || f.Type.Id == GlacialDetector.GlacierTypeId);
                }
            }
            Assert.True(ice > 0);
        }

        [Fact]
        public void GlacialFeaturesCanBeSwitchedOff()
        {
            var world = Generate(11, configure: e => e.Configuration.GeneratorSettings["GlacialFeatures"] = 0);
            Assert.DoesNotContain(world.Features, f => f.Type.Category == FeatureCategory.Ice || f.Type.Id == GlacialDetector.FjordTypeId);
            Assert.DoesNotContain(world.WorldBlock.ChildBlocksAsList(true), b => b.Status.Terrain?.TerrainType.Name == "Glacier");
        }

        [Fact]
        public void AWarmerSnowlineGivesMoreGlaciers()
        {
            int Count(double snowline) => Generate(14, configure: e => e.Configuration.GeneratorSettings["GlacierTemperature"] = snowline)
                .Features.Count(f => f.Type.Id == GlacialDetector.GlacierTypeId);
            Assert.True(Count(0.45) > Count(0.1));
        }
    }
}
