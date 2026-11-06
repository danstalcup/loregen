using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LoreGen.Simulation;
using LoreGen.WorldGen;
using LoreGen.WorldGen.Generators;
using Xunit;

namespace LoreGen.Tests
{
    /// <summary>
    /// Behaviour specific to the noise generator. (What every generator must do is in GeneratorContractTests.)
    /// </summary>
    public class NoiseWorldGeneratorTests
    {
        private static SimEngine Generate(int seed, Dictionary<string, string> rules = null)
        {
            var engine = new SimEngine(seed);
            engine.Configuration.GeneratorId = "noise";
            if (rules != null)
            {
                string folder = Path.Combine(Path.GetTempPath(), "loregen-test-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(folder);
                var lines = typeof(SimEngine).Assembly.GetManifestResourceStream("LoreGen.Resources." + NoiseGeneratorRules.Filename);
                var original = new StreamReader(lines).ReadToEnd().Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0);
                File.WriteAllLines(Path.Combine(folder, NoiseGeneratorRules.Filename), original.Select(l =>
                {
                    string key = l.Split(',')[0];
                    return rules.ContainsKey(key) ? key + "," + rules[key] : l;
                }));
                engine.SimData.DataFolder = folder;
                try { engine.GenerateWorld(); }
                finally { Directory.Delete(folder, true); }
            }
            else engine.GenerateWorld();
            return engine;
        }

        private static double LandShare(World world)
        {
            var blocks = world.WorldBlock.ChildBlocksAsList();
            return blocks.Count(b => b.Status.WaterStatus != WorldBlockWaterStatus.Water) / (double)blocks.Count;
        }

        // ---- Land and water -------------------------------------------------------------------

        [Fact]
        public void ContinentsAreConnectedAndNeverTouch()
        {
            for (int seed = 1; seed <= 40; seed++)
            {
                var world = Generate(seed).World;
                foreach (var continent in world.Continents)
                    Assert.True(continent.ContinentArea.ContiguousAreas().Count == 1, "seed " + seed + ": " + continent.Name + " is in pieces");

                foreach (var block in world.WorldBlock.ChildBlocksAsList().Where(b => b.Status.Continent != null))
                    foreach (var neighbour in block.SurroundingBlocksAsList().Where(n => n.Status.Continent != null))
                        Assert.Same(block.Status.Continent, neighbour.Status.Continent);
            }
        }

        [Fact]
        public void TinySpecksOfLandAreNotKept()
        {
            var rules = NoiseGeneratorRules.Load(new SimEngine(1).SimData);
            for (int seed = 1; seed <= 40; seed++)
            {
                var world = Generate(seed).World;
                if (world.Continents.Count == 1) continue; // a world always keeps its largest piece of land
                Assert.All(world.Continents, c => Assert.True(c.ContinentArea.Area() >= rules.MinLandmassBlocks, "seed " + seed + ": " + c.ContinentArea.Area() + " blocks"));
            }
        }

        [Fact]
        public void TheMapEdgeIsOcean()
        {
            for (int seed = 1; seed <= 40; seed++)
            {
                var root = Generate(seed).World.WorldBlock;
                var edge = root.ChildBlocksAsList().Where(b => b.X == 0 || b.Y == 0 || b.X == root.Width - 1 || b.Y == root.Height - 1).ToList();
                double water = edge.Count(b => b.Status.WaterStatus == WorldBlockWaterStatus.Water) / (double)edge.Count;
                Assert.True(water >= 0.95, "seed " + seed + ": only " + water.ToString("P0") + " of the edge is water");
            }
        }

        [Fact]
        public void CoastlineIsLandThatTouchesWater()
        {
            var world = Generate(5).World;
            foreach (var block in world.WorldBlock.ChildBlocksAsList().Where(b => b.Status.WaterStatus != WorldBlockWaterStatus.Water))
            {
                bool touchesWater = block.SurroundingBlocksAsList().Any(n => n.Status.WaterStatus == WorldBlockWaterStatus.Water);
                Assert.Equal(touchesWater, block.Status.WaterStatus == WorldBlockWaterStatus.Coastline);
            }
        }

        [Fact]
        public void LandShareFollowsTheRules()
        {
            Func<double, double> averageShare = target =>
            {
                var rules = new Dictionary<string, string>
                {
                    { "LandFractionMean", target.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                    { "LandFractionStdDev", "0" },
                    { "LandFractionMin", (target - 0.01).ToString(System.Globalization.CultureInfo.InvariantCulture) },
                    { "LandFractionMax", (target + 0.01).ToString(System.Globalization.CultureInfo.InvariantCulture) },
                };
                return Enumerable.Range(1, 8).Average(seed => LandShare(Generate(seed, rules).World));
            };

            double little = averageShare(0.2), lots = averageShare(0.45);
            Assert.InRange(little, 0.15, 0.27);
            Assert.InRange(lots, 0.40, 0.52);
            Assert.True(lots > little + 0.15);
        }

        [Fact]
        public void WorldsVaryInTheNumberOfContinents()
        {
            var counts = Enumerable.Range(1, 40).Select(seed => Generate(seed).World.Continents.Count).Distinct().ToList();
            Assert.True(counts.Count >= 3, "Only saw " + string.Join(",", counts));
        }

        // ---- Elevation ------------------------------------------------------------------------

        [Fact]
        public void ElevationAgreesWithLandAndWater()
        {
            for (int seed = 1; seed <= 20; seed++)
            {
                var world = Generate(seed).World;
                foreach (var block in world.WorldBlock.ChildBlocksAsList())
                {
                    double? e = block.Status.Elevation;
                    Assert.True(e.HasValue, "seed " + seed + ": block without elevation");
                    Assert.InRange(e.Value, -1.0, 1.0);
                    if (block.Status.WaterStatus == WorldBlockWaterStatus.Water) Assert.True(e.Value < 0, "seed " + seed + ": water at " + e.Value);
                    else Assert.True(e.Value > 0, "seed " + seed + ": land at " + e.Value);
                }
            }
        }

        [Fact]
        public void LegacyWorldsHaveNoElevation()
        {
            var engine = new SimEngine(3);
            engine.GenerateWorld();
            Assert.All(engine.World.WorldBlock.ChildBlocksAsList(), b => Assert.Null(b.Status.Elevation));
        }

        // ---- Seas and oceans ------------------------------------------------------------------

        [Fact]
        public void EnclosedWaterBecomesSeasAndOpenWaterBecomesOceans()
        {
            int seasSeen = 0;
            for (int seed = 1; seed <= 60; seed++)
            {
                var world = Generate(seed).World;
                var root = world.WorldBlock;
                foreach (var continent in world.Continents)
                {
                    foreach (var sea in continent.Seas)
                    {
                        seasSeen++;
                        Assert.True(sea.SeaBlock.ContiguousAreas().Count == 1, "seed " + seed + ": sea in pieces");
                        Assert.All(sea.SeaBlock.Blocks(), b =>
                        {
                            Assert.Equal(WorldBlockWaterStatus.Water, b.Status.WaterStatus);
                            Assert.Null(b.Status.Ocean);
                            Assert.False(b.X == 0 || b.Y == 0 || b.X == root.Width - 1 || b.Y == root.Height - 1, "a sea touching the map edge");
                        });
                    }
                }
                // Every water block that is not part of a sea belongs to an ocean
                Assert.All(root.ChildBlocksAsList().Where(b => b.Status.WaterStatus == WorldBlockWaterStatus.Water && b.Status.Sea == null),
                    b => Assert.NotNull(b.Status.Ocean));
            }
            Assert.True(seasSeen > 0, "No sea in 60 worlds; the sea logic is never exercised");
        }

        // ---- Regions --------------------------------------------------------------------------

        [Fact]
        public void RegionsAreConnectedAndCoverEachContinent()
        {
            for (int seed = 1; seed <= 25; seed++)
            {
                var world = Generate(seed).World;
                foreach (var continent in world.Continents)
                {
                    foreach (var region in continent.Regions)
                        Assert.True(region.RegionArea.ContiguousAreas().Count == 1, "seed " + seed + ": " + region.Name + " is in pieces");

                    var inRegions = continent.Regions.SelectMany(r => r.RegionArea.BlocksList).ToList();
                    Assert.Equal(continent.ContinentArea.Area(), inRegions.Count);
                    Assert.Equal(inRegions.Count, inRegions.Distinct().Count());
                }
            }
        }

        [Fact]
        public void RegionCountIsNearTheRules()
        {
            var rules = new SimEngine(1).SimData.WorldRules;
            for (int seed = 1; seed <= 25; seed++)
            {
                int regions = Generate(seed).World.Continents.Sum(c => c.Regions.Count);
                // At least one per continent can push the total over the maximum
                Assert.InRange(regions, 1, rules.RegionsMax + 15);
            }
        }

        // ---- Settings -------------------------------------------------------------------------

        [Fact]
        public void ShippedRulesLoadAndAreValid()
        {
            var rules = NoiseGeneratorRules.Load(new SimEngine(1).SimData);
            Assert.InRange(rules.LandFractionMean, rules.LandFractionMin, rules.LandFractionMax);
            Assert.True(rules.Octaves >= 1);
        }

        [Theory]
        [InlineData("MaskStrength", "2", "MaskStrength")]
        [InlineData("LandFractionMax", "1", "LandFraction")]
        [InlineData("Octaves", "0", "Octaves")]
        [InlineData("EdgeBorder", "0", "EdgeBorder")]
        [InlineData("MinSeaBlocks", "0", "MinSeaBlocks")]
        public void OutOfRangeSettingsAreRejectedWithAClearMessage(string key, string value, string mentioned)
        {
            var ex = Assert.Throws<InvalidDataException>(() => Generate(1, new Dictionary<string, string> { { key, value } }));
            Assert.Contains(NoiseGeneratorRules.Filename, ex.Message);
            Assert.Contains(mentioned, ex.Message);
        }

        [Fact]
        public void FailedGenerationFromBadSettingsKeepsThePreviousWorld()
        {
            var engine = new SimEngine(4);
            engine.GenerateWorld();
            var before = engine.World;
            engine.Configuration.GeneratorId = "noise";
            engine.SimData.DataFolder = Path.Combine(Path.GetTempPath(), "loregen-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(engine.SimData.DataFolder);
            try
            {
                File.WriteAllLines(Path.Combine(engine.SimData.DataFolder, NoiseGeneratorRules.Filename), new[] { "Octaves,5" });
                Assert.Throws<InvalidDataException>(() => engine.GenerateWorld());
                Assert.Same(before, engine.World);
            }
            finally { Directory.Delete(engine.SimData.DataFolder, true); }
        }
    }

    public class GridLabelingTests
    {
        private static bool[,] Grid(params string[] rows)
        {
            var cells = new bool[rows[0].Length, rows.Length];
            for (int y = 0; y < rows.Length; y++)
                for (int x = 0; x < rows[0].Length; x++)
                    cells[x, y] = rows[y][x] == '#';
            return cells;
        }

        [Fact]
        public void FindsSeparateComponentsInScanOrder()
        {
            var l = GridLabeling.Label(Grid(
                "##...",
                "#..#.",
                "....#"));
            Assert.Equal(3, l.Count);
            Assert.Equal(new[] { 3, 1, 1 }, l.Sizes);
            Assert.Equal(0, l.Labels[0, 0]);
            Assert.Equal(1, l.Labels[3, 1]);
            Assert.Equal(2, l.Labels[4, 2]);
            Assert.Equal(-1, l.Labels[2, 0]);
        }

        [Fact]
        public void DiagonalNeighboursAreNotConnected()
        {
            var l = GridLabeling.Label(Grid(
                "#.",
                ".#"));
            Assert.Equal(2, l.Count);
        }

        [Fact]
        public void ReportsWhichComponentsTouchTheEdge()
        {
            var l = GridLabeling.Label(Grid(
                "#....",
                ".....",
                "..##.",
                "..##.",
                "....."));
            Assert.Equal(new[] { true, false }, l.TouchesEdge);
        }

        [Fact]
        public void EmptyGridHasNoComponents()
        {
            Assert.Equal(0, GridLabeling.Label(Grid("...", "...")).Count);
        }

        [Fact]
        public void HandlesALargeSingleComponentWithoutOverflowingTheStack()
        {
            var cells = new bool[600, 600];
            for (int x = 0; x < 600; x++) for (int y = 0; y < 600; y++) cells[x, y] = true;
            var l = GridLabeling.Label(cells);
            Assert.Equal(1, l.Count);
            Assert.Equal(360000, l.Sizes[0]);
        }
    }
}
