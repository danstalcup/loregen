using System;
using System.Collections.Generic;
using System.Linq;
using LoreGen.Features;
using LoreGen.Lang;
using LoreGen.Simulation;
using LoreGen.WorldGen;
using LoreGen.WorldGen.Generators;
using Xunit;

namespace LoreGen.Tests
{
    /// <summary>
    /// Features found while zooming in (FineFeatureDetector) and subregions (SubregionPlanner).
    /// </summary>
    public class FineDetailFeatureTests
    {
        private static World Generate(int seed, string generator = TectonicWorldGenerator.GeneratorId, Action<SimEngine> configure = null)
        {
            var engine = new SimEngine(seed);
            engine.Configuration.GeneratorId = generator;
            configure?.Invoke(engine);
            engine.GenerateWorld();
            return engine.World;
        }

        // The first few 1000 km coastal blocks with detail, zoomed in to 10 km throughout
        private static List<WorldBlock> ZoomCoasts(World world, int count = 2)
        {
            var tops = world.WorldBlock.ChildBlocks.Cast<WorldBlock>()
                .Where(b => b.ChildBlocks != null && b.Status.WaterStatus == WorldBlockWaterStatus.Coastline)
                .OrderBy(b => b.Y).ThenBy(b => b.X).Take(count).ToList();
            foreach (var top in tops) foreach (var block in top.ChildBlocks) world.EnsureChildren(block);
            return tops;
        }

        private static string Describe(GeographicFeature f) =>
            $"{f.Type.Id} {f.Name} [{string.Join(" ", f.Footprint.Blocks.Select(b => b.TrueX + "," + b.TrueY))}] {f.Parent?.Name}";

        [Fact]
        public void ZoomingInFindsSmallFeatures()
        {
            var world = Generate(5);
            int before = world.Features.Count;
            var tops = ZoomCoasts(world);
            var found = world.Features.Skip(before).ToList();
            Assert.Contains(found, f => f.Scope == FeatureScope.Local);
            Assert.Contains(found, f => f.Type.Id == "Headland" || f.Type.Id == "Cove");
            foreach (var feature in found)
            {
                Assert.False(string.IsNullOrWhiteSpace(feature.Name));
                Assert.Contains(feature.Type.Id, FineFeatureDetector.TypeIds);
                Assert.All(feature.Footprint.Blocks, b => Assert.Equal(feature.Depth, b.Depth));
                // Each lies within the one block whose children it was found in
                Assert.Single(feature.Footprint.Blocks.Select(b => b.ParentBlock).Distinct());
                if (feature.Type.Shape == FeatureShape.Point) Assert.Single(feature.Spine);
                if (feature.Type.ParentTypes.Count > 0) Assert.NotNull(feature.Parent);
            }

            // And 1 km features further in
            int middle = world.Features.Count;
            var block = tops[0].ChildBlocks.Cast<WorldBlock>().Where(b => b.Status.WaterStatus != WorldBlockWaterStatus.Water).Take(20).ToList();
            foreach (var b in block) foreach (var child in b.ChildBlocks) world.EnsureChildren(child);
            Assert.Contains(world.Features.Skip(middle), f => f.Scope == FeatureScope.Minor);
        }

        [Fact]
        public void FoundFeaturesDoNotDependOnTheOrderOfZooming()
        {
            var a = Generate(8);
            var b = Generate(8);
            int before = a.Features.Count;
            var tops = ZoomCoasts(a, 1);
            var top = tops[0];
            var blocks = top.ChildBlocks.Cast<WorldBlock>().Select(c => (c.X, c.Y)).ToList();
            // b: the same blocks backwards, after zooming into a neighbour first
            var bTop = b.WorldBlock.ChildBlocks[top.X, top.Y];
            var neighbour = b.WorldBlock.ChildBlocks[Math.Min(top.X + 1, b.WorldBlock.Width - 1), top.Y];
            b.EnsureChildren(neighbour);
            foreach (var child in neighbour.ChildBlocks) b.EnsureChildren(child);
            foreach (var (x, y) in Enumerable.Reverse(blocks)) b.EnsureChildren(bTop.ChildBlocks[x, y]);

            Func<World, IEnumerable<string>> inTop = w => w.Features.Skip(before)
                .Where(f => f.Footprint.Blocks.All(x => x.AncestorAt(1).X == top.X && x.AncestorAt(1).Y == top.Y))
                .Select(Describe).OrderBy(s => s, StringComparer.Ordinal);
            Assert.Equal(inTop(a), inTop(b));
        }

        [Fact]
        public void FoundFeaturesStayInsideTheirBlocksWhenZoomedInto()
        {
            var world = Generate(5);
            int before = world.Features.Count;
            ZoomCoasts(world, 1);
            var areas = world.Features.Skip(before).Where(f => f.Type.Shape == FeatureShape.Blob && f.Footprint.Count >= 3).Take(5).ToList();
            Assert.NotEmpty(areas);
            foreach (var feature in areas)
            {
                foreach (var block in feature.Footprint.Blocks) world.EnsureChildren(block);
                var parent = feature.Footprint.Blocks[0].ParentBlock;
                // Next door, inside the same parent but outside the footprint, never part of it
                foreach (var cell in parent.ChildBlocks.Cast<WorldBlock>().Where(c => !feature.Footprint.IsMember(c)).Take(10))
                {
                    world.EnsureChildren(cell);
                    Assert.DoesNotContain(cell.ChildBlocks.Cast<WorldBlock>(), c => world.FeaturesAt(c).Contains(feature));
                }
                Assert.Contains(feature.Footprint.Blocks.SelectMany(b => b.ChildBlocks.Cast<WorldBlock>()), c => world.FeaturesAt(c).Contains(feature));
            }
        }

        [Fact]
        public void FoundForestsStayWoodedFurtherIn()
        {
            World world = null;
            GeographicFeature forest = null;
            foreach (var (seed, generator) in new[] { (8, TectonicWorldGenerator.GeneratorId), (3, NoiseWorldGenerator.GeneratorId), (5, TectonicWorldGenerator.GeneratorId) })
            {
                world = Generate(seed, generator);
                int before = world.Features.Count;
                // Open 100 km blocks beside wooded ones hold distinct woods (wholly wooded blocks are one big wood, which
                // is the character of the land rather than a feature in it)
                var w = world;
                var mixed = w.WorldBlock.ChildBlocks.Cast<WorldBlock>().Where(t => t.ChildBlocks != null)
                    .SelectMany(t => t.ChildBlocks.Cast<WorldBlock>()).Where(c => c.Status.Terrain != null && c.Status.Terrain.TerrainType.Trees < 0.5)
                    .Where(c => c.SurroundingBlocksAsList().Count(n => n.Status.Terrain != null && n.Status.Terrain.TerrainType.Trees >= 0.5) >= 2)
                    .OrderBy(c => c.TrueY).ThenBy(c => c.TrueX).Take(40).ToList();
                foreach (var block in mixed) w.EnsureChildren(block);
                forest = world.Features.Skip(before).FirstOrDefault(f => f.Type.Id == "Forest" && f.Footprint.Count >= 8);
                if (forest != null) break;
            }
            Assert.NotNull(forest);
            var children = new List<WorldBlock>();
            foreach (var block in forest.Footprint.Blocks)
            {
                world.EnsureChildren(block);
                children.AddRange(block.ChildBlocks.Cast<WorldBlock>().Where(c => c.Status.Terrain != null && world.FeaturesAt(c).Contains(forest)));
            }
            double wooded = children.Count(c => c.Status.Terrain.TerrainType.Trees >= 0.5) / (double)children.Count;
            Assert.True(wooded > 0.8, $"{wooded:P0} of a forest's finer blocks are wooded");
        }

        [Fact]
        public void NamesFromASeedAreRepeatableAndNotRecorded()
        {
            var engine = new SimEngine(3);
            engine.Configuration.GeneratorId = TectonicWorldGenerator.GeneratorId;
            engine.GenerateWorld();
            var language = engine.World.Continents[0].RootLanguage;
            var context = new NameContext(NameKind.Knoll);
            string first = engine.Language.NameFromSeed(language, context, 42);
            language.GetName(NameKind.Region); // other naming in between changes nothing
            Assert.Equal(first, engine.Language.NameFromSeed(language, context, 42));
            Assert.False(engine.Language.Names.Contains(first));
            // Subregion names use the landform they are given
            if (language is CommonLanguage)
                Assert.Contains("Hills", engine.Language.NameFromSeed(language, new NameContext(NameKind.Subregion) { Landform = "Hills" }, 7));
        }

        // ---- Subregions --------------------------------------------------------------------------

        [Fact]
        public void EveryRegionDividesIntoSubregions()
        {
            var world = Generate(5);
            foreach (var region in world.Continents.SelectMany(c => c.Regions).Take(8))
            {
                var subregions = world.SubregionsOf(region);
                Assert.NotEmpty(subregions);
                Assert.True(subregions.Count <= 12);
                Assert.Equal(subregions.Count, subregions.Select(s => s.Name).Distinct().Count());
                var covered = new HashSet<WorldBlock>();
                foreach (var subregion in subregions)
                {
                    Assert.Same(region, subregion.Region);
                    Assert.False(string.IsNullOrWhiteSpace(subregion.Name));
                    Assert.All(subregion.Blocks, b =>
                    {
                        Assert.Equal(2, b.Depth);
                        Assert.Same(region, b.Status.Region);
                        Assert.Same(subregion, b.Status.Subregion);
                        Assert.True(covered.Add(b));
                    });
                }
                // Every 100 km land block of the region is in exactly one
                var land = region.RegionArea.Blocks().SelectMany(t => t.ChildBlocks.Cast<WorldBlock>())
                    .Where(c => c.Status.Region == region && c.Status.WaterStatus != WorldBlockWaterStatus.Water);
                Assert.All(land, c => Assert.Contains(c, covered));
            }
        }

        [Fact]
        public void SubregionsDoNotDependOnTheOrderTheyAreAskedFor()
        {
            var a = Generate(11);
            var b = Generate(11);
            var regionsA = a.Continents.SelectMany(c => c.Regions).Take(4).ToList();
            var regionsB = b.Continents.SelectMany(c => c.Regions).Take(4).ToList();
            foreach (var r in regionsA) a.SubregionsOf(r);
            // b: a block zoomed into first, then the regions backwards
            var someLand = b.WorldBlock.ChildBlocks.Cast<WorldBlock>().First(t => t.Status.Region == regionsB[0]);
            b.EnsureChildren(someLand);
            b.EnsureChildren(someLand.ChildBlocks[5, 5]);
            foreach (var r in Enumerable.Reverse(regionsB)) b.SubregionsOf(r);
            Func<Region, string> describe = r => string.Join("\n", r.Subregions.Select(s => $"{s.Name} {s.Kind} [{string.Join(" ", s.Blocks.Select(x => x.TrueX + "," + x.TrueY))}]"));
            for (int i = 0; i < regionsA.Count; i++) Assert.Equal(describe(regionsA[i]), describe(regionsB[i]));
        }

        [Fact]
        public void SubregionsFollowTheLandscape()
        {
            var world = Generate(5);
            int insideChanges = 0, inside = 0, borderChanges = 0, border = 0;
            foreach (var region in world.Continents.SelectMany(c => c.Regions).Take(10))
                foreach (var subregion in world.SubregionsOf(region))
                    foreach (var block in subregion.Blocks)
                        foreach (var next in block.SurroundingBlocksAsList())
                        {
                            if (next.Status.Region != region || next.Status.Subregion == null || next.Status.Terrain == null) continue;
                            bool change = RegionPartitioner.Landscape(block.Status.Terrain) != RegionPartitioner.Landscape(next.Status.Terrain);
                            if (next.Status.Subregion == subregion) { inside++; if (change) insideChanges++; }
                            else { border++; if (change) borderChanges++; }
                        }
            double a = (double)borderChanges / border, b = (double)insideChanges / inside;
            Assert.True(a > b + 0.1, $"landscape changes across subregion borders {a:P0}, inside {b:P0}");
        }

        [Fact]
        public void FinerBlocksBelongToASubregionOfTheirRegion()
        {
            var world = Generate(5);
            var block = world.WorldBlock.ChildBlocks.Cast<WorldBlock>().First(b => b.ChildBlocks != null && b.Status.WaterStatus == WorldBlockWaterStatus.Land);
            var mid = block.ChildBlocks[5, 5];
            world.EnsureChildren(mid);
            world.EnsureChildren(mid.ChildBlocks[3, 3]);
            foreach (var c in mid.ChildBlocks.Cast<WorldBlock>().Concat(mid.ChildBlocks[3, 3].ChildBlocks.Cast<WorldBlock>()))
            {
                if (c.Status.WaterStatus == WorldBlockWaterStatus.Water) continue;
                Assert.NotNull(c.Status.Subregion);
                Assert.Same(c.Status.Region, c.Status.Subregion.Region);
            }
        }

        [Fact]
        public void WorldsWithoutSubregionsSaySo()
        {
            var world = Generate(5, LegacyWorldGenerator.GeneratorId);
            Assert.False(world.HasSubregions);
            Assert.Empty(world.SubregionsOf(world.Continents[0].Regions[0]));
        }

        [Theory]
        [InlineData("SubregionBlocks", 0)]
        [InlineData("SubregionsMax", 0)]
        public void BadSubregionSettingsAreRejected(string key, double value)
        {
            var engine = new SimEngine(1);
            engine.Configuration.GeneratorId = NoiseWorldGenerator.GeneratorId;
            engine.Configuration.GeneratorSettings[key] = value;
            var ex = Assert.ThrowsAny<Exception>(() => engine.GenerateWorld());
            Assert.Contains(key, ex.Message);
        }
    }
}
