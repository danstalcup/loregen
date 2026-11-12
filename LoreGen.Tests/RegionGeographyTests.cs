using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LoreGen.Randomizer;
using LoreGen.Simulation;
using LoreGen.WorldGen;
using LoreGen.WorldGen.Generators;
using Xunit;

namespace LoreGen.Tests
{
    internal static class NoiseTestWorlds
    {
        /// <summary>Generates a world with the noise generator, optionally overriding keys in NoiseGeneratorRules.csv.</summary>
        public static SimEngine Generate(int seed, params (string Key, string Value)[] rules)
        {
            var engine = new SimEngine(seed);
            engine.Configuration.GeneratorId = "noise";
            if (rules.Length == 0)
            {
                engine.GenerateWorld();
                return engine;
            }

            string folder = Path.Combine(Path.GetTempPath(), "loregen-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var original = new StreamReader(typeof(SimEngine).Assembly.GetManifestResourceStream("LoreGen.Resources." + NoiseGeneratorRules.Filename))
                    .ReadToEnd().Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0);
                File.WriteAllLines(Path.Combine(folder, NoiseGeneratorRules.Filename), original.Select(l =>
                {
                    string key = l.Split(',')[0];
                    foreach (var rule in rules) if (rule.Key == key) return key + "," + rule.Value;
                    return l;
                }));
                engine.SimData.DataFolder = folder;
                engine.GenerateWorld();
            }
            finally { Directory.Delete(folder, true); }
            return engine;
        }

        public static readonly (string, string)[] PlainSpreading =
        {
            ("RegionSlopeWeight", "0"), ("RegionHeightWeight", "0"), ("RegionClimateWeight", "0"), ("RegionFeatureWeight", "0"), ("RegionTerrainWeight", "0"), ("RegionJitter", "0")
        };
    }

    /// <summary>
    /// How the noise generator divides continents into regions.
    /// </summary>
    public class RegionGeographyTests
    {
        // Mean change between neighbouring land blocks across region borders, relative to the change inside regions
        private static (double Slope, double Climate) BorderContrast(params (string, string)[] rules)
        {
            double borderSlope = 0, innerSlope = 0, borderClimate = 0, innerClimate = 0;
            long borders = 0, inner = 0;
            for (int seed = 1; seed <= 30; seed++)
            {
                var root = NoiseTestWorlds.Generate(seed, rules).World.WorldBlock;
                foreach (var block in root.ChildBlocksAsList().Where(b => b.Status.Region != null))
                {
                    foreach (var other in block.SurroundingBlocksAsList().Where(n => n.Status.Region != null))
                    {
                        double slope = Math.Abs(block.Status.Elevation.Value - other.Status.Elevation.Value);
                        double climate = Math.Abs(block.Status.Temperature.Value - other.Status.Temperature.Value)
                                       + Math.Abs(block.Status.Moisture.Value - other.Status.Moisture.Value);
                        if (ReferenceEquals(block.Status.Region, other.Status.Region)) { innerSlope += slope; innerClimate += climate; inner++; }
                        else { borderSlope += slope; borderClimate += climate; borders++; }
                    }
                }
            }
            return ((borderSlope / borders) / (innerSlope / inner), (borderClimate / borders) / (innerClimate / inner));
        }

        [Fact]
        public void BordersFallOnSteepGroundAndWhereTheClimateChanges()
        {
            var geography = BorderContrast();
            var plain = BorderContrast(NoiseTestWorlds.PlainSpreading);

            Assert.True(geography.Slope > 1.25, "borders only " + geography.Slope.ToString("0.00") + "x as steep as region interiors");
            Assert.True(geography.Climate > 1.15, "climate changes only " + geography.Climate.ToString("0.00") + "x as much across borders");
            // Without the terrain weights, regions have no reason to meet on steep ground
            Assert.True(plain.Slope < 1.2, "plain spreading gave " + plain.Slope.ToString("0.00"));
            Assert.True(geography.Slope > plain.Slope + 0.2);
            Assert.True(geography.Climate > plain.Climate + 0.1);
        }

        [Fact]
        public void NoRegionIsTinyUnlessItsContinentIsTheOnlyRegion()
        {
            var rules = NoiseGeneratorRules.Load(new SimEngine(1).SimData);
            for (int seed = 1; seed <= 30; seed++)
                foreach (var continent in NoiseTestWorlds.Generate(seed).World.Continents.Where(c => c.Regions.Count > 1))
                    Assert.All(continent.Regions, r => Assert.True(r.RegionArea.BlocksList.Count >= rules.RegionMinBlocks,
                        "seed " + seed + ": " + r.Name + " has " + r.RegionArea.BlocksList.Count + " blocks"));
        }

        [Fact]
        public void TinyRegionsDoAppearWithoutTheMinimumSize()
        {
            int tiny = 0;
            for (int seed = 1; seed <= 30; seed++)
                tiny += NoiseTestWorlds.Generate(seed, ("RegionMinBlocks", "1")).World.Continents.SelectMany(c => c.Regions).Count(r => r.RegionArea.BlocksList.Count < 3);
            Assert.True(tiny > 0, "The minimum size never mattered, so it is not being tested");
        }

        // ---- The partitioner itself -----------------------------------------------------------

        private static List<WorldBlock> LargestContinent(World world)
        {
            return world.Continents.OrderByDescending(c => c.ContinentArea.Area()).First().ContinentArea.Blocks().ToList();
        }

        [Fact]
        public void PartitionCoversEveryBlockOnceAndEachGroupIsConnected()
        {
            var rules = NoiseGeneratorRules.Load(new SimEngine(1).SimData);
            for (int seed = 1; seed <= 12; seed++)
            {
                var blocks = LargestContinent(NoiseTestWorlds.Generate(seed).World);
                foreach (int count in new[] { 1, 5, 12 })
                {
                    if (count > blocks.Count) continue;
                    var groups = RegionPartitioner.Partition(blocks, count, rules, new Rnd(seed));
                    Assert.Equal(blocks.Count, groups.Sum(g => g.Count));
                    Assert.Equal(blocks.Count, groups.SelectMany(g => g).Distinct().Count());
                    Assert.All(groups, g => Assert.Equal(1, new WorldBlockArea(g.ToList(), null).ContiguousAreas().Count));
                    Assert.InRange(groups.Count, 1, count);
                }
            }
        }

        [Fact]
        public void PartitionIsRepeatable()
        {
            var rules = NoiseGeneratorRules.Load(new SimEngine(1).SimData);
            var blocks = LargestContinent(NoiseTestWorlds.Generate(4).World);
            Func<string> run = () => string.Join("|", RegionPartitioner.Partition(blocks, 9, rules, new Rnd(77)).Select(g => string.Join(",", g.Select(b => b.X + ":" + b.Y))));
            Assert.Equal(run(), run());
        }

        [Fact]
        public void PartitionKeepsEveryGroupAtLeastTheMinimumUnlessThereIsOnlyOne()
        {
            var rules = NoiseGeneratorRules.Load(new SimEngine(1).SimData);
            for (int seed = 1; seed <= 12; seed++)
            {
                var blocks = LargestContinent(NoiseTestWorlds.Generate(seed).World);
                var groups = RegionPartitioner.Partition(blocks, Math.Min(25, blocks.Count), rules, new Rnd(seed));
                if (groups.Count > 1) Assert.All(groups, g => Assert.True(g.Count >= rules.RegionMinBlocks));
            }
        }

        [Fact]
        public void CentresAreSpreadOut()
        {
            var blocks = LargestContinent(NoiseTestWorlds.Generate(2).World);
            Assert.True(blocks.Count >= 100);

            Func<int, double> meanClosestPair = candidates => Enumerable.Range(0, 40).Average(trial =>
            {
                var seeds = RegionPartitioner.ChooseSeeds(blocks, 10, candidates, new Rnd(trial));
                Assert.Equal(10, seeds.Distinct().Count());
                return seeds.SelectMany((a, i) => seeds.Skip(i + 1).Select(b => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y)))).Min();
            });
            Assert.True(meanClosestPair(8) > meanClosestPair(1) + 0.5, "spread " + meanClosestPair(8) + " vs random " + meanClosestPair(1));
        }

        [Fact]
        public void BadRegionCountsAreRejected()
        {
            var rules = NoiseGeneratorRules.Load(new SimEngine(1).SimData);
            var blocks = LargestContinent(NoiseTestWorlds.Generate(2).World);
            Assert.Throws<ArgumentOutOfRangeException>(() => RegionPartitioner.ChooseSeeds(blocks, 0, 8, new Rnd(1)));
            Assert.Throws<ArgumentOutOfRangeException>(() => RegionPartitioner.ChooseSeeds(blocks, blocks.Count + 1, 8, new Rnd(1)));
        }

        // ---- Settings -------------------------------------------------------------------------

        [Theory]
        [InlineData("RegionSeedCandidates", "0")]
        [InlineData("RegionSlopeWeight", "-1")]
        [InlineData("RegionHeightWeight", "-1")]
        [InlineData("RegionClimateWeight", "-1")]
        [InlineData("RegionJitter", "-1")]
        [InlineData("RegionMinBlocks", "0")]
        [InlineData("RegionBorderDetail", "2")]
        [InlineData("RegionBorderWarp", "0.9")]
        [InlineData("RegionBorderFrequency", "0")]
        [InlineData("DetailMinIsletCells", "0")]
        public void OutOfRangeRegionSettingsAreRejected(string key, string value)
        {
            var ex = Assert.Throws<InvalidDataException>(() => NoiseTestWorlds.Generate(1, (key, value)));
            Assert.Contains(NoiseGeneratorRules.Filename, ex.Message);
        }
    }
}
