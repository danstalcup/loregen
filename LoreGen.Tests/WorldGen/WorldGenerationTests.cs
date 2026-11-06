using LoreGen.Simulation;
using Xunit;

namespace LoreGen.Tests.WorldGen
{
    public class WorldGenerationTests
    {
        [Fact]
        public void QuickStart_GeneratesWorld_WithoutThrowing()
        {
            SimEngine engine = SimEngine.QuickStart();

            Assert.NotNull(engine.World);
            Assert.NotEmpty(engine.World.Continents);
            foreach (LoreGen.WorldGen.Continent continent in engine.World.Continents)
            {
                Assert.NotEmpty(continent.Regions);
            }
        }
    }
}
