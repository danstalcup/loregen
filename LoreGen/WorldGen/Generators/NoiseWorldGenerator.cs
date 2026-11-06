using System;
using System.Collections.Generic;
using System.Linq;

using LoreGen.Noise;
using LoreGen.Randomizer;
using LoreGen.Simulation;

namespace LoreGen.WorldGen.Generators
{
    /// <summary>
    /// Builds the land from a height field of fractal noise instead of carving it out of a partitioned grid.
    ///
    /// An elevation value is computed for every block (fractal noise, bent by domain warping, pulled up around a few
    /// randomly placed continent centres and down toward the map edges). Sea level is then chosen so that a target
    /// share of the map is land. Continents, islands, inland seas and oceans are discovered from the result rather than
    /// decided beforehand, so the number of continents is a target, not a promise.
    ///
    /// Settings are in NoiseGeneratorRules.csv; world size and the number of continents and regions come from the
    /// shared WorldRules.csv.
    /// </summary>
    public class NoiseWorldGenerator : IWorldGenerator
    {
        /// <summary>
        /// Id of this generator.
        /// </summary>
        public const string GeneratorId = "noise";

        public string Id { get { return GeneratorId; } }

        public string DisplayName { get { return "Noise"; } }

        public string Description
        {
            get { return "Land is shaped by an elevation map of fractal noise, so continents have natural coastlines, peninsulas, islands and inland seas."; }
        }

        public string Version { get { return "1"; } }

        private const double Epsilon = 0.001;

        public World Generate(SimEngine engine)
        {
            NoiseGeneratorRules rules = NoiseGeneratorRules.Load(engine.SimData);
            Rnd rnd = engine.Rnd;

            World world = new World(engine);
            WorldGridBuilder.InitializeWorldBlock(world);
            WorldBlock[,] grid = world.WorldBlock.ChildBlocks;
            int width = world.WorldBlock.Width, height = world.WorldBlock.Height;

            // 1. Elevation, and the sea level that gives the target share of land
            double landFraction = Math.Min(rules.LandFractionMax, Math.Max(rules.LandFractionMin, rnd.Normal(rules.LandFractionMean, rules.LandFractionStdDev)));
            Heightmap field = BuildField(engine, rules, width, height);
            double seaLevel = field.Percentile(1.0 - landFraction);
            double low = field.Min(), high = field.Max();

            bool[,] land = new bool[width, height];
            double[,] elevation = new double[width, height];
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    double value = field[x, y];
                    land[x, y] = value >= seaLevel;
                    elevation[x, y] = land[x, y]
                        ? Math.Max(Epsilon, high > seaLevel ? (value - seaLevel) / (high - seaLevel) : Epsilon)
                        : Math.Min(-Epsilon, seaLevel > low ? (value - seaLevel) / (seaLevel - low) : -Epsilon);
                }
            }

            // 2. Tidy up: drop specks of land too small to be an island, then fill in puddles too small to be a sea
            RemoveSmallLandmasses(land, elevation, rules.MinLandmassBlocks);
            FillSmallEnclosedWater(land, elevation, rules.MinSeaBlocks);

            // 3. Record what each block is
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    WorldBlockStatus status = grid[x, y].Status;
                    status.Elevation = elevation[x, y];
                    status.WaterStatus = !land[x, y] ? WorldBlockWaterStatus.Water
                        : TouchesWater(land, x, y, width, height) ? WorldBlockWaterStatus.Coastline
                        : WorldBlockWaterStatus.Land;
                }
            }

            // 4. Continents are the connected pieces of land
            GridLabeling landmasses = GridLabeling.Label(land);
            List<List<WorldBlock>> landmassBlocks = BlocksByLabel(grid, landmasses);
            foreach (List<WorldBlock> blocks in landmassBlocks)
            {
                Continent continent = Continent.GenerateContinentWithAreaFromStartingBlock(world, blocks[0]);
                foreach (WorldBlock block in blocks.Skip(1)) continent.AddBlock(block);
            }

            // 5. Water: enclosed bodies are seas of the continent they touch most; the rest is split into oceans
            bool[,] water = new bool[width, height];
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    water[x, y] = !land[x, y];
            GridLabeling bodies = GridLabeling.Label(water);
            List<List<WorldBlock>> bodyBlocks = BlocksByLabel(grid, bodies);

            int openWaterBlocks = 0;
            for (int i = 0; i < bodies.Count; i++)
                if (bodies.TouchesEdge[i]) openWaterBlocks += bodies.Sizes[i];
            int oceanTarget = Math.Max(1, world.Continents.Count);

            for (int i = 0; i < bodies.Count; i++)
            {
                if (bodies.TouchesEdge[i]) CreateOceans(world, bodyBlocks[i], oceanTarget, openWaterBlocks, rnd);
                else CreateSea(world, bodyBlocks[i]);
            }

            // 6. Regions: each continent is divided up, and each region gets a biome, a language and terrain
            CreateRegions(engine, world);

            return world;
        }

        // ---- Elevation ------------------------------------------------------------------------

        private static Heightmap BuildField(SimEngine engine, NoiseGeneratorRules rules, int width, int height)
        {
            Rnd rnd = engine.Rnd;
            WorldRules worldRules = engine.SimData.WorldRules;
            FractalNoise noise = new FractalNoise(new SimplexNoise(rnd.Unweighted(int.MaxValue)),
                rules.Octaves, rules.Frequency, rules.Lacunarity, rules.Persistence);

            int centreCount = worldRules.ContinentsMax > worldRules.ContinentsMin
                ? rnd.Unweighted(worldRules.ContinentsMin, worldRules.ContinentsMax)
                : worldRules.ContinentsMin;
            centreCount = Math.Max(1, centreCount);
            double[] centreU = new double[centreCount], centreV = new double[centreCount], radius = new double[centreCount];
            for (int i = 0; i < centreCount; i++)
            {
                centreU[i] = rnd.Unweighted(0.12, 0.88);
                centreV[i] = rnd.Unweighted(0.15, 0.85);
                radius[i] = rnd.Unweighted(rules.MaskRadiusMin, rules.MaskRadiusMax);
            }

            // Work in units where the map is `aspect` wide and 1 tall, so features aren't stretched
            double aspect = (double)width / height;
            return Heightmap.Sample(width, height, (u, v) =>
            {
                double n = (noise.Warped(u * aspect, v, rules.WarpStrength) + 1.0) * 0.5;

                double mask = 0.0;
                for (int i = 0; i < centreCount; i++)
                {
                    double dx = (u - centreU[i]) * aspect, dy = v - centreV[i];
                    double d = Math.Sqrt(dx * dx + dy * dy) / radius[i];
                    if (d < 1.0) mask = Math.Max(mask, 1.0 - Smoothstep(0.0, 1.0, d));
                }

                double edge = Math.Min(Math.Min(u, 1.0 - u), Math.Min(v, 1.0 - v));
                double edgeFactor = Smoothstep(0.0, rules.EdgeBorder, edge);

                return ((1.0 - rules.MaskStrength) * n + rules.MaskStrength * mask) * edgeFactor;
            });
        }

        private static double Smoothstep(double from, double to, double x)
        {
            double t = Math.Min(1.0, Math.Max(0.0, (x - from) / (to - from)));
            return t * t * (3.0 - 2.0 * t);
        }

        // ---- Tidying --------------------------------------------------------------------------

        private static void RemoveSmallLandmasses(bool[,] land, double[,] elevation, int minimum)
        {
            GridLabeling labels = GridLabeling.Label(land);
            if (labels.Count == 0) return;

            // Never remove everything: if no piece is big enough, keep the largest
            bool anyBigEnough = labels.Sizes.Any(s => s >= minimum);
            int largest = labels.Sizes.IndexOf(labels.Sizes.Max());

            for (int x = 0; x < land.GetLength(0); x++)
            {
                for (int y = 0; y < land.GetLength(1); y++)
                {
                    int label = labels.Labels[x, y];
                    if (label < 0 || labels.Sizes[label] >= minimum) continue;
                    if (!anyBigEnough && label == largest) continue;
                    land[x, y] = false;
                    elevation[x, y] = Math.Min(elevation[x, y], -Epsilon);
                }
            }
        }

        private static void FillSmallEnclosedWater(bool[,] land, double[,] elevation, int minimum)
        {
            int width = land.GetLength(0), height = land.GetLength(1);
            bool[,] water = new bool[width, height];
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    water[x, y] = !land[x, y];

            GridLabeling labels = GridLabeling.Label(water);
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    int label = labels.Labels[x, y];
                    if (label < 0 || labels.TouchesEdge[label] || labels.Sizes[label] >= minimum) continue;
                    land[x, y] = true;
                    elevation[x, y] = Math.Max(elevation[x, y], Epsilon);
                }
            }
        }

        private static bool TouchesWater(bool[,] land, int x, int y, int width, int height)
        {
            return (x > 0 && !land[x - 1, y]) || (x < width - 1 && !land[x + 1, y])
                || (y > 0 && !land[x, y - 1]) || (y < height - 1 && !land[x, y + 1]);
        }

        private static List<List<WorldBlock>> BlocksByLabel(WorldBlock[,] grid, GridLabeling labeling)
        {
            List<List<WorldBlock>> groups = new List<List<WorldBlock>>();
            for (int i = 0; i < labeling.Count; i++) groups.Add(new List<WorldBlock>());
            for (int y = 0; y < grid.GetLength(1); y++)
                for (int x = 0; x < grid.GetLength(0); x++)
                    if (labeling.Labels[x, y] >= 0) groups[labeling.Labels[x, y]].Add(grid[x, y]);
            return groups;
        }

        // ---- Water ----------------------------------------------------------------------------

        private static void CreateOceans(World world, List<WorldBlock> body, int oceanTarget, int openWaterBlocks, Rnd rnd)
        {
            // Bigger bodies of water get proportionally more of the oceans, but every body gets at least one
            int count = (int)Math.Round(oceanTarget * (double)body.Count / openWaterBlocks, MidpointRounding.AwayFromZero);
            count = Math.Max(1, Math.Min(count, body.Count));

            List<WorldBlock> remaining = body.ToList();
            for (int i = 0; i < count; i++)
            {
                WorldBlock start = ListR<WorldBlock>.RandomFromList(remaining, rnd);
                remaining.Remove(start);
                new Ocean(world, new WorldBlockArea(new List<WorldBlock> { start }, null));
            }
            BlockGrowth.Grow(body, b => b.Status.Ocean, (b, ocean) => ocean.AddBlock(b), rnd);
        }

        private static void CreateSea(World world, List<WorldBlock> body)
        {
            // The sea belongs to whichever continent has the most land touching it (the first one wins a tie)
            Dictionary<Continent, int> contact = new Dictionary<Continent, int>();
            foreach (WorldBlock block in body)
                foreach (WorldBlock neighbour in block.SurroundingBlocksAsList())
                    if (neighbour.Status.Continent != null)
                        contact[neighbour.Status.Continent] = (contact.ContainsKey(neighbour.Status.Continent) ? contact[neighbour.Status.Continent] : 0) + 1;

            Continent owner = null;
            foreach (Continent continent in world.Continents)
                if (contact.ContainsKey(continent) && (owner == null || contact[continent] > contact[owner]))
                    owner = continent;

            new Sea(owner, body);
        }

        // ---- Regions --------------------------------------------------------------------------

        private static void CreateRegions(SimEngine engine, World world)
        {
            Rnd rnd = engine.Rnd;
            WorldRules rules = engine.SimData.WorldRules;

            int totalArea = world.Continents.Sum(c => c.ContinentArea.Area());
            int numRegions = (int)rnd.Normal(rules.RegionsMean, rules.RegionsStdDev);
            numRegions = Math.Min(numRegions, rules.RegionsMax);
            numRegions = Math.Max(numRegions, rules.RegionsMin);

            foreach (Continent continent in world.Continents.ToList())
            {
                List<WorldBlock> blocks = continent.ContinentArea.Blocks().ToList();

                // Share of the regions by size, but every continent has at least one and none has more than it has blocks
                int count = (int)((double)blocks.Count / totalArea * numRegions);
                count = Math.Max(1, Math.Min(count, blocks.Count));

                List<WorldBlock> candidates = blocks.ToList();
                for (int i = 0; i < count; i++)
                {
                    WorldBlock start = ListR<WorldBlock>.RandomFromList(candidates, rnd);
                    candidates.Remove(start);
                    Region.GenerateRegion(continent, new WorldBlockArea(new List<WorldBlock> { start }, null));
                }
                BlockGrowth.Grow(blocks, b => b.Status.Region, (b, region) => region.AddBlock(b), rnd);
            }
        }
    }
}
