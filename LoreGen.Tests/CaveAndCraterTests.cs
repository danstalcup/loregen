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
    /// Cave systems and caves, craters and crater lakes: all found while zooming in.
    /// </summary>
    public class CaveAndCraterTests
    {
        private static World Generate(int seed, string generator = TectonicWorldGenerator.GeneratorId, Action<SimEngine> configure = null)
        {
            var engine = new SimEngine(seed);
            engine.Configuration.GeneratorId = generator;
            configure?.Invoke(engine);
            engine.GenerateWorld();
            return engine.World;
        }

        // Zooms into every 100 km block of a world's mountain ranges, then into the cave systems found
        private static void ZoomMountains(World world, int ranges = 3)
        {
            foreach (var range in world.Features.Where(f => f.Type.Id == MountainRangeDetector.TypeId).Take(ranges).ToList())
                foreach (var top in range.Footprint.Blocks)
                {
                    world.EnsureChildren(top);
                    foreach (var block in top.ChildBlocks) if (range.Contains(block)) world.EnsureChildren(block);
                }
            foreach (var system in world.Features.Where(f => f.Type.Id == "CaveSystem").ToList())
                foreach (var block in system.Footprint.Blocks) world.EnsureChildren(block);
        }

        // Zooms into the 100 km land blocks of the first few 1000 km land blocks
        private static void ZoomLand(World world, int tops)
        {
            foreach (var top in world.WorldBlock.ChildBlocks.Cast<WorldBlock>().Where(b => b.Status.WaterStatus != WorldBlockWaterStatus.Water).Take(tops).ToList())
            {
                world.EnsureChildren(top);
                foreach (var block in top.ChildBlocks) if (block.Status.WaterStatus != WorldBlockWaterStatus.Water) world.EnsureChildren(block);
            }
        }

        [Fact]
        public void CaveSystemsLieUnderRuggedMountainGroundWithAFewWaysIn()
        {
            var world = Generate(5);
            ZoomMountains(world);
            var systems = world.Features.Where(f => f.Type.Id == "CaveSystem").ToList();
            Assert.NotEmpty(systems);
            Assert.All(systems, s =>
            {
                Assert.Equal(MountainRangeDetector.TypeId, s.Parent?.Type.Id);
                Assert.InRange(s.Footprint.Count, 3, 12);
                Assert.Single(s.Footprint.Blocks.Select(b => b.ParentBlock).Distinct());
                Assert.InRange(s.Children.Count, 0, 2);
            });
            var caves = world.Features.Where(f => f.Type.Id == "Cave").ToList();
            Assert.NotEmpty(caves);
            Assert.All(caves, c =>
            {
                Assert.Equal("CaveSystem", c.Parent?.Type.Id);
                Assert.Single(c.Spine);
                Assert.True(c.Spine[0].Status.Terrain.TerrainType.Relief >= 2);
            });
        }

        [Fact]
        public void CaveSystemsLeaveTheTerrainAlone()
        {
            Assert.Null(new TerrainModelProbe().Influence("CaveSystem"));
            Assert.Null(new TerrainModelProbe().Influence("Cave"));
        }

        private sealed class TerrainModelProbe
        {
            private readonly TerrainModel model;
            public TerrainModelProbe()
            {
                var engine = new SimEngine(1);
                model = new TerrainModel(engine, NoiseGeneratorRules.Load(engine.SimData));
            }
            public TerrainInfluence Influence(string id) => model.InfluenceOf(id);
        }

        [Fact]
        public void CratersAreRareRoundAndOnLand()
        {
            var world = Generate(8);
            ZoomLand(world, 25);
            int zoomed = world.WorldBlock.ChildBlocks.Cast<WorldBlock>().Where(t => t.ChildBlocks != null)
                .SelectMany(t => t.ChildBlocks.Cast<WorldBlock>()).Count(b => b.ChildBlocks != null && b.Status.WaterStatus != WorldBlockWaterStatus.Water);
            var craters = world.Features.Where(f => f.Type.Id == "Crater").ToList();
            Assert.NotEmpty(craters);
            Assert.True(craters.Count < 0.05 * zoomed, $"{craters.Count} craters in {zoomed} blocks");
            Assert.All(craters, c =>
            {
                Assert.True(c.Parent == null);
                Assert.Single(c.Footprint.Blocks.Select(b => b.ParentBlock).Distinct());
                // Round: no block lies beyond the radius from the centre
                var circle = Assert.IsType<CircleRefiner>(Assert.IsType<InsideRefiner>(c.Refiner).Inner);
                Assert.All(c.Footprint.Blocks, b =>
                {
                    double dx = b.TrueX + b.TrueWidth / 2.0 - circle.CentreX, dy = b.TrueY + b.TrueHeight / 2.0 - circle.CentreY;
                    Assert.True(Math.Sqrt(dx * dx + dy * dy) <= circle.Radius + 1e-6);
                });
            });
        }

        [Fact]
        public void CraterLakesAreWaterInsideTheirCraters()
        {
            var lakes = new List<GeographicFeature>();
            foreach (int seed in new[] { 5, 8 })
            {
                var world = Generate(seed, configure: e => e.Configuration.GeneratorSettings["CraterChance"] = 0.2);
                ZoomLand(world, 12);
                lakes.AddRange(world.Features.Where(f => f.Type.Id == "CraterLake"));
                foreach (var lake in world.Features.Where(f => f.Type.Id == "CraterLake"))
                {
                    var crater = world.Features.First(f => f.Type.Id == "Crater" && f.Footprint.Blocks.Contains(lake.Footprint.Blocks[0]));
                    Assert.All(lake.Footprint.Blocks, b =>
                    {
                        Assert.Equal(WorldBlockWaterStatus.Water, b.Status.WaterStatus);
                        Assert.Same(lake, b.Status.Lake);
                        Assert.True(b.Status.Elevation < 0);
                        Assert.Null(b.Status.Region);
                        // Never on the outer row of its 100 km block
                        Assert.InRange(b.X, 1, 8);
                        Assert.InRange(b.Y, 1, 8);
                        Assert.Contains(b, crater.Footprint.Blocks);
                    });
                    // The shore around it is coastline
                    foreach (var b in lake.Footprint.Blocks)
                        foreach (var n in b.SurroundingBlocksAsList().Where(n => n.Status.WaterStatus != WorldBlockWaterStatus.Water))
                            Assert.Equal(WorldBlockWaterStatus.Coastline, n.Status.WaterStatus);
                }
            }
            Assert.NotEmpty(lakes);
        }

        [Fact]
        public void CraterFloorOnlyLiesInCraters()
        {
            var world = Generate(8, configure: e => e.Configuration.GeneratorSettings["CraterChance"] = 0.2);
            ZoomLand(world, 8);
            int floor = 0;
            foreach (var block in world.WorldBlock.ChildBlocksAsList(true))
            {
                if (block.Status.Terrain?.TerrainType.Name != "Crater Floor") continue;
                floor++;
                Assert.Contains(world.FeaturesAt(block), f => f.Type.Id == "Crater");
            }
            Assert.True(floor > 0);
        }

        [Fact]
        public void CavesAndCratersCanBeSwitchedOff()
        {
            var world = Generate(5, configure: e =>
            {
                e.Configuration.GeneratorSettings["CaveChance"] = 0;
                e.Configuration.GeneratorSettings["CraterChance"] = 0;
            });
            ZoomMountains(world, 2);
            ZoomLand(world, 5);
            Assert.DoesNotContain(world.Features, f => f.Type.Category == FeatureCategory.Cave || f.Type.Category == FeatureCategory.Impact);
        }

        [Fact]
        public void CratersDoNotDependOnTheOrderOfZooming()
        {
            string Describe(World w) => string.Join("\n", w.Features.Where(f => f.Type.Id == "Crater" || f.Type.Id == "CraterLake")
                .Select(f => $"{f.Type.Id} {f.Name} {string.Join(" ", f.Footprint.Blocks.Select(b => b.TrueX + "," + b.TrueY))}").OrderBy(s => s, StringComparer.Ordinal));
            var a = Generate(8, configure: e => e.Configuration.GeneratorSettings["CraterChance"] = 0.2);
            var b = Generate(8, configure: e => e.Configuration.GeneratorSettings["CraterChance"] = 0.2);
            var tops = a.WorldBlock.ChildBlocks.Cast<WorldBlock>().Where(t => t.Status.WaterStatus != WorldBlockWaterStatus.Water).Take(3).Select(t => (t.X, t.Y)).ToList();
            foreach (var (x, y) in tops)
            {
                var t = a.WorldBlock.ChildBlocks[x, y];
                a.EnsureChildren(t);
                foreach (var c in t.ChildBlocks) a.EnsureChildren(c);
            }
            foreach (var (x, y) in Enumerable.Reverse(tops))
            {
                var t = b.WorldBlock.ChildBlocks[x, y];
                b.EnsureChildren(t);
                foreach (var c in t.ChildBlocks.Cast<WorldBlock>().Reverse()) b.EnsureChildren(c);
            }
            Assert.Equal(Describe(a), Describe(b));
        }
    }
}
