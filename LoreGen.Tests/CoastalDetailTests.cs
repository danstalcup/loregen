using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LoreGen.Simulation;
using LoreGen.WorldGen;
using LoreGen.WorldGen.Generators;
using Xunit;

namespace LoreGen.Tests
{
    /// <summary>
    /// The fine-grained coastlines the noise generator adds along coasts.
    /// </summary>
    public class CoastalDetailTests
    {
        private const int N = 10;

        private static SimEngine Generate(int seed, params (string Key, string Value)[] rules)
        {
            var engine = new SimEngine(seed);
            engine.Configuration.GeneratorId = "noise";
            if (rules.Length == 0)
            {
                engine.GenerateWorld();
                return engine;
            }

            string folder = Path.Combine(Path.GetTempPath(), "loregen-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var original = new StreamReader(typeof(SimEngine).Assembly.GetManifestResourceStream("LoreGen.Resources." + NoiseGeneratorRules.Filename))
                    .ReadToEnd().Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0);
                File.WriteAllLines(Path.Combine(folder, NoiseGeneratorRules.Filename), original.Select(l =>
                {
                    string key = l.Split(',')[0];
                    foreach (var rule in rules) if (rule.Key == key) return key + "," + rule.Value;
                    return l;
                }));
                engine.SimData.DataFolder = folder;
                engine.GenerateWorld();
            }
            finally { Directory.Delete(folder, true); }
            return engine;
        }

        /// <summary>The world at its finest resolution: sub-blocks where there are any, repeated coarse blocks elsewhere.</summary>
        private class FineMap
        {
            public int Width, Height;
            public WorldBlockStatus[,] Status;
            public bool[,] InDetailBlock;
            public bool[,] CoarseLand;

            public FineMap(World world)
            {
                var root = world.WorldBlock;
                Width = root.Width * N;
                Height = root.Height * N;
                Status = new WorldBlockStatus[Width, Height];
                InDetailBlock = new bool[Width, Height];
                CoarseLand = new bool[Width, Height];
                for (int x = 0; x < Width; x++)
                {
                    for (int y = 0; y < Height; y++)
                    {
                        var block = root.ChildBlocks[x / N, y / N];
                        CoarseLand[x, y] = block.Status.WaterStatus != WorldBlockWaterStatus.Water;
                        InDetailBlock[x, y] = block.ChildBlocks != null;
                        Status[x, y] = block.ChildBlocks != null ? block.ChildBlocks[x % N, y % N].Status : block.Status;
                    }
                }
            }

            public bool IsLand(int x, int y) { return Status[x, y].WaterStatus != WorldBlockWaterStatus.Water; }

            public int CountCoast()
            {
                int count = 0;
                for (int x = 0; x < Width; x++)
                    for (int y = 0; y < Height; y++)
                        if (Status[x, y].WaterStatus == WorldBlockWaterStatus.Coastline) count++;
                return count;
            }
        }

        // ---- Where detail exists --------------------------------------------------------------

        [Fact]
        public void OnlyBlocksNearACoastGetSubBlocks()
        {
            for (int seed = 1; seed <= 15; seed++)
            {
                var root = Generate(seed, ("RegionBorderDetail", "0"), ("FeatureDetail", "0"), ("Hydrology", "0")).World.WorldBlock;
                for (int x = 0; x < root.Width; x++)
                {
                    for (int y = 0; y < root.Height; y++)
                    {
                        var around = new List<WorldBlock>();
                        for (int dx = -1; dx <= 1; dx++)
                            for (int dy = -1; dy <= 1; dy++)
                                if (x + dx >= 0 && y + dy >= 0 && x + dx < root.Width && y + dy < root.Height) around.Add(root.ChildBlocks[x + dx, y + dy]);

                        bool mixed = around.Any(b => b.Status.WaterStatus == WorldBlockWaterStatus.Water)
                                  && around.Any(b => b.Status.WaterStatus != WorldBlockWaterStatus.Water);
                        var block = root.ChildBlocks[x, y];
                        Assert.Equal(mixed, block.ChildBlocks != null);
                        if (mixed)
                        {
                            Assert.Equal(N, block.ChildBlocks.GetLength(0));
                            Assert.Equal(N, block.ChildBlocks.GetLength(1));
                        }
                    }
                }
            }
        }

        [Fact]
        public void DetailCanBeSwitchedOffAndTheCoarseWorldIsTheSameEitherWay()
        {
            for (int seed = 1; seed <= 10; seed++)
            {
                // Hydrology changes the coarse world too (river and lake water shape the terrain), so it is off in both
                var with = Generate(seed, ("Hydrology", "0")).World;
                var without = Generate(seed, ("CoastDetail", "0"), ("RegionBorderDetail", "0"), ("FeatureDetail", "0"), ("Hydrology", "0")).World;

                Assert.All(without.WorldBlock.ChildBlocksAsList(), b => Assert.Null(b.ChildBlocks));
                Assert.Contains(with.WorldBlock.ChildBlocksAsList(), b => b.ChildBlocks != null);

                // Detail is added after everything else is decided, so it must not change any of it
                Assert.Equal(CoarseDescription(without), CoarseDescription(with));
            }
        }

        private static string CoarseDescription(World world)
        {
            var parts = world.Continents.Select(c => c.Name + ":" + c.Regions.Count + ":" + c.Seas.Count).Concat(world.Oceans.Select(o => o.Name)).ToList();
            parts.AddRange(world.WorldBlock.ChildBlocksAsList().Select(b =>
                (int)b.Status.WaterStatus + b.Status.Continent?.Name + "/" + b.Status.Region?.Name + "/" + b.Status.Terrain?.Name + "/" + b.Status.Ocean?.Name + "/" + b.Status.Sea?.Name + "/" + b.Status.Elevation));
            return string.Join("|", parts);
        }

        // ---- The fine map is faithful to the coarse map ---------------------------------------

        [Fact]
        public void FineLandOnlyReshapesExistingCoasts()
        {
            for (int seed = 1; seed <= 15; seed++)
            {
                var fine = new FineMap(Generate(seed).World);

                // Land share barely moves
                int fineLand = 0, coarseLand = 0;
                for (int x = 0; x < fine.Width; x++)
                    for (int y = 0; y < fine.Height; y++)
                    {
                        if (fine.IsLand(x, y)) fineLand++;
                        if (fine.CoarseLand[x, y]) coarseLand++;
                    }
                Assert.True(Math.Abs(fineLand - coarseLand) / (double)(fine.Width * fine.Height) < 0.03, "seed " + seed + ": land share moved by too much");

                // No stray islets: every piece of fine land touches a coarse land block
                var land = new bool[fine.Width, fine.Height];
                var water = new bool[fine.Width, fine.Height];
                for (int x = 0; x < fine.Width; x++)
                    for (int y = 0; y < fine.Height; y++)
                    {
                        land[x, y] = fine.IsLand(x, y);
                        water[x, y] = !land[x, y];
                    }
                AssertEveryRegionTouches(GridLabeling.Label(land), fine, true, "seed " + seed + ": fine land with no coarse land");
                // No stray water: every body of fine water touches a coarse water block, or is a lake
                AssertEveryRegionTouches(GridLabeling.Label(water), fine, false, "seed " + seed + ": fine water with no coarse water");
            }
        }

        private static void AssertEveryRegionTouches(GridLabeling labels, FineMap fine, bool land, string message)
        {
            var anchored = new bool[labels.Count];
            for (int x = 0; x < fine.Width; x++)
                for (int y = 0; y < fine.Height; y++)
                    if (labels.Labels[x, y] >= 0 && (fine.CoarseLand[x, y] == land || (!land && fine.Status[x, y].Lake != null))) anchored[labels.Labels[x, y]] = true;
            Assert.True(anchored.All(a => a), message);
        }

        [Fact]
        public void FineLandOfDifferentContinentsNeverTouches()
        {
            for (int seed = 1; seed <= 15; seed++)
            {
                var fine = new FineMap(Generate(seed).World);
                for (int x = 0; x < fine.Width; x++)
                {
                    for (int y = 0; y < fine.Height; y++)
                    {
                        if (!fine.IsLand(x, y)) continue;
                        if (x + 1 < fine.Width && fine.IsLand(x + 1, y)) Assert.Same(fine.Status[x, y].Continent, fine.Status[x + 1, y].Continent);
                        if (y + 1 < fine.Height && fine.IsLand(x, y + 1)) Assert.Same(fine.Status[x, y].Continent, fine.Status[x, y + 1].Continent);
                    }
                }
            }
        }

        [Fact]
        public void CoastlineStatusIsExactAtEveryResolution()
        {
            for (int seed = 1; seed <= 10; seed++)
            {
                var fine = new FineMap(Generate(seed).World);
                for (int x = 0; x < fine.Width; x++)
                {
                    for (int y = 0; y < fine.Height; y++)
                    {
                        if (!fine.InDetailBlock[x, y] || !fine.IsLand(x, y)) continue; // blocks without detail keep their coarse status
                        bool touchesWater = (x > 0 && !fine.IsLand(x - 1, y)) || (x < fine.Width - 1 && !fine.IsLand(x + 1, y))
                                         || (y > 0 && !fine.IsLand(x, y - 1)) || (y < fine.Height - 1 && !fine.IsLand(x, y + 1));
                        Assert.Equal(touchesWater, fine.Status[x, y].WaterStatus == WorldBlockWaterStatus.Coastline);
                    }
                }
            }
        }

        [Fact]
        public void SubBlocksCarryEverythingTheirKindNeeds()
        {
            for (int seed = 1; seed <= 15; seed++)
            {
                var world = Generate(seed).World;
                foreach (var block in world.WorldBlock.ChildBlocksAsList().Where(b => b.ChildBlocks != null))
                {
                    foreach (var sub in block.ChildBlocksAsList())
                    {
                        var s = sub.Status;
                        Assert.True(s.Elevation.HasValue);
                        if (s.WaterStatus == WorldBlockWaterStatus.Water)
                        {
                            Assert.True(s.Elevation < 0);
                            Assert.True(s.Ocean != null || s.Sea != null || s.Lake != null, "seed " + seed + ": water sub-block that is neither ocean, sea nor lake");
                            Assert.Null(s.Continent);
                            Assert.Null(s.Region);
                            Assert.Null(s.Terrain);
                        }
                        else
                        {
                            Assert.True(s.Elevation > 0);
                            Assert.NotNull(s.Continent);
                            Assert.NotNull(s.Region);
                            Assert.NotNull(s.Terrain);
                            Assert.Same(s.Continent, s.Region.Continent);
                            Assert.Contains(s.Region, s.Continent.Regions);
                        }
                    }
                }
            }
        }

        // ---- Region borders -------------------------------------------------------------------

        [Fact]
        public void BlocksOnRegionBordersGetSubBlocksToo()
        {
            for (int seed = 1; seed <= 12; seed++)
            {
                var root = Generate(seed, ("CoastDetail", "0"), ("FeatureDetail", "0"), ("Hydrology", "0")).World.WorldBlock;
                int bordered = 0;
                for (int x = 0; x < root.Width; x++)
                {
                    for (int y = 0; y < root.Height; y++)
                    {
                        var block = root.ChildBlocks[x, y];
                        bool onBorder = false;
                        if (block.Status.WaterStatus != WorldBlockWaterStatus.Water)
                            for (int dx = -1; dx <= 1; dx++)
                                for (int dy = -1; dy <= 1; dy++)
                                {
                                    int nx = x + dx, ny = y + dy;
                                    if (nx < 0 || ny < 0 || nx >= root.Width || ny >= root.Height) continue;
                                    var other = root.ChildBlocks[nx, ny];
                                    if (other.Status.WaterStatus != WorldBlockWaterStatus.Water && !ReferenceEquals(other.Status.Region, block.Status.Region)) onBorder = true;
                                }
                        Assert.Equal(onBorder, block.ChildBlocks != null);
                        if (onBorder) bordered++;
                    }
                }
                Assert.True(bordered > 0 || seed > 1);
            }
        }

        [Fact]
        public void RegionBordersWindInsteadOfFollowingBlockEdges()
        {
            Func<(string, string)[], double> sharePulledAcross = rules =>
            {
                long total = 0, differing = 0;
                for (int seed = 1; seed <= 12; seed++)
                {
                    var root = Generate(seed, rules).World.WorldBlock;
                    foreach (var block in root.ChildBlocksAsList().Where(b => b.ChildBlocks != null && b.Status.WaterStatus != WorldBlockWaterStatus.Water))
                        foreach (var sub in block.ChildBlocksAsList().Where(s => s.Status.WaterStatus != WorldBlockWaterStatus.Water))
                        {
                            total++;
                            if (!ReferenceEquals(sub.Status.Region, block.Status.Region)) differing++;
                        }
                }
                return differing / (double)total;
            };

            // Without winding borders, borders follow block edges. (A few sub-blocks still change hands: a small piece that a
            // coastal inlet cuts off from its region is given to the neighbouring region it touches.)
            double straight = sharePulledAcross(new[] { ("RegionBorderDetail", "0") });
            double wound = sharePulledAcross(new (string, string)[0]);
            Assert.True(straight < 0.01, "straight borders still moved " + straight);
            Assert.InRange(wound, 0.01, 0.4);                    // a real share crosses over, but borders stay near where they were
            Assert.True(wound > straight * 5, "winding " + wound + " vs none " + straight);
        }

        [Fact]
        public void FineRegionBordersFollowTheFineTerrain()
        {
            // Within blocks that a border runs through, the landscape changes more often across the border than inside a
            // region, and more so when borders are drawn to follow it
            Func<string, double> contrast = weight =>
            {
                long border = 0, borderChange = 0, inner = 0, innerChange = 0;
                for (int seed = 1; seed <= 10; seed++)
                {
                    var fine = new FineMap(Generate(seed, ("RegionTerrainWeight", weight)).World);
                    for (int x = 0; x + 1 < fine.Width; x++)
                        for (int y = 0; y + 1 < fine.Height; y++)
                        {
                            if (!fine.IsLand(x, y) || fine.Status[x, y].Terrain == null) continue;
                            foreach (var (nx, ny) in new[] { (x + 1, y), (x, y + 1) })
                            {
                                if (!fine.IsLand(nx, ny) || fine.Status[nx, ny].Terrain == null || !fine.InDetailBlock[x, y] || !fine.InDetailBlock[nx, ny]) continue;
                                bool change = RegionPartitioner.Landscape(fine.Status[x, y].Terrain) != RegionPartitioner.Landscape(fine.Status[nx, ny].Terrain);
                                if (!ReferenceEquals(fine.Status[x, y].Region, fine.Status[nx, ny].Region)) { border++; if (change) borderChange++; }
                                else { inner++; if (change) innerChange++; }
                            }
                        }
                }
                return (borderChange / (double)border) / (innerChange / (double)inner);
            };
            double following = contrast("4"), ignoring = contrast("0");
            Assert.True(following > 1.3, "landscape changes only " + following.ToString("0.00") + "x as often across borders");
            Assert.True(following > ignoring + 0.2, following.ToString("0.00") + " following the terrain vs " + ignoring.ToString("0.00") + " ignoring it");
        }

        [Fact]
        public void BorderDetailCanBeSwitchedOffAndOnlyAddsDetail()
        {
            for (int seed = 1; seed <= 8; seed++)
            {
                var with = Generate(seed).World;
                var without = Generate(seed, ("RegionBorderDetail", "0")).World;
                Assert.Equal(CoarseDescription(without), CoarseDescription(with));
                Assert.True(with.WorldBlock.ChildBlocksAsList().Count(b => b.ChildBlocks != null) > without.WorldBlock.ChildBlocksAsList().Count(b => b.ChildBlocks != null));
            }
        }

        [Fact]
        public void RegionBordersNeverSplitARegionIntoPieces()
        {
            // A region may have a small offshore islet of its own (separated from it only by water), but winding borders
            // must never leave part of a region cut off by another region: every piece other than its largest must touch
            // no other region.
            for (int seed = 1; seed <= 25; seed++)
            {
                var fine = new FineMap(Generate(seed).World);
                var seen = new bool[fine.Width, fine.Height];
                var pieces = new Dictionary<Region, List<(int Size, bool TouchesOtherRegion)>>();
                for (int x = 0; x < fine.Width; x++)
                {
                    for (int y = 0; y < fine.Height; y++)
                    {
                        if (!fine.IsLand(x, y) || seen[x, y]) continue;
                        var region = fine.Status[x, y].Region;
                        int size = 0;
                        bool touchesOtherRegion = false;
                        var stack = new Stack<(int, int)>();
                        stack.Push((x, y));
                        seen[x, y] = true;
                        while (stack.Count > 0)
                        {
                            var (px, py) = stack.Pop();
                            size++;
                            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                            {
                                int nx = px + dx, ny = py + dy;
                                if (nx < 0 || ny < 0 || nx >= fine.Width || ny >= fine.Height || !fine.IsLand(nx, ny)) continue;
                                if (!ReferenceEquals(fine.Status[nx, ny].Region, region)) { touchesOtherRegion = true; continue; }
                                if (seen[nx, ny]) continue;
                                seen[nx, ny] = true;
                                stack.Push((nx, ny));
                            }
                        }
                        if (!pieces.ContainsKey(region)) pieces[region] = new List<(int, bool)>();
                        pieces[region].Add((size, touchesOtherRegion));
                    }
                }

                foreach (var entry in pieces)
                {
                    var ordered = entry.Value.OrderByDescending(p => p.Size).ToList();
                    foreach (var extra in ordered.Skip(1))
                        Assert.False(extra.TouchesOtherRegion, "seed " + seed + ": " + entry.Key.Name + " has a piece of " + extra.Size + " cells cut off by another region");
                }
            }
        }

        [Fact]
        public void NoTinySpecksOfFineLand()
        {
            var minimum = NoiseGeneratorRules.Load(new SimEngine(1).SimData).DetailMinIsletCells;
            Func<string, int> specks = setting =>
            {
                int found = 0;
                for (int seed = 1; seed <= 12; seed++)
                {
                    var fine = new FineMap(Generate(seed, ("DetailMinIsletCells", setting)).World);
                    var land = new bool[fine.Width, fine.Height];
                    for (int x = 0; x < fine.Width; x++) for (int y = 0; y < fine.Height; y++) land[x, y] = fine.IsLand(x, y);
                    found += GridLabeling.Label(land).Sizes.Count(size => size < minimum);
                }
                return found;
            };

            Assert.Equal(0, specks(minimum.ToString()));
            Assert.True(specks("1") > 0, "Specks never appear without the rule, so it is not being tested");
        }

        // ---- Seams ----------------------------------------------------------------------------

        [Fact]
        public void CoastlinesCarryOnSeamlesslyAcrossBlockBoundaries()
        {
            // If blocks were shaped independently, land and water would flip far more often across a block boundary than
            // inside a block. Compare the rate of change over boundaries with the rate inside blocks.
            long insideChanges = 0, insidePairs = 0, acrossChanges = 0, acrossPairs = 0;
            for (int seed = 1; seed <= 20; seed++)
            {
                var fine = new FineMap(Generate(seed).World);
                for (int x = 0; x < fine.Width - 1; x++)
                {
                    for (int y = 0; y < fine.Height; y++)
                    {
                        if (!fine.InDetailBlock[x, y] || !fine.InDetailBlock[x + 1, y]) continue;
                        bool changes = fine.IsLand(x, y) != fine.IsLand(x + 1, y);
                        if ((x + 1) % N == 0) { acrossPairs++; if (changes) acrossChanges++; }
                        else { insidePairs++; if (changes) insideChanges++; }
                    }
                }
            }
            double inside = insideChanges / (double)insidePairs, across = acrossChanges / (double)acrossPairs;
            Assert.True(insidePairs > 1000 && acrossPairs > 1000, "Too little detail to test");
            Assert.InRange(across / inside, 0.6, 1.5);
        }

        [Fact]
        public void StrongerDetailGivesMoreRuggedCoasts()
        {
            Func<string, double> coastCells = strength => Enumerable.Range(1, 8).Average(seed =>
                new FineMap(Generate(seed, ("DetailStrength", strength)).World).CountCoast());
            Assert.True(coastCells("0.3") > coastCells("0") * 1.05, "Wiggle should lengthen the coastline");
        }

        // ---- Settings -------------------------------------------------------------------------

        [Theory]
        [InlineData("CoastDetail", "2")]
        [InlineData("DetailStrength", "-1")]
        [InlineData("DetailRange", "0")]
        [InlineData("DetailRange", "1.5")]
        [InlineData("DetailFrequency", "0")]
        [InlineData("DetailOctaves", "0")]
        public void OutOfRangeDetailSettingsAreRejected(string key, string value)
        {
            var ex = Assert.Throws<InvalidDataException>(() => Generate(1, (key, value)));
            Assert.Contains(key, ex.Message);
        }
    }
}
