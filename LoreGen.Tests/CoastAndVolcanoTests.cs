using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LoreGen.Features;
using LoreGen.Simulation;
using LoreGen.WorldGen;
using LoreGen.WorldGen.Generators;
using Xunit;

namespace LoreGen.Tests
{
    /// <summary>
    /// Coastal features (peninsulas, deltas, beaches, cliffs, lagoons), volcanic ones (volcanoes, calderas), and terrain
    /// that belongs to features (beaches, lava fields).
    /// </summary>
    public class CoastAndVolcanoTests
    {
        private static World Generate(int seed, string generator = TectonicWorldGenerator.GeneratorId, Action<SimEngine> configure = null)
        {
            var engine = new SimEngine(seed);
            engine.Configuration.GeneratorId = generator;
            configure?.Invoke(engine);
            engine.GenerateWorld();
            return engine.World;
        }

        private static IEnumerable<GeographicFeature> OfType(World world, string id) => world.Features.Where(f => f.Type.Id == id);

        // Zooms into the 100 km blocks of the first few coastal 1000 km blocks
        private static void ZoomCoasts(World world, int count = 3)
        {
            foreach (var top in world.WorldBlock.ChildBlocks.Cast<WorldBlock>()
                .Where(b => b.ChildBlocks != null && b.Status.WaterStatus == WorldBlockWaterStatus.Coastline).OrderBy(b => b.Y).ThenBy(b => b.X).Take(count).ToList())
                foreach (var block in top.ChildBlocks) world.EnsureChildren(block);
        }

        [Fact]
        public void PeninsulasReachOutIntoTheSea()
        {
            var world = Generate(5);
            var peninsulas = OfType(world, "Peninsula").ToList();
            Assert.NotEmpty(peninsulas);
            foreach (var peninsula in peninsulas)
            {
                Assert.True(peninsula.Footprint.Count >= 25);
                Assert.All(peninsula.Footprint.Blocks, b => Assert.NotEqual(WorldBlockWaterStatus.Water, b.Status.WaterStatus));
                // Much of a peninsula lies near the sea
                int nearSea = peninsula.Footprint.Blocks.Count(b => b.SurroundingBlocksAsList().Any(n => n.Status.WaterStatus == WorldBlockWaterStatus.Water)
                    || b.Status.WaterStatus == WorldBlockWaterStatus.Coastline);
                Assert.True(nearSea >= peninsula.Footprint.Count / 4, peninsula.Name);
            }
        }

        [Fact]
        public void DeltasLieAtRiverMouthsOnLowLand()
        {
            var deltas = new[] { 5, 8, 11 }.SelectMany(s => OfType(Generate(s), "Delta")).ToList();
            Assert.NotEmpty(deltas);
            Assert.All(deltas, d =>
            {
                Assert.True(d.Footprint.Count >= 4);
                Assert.All(d.Footprint.Blocks, b => Assert.True(b.Status.Elevation <= 0.06));
                Assert.Contains(d.Footprint.Blocks, b => b.World.FeaturesAt(b).Any(f => f.Type.Id == WaterFeatureDetector.RiverTypeId));
            });
        }

        [Fact]
        public void VolcanoesArePartsOfRangesWhereOceanFloorDivesUnderAPlate()
        {
            var volcanoes = new[] { 5, 11 }.SelectMany(s => OfType(Generate(s), "Volcano")).ToList();
            Assert.NotEmpty(volcanoes);
            Assert.All(volcanoes, v =>
            {
                Assert.Equal(MountainRangeDetector.TypeId, v.Parent?.Type.Id);
                Assert.Single(v.Spine);
                // On or beside a converging boundary
                var top = v.Spine[0].AncestorAt(1);
                var grid = v.World.WorldBlock.ChildBlocks;
                bool converging = false;
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int x = top.X + dx, y = top.Y + dy;
                        if (x < 0 || y < 0 || x >= grid.GetLength(0) || y >= grid.GetLength(1)) continue;
                        if (grid[x, y].Status.Plate?.BoundaryAt(grid[x, y]) == PlateBoundaryKind.Convergent) converging = true;
                    }
                Assert.True(converging, v.Name);
            });
        }

        [Fact]
        public void OnlyTectonicWorldsHaveVolcanoes()
        {
            Assert.Empty(OfType(Generate(3, NoiseWorldGenerator.GeneratorId), "Volcano"));
            Assert.Empty(OfType(Generate(5, configure: e => e.Configuration.GeneratorSettings["VolcanoesPerRange"] = 0), "Volcano"));
        }

        [Fact]
        public void ZoomingInFindsBeachesCliffsLagoonsAndCalderas()
        {
            var found = new List<GeographicFeature>();
            foreach (var (seed, generator) in new[] { (5, TectonicWorldGenerator.GeneratorId), (3, NoiseWorldGenerator.GeneratorId), (11, TectonicWorldGenerator.GeneratorId) })
            {
                var world = Generate(seed, generator);
                int before = world.Features.Count;
                ZoomCoasts(world, 4);
                foreach (var volcano in OfType(world, "Volcano").ToList()) world.EnsureChildren(volcano.Spine[0]);
                found.AddRange(world.Features.Skip(before));
            }
            foreach (string id in new[] { "Beach", "Cliffs", "Lagoon", "Caldera" })
                Assert.Contains(found, f => f.Type.Id == id);
            Assert.All(found.Where(f => f.Type.Id == "Beach" || f.Type.Id == "Cliffs"), f =>
                Assert.All(f.Footprint.Blocks, b => Assert.Equal(WorldBlockWaterStatus.Coastline, b.Status.WaterStatus)));
            Assert.All(found.Where(f => f.Type.Id == "Lagoon"), f =>
                Assert.All(f.Footprint.Blocks, b => Assert.Equal(WorldBlockWaterStatus.Water, b.Status.WaterStatus)));
            Assert.All(found.Where(f => f.Type.Id == "Caldera"), f => Assert.Equal("Volcano", f.Parent?.Type.Id));
        }

        [Fact]
        public void TerrainThatBelongsToFeaturesStaysOnThem()
        {
            // Beaches only on beaches, lava fields only on volcanoes and calderas, at every level made
            int beach = 0, lava = 0;
            foreach (int seed in new[] { 5, 11 })
            {
                var world = Generate(seed);
                ZoomCoasts(world, 3);
                foreach (var volcano in OfType(world, "Volcano").ToList()) world.EnsureChildren(volcano.Spine[0]);
                foreach (var block in world.WorldBlock.ChildBlocksAsList(true))
                {
                    var type = block.Status.Terrain?.TerrainType;
                    if (type == null || type.RequiresFeature.Count == 0) continue;
                    if (type.Name == "Beach") beach++; else lava++;
                    Assert.Contains(world.FeaturesAt(block), f => type.RequiresFeature.Contains(f.Type.Id));
                }
            }
            Assert.True(beach > 0 && lava > 0, $"beach {beach}, lava {lava}");
        }

        [Fact]
        public void OnlyTheNewTerrainTypesBelongToFeatures()
        {
            // The original terrain types go anywhere; only the newer ones are tied to features
            var data = new SimEngine(1).SimData;
            Assert.All(data.TerrainTypes.Where(t => t.RequiresFeature.Count > 0), t => Assert.True(t.ID > 10));
            Assert.All(data.TerrainTypes.Where(t => t.ID <= 10), t => Assert.Empty(t.RequiresFeature));
        }

        [Fact]
        public void TerrainTypesMustBelongToKnownFeatures()
        {
            var engine = new SimEngine(1);
            engine.Configuration.GeneratorId = TectonicWorldGenerator.GeneratorId;
            engine.SimData.TerrainTypes.First(t => t.Name == "Beach").RequiresFeature = new[] { "Sandbar" };
            var ex = Assert.ThrowsAny<InvalidDataException>(() => engine.GenerateWorld());
            Assert.Contains("Sandbar", ex.Message);
        }

        [Fact]
        public void NewTerrainHasItsOwnColour()
        {
            var data = new SimEngine(1).SimData;
            var sandy = data.Terrains.First(t => t.TerrainType.Name == "Beach");
            var flats = data.Terrains.First(t => t.TerrainType.Name == "Flats" && t.Climate == sandy.Climate);
            Assert.NotEqual(flats.DisplayInfo.DisplayColor.ToRgb(), sandy.DisplayInfo.DisplayColor.ToRgb());
        }
    }
}
