using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LoreGen.Features;
using LoreGen.Simulation;
using LoreGen.Terrains;
using LoreGen.WorldGen;
using LoreGen.WorldGen.Generators;
using Xunit;

namespace LoreGen.Tests
{
    /// <summary>
    /// How the elevation-based generators choose terrain from height, climate, water, features and plate boundaries, and
    /// how regions follow it.
    /// </summary>
    public class TerrainModelTests
    {
        private static TerrainModel Model(params (string Key, double Value)[] settings)
        {
            var engine = new SimEngine(1);
            var overrides = settings.ToDictionary(s => s.Key, s => s.Value);
            return new TerrainModel(engine, NoiseGeneratorRules.Load(engine.SimData, overrides));
        }

        private static TerrainSite Site(double elevation = 0.1, double temperature = 0.5, double moisture = 0.5, double hydration = -1,
            PlateBoundaryKind boundary = PlateBoundaryKind.None)
        {
            return new TerrainSite
            {
                Elevation = elevation, Temperature = temperature, Moisture = moisture,
                Hydration = hydration < 0 ? moisture : hydration, Boundary = boundary, Features = Array.Empty<GeographicFeature>()
            };
        }

        // Average of a terrain-type property over a place's terrain mix
        private static double Expected(TerrainModel model, TerrainSite site, Func<TerrainType, double> property)
        {
            var weights = model.Weights(site);
            return weights.Sum(w => w.Weight * property(w.Terrain.TerrainType)) / weights.Sum(w => w.Weight);
        }

        private static IEnumerable<WorldBlock> LandBlocks(World world)
        {
            return world.WorldBlock.ChildBlocksAsList().Where(b => b.Status.Terrain != null);
        }

        private static IEnumerable<WorldBlock> LandSubBlocks(World world)
        {
            return world.WorldBlock.ChildBlocksAsList().Where(b => b.ChildBlocks != null)
                .SelectMany(b => b.ChildBlocksAsList()).Where(s => s.Status.Terrain != null);
        }

        // ---- Data ----------------------------------------------------------------------------

        [Fact]
        public void ShippedClimateTypesHaveIdealClimates()
        {
            var types = new SimEngine(1).SimData.ClimateTypes.ToDictionary(t => t.Name);
            Assert.True(types["Ice"].IdealTemperature < types["Grassy"].IdealTemperature);
            Assert.True(types["Arid"].IdealTemperature > types["Grassy"].IdealTemperature);
            Assert.True(types["Lush"].IdealMoisture > types["Sandy"].IdealMoisture);
            Assert.All(types.Values, t => { Assert.InRange(t.IdealTemperature, 0, 1); Assert.InRange(t.IdealMoisture, 0, 1); });
        }

        [Fact]
        public void ShippedTerrainTypesHaveWetnessAndTrees()
        {
            var types = new SimEngine(1).SimData.TerrainTypes.ToDictionary(t => t.Name);
            Assert.Equal(1, types["Marsh"].Wetness);
            Assert.Equal(1, types["Forest"].Trees);
            Assert.Equal(0, types["Flats"].Wetness);
            Assert.Equal(0, types["Peaks"].Trees);
            Assert.All(types.Values, t => { Assert.InRange(t.Wetness, 0, 1); Assert.InRange(t.Trees, 0, 1); });
        }

        [Fact]
        public void ShippedInfluencesNameRealFeatureTypes()
        {
            var data = new SimEngine(1).SimData;
            var influences = TerrainInfluence.Parse(data.LoadLines(TerrainInfluence.Filename), data);
            Assert.Equal(4, influences["Peak"].ReliefMin);
            Assert.True(influences["Plate:Convergent"].ReliefAdd > 0);
            Assert.True(influences["Oasis"].WetMin > 0.5);
        }

        [Theory]
        [InlineData("Kraken,,,,,,,,,")]                       // not a feature type
        [InlineData("Plate:Sideways,,,,,,,,,")]               // not a kind of boundary
        [InlineData("Peak,,high,,,,,,,")]                     // not a number
        [InlineData("Peak,,4")]                               // too few columns
        public void BadInfluencesAreRejected(string line)
        {
            var data = new SimEngine(1).SimData;
            var ex = Assert.Throws<InvalidDataException>(() => TerrainInfluence.Parse(new[] { line }, data));
            Assert.Contains(TerrainInfluence.Filename, ex.Message);
        }

        [Fact]
        public void DuplicateInfluencesAreRejected()
        {
            var data = new SimEngine(1).SimData;
            Assert.Throws<InvalidDataException>(() => TerrainInfluence.Parse(new[] { "Peak,,4,,,,,,,", "peak,,3,,,,,,," }, data));
        }

        // ---- The model -----------------------------------------------------------------------

        [Fact]
        public void HigherGroundIsSteeper()
        {
            var model = Model();
            Assert.True(Expected(model, Site(elevation: 0.9), t => t.Relief) > Expected(model, Site(elevation: 0.05), t => t.Relief) + 1.5);
        }

        [Fact]
        public void PlatesPushingTogetherMakeTheLandMoreRugged()
        {
            var model = Model();
            double plain = model.Targets(Site(elevation: 0.3)).Relief;
            Assert.True(model.Targets(Site(elevation: 0.3, boundary: PlateBoundaryKind.Convergent)).Relief > plain + 0.5);
            Assert.True(model.Targets(Site(elevation: 0.3, boundary: PlateBoundaryKind.Divergent)).Relief > plain);
        }

        [Fact]
        public void WetFlatGroundIsMarshyButWetSlopesAreNot()
        {
            var model = Model();
            Assert.True(model.Targets(Site(elevation: 0.05, hydration: 0.95)).Wet > 0.5);
            Assert.Equal(0, model.Targets(Site(elevation: 0.05, hydration: 0.4)).Wet);
            Assert.Equal(0, model.Targets(Site(elevation: 0.8, hydration: 0.95)).Wet);
            Assert.True(Expected(model, Site(elevation: 0.05, moisture: 0.6, hydration: 0.95), t => t.Wetness)
                      > Expected(model, Site(elevation: 0.05, moisture: 0.6, hydration: 0.4), t => t.Wetness) + 0.2);
        }

        [Fact]
        public void TreesNeedWaterAndWarmthAndStopAtTheTreeLine()
        {
            var model = Model();
            Assert.True(model.Targets(Site(temperature: 0.6, moisture: 0.8)).Trees > 0.8);
            Assert.Equal(0, model.Targets(Site(temperature: 0.6, moisture: 0.1)).Trees);
            Assert.Equal(0, model.Targets(Site(temperature: 0.05, moisture: 0.8)).Trees);
            Assert.Equal(0, model.Targets(Site(elevation: 1, temperature: 0.6, moisture: 0.8)).Trees);
        }

        [Fact]
        public void ClimateComesFromTemperatureAndMoisture()
        {
            var model = Model();
            Func<TerrainSite, double> coldShare = site => model.Weights(site).Where(w => w.Terrain.Climate.PrimaryClimateType.Name == "Ice").Sum(w => w.Weight) / model.Weights(site).Sum(w => w.Weight);
            Func<TerrainSite, double> dryShare = site => model.Weights(site)
                .Where(w => w.Terrain.Climate.PrimaryClimateType.Name == "Sandy" || w.Terrain.Climate.PrimaryClimateType.Name == "Arid").Sum(w => w.Weight) / model.Weights(site).Sum(w => w.Weight);
            Assert.True(coldShare(Site(temperature: 0.05)) > 0.5);
            Assert.True(coldShare(Site(temperature: 0.7)) < 0.05);
            Assert.True(dryShare(Site(temperature: 0.75, moisture: 0.05)) > 0.5);
            Assert.True(dryShare(Site(temperature: 0.75, moisture: 0.8)) < 0.1);
        }

        [Fact]
        public void HeightCanBeIgnored()
        {
            var model = Model(("TerrainReliefStrength", 0));
            // (Dry, so that height does not change the tree cover wanted either)
            Assert.Equal(Expected(model, Site(elevation: 0.05, moisture: 0.1), t => t.Relief), Expected(model, Site(elevation: 0.9, moisture: 0.1), t => t.Relief), 6);
        }

        // ---- Worlds --------------------------------------------------------------------------

        [Fact]
        public void TopLevelTerrainFollowsMoisture()
        {
            var wet = new List<double>(); var dry = new List<double>();
            for (int seed = 1; seed <= 10; seed++)
                foreach (var b in LandBlocks(NoiseTestWorlds.Generate(seed).World))
                {
                    if (b.Status.Temperature < 0.3) continue;
                    if (b.Status.Moisture > 0.65) wet.Add(b.Status.Terrain.TerrainType.Trees);
                    else if (b.Status.Moisture < 0.2) dry.Add(b.Status.Terrain.TerrainType.Trees);
                }
            Assert.True(wet.Count > 20 && dry.Count > 20, wet.Count + " wet and " + dry.Count + " dry blocks");
            Assert.True(wet.Average() > dry.Average() + 0.3, "wet " + wet.Average() + " vs dry " + dry.Average());
        }

        [Fact]
        public void FeaturesShapeTheTerrainOnThem()
        {
            var relief = new Dictionary<string, List<int>>();
            var trees = new Dictionary<string, List<double>>();
            var wetness = new Dictionary<string, List<double>>();
            var all = new List<double>();
            for (int seed = 1; seed <= 8; seed++)
            {
                var world = NoiseTestWorlds.Generate(seed).World;
                foreach (var sub in LandSubBlocks(world))
                {
                    var type = sub.Status.Terrain.TerrainType;
                    all.Add(type.Wetness + type.Trees);
                    foreach (var feature in world.FeaturesAt(sub))
                    {
                        string id = feature.Type.Id;
                        if (!relief.ContainsKey(id)) { relief[id] = new List<int>(); trees[id] = new List<double>(); wetness[id] = new List<double>(); }
                        relief[id].Add(type.Relief); trees[id].Add(type.Trees); wetness[id].Add(type.Wetness);
                    }
                }
            }
            foreach (var id in new[] { "Peak", "Erg", "SaltFlat", "Oasis", "River" }) Assert.True(relief.ContainsKey(id) && relief[id].Count > 0, "no " + id + " seen");

            Assert.True(relief["Peak"].Average(r => r >= 3 ? 1.0 : 0.0) > 0.9, "peaks are not all cliffs and peaks");
            Assert.True(relief["MountainRange"].Average() > 2.2, "mountain ranges average relief " + relief["MountainRange"].Average());
            Assert.True(relief["Erg"].Average(r => r >= 1 && r <= 2 ? 1.0 : 0.0) > 0.9, "ergs are not dunes");
            Assert.True(trees["Erg"].Average() < 0.05 && trees["SaltFlat"].Average() < 0.05, "trees in ergs or on salt flats");
            Assert.True(relief["SaltFlat"].Average() < 0.6, "salt flats average relief " + relief["SaltFlat"].Average());
            Assert.True(wetness["Oasis"].Average() > 0.4, "oases are dry: " + wetness["Oasis"].Average());
            double river = wetness["River"].Zip(trees["River"], (w, t) => w + t).Average();
            Assert.True(river > all.Average() + 0.1, "river banks " + river + " vs everywhere " + all.Average());
        }

        [Fact]
        public void RiversAndLakesWetTheGroundAroundThem()
        {
            Func<(string, string)[], double> wetShare = rules => Enumerable.Range(1, 6).Average(seed =>
                LandBlocks(NoiseTestWorlds.Generate(seed, rules).World).Average(b => b.Status.Terrain.TerrainType.Wetness + b.Status.Terrain.TerrainType.Trees));
            double with = wetShare(new (string, string)[0]);
            double without = wetShare(new[] { ("RiverHydration", "0"), ("LakeHydration", "0") });
            Assert.True(with > without, "with river and lake water " + with + " vs without " + without);
        }

        [Fact]
        public void TerrainComesInPatches()
        {
            Func<string, double> sameAsNeighbour = size =>
            {
                long pairs = 0, same = 0;
                for (int seed = 1; seed <= 6; seed++)
                    foreach (var parent in NoiseTestWorlds.Generate(seed, ("TerrainPatchSize", size)).World.WorldBlock.ChildBlocksAsList().Where(b => b.ChildBlocks != null))
                        for (int i = 0; i + 1 < 10; i++)
                            for (int j = 0; j < 10; j++)
                            {
                                var a = parent.ChildBlocks[i, j].Status.Terrain; var b = parent.ChildBlocks[i + 1, j].Status.Terrain;
                                if (a == null || b == null) continue;
                                pairs++; if (ReferenceEquals(a, b)) same++;
                            }
                return same / (double)pairs;
            };
            double patchy = sameAsNeighbour("4"), speckled = sameAsNeighbour("0");
            Assert.True(patchy > speckled + 0.1, "patches " + patchy.ToString("P0") + " vs independent draws " + speckled.ToString("P0"));
        }

        [Fact]
        public void LandscapesAreEvenedOutAtTheTopLevel()
        {
            Func<string, double> sameAsNeighbour = smoothing =>
            {
                long pairs = 0, same = 0;
                for (int seed = 1; seed <= 6; seed++)
                    foreach (var b in LandBlocks(NoiseTestWorlds.Generate(seed, ("TerrainSmoothing", smoothing)).World))
                        foreach (var n in b.SurroundingBlocksAsList().Where(n => n.Status.Terrain != null))
                        {
                            pairs++;
                            if (RegionPartitioner.Landscape(b.Status.Terrain) == RegionPartitioner.Landscape(n.Status.Terrain)) same++;
                        }
                return same / (double)pairs;
            };
            Assert.True(sameAsNeighbour("0.75") > sameAsNeighbour("0") + 0.05);
        }

        [Fact]
        public void TectonicWorldsGetTerrainEverywhereToo()
        {
            var engine = new SimEngine(5);
            engine.Configuration.GeneratorId = TectonicWorldGenerator.GeneratorId;
            engine.GenerateWorld();
            var land = engine.World.WorldBlock.ChildBlocksAsList().Where(b => b.Status.WaterStatus != WorldBlockWaterStatus.Water).ToList();
            Assert.All(land, b => Assert.NotNull(b.Status.Terrain));
            Assert.All(land.Where(b => b.ChildBlocks != null).SelectMany(b => b.ChildBlocksAsList()).Where(s => s.Status.WaterStatus != WorldBlockWaterStatus.Water),
                s => Assert.NotNull(s.Status.Terrain));
        }

        // ---- Regions -------------------------------------------------------------------------

        [Fact]
        public void LandscapesAreComparedByClimateRuggednessAndCover()
        {
            var data = new SimEngine(1).SimData;
            Terrain Named(string name) => data.Terrains.First(t => t.Name == name);
            int plains = RegionPartitioner.Landscape(Named("Grassy Plains")), hills = RegionPartitioner.Landscape(Named("Grassy Hills"));
            int forest = RegionPartitioner.Landscape(Named("Grassy Forest")), dunes = RegionPartitioner.Landscape(Named("Dunes"));
            Assert.Equal(0, RegionPartitioner.LandscapeDifference(plains, plains));
            Assert.Equal(0.5, RegionPartitioner.LandscapeDifference(plains, hills));
            Assert.Equal(0.5, RegionPartitioner.LandscapeDifference(plains, forest));
            Assert.True(RegionPartitioner.LandscapeDifference(plains, dunes) >= 1);
        }

        [Fact]
        public void RegionsFollowTheLandscape()
        {
            // How much more often the landscape changes across region borders than inside regions
            Func<string, double> contrast = weight =>
            {
                long border = 0, borderChange = 0, inner = 0, innerChange = 0;
                for (int seed = 1; seed <= 10; seed++)
                    foreach (var b in LandBlocks(NoiseTestWorlds.Generate(seed, ("RegionTerrainWeight", weight)).World))
                        foreach (var n in b.SurroundingBlocksAsList().Where(n => n.Status.Terrain != null))
                        {
                            bool change = RegionPartitioner.Landscape(b.Status.Terrain) != RegionPartitioner.Landscape(n.Status.Terrain);
                            if (ReferenceEquals(b.Status.Region, n.Status.Region)) { inner++; if (change) innerChange++; }
                            else { border++; if (change) borderChange++; }
                        }
                return (borderChange / (double)border) / (innerChange / (double)inner);
            };
            double following = contrast("4"), ignoring = contrast("0");
            Assert.True(following > 1.5, "landscape changes only " + following.ToString("0.00") + "x as often across borders");
            Assert.True(following > ignoring + 0.15, following.ToString("0.00") + " following the landscape vs " + ignoring.ToString("0.00") + " ignoring it");
        }

        [Fact]
        public void RegionCountStaysNearTheTarget()
        {
            Func<string, double> average = target => Enumerable.Range(1, 6).Average(seed => NoiseTestWorlds.Generate(seed, ("RegionTarget", target)).World.Continents.Sum(c => c.Regions.Count));
            Assert.InRange(average("40"), 28, 44);
            Assert.InRange(average("100"), 50, 105);
        }

        [Fact]
        public void ZonedPartitionCoversEveryBlockOnceAndEachGroupIsConnected()
        {
            var rules = NoiseGeneratorRules.Load(new SimEngine(1).SimData);
            for (int seed = 1; seed <= 8; seed++)
            {
                var world = NoiseTestWorlds.Generate(seed).World;
                var blocks = world.Continents.OrderByDescending(c => c.ContinentArea.Area()).First().ContinentArea.Blocks().ToList();
                var landscapes = blocks.ToDictionary(b => b, b => RegionPartitioner.Landscape(b.Status.Terrain));
                foreach (int count in new[] { 1, 5, 12 })
                {
                    if (count > blocks.Count) continue;
                    var groups = RegionPartitioner.Partition(blocks, count, rules, new Randomizer.Rnd(seed), null, landscapes);
                    Assert.Equal(blocks.Count, groups.Sum(g => g.Count));
                    Assert.Equal(blocks.Count, groups.SelectMany(g => g).Distinct().Count());
                    Assert.All(groups, g => Assert.Equal(1, new WorldBlockArea(g.ToList(), null).ContiguousAreas().Count));
                }
            }
        }

        [Fact]
        public void ZonesAreConnectedStretchesOfOneLandscape()
        {
            var world = NoiseTestWorlds.Generate(3).World;
            var blocks = world.Continents.OrderByDescending(c => c.ContinentArea.Area()).First().ContinentArea.Blocks().ToList();
            var landscapes = blocks.ToDictionary(b => b, b => RegionPartitioner.Landscape(b.Status.Terrain));
            var zones = RegionPartitioner.Zones(blocks, null, landscapes);
            Assert.Equal(blocks.Count, zones.Sum(z => z.Count));
            Assert.All(zones, z => Assert.Single(z.Select(b => landscapes[b]).Distinct()));
            Assert.All(zones, z => Assert.Equal(1, new WorldBlockArea(z.ToList(), null).ContiguousAreas().Count));
            for (int i = 1; i < zones.Count; i++) Assert.True(zones[i - 1].Count >= zones[i].Count);
        }

        // ---- Settings ------------------------------------------------------------------------

        [Theory]
        [InlineData("TerrainClimateSpread", "0")]
        [InlineData("TerrainPatchSize", "-1")]
        [InlineData("TerrainSmoothing", "1.5")]
        [InlineData("TerrainWetStrength", "-1")]
        [InlineData("TerrainTreeStrength", "-1")]
        [InlineData("WetlandFrom", "1")]
        [InlineData("TreeMoistureFrom", "0.9")]
        [InlineData("TreeTemperatureFrom", "2")]
        [InlineData("TreeLineRelief", "-1")]
        [InlineData("RiverHydration", "2")]
        [InlineData("LakeHydration", "-0.1")]
        [InlineData("RegionTerrainWeight", "-1")]
        [InlineData("RegionZoneMinBlocks", "0")]
        [InlineData("BiomeTerrainWeight", "-1")]
        public void OutOfRangeTerrainSettingsAreRejected(string key, string value)
        {
            var ex = Assert.Throws<InvalidDataException>(() => NoiseTestWorlds.Generate(1, (key, value)));
            Assert.Contains(NoiseGeneratorRules.Filename, ex.Message);
        }

        [Fact]
        public void PlateBoundariesFadeOutRatherThanStoppingAtBlockEdges()
        {
            // Boundaries are recorded on 1000 km blocks; at 100 km, ruggedness should not change much more often across the
            // edge of a boundary block than anywhere else (it was about three times as often before boundaries were blended)
            int lineChanges = 0, linePairs = 0, insideChanges = 0, insidePairs = 0;
            foreach (int seed in new[] { 5, 8, 11 })
            {
                var engine = new SimEngine(seed);
                engine.Configuration.GeneratorId = TectonicWorldGenerator.GeneratorId;
                engine.GenerateWorld();
                var top = engine.World.WorldBlock.ChildBlocks;
                PlateBoundaryKind Kind(WorldBlock t) => t.Status.Plate?.BoundaryAt(t) ?? PlateBoundaryKind.None;
                int width = top.GetLength(0) * 10, height = top.GetLength(1) * 10;
                WorldBlock At(int x, int y) => top[x / 10, y / 10].ChildBlocks?[x % 10, y % 10];
                for (int x = 0; x < width; x++)
                    for (int y = 0; y < height; y++)
                        foreach (var (dx, dy) in new[] { (1, 0), (0, 1) })
                        {
                            int a = x + dx, b = y + dy;
                            if (a >= width || b >= height) continue;
                            var p = At(x, y);
                            var q = At(a, b);
                            if (p?.Status.Terrain == null || q?.Status.Terrain == null) continue;
                            bool change = Math.Abs(p.Status.Terrain.TerrainType.Relief - q.Status.Terrain.TerrainType.Relief) >= 1;
                            bool line = (dx == 1 && a % 10 == 0) || (dy == 1 && b % 10 == 0);
                            if (line && Kind(p.ParentBlock) != Kind(q.ParentBlock)) { linePairs++; if (change) lineChanges++; }
                            else if (!line) { insidePairs++; if (change) insideChanges++; }
                        }
            }
            double ratio = ((double)lineChanges / linePairs) / ((double)insideChanges / insidePairs);
            Assert.True(ratio < 2.4, $"ruggedness changes {ratio:0.00} times as often where a plate boundary starts or stops");
        }

        [Fact]
        public void TerrainBordersAtFineDetailDoNotRunAlongTheTopLevelGrid()
        {
            // Climate is read at nudged positions, so where it crosses a threshold (trees, marsh, a kind of climate) the
            // border wanders instead of running straight along the 1000 km grid. Measured as the share of landscape-border
            // length in straight runs of six or more 100 km blocks (it was about 12% before the nudge, about 7% after)
            int total = 0, straight = 0;
            foreach (int seed in new[] { 5, 8 })
            {
                var engine = new SimEngine(seed);
                engine.Configuration.GeneratorId = TectonicWorldGenerator.GeneratorId;
                engine.GenerateWorld();
                var top = engine.World.WorldBlock.ChildBlocks;
                int width = top.GetLength(0) * 10, height = top.GetLength(1) * 10;
                WorldBlock At(int x, int y) => top[x / 10, y / 10].ChildBlocks?[x % 10, y % 10];
                bool Differ(int x, int y, int a, int b)
                {
                    var p = At(x, y)?.Status.Terrain;
                    var q = At(a, b)?.Status.Terrain;
                    return p != null && q != null && RegionPartitioner.Landscape(p) != RegionPartitioner.Landscape(q);
                }
                for (int x = 0; x < width - 1; x++)
                {
                    int run = 0;
                    for (int y = 0; y <= height; y++)
                    {
                        if (y < height && Differ(x, y, x + 1, y)) { run++; total++; }
                        else { if (run >= 6) straight += run; run = 0; }
                    }
                }
                for (int y = 0; y < height - 1; y++)
                {
                    int run = 0;
                    for (int x = 0; x <= width; x++)
                    {
                        if (x < width && Differ(x, y, x, y + 1)) { run++; total++; }
                        else { if (run >= 6) straight += run; run = 0; }
                    }
                }
            }
            double share = (double)straight / total;
            Assert.True(share < 0.1, $"{share:P1} of landscape borders run straight for 6 or more blocks");
        }
    }
}
