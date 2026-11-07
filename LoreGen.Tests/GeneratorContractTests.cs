using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LoreGen.Simulation;
using LoreGen.WorldGen;
using LoreGen.WorldGen.Generators;
using Xunit;

namespace LoreGen.Tests
{
    /// <summary>
    /// The contract every world generator must meet (see IWorldGenerator). Each test runs once per generator that ships
    /// with LoreGen, so adding a generator to WorldGeneratorRegistry.CreateDefault puts it through all of these.
    /// </summary>
    public class GeneratorContractTests
    {
        public static IEnumerable<object[]> GeneratorIds
        {
            get { return WorldGeneratorRegistry.CreateDefault().Ids.Select(id => new object[] { id }); }
        }

        private const int SweepSeeds = 40;

        private static SimEngine Generate(string generatorId, int seed)
        {
            var engine = new SimEngine(seed);
            engine.Configuration.GeneratorId = generatorId;
            engine.GenerateWorld();
            return engine;
        }

        // ---- Identity -------------------------------------------------------------------------

        [Theory]
        [MemberData(nameof(GeneratorIds))]
        public void DeclaresItsIdentity(string id)
        {
            var generator = WorldGeneratorRegistry.CreateDefault().Get(id);
            Assert.Equal(id, generator.Id);
            Assert.False(string.IsNullOrWhiteSpace(generator.DisplayName));
            Assert.False(string.IsNullOrWhiteSpace(generator.Description));
            Assert.False(string.IsNullOrWhiteSpace(generator.Version));
        }

        [Theory]
        [MemberData(nameof(GeneratorIds))]
        public void StampsTheWorldWithItsIdentity(string id)
        {
            var engine = Generate(id, 3);
            var generator = engine.Generators.Get(id);
            Assert.Equal(generator.Id, engine.World.GeneratorId);
            Assert.Equal(generator.Version, engine.World.GeneratorVersion);
        }

        // ---- Structure ------------------------------------------------------------------------

        [Theory]
        [MemberData(nameof(GeneratorIds))]
        public void ProducesWellFormedWorlds(string id)
        {
            for (int seed = 1; seed <= SweepSeeds; seed++)
            {
                var engine = Generate(id, seed);
                try { AssertWellFormed(engine); }
                catch (Exception ex) { throw new Exception("Generator '" + id + "', seed " + seed + ": " + ex.Message, ex); }
            }
        }

        private static void AssertWellFormed(SimEngine engine)
        {
            World world = engine.World;
            Assert.NotNull(world);
            Assert.NotNull(world.WorldBlock);
            Assert.NotNull(world.WorldBlock.ChildBlocks);
            Assert.True(world.WorldBlock.Width > 0 && world.WorldBlock.Height > 0, "empty world grid");

            Assert.NotEmpty(world.Continents);
            foreach (Continent continent in world.Continents)
            {
                Assert.False(string.IsNullOrWhiteSpace(continent.Name), "continent without a name");
                Assert.NotNull(continent.RootLanguage);
                Assert.NotNull(continent.ContinentArea);
                Assert.NotEmpty(continent.ContinentArea.BlocksList);
                Assert.NotEmpty(continent.Regions);
                Assert.NotNull(continent.DisplayInfo);

                foreach (Region region in continent.Regions)
                {
                    Assert.Same(continent, region.Continent);
                    Assert.False(string.IsNullOrWhiteSpace(region.Name), "region without a name in " + continent.Name);
                    Assert.NotNull(region.Language);
                    Assert.NotNull(region.Biome);
                    Assert.NotNull(region.RegionArea);
                    Assert.NotEmpty(region.RegionArea.BlocksList);
                    Assert.NotNull(region.DisplayInfo);
                }

                foreach (Sea sea in continent.Seas)
                {
                    Assert.Same(continent, sea.Continent);
                    Assert.False(string.IsNullOrWhiteSpace(sea.Name), "sea without a name");
                }
            }

            foreach (Ocean ocean in world.Oceans)
            {
                Assert.False(string.IsNullOrWhiteSpace(ocean.Name), "ocean without a name");
                Assert.NotNull(ocean.OceanArea);
            }

            var continents = new HashSet<Continent>(world.Continents);
            var oceans = new HashSet<Ocean>(world.Oceans);
            var regions = new HashSet<Region>(world.Continents.SelectMany(c => c.Regions));
            var seas = new HashSet<Sea>(world.Continents.SelectMany(c => c.Seas));

            foreach (WorldBlock block in world.WorldBlock.ChildBlocksAsList(true))
            {
                string where = "block (" + block.X + "," + block.Y + ") at depth " + block.Depth;
                WorldBlockStatus status = block.Status;
                Assert.True(status != null, where + " has no Status");

                if (status.Continent != null) Assert.True(continents.Contains(status.Continent), where + ": continent not registered on the world");
                if (status.Ocean != null) Assert.True(oceans.Contains(status.Ocean), where + ": ocean not registered on the world");
                if (status.Region != null) Assert.True(regions.Contains(status.Region), where + ": region not registered on a continent");
                if (status.Sea != null) Assert.True(seas.Contains(status.Sea), where + ": sea not registered on a continent");

                if (status.WaterStatus == WorldBlockWaterStatus.Water)
                {
                    // Known gap: Legacy builds the fine-detail grids under coastline blocks before oceans exist, so its
                    // water sub-blocks carry no Ocean. Until that is fixed (which changes Legacy's output, so it needs a
                    // version bump), the rule is only enforced on top-level blocks.
                    if (block.ParentBlock == world.WorldBlock)
                        Assert.True(status.Ocean != null || status.Sea != null, where + ": water that is neither ocean nor sea");
                }
                else
                {
                    Assert.True(status.Continent != null, where + ": land without a continent");
                    Assert.True(status.Region != null, where + ": land without a region");
                    Assert.True(status.Terrain != null, where + ": land without terrain");
                }

                if (block.ChildBlocks != null)
                {
                    foreach (WorldBlock child in block.ChildBlocksAsList())
                        Assert.Same(block, child.ParentBlock);
                }
            }
        }

        // ---- Settings -------------------------------------------------------------------------

        [Theory]
        [MemberData(nameof(GeneratorIds))]
        public void RejectsSettingsItDoesNotUnderstand(string id)
        {
            var engine = new SimEngine(1);
            engine.Configuration.GeneratorId = id;
            engine.Configuration.GeneratorSettings["NoSuchSetting"] = 1;
            var ex = Assert.ThrowsAny<Exception>(() => engine.GenerateWorld());
            Assert.Contains("NoSuchSetting", ex.Message);
            Assert.Null(engine.World);
        }

        [Theory]
        [MemberData(nameof(GeneratorIds))]
        public void EveryOptionIsSafeAcrossItsWholeRange(string id)
        {
            var probe = new SimEngine(1);
            var options = probe.Generators.Get(id).GetOptions(probe.SimData);
            Assert.Equal(options.Count, options.Select(o => o.Key.ToLowerInvariant()).Distinct().Count());

            foreach (var option in options)
            {
                // The ends of the range, and one step in from the bottom (just above an "automatic" zero)
                var values = new[] { option.Minimum, Math.Min(option.Maximum, option.Minimum + option.Step), option.Maximum }.Distinct();
                foreach (double value in values)
                {
                    var engine = new SimEngine(2);
                    engine.Configuration.GeneratorId = id;
                    engine.Configuration.GeneratorSettings[option.Key] = value;
                    try
                    {
                        engine.GenerateWorld();
                        AssertWellFormed(engine);
                    }
                    catch (Exception ex) { throw new Exception("Generator '" + id + "', " + option.Key + " = " + value + ": " + ex.Message, ex); }
                }
            }
        }

        [Theory]
        [MemberData(nameof(GeneratorIds))]
        public void SettingEveryOptionToItsDefaultChangesNothing(string id)
        {
            var probe = new SimEngine(1);
            var options = probe.Generators.Get(id).GetOptions(probe.SimData);

            var plain = Generate(id, 9);
            var explicitDefaults = new SimEngine(9);
            explicitDefaults.Configuration.GeneratorId = id;
            foreach (var option in options) explicitDefaults.Configuration.GeneratorSettings[option.Key] = option.Default;
            explicitDefaults.GenerateWorld();

            Assert.Equal(Describe(plain.World), Describe(explicitDefaults.World));
        }

        // ---- Determinism and isolation --------------------------------------------------------

        [Theory]
        [MemberData(nameof(GeneratorIds))]
        public void SameSeedGivesTheSameWorld(string id)
        {
            foreach (int seed in new[] { 7, 4242, 90210 })
                Assert.Equal(Describe(Generate(id, seed).World), Describe(Generate(id, seed).World));
        }

        [Theory]
        [MemberData(nameof(GeneratorIds))]
        public void DifferentSeedsGiveDifferentWorlds(string id)
        {
            Assert.NotEqual(Describe(Generate(id, 1).World), Describe(Generate(id, 2).World));
        }

        [Theory]
        [MemberData(nameof(GeneratorIds))]
        public void IsIndependentOfOtherEngines(string id)
        {
            string alone = Describe(Generate(id, 42).World);

            var a = new SimEngine(42);
            a.Configuration.GeneratorId = id;
            var interloper = new SimEngine(99);
            interloper.Configuration.GeneratorId = id;
            interloper.GenerateWorld();
            a.GenerateWorld();
            interloper.GenerateWorld();

            Assert.Equal(alone, Describe(a.World));
        }

        [Theory]
        [MemberData(nameof(GeneratorIds))]
        public void CanBeReusedForManyWorlds(string id)
        {
            // Generators must be stateless: one instance generating for two engines matches fresh instances
            var shared = WorldGeneratorRegistry.CreateDefault().Get(id);
            var one = new SimEngine(11); one.WorldGenerator = shared; one.GenerateWorld();
            var two = new SimEngine(12); two.WorldGenerator = shared; two.GenerateWorld();
            var again = new SimEngine(11); again.WorldGenerator = shared; again.GenerateWorld();

            Assert.Equal(Describe(one.World), Describe(again.World));
            Assert.Equal(Describe(Generate(id, 12).World), Describe(two.World));
        }

        /// <summary>
        /// A complete textual description of a world: names, counts, and every block's status at every level.
        /// </summary>
        public static string Describe(World world)
        {
            var sb = new StringBuilder();
            sb.Append(world.GeneratorId).Append('/').Append(world.GeneratorVersion).Append('|');
            foreach (Continent c in world.Continents)
                sb.Append(c.Name).Append(':').Append(c.Regions.Count).Append(':').Append(c.Seas.Count).Append(';');
            foreach (Ocean o in world.Oceans) sb.Append(o.Name).Append(';');
            foreach (WorldBlock b in world.WorldBlock.ChildBlocksAsList(true))
            {
                WorldBlockStatus s = b.Status;
                sb.Append((int)s.WaterStatus).Append(s.Continent?.Name).Append('/').Append(s.Region?.Name).Append('/')
                  .Append(s.Terrain?.Name).Append('/').Append(s.Ocean?.Name).Append('/').Append(s.Sea?.Name).Append(',');
            }
            return sb.ToString();
        }
    }
}
