using System;
using System.Collections.Generic;
using System.Linq;

using LoreGen.Noise;
using LoreGen.Randomizer;
using LoreGen.Simulation;
using LoreGen.Terrains;

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
    /// Temperature and moisture are worked out from latitude, altitude, nearness to water and noise, and each region gets
    /// the existing biome that best fits its climate and terrain roughness; within a region, higher ground favours steeper
    /// terrain. Blocks along the coast then get a finer grid of sub-blocks (see CoastalDetail).
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

        public string Version { get { return "4"; } }

        private const double Epsilon = 0.001;

        public IReadOnlyList<GeneratorOption> GetOptions(SimData data)
        {
            // Defaults come from the rules file, so they follow any customised data
            RulesFile file = data.LoadRules(NoiseGeneratorRules.Filename);
            Func<string, double> usual = key => file.D(key);

            return new List<GeneratorOption>
            {
                GeneratorOption.Number("LandFraction", "Land share", "How much of the map is land. Random varies it from world to world.", "Land", 0, 0.7, 0.01, usual("LandFraction"), "random"),
                GeneratorOption.Integer("ContinentCentres", "Land centres", "How many places land gathers around. More centres spread the land over more of the map. How many continents result varies, since land can merge or break apart.", "Land", 0, 12, (int)usual("ContinentCentres"), "random"),
                GeneratorOption.Number("MaskStrength", "Continent blobbiness", "Low: land is shaped by noise alone and is scattered. High: land gathers into smooth blobs around the centres.", "Land", 0, 1, 0.05, usual("MaskStrength")),
                GeneratorOption.Number("Frequency", "Fragmentation", "Higher gives smaller, more numerous features.", "Land", 1, 8, 0.25, usual("Frequency")),
                GeneratorOption.Number("WarpStrength", "Coast winding", "How much coastlines twist and wind.", "Land", 0, 1.5, 0.05, usual("WarpStrength")),

                GeneratorOption.Toggle("CoastDetail", "Fine coastlines", "Gives coastal blocks a finer grid of sub-blocks. Switch off for a lighter world.", "Coasts", usual("CoastDetail") != 0),
                GeneratorOption.Number("DetailStrength", "Coast ruggedness", "How rugged fine coastlines are. 0 is smooth curves.", "Coasts", 0, 0.4, 0.01, usual("DetailStrength")),

                GeneratorOption.Number("TemperatureOffset", "Global temperature", "Negative is an ice age, positive a hothouse.", "Climate", -0.3, 0.3, 0.01, usual("TemperatureOffset")),
                GeneratorOption.Number("TemperatureLapse", "Altitude chill", "How much colder high ground is.", "Climate", 0, 1, 0.05, usual("TemperatureLapse")),
                GeneratorOption.Number("MoistureColdPenalty", "Cold dryness", "How much drier cold places are.", "Climate", 0, 1, 0.05, usual("MoistureColdPenalty")),

                GeneratorOption.Number("TerrainReliefStrength", "Terrain follows height", "How strongly high ground favours steep terrain such as peaks. 0 ignores height.", "Regions and terrain", 0, 2, 0.1, usual("TerrainReliefStrength")),
                GeneratorOption.Integer("RegionTarget", "Regions (target)", "About how many regions the world is divided into. Very small regions are merged, so the final number is a little lower.", "Regions and terrain", 0, 150, (int)usual("RegionTarget"), "random"),
                GeneratorOption.Number("RegionSlopeWeight", "Terrain-following borders", "How strongly region borders stick to steep ground. 0 ignores the lie of the land.", "Regions and terrain", 0, 30, 1, usual("RegionSlopeWeight")),
                GeneratorOption.Toggle("RegionBorderDetail", "Winding region borders", "Gives blocks on region borders a finer grid so borders wind. Switch off for a lighter world.", "Regions and terrain", usual("RegionBorderDetail") != 0),
            };
        }

        public World Generate(SimEngine engine)
        {
            NoiseGeneratorRules rules = NoiseGeneratorRules.Load(engine.SimData, engine.Configuration.GeneratorSettings);
            Rnd rnd = engine.Rnd;

            World world = new World(engine);
            WorldGridBuilder.InitializeWorldBlock(world);
            WorldBlock[,] grid = world.WorldBlock.ChildBlocks;
            int width = world.WorldBlock.Width, height = world.WorldBlock.Height;

            // 1. Elevation, and the sea level that gives the target share of land
            // (The random choice is always made, even when a setting then overrides it, so that giving a setting its usual
            // value gives the same world as leaving it out.)
            double landFraction = Math.Min(rules.LandFractionMax, Math.Max(rules.LandFractionMin, rnd.Normal(rules.LandFractionMean, rules.LandFractionStdDev)));
            if (rules.LandFraction > 0) landFraction = rules.LandFraction;
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
            MergeLandmassesTouchingAtCorners(land, elevation);
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

            // 6. Climate: temperature and moisture for every block
            ClimateField climate = ClimateModel.Compute(engine, rules, land, elevation);
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    grid[x, y].Status.Temperature = climate.Temperature[x, y];
                    grid[x, y].Status.Moisture = climate.Moisture[x, y];
                }
            }

            // 7. Regions: each continent is divided up, and each region gets a biome fitting its climate, a language and terrain
            TerrainPicker terrain = new TerrainPicker(engine, rules);
            CreateRegions(engine, world, rules, terrain);

            // 8. Fine-grained coastlines and region borders
            if (rules.CoastDetail != 0 || rules.RegionBorderDetail != 0) CoastalDetail.Apply(engine, world, rules, land, elevation, terrain);

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
            if (rules.ContinentCentres > 0) centreCount = rules.ContinentCentres;
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

        /// <summary>
        /// Land that only meets other land at a corner is one landmass, not two. Where two separate pieces touch
        /// diagonally, joins them by turning one of the two water blocks between them into land.
        /// </summary>
        private static void MergeLandmassesTouchingAtCorners(bool[,] land, double[,] elevation)
        {
            int width = land.GetLength(0), height = land.GetLength(1);
            bool merged;
            do
            {
                merged = false;
                GridLabeling labels = GridLabeling.Label(land);
                for (int y = 0; y < height - 1 && !merged; y++)
                {
                    for (int x = 0; x < width - 1 && !merged; x++)
                    {
                        // The 2x2 window is   a b   with a and d (or b and c) land and the other two water
                        //                     c d
                        if (land[x, y] && land[x + 1, y + 1] && !land[x + 1, y] && !land[x, y + 1]
                            && labels.Labels[x, y] != labels.Labels[x + 1, y + 1])
                        {
                            Raise(land, elevation, x + 1, y, x, y + 1);
                            merged = true;
                        }
                        else if (land[x + 1, y] && land[x, y + 1] && !land[x, y] && !land[x + 1, y + 1]
                            && labels.Labels[x + 1, y] != labels.Labels[x, y + 1])
                        {
                            Raise(land, elevation, x, y, x + 1, y + 1);
                            merged = true;
                        }
                    }
                }
            } while (merged); // each merge joins two pieces, so this ends
        }

        // Turns the higher (closer to sea level) of two water blocks into land
        private static void Raise(bool[,] land, double[,] elevation, int x1, int y1, int x2, int y2)
        {
            bool first = elevation[x1, y1] >= elevation[x2, y2];
            int x = first ? x1 : x2, y = first ? y1 : y2;
            land[x, y] = true;
            elevation[x, y] = Math.Max(elevation[x, y], Epsilon);
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

        private static void CreateRegions(SimEngine engine, World world, NoiseGeneratorRules rules, TerrainPicker terrain)
        {
            Rnd rnd = engine.Rnd;
            WorldRules worldRules = engine.SimData.WorldRules;

            int totalArea = world.Continents.Sum(c => c.ContinentArea.Area());
            int numRegions = (int)rnd.Normal(worldRules.RegionsMean, worldRules.RegionsStdDev);
            numRegions = Math.Min(numRegions, worldRules.RegionsMax);
            numRegions = Math.Max(numRegions, worldRules.RegionsMin);
            if (rules.RegionTarget > 0) numRegions = rules.RegionTarget;

            foreach (Continent continent in world.Continents.ToList())
            {
                List<WorldBlock> blocks = continent.ContinentArea.Blocks().ToList();

                // Share of the regions by size, but every continent has at least one and none has more than it has blocks
                int count = (int)((double)blocks.Count / totalArea * numRegions);
                count = Math.Max(1, Math.Min(count, blocks.Count));

                // Work out the regions' shapes from the lie of the land first...
                List<List<WorldBlock>> shapes = RegionPartitioner.Partition(blocks, count, rules, rnd);

                // ...then, knowing each region's climate, choose its biome and create it
                foreach (List<WorldBlock> shape in shapes)
                {
                    Biome biome = ChooseBiome(engine, rules, shape, rnd);
                    Region region = Region.GenerateRegion(continent, new WorldBlockArea(shape, null), biome);
                    foreach (WorldBlock block in shape)
                        block.Status.Terrain = terrain.Pick(biome, block.Status.Elevation.Value);
                }
            }
        }

        private static Biome ChooseBiome(SimEngine engine, NoiseGeneratorRules rules, List<WorldBlock> blocks, Rnd rnd)
        {
            double temperature = blocks.Average(b => b.Status.Temperature.Value);
            double moisture = blocks.Average(b => b.Status.Moisture.Value);

            // Terrain roughness: how high the region is, plus how much that varies across it
            double meanElevation = blocks.Average(b => b.Status.Elevation.Value);
            double spread = Math.Sqrt(blocks.Average(b => (b.Status.Elevation.Value - meanElevation) * (b.Status.Elevation.Value - meanElevation)));

            return BiomeSelector.Choose(engine.SimData.Biomes, ClimateScale.Temperature(temperature), ClimateScale.Precipitation(moisture),
                BiomeSelector.Contour(meanElevation + spread, rules), rules, rnd);
        }
    }
}
