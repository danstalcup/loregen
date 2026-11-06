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
        public void GeneratesLanguageNames()
        {
            var engine = new SimEngine(5);
            var lang = engine.Language.GetRandomLanguage();
            Assert.False(string.IsNullOrWhiteSpace(lang.GetName()));
        }
    }
}
