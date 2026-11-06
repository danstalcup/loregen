using LoreGen.Landforms;
using LoreGen.Simulation;
using Xunit;

namespace LoreGen.Tests.Landforms
{
    public class LandformTests
    {
        [Fact]
        public void Construct_AssignsRandomDisplayColor()
        {
            SimEngine engine = SimEngine.QuickStart();

            Landform landform = new Landform(engine.World);

            Assert.Equal(engine.World, landform.World);
            Assert.NotNull(landform.DisplayInfo);
        }

        [Fact]
        public void LandformType_HasExpectedShapeValues()
        {
            LandformType type = new LandformType
            {
                ID = 1,
                Name = "Mountain",
                Description = "A tall landform",
                Shape = LandformShape.Area,
                Magnitude = 5
            };

            Assert.Equal(LandformShape.Area, type.Shape);
            Assert.Equal(5, type.Magnitude);
        }
    }
}
