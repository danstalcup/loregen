using System.Linq;
using LoreGen.Simulation;
using Xunit;

namespace LoreGen.Tests.Simulation
{
    public class SimEngineTests
    {
        [Fact]
        public void Construct_LoadsAllResourceFiles_WithoutThrowing()
        {
            SimEngine engine = new SimEngine();

            Assert.NotEmpty(engine.SimData.LetterSounds);
            Assert.NotEmpty(engine.SimData.PhoneticInventories);
            Assert.NotEmpty(engine.SimData.EdgePatterns);
            Assert.NotEmpty(engine.SimData.ClimateTypes);
            Assert.NotEmpty(engine.SimData.Climates);
            Assert.NotEmpty(engine.SimData.Biomes);
            Assert.NotEmpty(engine.SimData.TerrainTypes);
            Assert.NotEmpty(engine.SimData.Terrains);
        }

        [Fact]
        public void WithPresetSeed_IsReproducible_WithinThisRuntime()
        {
            SimEngineConfiguration configA = new SimEngineConfiguration("nonexistent.config")
            {
                UsePresetSeed = true,
                PresetSeed = 12345
            };
            SimEngineConfiguration configB = new SimEngineConfiguration("nonexistent.config")
            {
                UsePresetSeed = true,
                PresetSeed = 12345
            };

            Randomizer.Rnd rndA = new Randomizer.Rnd(configA.PresetSeed);
            Randomizer.Rnd rndB = new Randomizer.Rnd(configB.PresetSeed);

            // Same seed must produce the same sequence within a single .NET runtime. This does NOT assert
            // parity with pre-migration .NET Framework output: System.Random's algorithm changed between
            // .NET Framework and modern .NET, so seeded output is expected to differ from the old runtime.
            for (int i = 0; i < 20; i++)
            {
                Assert.Equal(rndA.Unweighted(1000), rndB.Unweighted(1000));
            }
        }
    }
}
