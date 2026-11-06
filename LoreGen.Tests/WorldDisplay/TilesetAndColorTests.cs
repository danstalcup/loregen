using System.Linq;
using LoreGen.Simulation;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
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

        [Fact]
        public void Tileset_DrawTile_DoesNotThrow_AgainstInMemoryImage()
        {
            using Image<Rgba32> source = new Image<Rgba32>(45, 45);
            string tempFile = System.IO.Path.GetTempFileName() + ".png";
            source.SaveAsPng(tempFile);

            try
            {
                LoreGen.WorldDisplay.Tileset tileset = new LoreGen.WorldDisplay.Tileset(tempFile, 15, 15, 3, 3, 1);
                using Image<Rgba32> target = new Image<Rgba32>(15, 15);

                tileset.DrawTile(0, 0, new Rectangle(0, 0, 15, 15), target);

                using Image<Rgba32> clone = tileset.GetClone(1, 1);
                Assert.Equal(15, clone.Width);
            }
            finally
            {
                System.IO.File.Delete(tempFile);
            }
        }
    }
}
