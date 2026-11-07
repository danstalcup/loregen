using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LoreGen.Randomizer;
using LoreGen.Simulation;
using LoreGen.Terrains;
using LoreGen.WorldGen;
using LoreGen.WorldGen.Generators;
using Xunit;

namespace LoreGen.Tests
{
    public class ClimateScaleTests
    {
        [Theory]
        [InlineData(0.0, BiomeTemperature.Frigid)]
        [InlineData(0.199, BiomeTemperature.Frigid)]
        [InlineData(0.2, BiomeTemperature.Cold)]
        [InlineData(0.5, BiomeTemperature.Temperate)]
        [InlineData(0.79, BiomeTemperature.Warm)]
        [InlineData(0.8, BiomeTemperature.Sweltering)]
        [InlineData(1.0, BiomeTemperature.Sweltering)]
        [InlineData(-3.0, BiomeTemperature.Frigid)]
        [InlineData(7.0, BiomeTemperature.Sweltering)]
        public void TemperatureClassesAreEqualBands(double value, BiomeTemperature expected)
        {
            Assert.Equal(expected, ClimateScale.Temperature(value));
        }

        [Theory]
        [InlineData(0.0, BiomePrecipitation.Arid)]
        [InlineData(0.25, BiomePrecipitation.Dry)]
        [InlineData(0.5, BiomePrecipitation.Moderate)]
        [InlineData(0.7, BiomePrecipitation.Wet)]
        [InlineData(0.95, BiomePrecipitation.Tropical)]
        public void PrecipitationClassesAreEqualBands(double value, BiomePrecipitation expected)
        {
            Assert.Equal(expected, ClimateScale.Precipitation(value));
        }

        [Fact]
        public void NotANumberIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ClimateScale.Temperature(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => ClimateScale.Precipitation(double.NaN));
        }
    }

    public class BiomeSelectorTests
    {
        private static readonly SimEngine Engine = new SimEngine(1);
        private static readonly NoiseGeneratorRules Rules = NoiseGeneratorRules.Load(Engine.SimData);

        private static double Distance(Biome b, BiomeTemperature t, BiomePrecipitation p, BiomeContour c)
        {
            return Rules.BiomeTemperatureWeight * Math.Abs((int)t - (int)b.Temperature)
                 + Rules.BiomePrecipitationWeight * Math.Abs((int)p - (int)b.Precipitation)
                 + Rules.BiomeContourWeight * Math.Abs((int)c - (int)b.Contour);
        }

        [Fact]
        public void AnExactMatchIsChosen()
        {
            var biome = BiomeSelector.Choose(Engine.SimData.Biomes, BiomeTemperature.Frigid, BiomePrecipitation.Arid, BiomeContour.Flat, Rules, new Rnd(1));
            Assert.Equal("Polar Desert", biome.Name);
        }

        [Fact]
        public void WithoutAnExactMatchTheNearestExistingBiomeIsChosen()
        {
            // No sweltering, wet, flat biome exists; Jungle Bog (sweltering, wet, lowlands) is the closest
            var biome = BiomeSelector.Choose(Engine.SimData.Biomes, BiomeTemperature.Sweltering, BiomePrecipitation.Wet, BiomeContour.Flat, Rules, new Rnd(1));
            Assert.Equal("Jungle Bog", biome.Name);
        }

        [Fact]
        public void EveryClimateGetsAClosestBiome()
        {
            foreach (BiomeTemperature t in Enum.GetValues(typeof(BiomeTemperature)))
                foreach (BiomePrecipitation p in Enum.GetValues(typeof(BiomePrecipitation)))
                    foreach (BiomeContour c in Enum.GetValues(typeof(BiomeContour)))
                    {
                        var chosen = BiomeSelector.Choose(Engine.SimData.Biomes, t, p, c, Rules, new Rnd(5));
                        double best = Engine.SimData.Biomes.Min(b => Distance(b, t, p, c));
                        Assert.Equal(best, Distance(chosen, t, p, c), 9);
                    }
        }

        [Fact]
        public void TiesAreBrokenAtRandomButOnlyAmongTheClosest()
        {
            // Rainforest and Monsoon Forest are both (sweltering, tropical, hilly)
            var seen = new HashSet<string>();
            for (int seed = 0; seed < 60; seed++)
                seen.Add(BiomeSelector.Choose(Engine.SimData.Biomes, BiomeTemperature.Sweltering, BiomePrecipitation.Tropical, BiomeContour.Hilly, Rules, new Rnd(seed)).Name);
            Assert.Equal(new[] { "Monsoon Forest", "Rainforest" }, seen.OrderBy(n => n));
        }

        [Fact]
        public void TheSameRandomSeedChoosesTheSameBiome()
        {
            Func<string> choose = () => BiomeSelector.Choose(Engine.SimData.Biomes, BiomeTemperature.Sweltering, BiomePrecipitation.Tropical, BiomeContour.Hilly, Rules, new Rnd(9)).Name;
            Assert.Equal(choose(), choose());
        }

        [Fact]
        public void ContourFollowsRoughnessThresholds()
        {
            Assert.Equal(BiomeContour.Lowlands, BiomeSelector.Contour(0.0, Rules));
            Assert.Equal(BiomeContour.Flat, BiomeSelector.Contour(Rules.ContourFlatFrom, Rules));
            Assert.Equal(BiomeContour.Hilly, BiomeSelector.Contour(Rules.ContourHillyFrom, Rules));
            Assert.Equal(BiomeContour.Jagged, BiomeSelector.Contour(Rules.ContourJaggedFrom, Rules));
            Assert.Equal(BiomeContour.Mountainous, BiomeSelector.Contour(Rules.ContourMountainousFrom, Rules));
            Assert.Equal(BiomeContour.Mountainous, BiomeSelector.Contour(5.0, Rules));
        }

        [Fact]
        public void NoBiomesIsAnError()
        {
            Assert.Throws<InvalidOperationException>(() => BiomeSelector.Choose(new List<Biome>(), BiomeTemperature.Cold, BiomePrecipitation.Dry, BiomeContour.Flat, Rules, new Rnd(1)));
        }
    }

    public class TerrainReliefDataTests
    {
        [Fact]
        public void ShippedTerrainTypesHaveRelief()
        {
            var types = new SimEngine(1).SimData.TerrainTypes.ToDictionary(t => t.Name);
            Assert.Equal(0, types["Flats"].Relief);
            Assert.Equal(2, types["Hills"].Relief);
            Assert.Equal(3, types["Cliffs"].Relief);
            Assert.Equal(4, types["Peaks"].Relief);
            Assert.All(types.Values, t => Assert.InRange(t.Relief, 0, 4));
        }

        [Fact]
        public void TerrainTypesWithoutAReliefColumnGetTheDefault()
        {
            string folder = Path.Combine(Path.GetTempPath(), "loregen-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                File.WriteAllLines(Path.Combine(folder, "TerrainTypes.csv"), new[]
                {
                    "Flats,1,Flat", "Hills,2,Slopes", "Cliffs,3,Jagged", "Peaks,4,High", "Marsh,5,Wet",
                    "Pond,6,Ponds", "Thickets,7,Shrubs", "Woodland,8,Trees", "Forest,9,Dense", "Wooded Slopes,10,Trees on slopes"
                });
                var engine = new SimEngine(1);
                engine.SimData.DataFolder = folder;
                engine.SimData.Initialize();
                Assert.All(engine.SimData.TerrainTypes, t => Assert.Equal(TerrainType.DefaultRelief, t.Relief));
            }
            finally { Directory.Delete(folder, true); }
        }
    }

    /// <summary>
    /// The noise generator's temperature, moisture, biomes and relief-biased terrain.
    /// </summary>
    public class NoiseClimateTests
    {
        private static SimEngine Generate(int seed, params (string Key, string Value)[] rules)
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

        private static List<WorldBlock> LandBlocks(World world)
        {
            return world.WorldBlock.ChildBlocksAsList().Where(b => b.Status.WaterStatus != WorldBlockWaterStatus.Water).ToList();
        }

        // ---- Fields ---------------------------------------------------------------------------

        [Fact]
        public void EveryBlockHasClimateInRange()
        {
            for (int seed = 1; seed <= 15; seed++)
            {
                var world = Generate(seed).World;
                foreach (var block in world.WorldBlock.ChildBlocksAsList(true))
                {
                    Assert.InRange(block.Status.Temperature.Value, 0.0, 1.0);
                    Assert.InRange(block.Status.Moisture.Value, 0.0, 1.0);
                }
            }
        }

        [Fact]
        public void SubBlocksShareTheirParentsClimate()
        {
            var world = Generate(3).World;
            var withDetail = world.WorldBlock.ChildBlocksAsList().Where(b => b.ChildBlocks != null).ToList();
            Assert.NotEmpty(withDetail);
            foreach (var block in withDetail)
                Assert.All(block.ChildBlocksAsList(), sub =>
                {
                    Assert.Equal(block.Status.Temperature, sub.Status.Temperature);
                    Assert.Equal(block.Status.Moisture, sub.Status.Moisture);
                });
        }

        [Fact]
        public void LegacyWorldsHaveNoClimate()
        {
            var engine = new SimEngine(3);
            engine.GenerateWorld();
            Assert.All(engine.World.WorldBlock.ChildBlocksAsList(true), b =>
            {
                Assert.Null(b.Status.Temperature);
                Assert.Null(b.Status.Moisture);
            });
        }

        [Fact]
        public void ItIsWarmestAtTheEquatorAndColdestAtThePoles()
        {
            double equator = 0, poles = 0;
            int worlds = 0;
            for (int seed = 1; seed <= 20; seed++)
            {
                var world = Generate(seed).World;
                int height = world.WorldBlock.Height;
                var land = LandBlocks(world);
                var middle = land.Where(b => b.Y >= height / 3 && b.Y < 2 * height / 3).ToList();
                var ends = land.Where(b => b.Y < height / 3 || b.Y >= 2 * height / 3).ToList();
                if (middle.Count < 10 || ends.Count < 10) continue;
                equator += middle.Average(b => b.Status.Temperature.Value);
                poles += ends.Average(b => b.Status.Temperature.Value);
                worlds++;
            }
            Assert.True(worlds >= 10);
            Assert.True(equator / worlds > poles / worlds + 0.15, "middle " + equator / worlds + " vs ends " + poles / worlds);
        }

        [Fact]
        public void HighGroundIsColderByTheLapseRate()
        {
            for (int seed = 1; seed <= 8; seed++)
            {
                var flat = Generate(seed, ("TemperatureLapse", "0")).World;
                var lapsed = Generate(seed, ("TemperatureLapse", "0.4")).World;
                int compared = 0;
                foreach (var pair in flat.WorldBlock.ChildBlocksAsList().Zip(lapsed.WorldBlock.ChildBlocksAsList(), (a, b) => (a, b)))
                {
                    double without = pair.a.Status.Temperature.Value, with = pair.b.Status.Temperature.Value;
                    if (pair.b.Status.WaterStatus == WorldBlockWaterStatus.Water || without <= 0.001 || with <= 0.001 || without >= 0.999) continue;
                    Assert.Equal(pair.b.Status.Elevation.Value * 0.4, without - with, 6);
                    compared++;
                }
                Assert.True(compared > 100);
            }
        }

        [Fact]
        public void ColdPlacesAreDrierThanWarmOnes()
        {
            var cold = new List<double>(); var warm = new List<double>();
            for (int seed = 1; seed <= 20; seed++)
                foreach (var b in LandBlocks(Generate(seed).World))
                {
                    var t = ClimateScale.Temperature(b.Status.Temperature.Value);
                    if (t <= BiomeTemperature.Cold) cold.Add(b.Status.Moisture.Value);
                    else if (t >= BiomeTemperature.Warm) warm.Add(b.Status.Moisture.Value);
                }
            Assert.True(warm.Average() > cold.Average() + 0.1, "warm " + warm.Average() + " vs cold " + cold.Average());
        }

        [Fact]
        public void LandNearWaterIsWetterThanLandFarFromIt()
        {
            var near = new List<double>(); var far = new List<double>();
            for (int seed = 1; seed <= 20; seed++)
            {
                var world = Generate(seed, ("MoistureColdPenalty", "0"), ("MoistureLatitudeWeight", "0")).World;
                foreach (var b in LandBlocks(world))
                {
                    if (b.Status.WaterStatus == WorldBlockWaterStatus.Coastline) near.Add(b.Status.Moisture.Value);
                    else if (!InsideRadius(b, 5)) far.Add(b.Status.Moisture.Value);
                }
            }
            Assert.NotEmpty(far);
            Assert.True(near.Average() > far.Average() + 0.05, "near " + near.Average() + " vs far " + far.Average());
        }

        // True if there is water within the given number of blocks (Chebyshev distance)
        private static bool InsideRadius(WorldBlock block, int radius)
        {
            var root = block.ParentBlock;
            for (int dx = -radius; dx <= radius; dx++)
                for (int dy = -radius; dy <= radius; dy++)
                {
                    int x = block.X + dx, y = block.Y + dy;
                    if (x < 0 || y < 0 || x >= root.Width || y >= root.Height) continue;
                    if (root.ChildBlocks[x, y].Status.WaterStatus == WorldBlockWaterStatus.Water) return true;
                }
            return false;
        }

        [Fact]
        public void MoistureUsesTheWholeRange()
        {
            for (int seed = 1; seed <= 15; seed++)
            {
                var land = LandBlocks(Generate(seed).World);
                if (land.Count < 100) continue;
                var moisture = land.Select(b => b.Status.Moisture.Value).OrderBy(m => m).ToList();
                Assert.True(moisture.First() < 0.05, "seed " + seed + ": driest " + moisture.First());
                Assert.True(moisture.Last() > 0.95, "seed " + seed + ": wettest " + moisture.Last());
            }
        }

        // ---- Biomes ---------------------------------------------------------------------------

        [Fact]
        public void BiomesFitTheirRegionsClimate()
        {
            int regions = 0, exactTemperature = 0, withinOne = 0;
            for (int seed = 1; seed <= 25; seed++)
            {
                foreach (var region in Generate(seed).World.Continents.SelectMany(c => c.Regions))
                {
                    double mean = region.RegionArea.BlocksList.Average(b => b.Status.Temperature.Value);
                    int gap = Math.Abs((int)ClimateScale.Temperature(mean) - (int)region.Biome.Temperature);
                    regions++;
                    if (gap == 0) exactTemperature++;
                    if (gap <= 1) withinOne++;
                }
            }
            Assert.True(exactTemperature / (double)regions > 0.7, "only " + exactTemperature + " of " + regions + " regions have a biome of their temperature");
            Assert.True(withinOne / (double)regions > 0.95, "only " + withinOne + " of " + regions + " regions are within one temperature class");
        }

        [Fact]
        public void PolarLandGetsColdBiomesAndTheTropicsGetWarmOnes()
        {
            var polar = new List<int>(); var tropical = new List<int>();
            for (int seed = 1; seed <= 25; seed++)
            {
                var world = Generate(seed).World;
                int height = world.WorldBlock.Height;
                foreach (var region in world.Continents.SelectMany(c => c.Regions))
                {
                    double row = region.RegionArea.BlocksList.Average(b => b.Y) / height;
                    if (row < 0.15 || row > 0.85) polar.Add((int)region.Biome.Temperature);
                    else if (row > 0.42 && row < 0.58) tropical.Add((int)region.Biome.Temperature);
                }
            }
            Assert.True(polar.Count >= 10 && tropical.Count >= 10, "Too few regions to judge: " + polar.Count + " polar, " + tropical.Count + " tropical");
            Assert.True(polar.Average() < 1.5, "polar biomes average temperature class " + polar.Average());
            Assert.True(tropical.Average() > 2.5, "tropical biomes average temperature class " + tropical.Average());
        }

        [Fact]
        public void DesertsAreDrierThanForests()
        {
            var moistureByBiome = new Dictionary<string, List<double>>();
            for (int seed = 1; seed <= 25; seed++)
                foreach (var region in Generate(seed).World.Continents.SelectMany(c => c.Regions))
                {
                    if (!moistureByBiome.ContainsKey(region.Biome.Name)) moistureByBiome[region.Biome.Name] = new List<double>();
                    moistureByBiome[region.Biome.Name].Add(region.RegionArea.BlocksList.Average(b => b.Status.Moisture.Value));
                }
            // Each is only checked if it came up at least a few times
            var deserts = moistureByBiome.Where(kv => kv.Key.Contains("Desert") && kv.Value.Count >= 3).SelectMany(kv => kv.Value).ToList();
            var forests = moistureByBiome.Where(kv => kv.Key.Contains("Forest") && kv.Value.Count >= 3).SelectMany(kv => kv.Value).ToList();
            Assert.NotEmpty(deserts);
            Assert.NotEmpty(forests);
            Assert.True(forests.Average() > deserts.Average() + 0.1, "forests " + forests.Average() + " vs deserts " + deserts.Average());
        }

        [Fact]
        public void WorldsUseAWideVarietyOfBiomes()
        {
            var all = new HashSet<string>();
            int variedWorlds = 0;
            for (int seed = 1; seed <= 40; seed++)
            {
                var biomes = Generate(seed).World.Continents.SelectMany(c => c.Regions).Select(r => r.Biome.Name).ToList();
                foreach (var name in biomes) all.Add(name);
                if (biomes.Distinct().Count() >= 5) variedWorlds++;
            }
            Assert.True(all.Count >= 25, "only " + all.Count + " biomes seen in 40 worlds");
            Assert.True(variedWorlds >= 30, "only " + variedWorlds + " of 40 worlds have 5 or more biomes");
        }

        // ---- Terrain --------------------------------------------------------------------------

        [Fact]
        public void HigherGroundHasSteeperTerrain()
        {
            Func<string, double> steepnessGap = strength =>
            {
                var high = new List<double>(); var low = new List<double>();
                for (int seed = 1; seed <= 15; seed++)
                    foreach (var b in LandBlocks(Generate(seed, ("TerrainReliefStrength", strength)).World))
                    {
                        if (b.Status.Elevation > 0.6) high.Add(b.Status.Terrain.TerrainType.Relief);
                        else if (b.Status.Elevation < 0.2) low.Add(b.Status.Terrain.TerrainType.Relief);
                    }
                Assert.True(high.Count > 30 && low.Count > 30);
                return high.Average() - low.Average();
            };

            double without = steepnessGap("0"), with = steepnessGap("0.6");
            Assert.True(with > without + 0.15, "gap with the bias " + with + " vs without " + without);
        }

        [Fact]
        public void TerrainStillComesFromTheRegionsBiome()
        {
            var world = Generate(4).World;
            foreach (var region in world.Continents.SelectMany(c => c.Regions))
            {
                var allowed = new HashSet<Terrain>(region.Biome.TerrainPattern.GetAllElements());
                Assert.All(region.RegionArea.BlocksList, b => Assert.Contains(b.Status.Terrain, allowed));
            }
        }

        [Fact]
        public void SubBlockTerrainFollowsTheirOwnHeight()
        {
            var world = Generate(6).World;
            var allowedByRegion = new Dictionary<Region, HashSet<Terrain>>();
            foreach (var block in world.WorldBlock.ChildBlocksAsList().Where(b => b.ChildBlocks != null))
                foreach (var sub in block.ChildBlocksAsList().Where(s => s.Status.WaterStatus != WorldBlockWaterStatus.Water))
                {
                    var region = sub.Status.Region;
                    if (!allowedByRegion.ContainsKey(region)) allowedByRegion[region] = new HashSet<Terrain>(region.Biome.TerrainPattern.GetAllElements());
                    Assert.Contains(sub.Status.Terrain, allowedByRegion[region]);
                }
        }

        // ---- Settings -------------------------------------------------------------------------

        [Theory]
        [InlineData("ClimateFrequency", "0")]
        [InlineData("ClimateOctaves", "0")]
        [InlineData("TemperatureLapse", "-0.1")]
        [InlineData("MoistureReach", "0")]
        [InlineData("MoistureColdPenalty", "1.5")]
        [InlineData("ContourHillyFrom", "0.01")]
        [InlineData("BiomeTemperatureWeight", "-1")]
        [InlineData("TerrainReliefStrength", "-1")]
        [InlineData("ElevationPerRelief", "0")]
        public void OutOfRangeClimateSettingsAreRejected(string key, string value)
        {
            var ex = Assert.Throws<InvalidDataException>(() => Generate(1, (key, value)));
            Assert.Contains(NoiseGeneratorRules.Filename, ex.Message);
        }

        [Fact]
        public void AllMoistureWeightsZeroIsRejected()
        {
            Assert.Throws<InvalidDataException>(() => Generate(1, ("MoistureNoiseWeight", "0"), ("MoistureCoastWeight", "0"), ("MoistureLatitudeWeight", "0")));
        }
    }
}
