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
    /// How the noise generator finds mountain ranges.
    /// </summary>
    public class MountainRangeDetectorTests
    {
        private static readonly int[] Seeds = { 1, 2, 3, 4, 5, 6, 7, 8 };

        private static IEnumerable<GeographicFeature> Ranges(World world)
        {
            return world.Features.Where(f => f.Type.Id == MountainRangeDetector.TypeId && f.Scope == FeatureScope.Continental);
        }

        [Fact]
        public void RangesAreHighGroundOnOneContinent()
        {
            foreach (int seed in Seeds)
            {
                SimEngine engine = NoiseTestWorlds.Generate(seed);
                NoiseGeneratorRules rules = NoiseGeneratorRules.Load(engine.SimData);
                var claimed = new HashSet<WorldBlock>();
                foreach (GeographicFeature range in Ranges(engine.World))
                {
                    Assert.Equal(FeatureScope.Continental, range.Scope);
                    Assert.True(range.Footprint.Count >= rules.MountainRangeMinBlocks);
                    Assert.Single(range.Footprint.Blocks.Select(b => b.Status.Continent).Distinct());
                    foreach (WorldBlock block in range.Footprint.Blocks)
                    {
                        Assert.NotEqual(WorldBlockWaterStatus.Water, block.Status.WaterStatus);
                        Assert.True(block.Status.Elevation.Value >= rules.MountainElevationFrom);
                        Assert.True(claimed.Add(block), "seed " + seed + ": a block is in two ranges");
                    }
                }
            }
        }

        [Fact]
        public void EveryHighBlockInALargeEnoughGroupIsInARange()
        {
            // With no passes, ranges are exactly the touching groups of high ground of at least the minimum size
            SimEngine engine = NoiseTestWorlds.Generate(3, ("MountainRangeGap", "0"));
            NoiseGeneratorRules rules = NoiseGeneratorRules.Load(engine.SimData);
            World world = engine.World;
            WorldBlock[,] grid = world.WorldBlock.ChildBlocks;
            int width = grid.GetLength(0), height = grid.GetLength(1);

            bool[,] high = new bool[width, height];
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    high[x, y] = grid[x, y].Status.WaterStatus != WorldBlockWaterStatus.Water && grid[x, y].Status.Elevation >= rules.MountainElevationFrom;

            var ranges = Ranges(world).ToList();
            Assert.NotEmpty(ranges);
            foreach (GeographicFeature range in ranges)
            {
                Assert.Single(range.Footprint.Pieces());
                // Nothing high touches the range from outside
                foreach (WorldBlock block in range.Footprint.Blocks)
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            int nx = block.X + dx, ny = block.Y + dy;
                            if (nx < 0 || ny < 0 || nx >= width || ny >= height || !high[nx, ny]) continue;
                            Assert.True(range.Footprint.IsMember(grid[nx, ny]) || grid[nx, ny].Status.Continent != block.Status.Continent);
                        }
            }
        }

        [Fact]
        public void PassesJoinRangesIntoSeveralPieces()
        {
            int broken = 0;
            foreach (int seed in Seeds)
            {
                World world = NoiseTestWorlds.Generate(seed).World;
                foreach (GeographicFeature range in Ranges(world))
                {
                    var pieces = range.Footprint.Pieces();
                    if (pieces.Count > 1) broken++;
                    // Each piece is within a pass (one block) of another piece
                    foreach (var piece in pieces)
                    {
                        if (pieces.Count == 1) break;
                        bool near = pieces.Where(p => p != piece).SelectMany(p => p)
                            .Any(other => piece.Any(b => Math.Abs(b.X - other.X) <= 2 && Math.Abs(b.Y - other.Y) <= 2));
                        Assert.True(near, "a piece of " + range.Name + " is not near the rest of it");
                    }
                }
            }
            Assert.True(broken > 0, "no range in any of the test worlds is broken by a pass");
        }

        [Fact]
        public void SpineRunsThroughTheRange()
        {
            foreach (int seed in Seeds)
            {
                foreach (GeographicFeature range in Ranges(NoiseTestWorlds.Generate(seed).World))
                {
                    var spine = range.Spine;
                    Assert.NotEmpty(spine);
                    Assert.Equal(spine.Count, spine.Distinct().Count());
                    for (int i = 1; i < spine.Count; i++)
                        Assert.True(Math.Abs(spine[i].X - spine[i - 1].X) <= 2 && Math.Abs(spine[i].Y - spine[i - 1].Y) <= 2,
                            range.Name + ": spine steps further than a pass");
                    if (range.Footprint.Count > 1) Assert.True(spine.Count > 1);
                }
            }
        }

        [Fact]
        public void SpineOfALineIsTheWholeLine()
        {
            World world = NoiseTestWorlds.Generate(1).World;
            var blocks = new List<WorldBlock> { world.WorldBlock.ChildBlocks[4, 3], world.WorldBlock.ChildBlocks[2, 3], world.WorldBlock.ChildBlocks[3, 3],
                world.WorldBlock.ChildBlocks[5, 4], world.WorldBlock.ChildBlocks[1, 3] };
            var spine = FeatureGeometry.Spine(blocks, 2);
            var xs = spine.Select(b => b.X).ToList();
            Assert.True(xs.SequenceEqual(new[] { 1, 2, 3, 4, 5 }) || xs.SequenceEqual(new[] { 5, 4, 3, 2, 1 }), string.Join(",", xs));
        }

        [Fact]
        public void RangesAreNamed()
        {
            var names = new List<string>();
            foreach (int seed in Seeds)
                names.AddRange(Ranges(NoiseTestWorlds.Generate(seed).World).Select(r => r.Name));
            Assert.NotEmpty(names);
            Assert.All(names, n => Assert.False(string.IsNullOrWhiteSpace(n)));
        }

        [Fact]
        public void RangesCanBeSwitchedOff()
        {
            SimEngine engine = NoiseTestWorlds.Generate(2, ("MountainRanges", "0"));
            Assert.DoesNotContain(engine.World.Features, f => f.Type.Id == MountainRangeDetector.TypeId);
        }

        [Fact]
        public void HigherThresholdGivesLessMountainousGround()
        {
            int Covered(string threshold) => Seeds.Take(4).Sum(seed =>
                Ranges(NoiseTestWorlds.Generate(seed, ("MountainElevationFrom", threshold)).World).Sum(f => f.Footprint.Count));
            Assert.True(Covered("0.8") < Covered("0.5"));
        }

        [Fact]
        public void RegionBordersFollowRangeEdges()
        {
            // Share of neighbouring land blocks on opposite sides of a range's edge that are in the same region
            double SpanShare(string weight)
            {
                long across = 0, sameRegion = 0;
                foreach (int seed in Seeds)
                {
                    World world = NoiseTestWorlds.Generate(seed, ("RegionFeatureWeight", weight)).World;
                    foreach (WorldBlock block in world.WorldBlock.ChildBlocksAsList().Where(b => b.Status.Region != null))
                        foreach (WorldBlock next in new[] { block.East(), block.South() }.Where(n => n != null && n.Status.Region != null))
                        {
                            if (Continental(world, block).SequenceEqual(Continental(world, next))) continue;
                            across++;
                            if (block.Status.Region == next.Status.Region) sameRegion++;
                        }
                }
                return (double)sameRegion / across;
            }
            Assert.True(SpanShare("10") < SpanShare("0"));
        }

        // Regions are shaped before sub-ranges are found, so only the continental features count for their borders
        private static IEnumerable<GeographicFeature> Continental(World world, WorldBlock block)
        {
            return world.FeaturesAt(block).Where(f => f.Scope == FeatureScope.Continental);
        }
    }
}
