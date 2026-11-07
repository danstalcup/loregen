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
    public class GeneratorOptionTests
    {
        [Fact]
        public void NumbersIntegersAndTogglesDescribeThemselves()
        {
            var number = GeneratorOption.Number("A", "Amount", "How much", "Group", 0, 1, 0.05, 0.3);
            Assert.Equal(GeneratorOptionKind.Number, number.Kind);
            Assert.Equal("0.35", number.Format(0.35));
            Assert.Equal("0.3", number.Format(0.3));

            var integer = GeneratorOption.Integer("B", "Count", null, null, 0, 12, 3);
            Assert.Equal(GeneratorOptionKind.Integer, integer.Kind);
            Assert.Equal(1, integer.Step);
            Assert.Equal("General", integer.Group);
            Assert.Equal(string.Empty, integer.Description);
            Assert.Equal("7", integer.Format(6.6));

            var toggle = GeneratorOption.Toggle("C", "Switch", "On or off", "Group", true);
            Assert.Equal(GeneratorOptionKind.Toggle, toggle.Kind);
            Assert.Equal(1, toggle.Default);
            Assert.Equal("On", toggle.Format(1));
            Assert.Equal("Off", toggle.Format(0));
        }

        [Fact]
        public void AutoLabelStandsInForZero()
        {
            var option = GeneratorOption.Number("A", "Land", "", "G", 0, 0.7, 0.01, 0, "random");
            Assert.Equal("random", option.Format(0));
            Assert.Equal("0.4", option.Format(0.4));
            Assert.Equal("0", GeneratorOption.Number("A", "Land", "", "G", 0, 0.7, 0.01, 0).Format(0));
        }

        [Fact]
        public void ClampKeepsValuesLegal()
        {
            var number = GeneratorOption.Number("A", "n", "", "G", 0, 1, 0.1, 0.5);
            Assert.Equal(1.0, number.Clamp(5));
            Assert.Equal(0.0, number.Clamp(-5));
            Assert.Equal(0.5, number.Clamp(double.NaN));
            Assert.Equal(0.37, number.Clamp(0.37));

            var integer = GeneratorOption.Integer("B", "n", "", "G", 0, 12, 3);
            Assert.Equal(7.0, integer.Clamp(6.6));
            Assert.Equal(12.0, integer.Clamp(99));

            var toggle = GeneratorOption.Toggle("C", "n", "", "G", false);
            Assert.Equal(1.0, toggle.Clamp(0.7));
            Assert.Equal(0.0, toggle.Clamp(0.2));
        }

        [Fact]
        public void NonsensicalDescriptorsAreRejected()
        {
            Assert.Throws<ArgumentException>(() => GeneratorOption.Number("", "n", "", "G", 0, 1, 0.1, 0.5));
            Assert.Throws<ArgumentException>(() => GeneratorOption.Number("A", " ", "", "G", 0, 1, 0.1, 0.5));
            Assert.Throws<ArgumentException>(() => GeneratorOption.Number("A", "n", "", "G", 1, 1, 0.1, 1));
            Assert.Throws<ArgumentException>(() => GeneratorOption.Number("A", "n", "", "G", 0, 1, 0, 0.5));
            Assert.Throws<ArgumentException>(() => GeneratorOption.Number("A", "n", "", "G", 0, 1, 0.1, 2));
        }
    }

    public class NoiseGeneratorOptionsTests
    {
        private static SimEngine Generate(int seed, params (string Key, double Value)[] settings)
        {
            var engine = new SimEngine(seed);
            engine.Configuration.GeneratorId = "noise";
            foreach (var setting in settings) engine.Configuration.GeneratorSettings[setting.Key] = setting.Value;
            engine.GenerateWorld();
            return engine;
        }

        private static IReadOnlyList<GeneratorOption> Options()
        {
            var engine = new SimEngine(1);
            return engine.Generators.Get("noise").GetOptions(engine.SimData);
        }

        private static double LandShare(World world)
        {
            var blocks = world.WorldBlock.ChildBlocksAsList();
            return blocks.Count(b => b.Status.WaterStatus != WorldBlockWaterStatus.Water) / (double)blocks.Count;
        }

        // ---- The descriptors ------------------------------------------------------------------

        [Fact]
        public void LegacyOffersNoOptions()
        {
            var engine = new SimEngine(1);
            Assert.Empty(engine.Generators.Get("legacy").GetOptions(engine.SimData));
        }

        [Fact]
        public void OptionsAreWellFormedAndMatchTheRulesFile()
        {
            var engine = new SimEngine(1);
            var file = engine.SimData.LoadRules(NoiseGeneratorRules.Filename);
            var options = Options();

            Assert.True(options.Count >= 10);
            Assert.Equal(options.Count, options.Select(o => o.Key.ToLowerInvariant()).Distinct().Count());
            foreach (var option in options)
            {
                Assert.True(file.Has(option.Key), option.Key + " is not a key in " + NoiseGeneratorRules.Filename);
                Assert.Equal(file.D(option.Key), option.Default);
                Assert.InRange(option.Default, option.Minimum, option.Maximum);
                Assert.False(string.IsNullOrWhiteSpace(option.Description), option.Key + " has no description");
            }
            Assert.True(options.Select(o => o.Group).Distinct().Count() >= 3);
        }

        [Fact]
        public void OptionDefaultsFollowCustomisedData()
        {
            string folder = Path.Combine(Path.GetTempPath(), "loregen-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var original = new StreamReader(typeof(SimEngine).Assembly.GetManifestResourceStream("LoreGen.Resources." + NoiseGeneratorRules.Filename))
                    .ReadToEnd().Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0);
                File.WriteAllLines(Path.Combine(folder, NoiseGeneratorRules.Filename), original.Select(l => l.StartsWith("WarpStrength,") ? "WarpStrength,1.1" : l));
                var engine = new SimEngine(1);
                engine.SimData.DataFolder = folder;
                Assert.Equal(1.1, engine.Generators.Get("noise").GetOptions(engine.SimData).Single(o => o.Key == "WarpStrength").Default);
            }
            finally { Directory.Delete(folder, true); }
        }

        // ---- Using them -----------------------------------------------------------------------

        [Fact]
        public void GivingEverySettingItsUsualValueChangesNothing()
        {
            // This is what makes it safe to build controls from the options: untouched controls leave worlds as they were
            var engine = new SimEngine(1);
            var usual = Options().Select(o => (o.Key, o.Default)).ToArray();
            foreach (int seed in new[] { 3, 8 })
                Assert.Equal(GeneratorContractTests.Describe(Generate(seed).World), GeneratorContractTests.Describe(Generate(seed, usual).World));
        }

        [Fact]
        public void LandShareCanBeSet()
        {
            foreach (double wanted in new[] { 0.2, 0.45 })
            {
                double average = Enumerable.Range(1, 8).Average(seed => LandShare(Generate(seed, ("LandFraction", wanted)).World));
                Assert.InRange(average, wanted - 0.07, wanted + 0.07);
            }
        }

        [Fact]
        public void MoreLandCentresSpreadTheLandOverMoreOfTheMap()
        {
            // The number of continents does NOT follow the number of centres (land share is fixed, so more centres makes
            // overlapping blobs that merge); what changes is how much of the map the land reaches
            Func<double, double> mapShareWithLand = centres => Enumerable.Range(1, 20).Average(seed =>
            {
                var root = Generate(seed, ("ContinentCentres", centres)).World.WorldBlock;
                var tiles = new HashSet<(int, int)>();
                foreach (var b in root.ChildBlocksAsList().Where(b => b.Status.WaterStatus != WorldBlockWaterStatus.Water))
                    tiles.Add((b.X * 6 / root.Width, b.Y * 6 / root.Height));
                return tiles.Count / 36.0;
            });
            double one = mapShareWithLand(1), many = mapShareWithLand(12);
            Assert.True(many > one + 0.05, "twelve centres reach " + many.ToString("P0") + " of the map, one reaches " + one.ToString("P0"));
        }

        [Fact]
        public void RegionCountCanBeSet()
        {
            Func<double, double> average = target => Enumerable.Range(1, 8).Average(seed => Generate(seed, ("RegionTarget", target)).World.Continents.Sum(c => c.Regions.Count));
            double few = average(15), many = average(100);
            Assert.InRange(few, 8, 25);          // near the target; very small regions are merged and every continent has at least one
            Assert.True(many > few * 3, many + " vs " + few);
        }

        [Fact]
        public void GlobalTemperatureShiftsTheClimate()
        {
            Func<double, double> meanTemperature = offset => Enumerable.Range(1, 8).Average(seed =>
                Generate(seed, ("TemperatureOffset", offset)).World.WorldBlock.ChildBlocksAsList()
                    .Where(b => b.Status.WaterStatus != WorldBlockWaterStatus.Water).Average(b => b.Status.Temperature.Value));
            Assert.True(meanTemperature(0.3) > meanTemperature(-0.3) + 0.3);
        }

        [Fact]
        public void TurningOffDetailRemovesSubBlocks()
        {
            var world = Generate(5, ("CoastDetail", 0), ("RegionBorderDetail", 0)).World;
            Assert.All(world.WorldBlock.ChildBlocksAsList(), b => Assert.Null(b.ChildBlocks));
        }

        [Fact]
        public void ChangingASettingChangesTheWorldButNotRepeatably()
        {
            var plain = GeneratorContractTests.Describe(Generate(7).World);
            var changed = GeneratorContractTests.Describe(Generate(7, ("WarpStrength", 1.4)).World);
            Assert.NotEqual(plain, changed);
            Assert.Equal(changed, GeneratorContractTests.Describe(Generate(7, ("WarpStrength", 1.4)).World));
        }

        // ---- Bad settings ---------------------------------------------------------------------

        [Fact]
        public void UnknownSettingsAreRejectedByName()
        {
            var ex = Assert.Throws<InvalidDataException>(() => Generate(1, ("Wobble", 1)));
            Assert.Contains("Wobble", ex.Message);
        }

        [Theory]
        [InlineData("LandFraction", 1.5)]
        [InlineData("LandFraction", -0.1)]
        [InlineData("ContinentCentres", 60)]
        [InlineData("RegionTarget", -3)]
        [InlineData("TemperatureOffset", 2)]
        public void OutOfRangeSettingsAreRejected(string key, double value)
        {
            var ex = Assert.Throws<InvalidDataException>(() => Generate(1, (key, value)));
            Assert.Contains(NoiseGeneratorRules.Filename, ex.Message);
        }

        [Fact]
        public void SettingNamesAreNotCaseSensitive()
        {
            Assert.Equal(GeneratorContractTests.Describe(Generate(2, ("landfraction", 0.4)).World), GeneratorContractTests.Describe(Generate(2, ("LandFraction", 0.4)).World));
        }

        [Fact]
        public void LegacyRejectsAnySetting()
        {
            var engine = new SimEngine(1);
            engine.Configuration.GeneratorSettings["LandFraction"] = 0.4;
            var ex = Assert.Throws<ArgumentException>(() => engine.GenerateWorld());
            Assert.Contains("LandFraction", ex.Message);
            Assert.Null(engine.World);
        }

        // ---- Recording and configuring --------------------------------------------------------

        [Fact]
        public void WorldsRecordTheSettingsTheyWereMadeWith()
        {
            var engine = Generate(4, ("LandFraction", 0.4), ("WarpStrength", 1));
            Assert.Equal(2, engine.World.GeneratorSettings.Count);
            Assert.Equal(0.4, engine.World.GeneratorSettings["LandFraction"]);

            // Later changes to the engine's settings don't rewrite history
            engine.Configuration.GeneratorSettings["LandFraction"] = 0.1;
            Assert.Equal(0.4, engine.World.GeneratorSettings["LandFraction"]);

            Assert.Empty(Generate(4).World.GeneratorSettings);
        }

        [Fact]
        public void SettingsCanBeGivenInTheConfigFile()
        {
            string folder = Path.Combine(Path.GetTempPath(), "loregen-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                string config = Path.Combine(folder, "loregen.config");
                File.WriteAllLines(config, new[] { "usepresetseed=true", "presetseed=6", "generatormode=noise", "setting.LandFraction=0.4", "setting.CoastDetail = 0" });
                var engine = new SimEngine(config);
                Assert.Equal(0.4, engine.Configuration.GeneratorSettings["LandFraction"]);
                Assert.Equal(0.0, engine.Configuration.GeneratorSettings["CoastDetail"]);
                engine.GenerateWorld();
                Assert.Equal(0.4, engine.World.GeneratorSettings["LandFraction"]);
            }
            finally { Directory.Delete(folder, true); }
        }
    }
}
