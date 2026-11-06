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

        [Fact]
        public void GeneratesWorldsForManySeeds()
        {
            for (int seed = 1; seed <= 150; seed++)
            {
                var engine = new SimEngine(seed);
                engine.GenerateWorld(); // seeds 30, 95 and 149 used to throw
                Assert.NotEmpty(engine.World.Oceans);
            }
        }

        [Fact]
        public void SeedIsIndependentOfOtherEngines()
        {
            var a = new SimEngine(42); a.GenerateWorld();
            var other = new SimEngine(99); // used to reseed a shared static generator
            var b = new SimEngine(42); b.GenerateWorld();
            Assert.Equal(a.World.Continents.Select(c => c.Name), b.World.Continents.Select(c => c.Name));
        }

        [Fact]
        public void DataLoadsUnderCommaDecimalCulture()
        {
            var old = System.Globalization.CultureInfo.CurrentCulture;
            try
            {
                System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
                var reference = new SimEngine(1);
                Assert.Equal(0.75, reference.SimData.WorldRules.WorldHeightLengthRatio, 6);
            }
            finally { System.Globalization.CultureInfo.CurrentCulture = old; }
        }

        [Fact]
        public void RegionsGetTheirOwnLanguage()
        {
            var engine = new SimEngine(777); engine.GenerateWorld();
            var c = engine.World.Continents.First(x => x.Regions.Count > 0);
            Assert.All(c.Regions, r => Assert.NotSame(c.RootLanguage, r.Language));
        }

        [Fact]
        public void SeasAreRegisteredOnTheirContinent()
        {
            var seaBlocks = new System.Collections.Generic.List<WorldBlock>();
            for (int seed = 1; seed < 50 && seaBlocks.Count == 0; seed++)
            {
                var engine = new SimEngine(seed); engine.GenerateWorld();
                seaBlocks = engine.World.WorldBlock.ChildBlocksAsList().Where(b => b.Status.Sea != null).ToList();
            }
            Assert.NotEmpty(seaBlocks);
            Assert.All(seaBlocks, b => Assert.Contains(b.Status.Sea, b.Status.Sea.Continent.Seas));
            Assert.All(seaBlocks, b => Assert.False(string.IsNullOrEmpty(b.Status.Sea.Name)));
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
