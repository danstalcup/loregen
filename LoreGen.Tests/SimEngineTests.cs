using System.Linq;
using LoreGen.Simulation;
using LoreGen.WorldGen;
using Xunit;

namespace LoreGen.Tests
{
    public class SimEngineTests
    {
        [Fact]
        public void LoadsEmbeddedData()
        {
            var engine = new SimEngine(1234);
            Assert.NotEmpty(engine.SimData.Biomes);
            Assert.NotEmpty(engine.SimData.Terrains);
            Assert.NotEmpty(engine.SimData.ClimateTypes);
            Assert.NotEmpty(engine.SimData.LetterSounds);
        }

        [Fact]
        public void GeneratesWorld()
        {
            var engine = new SimEngine(92212);
            engine.GenerateWorld();
            Assert.NotEmpty(engine.World.Continents);
            Assert.NotNull(engine.World.WorldBlock.ChildBlocks);
        }

        [Fact]
        public void SameSeedGivesSameWorld()
        {
            var a = new SimEngine(777); a.GenerateWorld();
            var b = new SimEngine(777); b.GenerateWorld();
            Assert.Equal(a.World.Continents.Select(c => c.Name), b.World.Continents.Select(c => c.Name));
        }

        // ---- Keyed rules files ----------------------------------------------------------------

        private static SimEngine EngineWithRulesFile(string filename, string[] lines, out string folder)
        {
            folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "loregen-test-" + System.Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(folder);
            System.IO.File.WriteAllLines(System.IO.Path.Combine(folder, filename), lines);
            var engine = new SimEngine(1);
            engine.SimData.DataFolder = folder;
            return engine;
        }

        [Fact]
        public void RuleRowOrderDoesNotMatter()
        {
            var baseline = new SimEngine(1).SimData.WorldRules;
            var embedded = typeof(SimEngine).Assembly.GetManifestResourceStream("LoreGen.Resources.WorldRules.csv");
            var lines = new System.IO.StreamReader(embedded).ReadToEnd()
                .Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).Reverse().ToArray();

            var engine = EngineWithRulesFile("WorldRules.csv", lines, out var folder);
            try
            {
                engine.SimData.InitializeWorldRules();
                Assert.Equal(baseline.ContinentsMax, engine.SimData.WorldRules.ContinentsMax);
                Assert.Equal(baseline.WorldHeightLengthRatio, engine.SimData.WorldRules.WorldHeightLengthRatio);
                Assert.Equal(baseline.ContinentCoastErosionMax, engine.SimData.WorldRules.ContinentCoastErosionMax);
            }
            finally { System.IO.Directory.Delete(folder, true); }
        }

        [Fact]
        public void MissingRuleKeyIsReportedByName()
        {
            var engine = EngineWithRulesFile("TerrainRules.csv", new[] { "# nothing here" }, out var folder);
            try
            {
                var ex = Assert.Throws<System.IO.InvalidDataException>(() => engine.SimData.InitializeTerrainRules());
                Assert.Contains("CombinationWeightReducer", ex.Message);
            }
            finally { System.IO.Directory.Delete(folder, true); }
        }

        [Fact]
        public void UnrecognizedRuleKeyIsRejected()
        {
            var engine = EngineWithRulesFile("TerrainRules.csv", new[] { "CombinationWeightReducer,4.5", "CombinationWeightReduser,3" }, out var folder);
            try
            {
                var ex = Assert.Throws<System.IO.InvalidDataException>(() => engine.SimData.InitializeTerrainRules());
                Assert.Contains("CombinationWeightReduser", ex.Message);
            }
            finally { System.IO.Directory.Delete(folder, true); }
        }

        // ---- Generator mode -------------------------------------------------------------------

        [Fact]
        public void LegacyGeneratorIsTheDefault()
        {
            var engine = new SimEngine(777);
            Assert.Equal(WorldGeneratorMode.Legacy, engine.Configuration.GeneratorMode);
            engine.GenerateWorld();
            Assert.NotEmpty(engine.World.Continents);
        }

        [Fact]
        public void UnimplementedGeneratorFailsWithoutLeavingAHalfBuiltWorld()
        {
            var engine = new SimEngine(777);
            engine.Configuration.GeneratorMode = WorldGeneratorMode.Noise;
            Assert.Throws<System.NotImplementedException>(() => engine.GenerateWorld());
            Assert.Null(engine.World);
        }

        [Fact]
        public void GeneratesLanguageNames()
        {
            var engine = new SimEngine(5);
            var lang = engine.Language.GetRandomLanguage();
            Assert.False(string.IsNullOrWhiteSpace(lang.GetName()));
        }
    }
}
