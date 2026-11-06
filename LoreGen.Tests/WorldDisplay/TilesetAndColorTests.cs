using System.Linq;
using LoreGen.Simulation;
using Xunit;

namespace LoreGen.Tests.WorldDisplay
{
    public class TilesetAndColorTests
    {
        [Fact]
        public void TerrainDisplayInfo_ResolvesEveryClimateTypeColorName()
        {
            SimEngine engine = new SimEngine();

            // Exercises ClimateTypeDisplayInfo's named-color lookup for every row in ClimateTypes.csv.
            Assert.NotEmpty(engine.SimData.ClimateTypes);
            foreach (LoreGen.Terrains.ClimateType climateType in engine.SimData.ClimateTypes)
            {
                Assert.NotNull(climateType.DisplayInfo);
            }
        }
    }
}
