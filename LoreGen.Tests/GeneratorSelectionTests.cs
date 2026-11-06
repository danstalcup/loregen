using System;
using System.IO;
using System.Linq;
using LoreGen.Simulation;
using LoreGen.WorldGen;
using LoreGen.WorldGen.Generators;
using Xunit;

namespace LoreGen.Tests
{
    public class GeneratorSelectionTests
    {
        /// <summary>A generator that builds nothing but an empty world, to test selection without a real algorithm.</summary>
        private class StubGenerator : IWorldGenerator
        {
            private readonly string id;
            public int Calls;
            public StubGenerator(string id = "stub") { this.id = id; }
            public string Id { get { return id; } }
            public string DisplayName { get { return "Stub"; } }
            public string Description { get { return "Test double"; } }
            public string Version { get { return "9"; } }
            public World Generate(SimEngine engine) { Calls++; return new World(engine); }
        }

        private class FailingGenerator : IWorldGenerator
        {
            public string Id { get { return "failing"; } }
            public string DisplayName { get { return "Failing"; } }
            public string Description { get { return "Always fails"; } }
            public string Version { get { return "1"; } }
            public World Generate(SimEngine engine) { throw new InvalidOperationException("kaboom"); }
        }

        // ---- Engine selection -----------------------------------------------------------------

        [Fact]
        public void LegacyIsTheDefaultGenerator()
        {
            var engine = new SimEngine(777);
            Assert.Equal("legacy", engine.Configuration.GeneratorId);
            engine.GenerateWorld();
            Assert.Equal("legacy", engine.World.GeneratorId);
            Assert.NotEmpty(engine.World.Continents);
        }

        [Fact]
        public void GeneratorIsChosenByIdCaseInsensitively()
        {
            var engine = new SimEngine(1);
            var stub = new StubGenerator("stub");
            engine.Generators.Register(stub);
            engine.Configuration.GeneratorId = "STUB";
            engine.GenerateWorld();
            Assert.Equal(1, stub.Calls);
            Assert.Equal("stub", engine.World.GeneratorId);
            Assert.Equal("9", engine.World.GeneratorVersion);
        }

        [Fact]
        public void InjectedGeneratorOverridesConfiguration()
        {
            var engine = new SimEngine(1);
            engine.Configuration.GeneratorId = "does-not-exist";
            var stub = new StubGenerator();
            engine.WorldGenerator = stub;
            engine.GenerateWorld();
            Assert.Equal(1, stub.Calls);
        }

        [Fact]
        public void UnknownGeneratorIdListsTheAvailableOnes()
        {
            var engine = new SimEngine(1);
            engine.Configuration.GeneratorId = "teleporting-castles";
            var ex = Assert.Throws<ArgumentException>(() => engine.GenerateWorld());
            Assert.Contains("teleporting-castles", ex.Message);
            Assert.Contains("legacy", ex.Message);
            Assert.Null(engine.World);
        }

        [Fact]
        public void FailedGenerationKeepsThePreviousWorld()
        {
            var engine = new SimEngine(777);
            engine.GenerateWorld();
            var before = engine.World;

            engine.WorldGenerator = new FailingGenerator();
            Assert.Throws<InvalidOperationException>(() => engine.GenerateWorld());
            Assert.Same(before, engine.World);
        }

        [Fact]
        public void FailedFirstGenerationLeavesNoWorld()
        {
            var engine = new SimEngine(777);
            engine.WorldGenerator = new FailingGenerator();
            Assert.Throws<InvalidOperationException>(() => engine.GenerateWorld());
            Assert.Null(engine.World);
        }

        [Fact]
        public void GeneratorReturningNothingIsAnError()
        {
            var engine = new SimEngine(1);
            engine.WorldGenerator = new NullReturningGenerator();
            Assert.Throws<InvalidOperationException>(() => engine.GenerateWorld());
            Assert.Null(engine.World);
        }

        private class NullReturningGenerator : IWorldGenerator
        {
            public string Id { get { return "null-returning"; } }
            public string DisplayName { get { return "x"; } }
            public string Description { get { return "x"; } }
            public string Version { get { return "1"; } }
            public World Generate(SimEngine engine) { return null; }
        }

        [Fact]
        public void EnginesDoNotShareRegistries()
        {
            var a = new SimEngine(1);
            var b = new SimEngine(2);
            a.Generators.Register(new StubGenerator());
            Assert.True(a.Generators.Contains("stub"));
            Assert.False(b.Generators.Contains("stub"));
        }

        [Fact]
        public void ConfigFileCanSelectTheGenerator()
        {
            string folder = Path.Combine(Path.GetTempPath(), "loregen-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                string config = Path.Combine(folder, "loregen.config");
                File.WriteAllLines(config, new[] { "usepresetseed=true", "presetseed=5", "generatormode = Stub" });
                var engine = new SimEngine(config);
                Assert.Equal("Stub", engine.Configuration.GeneratorId);
                engine.Generators.Register(new StubGenerator());
                engine.GenerateWorld();
                Assert.Equal("stub", engine.World.GeneratorId);
            }
            finally { Directory.Delete(folder, true); }
        }

        // ---- Registry -------------------------------------------------------------------------

        [Fact]
        public void DefaultRegistryHoldsTheShippedGenerators()
        {
            var registry = WorldGeneratorRegistry.CreateDefault();
            Assert.Contains("legacy", registry.Ids);
            Assert.True(registry.Contains(WorldGeneratorRegistry.DefaultId));
            Assert.All(registry.All, g => Assert.Same(g, registry.Get(g.Id)));
        }

        [Fact]
        public void RegistryKeepsRegistrationOrder()
        {
            var registry = new WorldGeneratorRegistry();
            registry.Register(new StubGenerator("zeta"));
            registry.Register(new StubGenerator("alpha"));
            Assert.Equal(new[] { "zeta", "alpha" }, registry.Ids);
        }

        [Fact]
        public void RegistryRejectsDuplicateIds()
        {
            var registry = new WorldGeneratorRegistry();
            registry.Register(new StubGenerator("dup"));
            Assert.Throws<InvalidOperationException>(() => registry.Register(new StubGenerator("DUP")));
        }

        [Theory]
        [InlineData("")]
        [InlineData("has space")]
        [InlineData("semi;colon")]
        [InlineData(null)]
        public void RegistryRejectsBadIds(string id)
        {
            var registry = new WorldGeneratorRegistry();
            Assert.Throws<ArgumentException>(() => registry.Register(new StubGenerator(id)));
        }

        [Fact]
        public void RegistryRejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => new WorldGeneratorRegistry().Register(null));
        }

        // ---- Keyed rules files for generators -------------------------------------------------

        [Fact]
        public void RulesFileReadsKeysInAnyOrderAndCulture()
        {
            var old = System.Globalization.CultureInfo.CurrentCulture;
            try
            {
                System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
                var rules = new RulesFile("Test.csv", new[] { "# comment", "", "Octaves,5,How many layers", "LandFraction,0.3" });
                Assert.Equal(0.3, rules.D("LandFraction"));
                Assert.Equal(5, rules.I("Octaves"));
                Assert.True(rules.Has("octaves"));
                Assert.False(rules.Has("Nope"));
                rules.Finish();
            }
            finally { System.Globalization.CultureInfo.CurrentCulture = old; }
        }

        [Fact]
        public void RulesFileReportsProblemsWithFileAndKey()
        {
            var rules = new RulesFile("Mine.csv", new[] { "A,1", "Typo,2" });
            Assert.Contains("Mine.csv", Assert.Throws<InvalidDataException>(() => rules.D("Missing")).Message);
            Assert.Contains("'Missing'", Assert.Throws<InvalidDataException>(() => rules.D("Missing")).Message);
            rules.D("A");
            Assert.Contains("Typo", Assert.Throws<InvalidDataException>(() => rules.Finish()).Message);

            Assert.Throws<InvalidDataException>(() => new RulesFile("x.csv", new[] { "A,1", "a,2" }));
            Assert.Throws<InvalidDataException>(() => new RulesFile("x.csv", new[] { "A,notanumber" }));
            Assert.Throws<InvalidDataException>(() => new RulesFile("x.csv", new[] { "justakey" }));
        }

        [Fact]
        public void SimDataLoadsGeneratorRulesFromTheDataFolderWithEmbeddedFallback()
        {
            string folder = Path.Combine(Path.GetTempPath(), "loregen-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                File.WriteAllLines(Path.Combine(folder, "MyGeneratorRules.csv"), new[] { "Roughness,0.8" });
                var engine = new SimEngine(1);
                engine.SimData.DataFolder = folder;
                Assert.Equal(0.8, engine.SimData.LoadRules("MyGeneratorRules.csv").D("Roughness"));
                // Not in the folder, so falls back to the embedded copy
                Assert.True(engine.SimData.LoadRules("TerrainRules.csv").Has("CombinationWeightReducer"));
            }
            finally { Directory.Delete(folder, true); }
        }
    }
}
