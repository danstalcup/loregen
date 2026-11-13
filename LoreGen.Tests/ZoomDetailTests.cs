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
    /// Detail made when zooming in (World.EnsureChildren and the ZoomDetail detailer), down to 100 m blocks.
    /// </summary>
    public class ZoomDetailTests
    {
        private static SimEngine Generate(int seed, string generator = TectonicWorldGenerator.GeneratorId, Action<SimEngine> configure = null)
        {
            var engine = new SimEngine(seed);
            engine.Configuration.GeneratorId = generator;
            configure?.Invoke(engine);
            engine.GenerateWorld();
            return engine;
        }

        /// <summary>
        /// The block at a depth and position (in blocks of that depth), zooming in to make it if need be; null off the map.
        /// </summary>
        private static WorldBlock Block(World world, int depth, int x, int y)
        {
            int scale = 1;
            for (int d = 1; d < depth; d++) scale *= 10;
            var top = world.WorldBlock.ChildBlocks;
            if (x < 0 || y < 0 || x / scale >= top.GetLength(0) || y / scale >= top.GetLength(1)) return null;
            var block = top[x / scale, y / scale];
            while (block.Depth < depth)
            {
                world.EnsureChildren(block);
                scale /= 10;
                block = block.ChildBlocks[(x / scale) % 10, (y / scale) % 10];
            }
            return block;
        }

        private static (int X, int Y) Cell(WorldBlock block) => (block.TrueX / block.TrueWidth, block.TrueY / block.TrueWidth);

        // A 100 km block on a coast, with more than one region in the blocks around it
        private static WorldBlock CoastalBorderBlock(World world)
        {
            foreach (var top in world.WorldBlock.ChildBlocks)
            {
                if (top.ChildBlocks == null) continue;
                foreach (var block in top.ChildBlocks)
                {
                    if (block.Status.WaterStatus != WorldBlockWaterStatus.Coastline) continue;
                    var (x, y) = Cell(block);
                    if (x < 2 || y < 2) continue;
                    var regions = new HashSet<Region>();
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            var r = Block(world, 2, x + dx, y + dy)?.Status.Region;
                            if (r != null) regions.Add(r);
                        }
                    if (regions.Count >= 2) return block;
                }
            }
            throw new InvalidOperationException("No coastal border block found.");
        }

        // The 3x3 100 km blocks around a block, each zoomed in to 10 km
        private static List<WorldBlock> ZoomRing(World world, WorldBlock centre)
        {
            var (x, y) = Cell(centre);
            var ring = new List<WorldBlock>();
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    var block = Block(world, 2, x + dx, y + dy);
                    if (block == null) continue;
                    world.EnsureChildren(block);
                    ring.Add(block);
                }
            return ring;
        }

        private static string Describe(WorldBlock block) => Describe(block, true);

        private static string Describe(WorldBlock block, bool withSubregion)
        {
            var s = block.Status;
            return $"{block.Depth}:{block.TrueX},{block.TrueY} {s.WaterStatus} e{s.Elevation:0.00000} t{s.Temperature:0.0000} m{s.Moisture:0.0000} h{s.Hydration:0.0000} "
                + $"{s.Continent?.Name}/{s.Region?.Name}/{(withSubregion ? s.Subregion?.Name : "")}/{s.Ocean?.Name}/{s.Sea?.Name}/{s.Lake?.Name} {s.Terrain?.Name}";
        }

        private static string DescribeChildren(WorldBlock block) =>
            string.Join("\n", block.ChildBlocks.Cast<WorldBlock>().Select(Describe));

        [Fact]
        public void BlocksAreDefinedDownTo100Metres()
        {
            var world = Generate(3).World;
            var land = world.WorldBlock.ChildBlocks.Cast<WorldBlock>().First(b => b.Status.WaterStatus == WorldBlockWaterStatus.Land);
            string[] names = { "1000 km", "100 km", "10 km", "1 km", "100 m" };
            var block = land;
            for (int depth = 1; depth <= WorldBlock.FinestDepth; depth++)
            {
                Assert.Equal(depth, block.Depth);
                Assert.Equal(names[depth - 1], block.ScaleName);
                Assert.Equal(block.TrueWidth * 100L, block.WidthInMetres);
                if (depth < WorldBlock.FinestDepth)
                {
                    Assert.True(block.CanSubdivide);
                    Assert.True(world.EnsureChildren(block));
                    block = block.ChildBlocks[4, 6];
                }
            }
            Assert.Equal(WorldBlock.FinestTrueSize, block.TrueWidth);
            Assert.True(block.IsFinest);
            Assert.False(block.CanSubdivide);
            Assert.False(world.EnsureChildren(block));
            Assert.Null(block.ChildBlocks);
        }

        [Fact]
        public void ZoomedBlocksAreComplete()
        {
            var world = Generate(5).World;
            var centre = CoastalBorderBlock(world);
            var ring = ZoomRing(world, centre);
            foreach (var child in centre.ChildBlocks) world.EnsureChildren(child);
            world.EnsureChildren(centre.ChildBlocks[5, 5].ChildBlocks[5, 5]);

            var blocks = ring.SelectMany(b => b.ChildBlocks.Cast<WorldBlock>())
                .Concat(centre.ChildBlocks.Cast<WorldBlock>().SelectMany(c => c.ChildBlocks.Cast<WorldBlock>()))
                .Concat(centre.ChildBlocks[5, 5].ChildBlocks[5, 5].ChildBlocks.Cast<WorldBlock>()).ToList();
            Assert.Contains(blocks, b => b.Status.WaterStatus == WorldBlockWaterStatus.Water);
            Assert.Contains(blocks, b => b.Status.WaterStatus == WorldBlockWaterStatus.Coastline);
            foreach (var block in blocks)
            {
                var s = block.Status;
                Assert.NotNull(s.Temperature);
                Assert.NotNull(s.Moisture);
                Assert.NotNull(s.Hydration);
                if (s.WaterStatus == WorldBlockWaterStatus.Water)
                {
                    Assert.True(s.Elevation < 0);
                    Assert.Null(s.Region);
                    Assert.True(s.Ocean != null || s.Sea != null || s.Lake != null, Describe(block));
                }
                else
                {
                    Assert.True(s.Elevation > 0);
                    Assert.NotNull(s.Continent);
                    Assert.NotNull(s.Terrain);
                    Assert.Contains(s.Region, s.Continent.Regions);
                    // Coastline exactly where land is beside water
                    var (x, y) = Cell(block);
                    bool beside = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }
                        .Select(d => Block(world, block.Depth, x + d.Item1, y + d.Item2))
                        .Any(n => n != null && n.Status.WaterStatus == WorldBlockWaterStatus.Water);
                    Assert.Equal(beside, s.WaterStatus == WorldBlockWaterStatus.Coastline);
                }
            }
        }

        [Fact]
        public void TheOrderOfZoomingDoesNotMatter()
        {
            var a = Generate(7).World;
            var b = Generate(7).World;
            var centreA = CoastalBorderBlock(a);
            var (x, y) = Cell(centreA);

            // a: the ring in order, then into the centre; b: deep into a neighbour first, then the ring backwards
            var ringA = ZoomRing(a, centreA);
            foreach (var child in centreA.ChildBlocks) a.EnsureChildren(child);

            var deep = Block(b, 4, (x + 1) * 100 + 3, y * 100 + 97);
            b.EnsureChildren(deep);
            var ringB = new List<WorldBlock>();
            for (int dy = 1; dy >= -1; dy--)
                for (int dx = 1; dx >= -1; dx--)
                {
                    var block = Block(b, 2, x + dx, y + dy);
                    b.EnsureChildren(block);
                    ringB.Insert(0, block);
                }
            var centreB = Block(b, 2, x, y);
            foreach (var child in centreB.ChildBlocks.Cast<WorldBlock>().Reverse()) b.EnsureChildren(child);

            Assert.Equal(ringA.Count, ringB.Count);
            for (int i = 0; i < ringA.Count; i++) Assert.Equal(DescribeChildren(ringA[i]), DescribeChildren(ringB[i]));
            for (int i = 0; i < 10; i++)
                for (int j = 0; j < 10; j++)
                    Assert.Equal(DescribeChildren(centreA.ChildBlocks[i, j]), DescribeChildren(centreB.ChildBlocks[i, j]));
        }

        [Fact]
        public void ZoomingLeavesTheGeneratedWorldAsItWas()
        {
            var engine = Generate(9);
            var world = engine.World;
            // Zooming in fills in the subregions of the 100 km blocks, but changes nothing else about them
            string Generated() => string.Join("\n", world.WorldBlock.ChildBlocksAsList()
                .SelectMany(t => new[] { t }.Concat(t.ChildBlocksAsList())).Select(b => Describe(b, false)));
            string before = Generated();
            var other = Generate(9);

            ZoomRing(world, CoastalBorderBlock(world));
            // The depth-1 and depth-2 blocks that were made with the world are unchanged (new depth-2 blocks may appear
            // under top-level blocks that had none)
            var now = Generated().Split('\n').ToHashSet();
            Assert.All(before.Split('\n'), line => Assert.Contains(line, now));
            // Zooming draws nothing from the engine's random numbers
            Assert.Equal(other.Rnd.Unweighted(int.MaxValue), engine.Rnd.Unweighted(int.MaxValue));
        }

        [Fact]
        public void NeighbouringBlocksMeetWithoutSeams()
        {
            var world = Generate(5).World;
            var ring = ZoomRing(world, CoastalBorderBlock(world));
            // Compare neighbouring 10 km blocks inside a 100 km block with those across the edge between two
            double inside = 0, across = 0;
            int insideCount = 0, acrossCount = 0, insideFlips = 0, acrossFlips = 0;
            foreach (var block in ring)
                foreach (var child in block.ChildBlocks)
                {
                    var (x, y) = Cell(child);
                    var east = Block(world, 3, x + 1, y);
                    if (east == null || east.ParentBlock.ChildBlocks == null || !ring.Contains(east.ParentBlock)) continue;
                    double difference = Math.Abs(child.Status.Elevation.Value - east.Status.Elevation.Value);
                    bool flip = (child.Status.WaterStatus == WorldBlockWaterStatus.Water) != (east.Status.WaterStatus == WorldBlockWaterStatus.Water);
                    if (east.ParentBlock == child.ParentBlock) { inside += difference; insideCount++; if (flip) insideFlips++; }
                    else { across += difference; acrossCount++; if (flip) acrossFlips++; }
                }
            Assert.True(acrossCount > 10 && insideCount > 100);
            Assert.True(across / acrossCount < 2.5 * (inside / insideCount), $"across {across / acrossCount:0.0000}, inside {inside / insideCount:0.0000}");
            Assert.True((double)acrossFlips / acrossCount < 3.0 * insideFlips / insideCount + 0.02,
                $"coast crossings across {acrossFlips}/{acrossCount}, inside {insideFlips}/{insideCount}");
        }

        [Fact]
        public void ZoomingFollowsTheCoarserMap()
        {
            double landInLand = 0, landInWater = 0;
            int landBlocks = 0, waterBlocks = 0;
            foreach (int seed in new[] { 5, 11, 12 })
            {
                var world = Generate(seed).World;
                foreach (var block in ZoomRing(world, CoastalBorderBlock(world)))
                {
                    double share = block.ChildBlocks.Cast<WorldBlock>().Count(c => c.Status.WaterStatus != WorldBlockWaterStatus.Water) / 100.0;
                    if (block.Status.WaterStatus == WorldBlockWaterStatus.Water) { landInWater += share; waterBlocks++; }
                    else { landInLand += share; landBlocks++; }
                }
            }
            Assert.True(landInLand / landBlocks > 0.8, $"land blocks are {landInLand / landBlocks:P0} land");
            Assert.True(waterBlocks == 0 || landInWater / waterBlocks < 0.2, $"water blocks are {landInWater / waterBlocks:P0} land");
        }

        [Fact]
        public void ZoomedTerrainMostlyCarriesOnFromTheBlockAbove()
        {
            var world = Generate(5).World;
            var ring = ZoomRing(world, CoastalBorderBlock(world));
            var land = ring.Where(b => b.Status.Terrain != null && b.Status.WaterStatus != WorldBlockWaterStatus.Water)
                .SelectMany(b => b.ChildBlocks.Cast<WorldBlock>()).Where(c => c.Status.WaterStatus != WorldBlockWaterStatus.Water).ToList();
            double kept = land.Count(c => c.Status.Terrain == c.ParentBlock.Status.Terrain) / (double)land.Count;
            Assert.InRange(kept, 0.3, 0.95);
            Assert.True(land.Select(c => c.Status.Terrain).Distinct().Count() > 1);

            // Patches rather than speckle: most land with land around it has a neighbour of the same terrain (lone specks of
            // land on a ragged coast have nothing to match)
            var inland = land.Where(c =>
            {
                var (x, y) = Cell(c);
                return new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }.Count(d => Block(world, 3, x + d.Item1, y + d.Item2)?.Status.Terrain != null) >= 3;
            }).ToList();
            int alone = inland.Count(c =>
            {
                var (x, y) = Cell(c);
                return !new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }.Any(d => Block(world, 3, x + d.Item1, y + d.Item2)?.Status.Terrain == c.Status.Terrain);
            });
            // (Where several different 100 km terrains meet, their mix leaves some lone blocks; speckle proper was 30-40%)
            Assert.True(alone < 0.12 * inland.Count, $"{alone} of {inland.Count} land blocks have no neighbour of their terrain");
        }

        [Theory]
        [InlineData(TectonicWorldGenerator.GeneratorId, 5)]
        [InlineData(TectonicWorldGenerator.GeneratorId, 8)]
        [InlineData(NoiseWorldGenerator.GeneratorId, 3)]
        public void TerrainBordersDoNotFollowTheCoarserBlocksEdges(string generator, int seed)
        {
            // The detail of every 100 km block in a varied 1000 km block: terrain should change about as often across the
            // edges between the 100 km blocks as anywhere else, rather than in straight lines along them
            var world = Generate(seed, generator).World;
            var top = world.WorldBlock.ChildBlocks.Cast<WorldBlock>().Where(b => b.ChildBlocks != null && b.Status.WaterStatus != WorldBlockWaterStatus.Water)
                .OrderByDescending(b => b.ChildBlocks.Cast<WorldBlock>().Select(c => c.Status.Terrain).Distinct().Count()).First();
            foreach (var block in top.ChildBlocks) world.EnsureChildren(block);
            WorldBlock At(int x, int y) => top.ChildBlocks[x / 10, y / 10].ChildBlocks[x % 10, y % 10];
            int edgeChanges = 0, edgePairs = 0, insideChanges = 0, insidePairs = 0;
            for (int x = 0; x < 100; x++)
                for (int y = 0; y < 100; y++)
                    foreach (var (dx, dy) in new[] { (1, 0), (0, 1) })
                    {
                        if (x + dx >= 100 || y + dy >= 100) continue;
                        var a = At(x, y).Status.Terrain;
                        var b = At(x + dx, y + dy).Status.Terrain;
                        if (a == null || b == null) continue;
                        bool edge = (dx == 1 && (x + 1) % 10 == 0) || (dy == 1 && (y + 1) % 10 == 0);
                        if (edge) { edgePairs++; if (a != b) edgeChanges++; }
                        else { insidePairs++; if (a != b) insideChanges++; }
                    }
            double ratio = ((double)edgeChanges / edgePairs) / ((double)insideChanges / insidePairs);
            Assert.True(ratio < 1.5, $"terrain changes {ratio:0.00} times as often across 100 km edges");
        }

        [Theory]
        [InlineData(5)]
        [InlineData(8)]
        public void TerrainBordersDoNotFollowTheEdgesOf10KmBlocksEither(int seed)
        {
            // One level further in: the 1 km detail of a 100 km block's 10 km blocks
            var world = Generate(seed).World;
            var top = world.WorldBlock.ChildBlocks.Cast<WorldBlock>().Where(b => b.ChildBlocks != null && b.Status.WaterStatus == WorldBlockWaterStatus.Land)
                .OrderByDescending(b => b.ChildBlocks.Cast<WorldBlock>().Select(c => c.Status.Terrain).Distinct().Count()).First();
            var block = top.ChildBlocks[4, 4];
            world.EnsureChildren(block);
            foreach (var child in block.ChildBlocks) world.EnsureChildren(child);
            WorldBlock At(int x, int y) => block.ChildBlocks[x / 10, y / 10].ChildBlocks[x % 10, y % 10];
            int edgeChanges = 0, edgePairs = 0, insideChanges = 0, insidePairs = 0;
            for (int x = 0; x < 100; x++)
                for (int y = 0; y < 100; y++)
                    foreach (var (dx, dy) in new[] { (1, 0), (0, 1) })
                    {
                        if (x + dx >= 100 || y + dy >= 100) continue;
                        var a = At(x, y).Status.Terrain;
                        var b = At(x + dx, y + dy).Status.Terrain;
                        if (a == null || b == null) continue;
                        bool edge = (dx == 1 && (x + 1) % 10 == 0) || (dy == 1 && (y + 1) % 10 == 0);
                        if (edge) { edgePairs++; if (a != b) edgeChanges++; }
                        else { insidePairs++; if (a != b) insideChanges++; }
                    }
            double ratio = ((double)edgeChanges / edgePairs) / ((double)insideChanges / insidePairs);
            Assert.True(ratio < 1.25, $"terrain changes {ratio:0.00} times as often across 10 km edges");
        }

        [Fact]
        public void PeaksPassesAndOasesAreSmallSpotsWhenZoomedIn()
        {
            var world = Generate(5).World;
            var points = world.Features.Where(f => f.Type.Shape == FeatureShape.Point && f.Footprint.Depth == 2).Take(6).ToList();
            Assert.NotEmpty(points);
            foreach (var point in points)
            {
                var block = point.Spine[0];
                world.EnsureChildren(block);
                int inside = block.ChildBlocks.Cast<WorldBlock>().Count(c => world.FeaturesAt(c).Contains(point));
                Assert.InRange(inside, 1, 12);
            }
        }

        [Fact]
        public void SubrangesErgsAndSaltFlatsHaveWindingEdgesWhenZoomedIn()
        {
            var world = Generate(5).World;
            var blobs = world.Features.Where(f => f.Footprint.Depth == 2 && f.Type.Shape != FeatureShape.Point
                && f.Type.Category != FeatureCategory.Water).ToList();
            Assert.NotEmpty(blobs);
            Assert.All(blobs, f => Assert.IsType<NoiseEdgeRefiner>(f.Refiner));
        }

        [Fact]
        public void KeepingMoreTerrainKeepsMore()
        {
            double Kept(double keep)
            {
                var world = Generate(5, configure: e => e.Configuration.GeneratorSettings["ZoomTerrainKeep"] = keep).World;
                var land = ZoomRing(world, CoastalBorderBlock(world)).Where(b => b.Status.Terrain != null)
                    .SelectMany(b => b.ChildBlocks.Cast<WorldBlock>()).Where(c => c.Status.Terrain != null).ToList();
                return land.Count(c => c.Status.Terrain == c.ParentBlock.Status.Terrain) / (double)land.Count;
            }
            Assert.True(Kept(0.9) > Kept(0.1) + 0.2);
        }

        [Fact]
        public void RiversCarryOnWhenZoomedIn()
        {
            var world = Generate(5).World;
            var river = world.Features.First(f => f.Type.Id == WaterFeatureDetector.RiverTypeId && f.Spine.Count > 4);
            var block = river.Spine[river.Spine.Count / 2];
            world.EnsureChildren(block);
            int inRiver = block.ChildBlocks.Cast<WorldBlock>().Count(c => world.FeaturesAt(c).Contains(river));
            Assert.InRange(inRiver, 1, 99);
            var child = block.ChildBlocks.Cast<WorldBlock>().First(c => world.FeaturesAt(c).Contains(river));
            world.EnsureChildren(child);
            Assert.Contains(child.ChildBlocks.Cast<WorldBlock>(), c => world.FeaturesAt(c).Contains(river));
        }

        [Fact]
        public void BlocksWithoutDetailCanBeZoomedInto()
        {
            // Interior land has no 100 km blocks of its own until zoomed into
            var world = Generate(5).World;
            var interior = world.WorldBlock.ChildBlocks.Cast<WorldBlock>().First(b => b.ChildBlocks == null && b.Status.WaterStatus == WorldBlockWaterStatus.Land);
            Assert.True(world.EnsureChildren(interior));
            Assert.All(interior.ChildBlocks.Cast<WorldBlock>(), c =>
            {
                Assert.NotEqual(WorldBlockWaterStatus.Water, c.Status.WaterStatus);
                Assert.NotNull(c.Status.Terrain);
                Assert.NotNull(c.Status.Region);
            });
        }

        [Fact]
        public void LegacyWorldsStillZoom()
        {
            var world = Generate(5, LegacyWorldGenerator.GeneratorId).World;
            Assert.Null(world.Detailer);
            var block = world.WorldBlock.ChildBlocks.Cast<WorldBlock>().First(b => b.ChildBlocks == null);
            Assert.True(world.EnsureChildren(block));
            Assert.Equal(100, block.ChildBlocks.Length);
        }

        [Theory]
        [InlineData("ZoomCoastWiggle", 1.5)]
        [InlineData("ZoomRelief", -0.1)]
        [InlineData("ZoomBorderWarp", 0.8)]
        [InlineData("ZoomTerrainKeep", 2)]
        public void OutOfRangeZoomSettingsAreRejected(string key, double value)
        {
            var engine = new SimEngine(1);
            engine.Configuration.GeneratorId = NoiseWorldGenerator.GeneratorId;
            engine.Configuration.GeneratorSettings[key] = value;
            var ex = Assert.ThrowsAny<Exception>(() => engine.GenerateWorld());
            Assert.Contains(key, ex.Message);
        }
    }
}
