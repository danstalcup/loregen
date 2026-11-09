using System;
using System.Collections.Generic;
using System.Linq;
using LoreGen.Features;
using LoreGen.Simulation;
using LoreGen.WorldDisplay;
using LoreGen.WorldGen;
using LoreGen.WorldGen.Generators;
using Xunit;

namespace LoreGen.Tests
{
    /// <summary>
    /// Behaviour specific to the tectonic generator. (What every generator must do is in GeneratorContractTests.)
    /// </summary>
    public class TectonicWorldGeneratorTests
    {
        private static readonly int[] Seeds = { 1, 2, 3, 4, 5, 6, 7, 8 };

        private static SimEngine Generate(int seed, params (string Key, double Value)[] settings)
        {
            var engine = new SimEngine(seed);
            engine.Configuration.GeneratorId = TectonicWorldGenerator.GeneratorId;
            foreach (var setting in settings) engine.Configuration.GeneratorSettings[setting.Key] = setting.Value;
            engine.GenerateWorld();
            return engine;
        }

        private static IEnumerable<WorldBlock> Neighbours(WorldBlock[,] grid, WorldBlock block, int reach, bool corners)
        {
            for (int dx = -reach; dx <= reach; dx++)
                for (int dy = -reach; dy <= reach; dy++)
                {
                    if ((dx == 0 && dy == 0) || (!corners && dx != 0 && dy != 0)) continue;
                    int x = block.X + dx, y = block.Y + dy;
                    if (x >= 0 && y >= 0 && x < grid.GetLength(0) && y < grid.GetLength(1)) yield return grid[x, y];
                }
        }

        [Fact]
        public void IsRegistered()
        {
            Assert.Contains(TectonicWorldGenerator.GeneratorId, WorldGeneratorRegistry.CreateDefault().Ids);
        }

        [Fact]
        public void PlatesAreInOnePieceAndWithinTheConfiguredCount()
        {
            foreach (int seed in Seeds)
            {
                SimEngine engine = Generate(seed);
                var rules = TectonicGeneratorRules.Load(engine.SimData);
                World world = engine.World;
                Assert.InRange(world.Plates.Count, rules.PlatesMin, rules.PlatesMax);
                foreach (TectonicPlate plate in world.Plates)
                {
                    // Plates grow through corners as well as sides
                    var members = new HashSet<WorldBlock>(plate.Blocks);
                    var reached = new HashSet<WorldBlock> { plate.Blocks[0] };
                    var pending = new Stack<WorldBlock>(reached);
                    while (pending.Count > 0)
                        foreach (WorldBlock next in Neighbours(world.WorldBlock.ChildBlocks, pending.Pop(), 1, true))
                            if (members.Contains(next) && reached.Add(next)) pending.Push(next);
                    Assert.Equal(members.Count, reached.Count);
                }
            }
        }

        [Fact]
        public void BoundariesAreWherePlatesMeet()
        {
            World world = Generate(3).World;
            WorldBlock[,] grid = world.WorldBlock.ChildBlocks;
            var kinds = new HashSet<PlateBoundaryKind>();
            foreach (WorldBlock block in grid)
            {
                bool meets = Neighbours(grid, block, 1, false).Any(n => n.Status.Plate != block.Status.Plate);
                PlateBoundaryKind kind = block.Status.Plate.BoundaryAt(block);
                Assert.Equal(meets, kind != PlateBoundaryKind.None);
                kinds.Add(kind);
                if (block.ChildBlocks != null) Assert.Equal(kind, block.Status.Plate.BoundaryAt(block.ChildBlocks[3, 3]));
            }
            Assert.Contains(PlateBoundaryKind.Convergent, kinds);
            Assert.Contains(PlateBoundaryKind.Divergent, kinds);
        }

        [Fact]
        public void LandRidesOnContinentalPlates()
        {
            int land = 0, onContinental = 0;
            foreach (int seed in Seeds)
                foreach (WorldBlock block in Generate(seed).World.WorldBlock.ChildBlocksAsList())
                {
                    if (block.Status.WaterStatus == WorldBlockWaterStatus.Water) continue;
                    land++;
                    if (block.Status.Plate.Continental) onContinental++;
                }
            Assert.True(onContinental > 0.9 * land, onContinental + " of " + land + " land blocks are on continental plates");
        }

        [Fact]
        public void MountainRangesFollowConvergentBoundaries()
        {
            int rangeBlocks = 0, nearBoundary = 0;
            foreach (int seed in Seeds)
            {
                World world = Generate(seed).World;
                WorldBlock[,] grid = world.WorldBlock.ChildBlocks;
                foreach (GeographicFeature range in world.Features.Where(f => f.Type.Id == MountainRangeDetector.TypeId && f.Scope == FeatureScope.Continental))
                    foreach (WorldBlock block in range.Footprint.Blocks)
                    {
                        rangeBlocks++;
                        if (Neighbours(grid, block, 2, true).Append(block).Any(n => n.Status.Plate.BoundaryAt(n) == PlateBoundaryKind.Convergent)) nearBoundary++;
                    }
            }
            Assert.True(rangeBlocks > 0, "no mountain ranges");
            Assert.True(nearBoundary > 0.9 * rangeBlocks, nearBoundary + " of " + rangeBlocks + " range blocks are near a convergent boundary");
        }

        [Fact]
        public void WithoutMountainBuildingThereAreFewerRanges()
        {
            int With() => Seeds.Sum(seed => Generate(seed).World.Features.Count(f => f.Type.Id == MountainRangeDetector.TypeId));
            int Without() => Seeds.Sum(seed => Generate(seed, ("CollisionUplift", 0), ("SubductionUplift", 0), ("IslandArcUplift", 0)).World.Features
                .Count(f => f.Type.Id == MountainRangeDetector.TypeId && f.Footprint.Blocks.Any(b => b.Status.Plate.BoundaryAt(b) == PlateBoundaryKind.Convergent)));
            Assert.True(Without() < With());
        }

        [Fact]
        public void PlateSettingsAreApplied()
        {
            Assert.Equal(5, Generate(1, ("Plates", 5)).World.Plates.Count);
            Assert.Single(Generate(1, ("Plates", 1)).World.Plates);
        }

        [Fact]
        public void SharedSettingsAreAccepted()
        {
            World world = Generate(1, ("MountainRanges", 0), ("Plates", 6)).World;
            Assert.DoesNotContain(world.Features, f => f.Type.Id == MountainRangeDetector.TypeId);
            Assert.Equal(6, world.Plates.Count);
        }

        [Fact]
        public void NoiseLandSettingsAreRejected()
        {
            var engine = new SimEngine(1);
            engine.Configuration.GeneratorId = TectonicWorldGenerator.GeneratorId;
            engine.Configuration.GeneratorSettings["MaskStrength"] = 0.5;
            var ex = Assert.ThrowsAny<Exception>(() => engine.GenerateWorld());
            Assert.Contains("MaskStrength", ex.Message);
        }

        [Fact]
        public void PlatesHaveDistinctNames()
        {
            foreach (int seed in Seeds)
            {
                var names = Generate(seed).World.Plates.Select(p => p.Name).ToList();
                Assert.Equal(names.Count, names.Distinct().Count());
                Assert.All(names, n => Assert.Contains(" Plate", n));
            }
        }

        [Fact]
        public void PlatesViewShowsPlatesAndBoundaries()
        {
            World world = Generate(2).World;
            WorldBlock inside = world.WorldBlock.ChildBlocksAsList().First(b => b.Status.Plate.BoundaryAt(b) == PlateBoundaryKind.None);
            WorldBlock convergent = world.WorldBlock.ChildBlocksAsList().First(b => b.Status.Plate.BoundaryAt(b) == PlateBoundaryKind.Convergent);
            Assert.Equal(inside.Status.Plate.Name, Map.PlateLabel(inside));
            Assert.Equal(inside.Status.Plate.DisplayInfo.DisplayColor.ToRgb(), Map.GetColor(inside, MapContext.Plates).ToRgb());
            Assert.Equal("Convergent boundary", Map.PlateLabel(convergent));
        }

        [Fact]
        public void HeadingNamesTheDirectionOfDrift()
        {
            World world = Generate(1).World;
            Assert.Equal("east", new TectonicPlate(world, 1, true, 1, 0, new List<WorldBlock>()).Heading);
            Assert.Equal("north", new TectonicPlate(world, 1, true, 0, -1, new List<WorldBlock>()).Heading);
            Assert.Equal("south-west", new TectonicPlate(world, 1, true, -1, 1, new List<WorldBlock>()).Heading);
        }
    }
}
