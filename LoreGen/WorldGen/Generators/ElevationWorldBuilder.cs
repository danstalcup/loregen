using System;
using System.Collections.Generic;
using System.Linq;

using LoreGen.Features;
using LoreGen.Noise;
using LoreGen.Randomizer;
using LoreGen.Simulation;
using LoreGen.Terrains;

namespace LoreGen.WorldGen.Generators
{
    /// <summary>
    /// Everything that happens to a world once its elevation is known, shared by the generators that shape the land with
    /// an elevation field (<see cref="NoiseWorldGenerator"/>, <see cref="TectonicWorldGenerator"/>). They differ only in
    /// how they make the field; from there:
    ///
    ///  1. Sea level is chosen so that a target share of the map is land, and elevations are scaled to -1..1.
    ///  2. Specks of land too small to be an island are dropped and puddles too small to be a sea are filled.
    ///  3-5. Continents are the connected pieces of land; enclosed water is a sea of the continent it touches most, and
    ///     open water is split into oceans.
    ///  6. Temperature and moisture are worked out from latitude, altitude, nearness to water and noise.
    ///  7. Geographic features (mountain ranges, deserts) are found.
    ///  8. Each continent is divided into regions that follow slopes, climate and feature edges, each with a biome fitting
    ///     its climate and terrain roughness; higher ground favours steeper terrain.
    ///  9. Blocks along coasts, region borders and feature edges (and all mountain blocks) get sub-blocks (CoastalDetail).
    ///  10. The main crests of each mountain range are named as sub-ranges.
    ///
    /// Settings come from NoiseGeneratorRules.csv.
    /// </summary>
    internal static class ElevationWorldBuilder
    {
        private const double Epsilon = 0.001;

        /// <summary>
        /// The share of the map that is land: the exact setting if there is one, otherwise random from the mean and spread.
        /// Always draws its random number, even when the setting overrides it, so that giving the setting its usual value
        /// gives the same world as leaving it out.
        /// </summary>
        public static double ChooseLandFraction(NoiseGeneratorRules rules, Rnd rnd)
        {
            double landFraction = Math.Min(rules.LandFractionMax, Math.Max(rules.LandFractionMin, rnd.Normal(rules.LandFractionMean, rules.LandFractionStdDev)));
            if (rules.LandFraction > 0) landFraction = rules.LandFraction;
            return landFraction;
        }

        /// <summary>
        /// The settings every elevation-based generator offers for what happens after the elevation is made: coasts,
        /// climate, regions and terrain, and features.
        /// </summary>
        public static List<GeneratorOption> SharedOptions(SimData data)
        {
            RulesFile file = data.LoadRules(NoiseGeneratorRules.Filename);
            Func<string, double> usual = key => file.D(key);
            return new List<GeneratorOption>
            {
                GeneratorOption.Toggle("CoastDetail", "Fine coastlines", "Gives coastal blocks a finer grid of sub-blocks. Switch off for a lighter world.", "Coasts", usual("CoastDetail") != 0),
                GeneratorOption.Number("DetailStrength", "Coast ruggedness", "How rugged fine coastlines are. 0 is smooth curves.", "Coasts", 0, 0.4, 0.01, usual("DetailStrength")),

                GeneratorOption.Number("TemperatureOffset", "Global temperature", "Negative is an ice age, positive a hothouse.", "Climate", -0.3, 0.3, 0.01, usual("TemperatureOffset")),
                GeneratorOption.Number("TemperatureLapse", "Altitude chill", "How much colder high ground is.", "Climate", 0, 1, 0.05, usual("TemperatureLapse")),
                GeneratorOption.Number("MoistureColdPenalty", "Cold dryness", "How much drier cold places are.", "Climate", 0, 1, 0.05, usual("MoistureColdPenalty")),

                GeneratorOption.Number("TerrainReliefStrength", "Terrain follows height", "How strongly high ground favours steep terrain such as peaks. 0 ignores height.", "Regions and terrain", 0, 2, 0.1, usual("TerrainReliefStrength")),
                GeneratorOption.Integer("RegionTarget", "Regions (target)", "About how many regions the world is divided into. Very small regions are merged, so the final number is a little lower.", "Regions and terrain", 0, 150, (int)usual("RegionTarget"), "random"),
                GeneratorOption.Number("RegionSlopeWeight", "Terrain-following borders", "How strongly region borders stick to steep ground. 0 ignores the lie of the land.", "Regions and terrain", 0, 30, 1, usual("RegionSlopeWeight")),
                GeneratorOption.Number("RegionFeatureWeight", "Feature-following borders", "How strongly region borders follow the edges of features such as mountain ranges. 0 ignores features.", "Regions and terrain", 0, 10, 0.5, usual("RegionFeatureWeight")),
                GeneratorOption.Toggle("RegionBorderDetail", "Winding region borders", "Gives blocks on region borders a finer grid so borders wind. Switch off for a lighter world.", "Regions and terrain", usual("RegionBorderDetail") != 0),

                GeneratorOption.Toggle("MountainRanges", "Mountain ranges", "Gathers high ground into named mountain ranges.", "Features", usual("MountainRanges") != 0),
                GeneratorOption.Number("MountainElevationFrom", "Mountain height", "How high land must be to count as part of a mountain range, as a share of the highest point. Lower gives more and bigger ranges.", "Features", 0.2, 0.95, 0.05, usual("MountainElevationFrom")),
                GeneratorOption.Toggle("SubRanges", "Named crests", "Finds the main crests within each mountain range and names them as smaller ranges. Needs fine feature edges.", "Features", usual("SubRanges") != 0),
                GeneratorOption.Number("MountainRelief", "Mountain ruggedness", "How much ridged relief high ground has at fine detail. 0 is smooth.", "Features", 0, 0.5, 0.05, usual("MountainRelief")),
                GeneratorOption.Toggle("Deserts", "Deserts", "Gathers dry, warm land into named deserts.", "Features", usual("Deserts") != 0),
                GeneratorOption.Number("DesertMoistureTo", "Desert dryness", "How dry land must be to count as desert. Higher gives more and bigger deserts.", "Features", 0.02, 0.5, 0.01, usual("DesertMoistureTo")),
                GeneratorOption.Toggle("FeatureDetail", "Fine feature edges", "Gives blocks along feature edges and in mountain ranges a finer grid of sub-blocks, so edges wind and ranges have crests. Switch off for a lighter world.", "Features", usual("FeatureDetail") != 0),
                GeneratorOption.Number("FeatureEdgeStrength", "Feature edge ruggedness", "How much fine feature edges such as a desert's wind. 0 is a smooth curve.", "Features", 0, 0.45, 0.05, usual("FeatureEdgeStrength")),
            };
        }

        /// <summary>
        /// Turns an elevation field over the world's top-level blocks into a finished world.
        /// </summary>
        /// <param name="engine">The engine</param>
        /// <param name="world">The world, with its top-level grid of blocks already made</param>
        /// <param name="rules">Settings</param>
        /// <param name="field">Elevation for every top-level block, in any units: only the order of values matters</param>
        /// <param name="landFraction">Share of the map that is to be land</param>
        public static World Build(SimEngine engine, World world, NoiseGeneratorRules rules, Heightmap field, double landFraction)
        {
            Rnd rnd = engine.Rnd;
            WorldBlock[,] grid = world.WorldBlock.ChildBlocks;
            int width = world.WorldBlock.Width, height = world.WorldBlock.Height;

            // 1. The sea level that gives the target share of land
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

            // 7. Geographic features, found in the land's shape and climate
            MountainRangeDetector.Detect(engine, world, rules);
            DesertDetector.Detect(engine, world, rules);

            // 8. Regions: each continent is divided up, and each region gets a biome fitting its climate, a language and terrain
            TerrainPicker terrain = new TerrainPicker(engine, rules);
            CreateRegions(engine, world, rules, terrain);

            // 9. Fine-grained coastlines, region borders and feature edges, and ridged relief on high ground
            if (rules.CoastDetail != 0 || rules.RegionBorderDetail != 0 || rules.FeatureDetail != 0) CoastalDetail.Apply(engine, world, rules, land, elevation, terrain);

            // 10. Features found in the fine detail: the crests within each mountain range
            SubRangeDetector.Detect(engine, world, rules);

            return world;
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
                List<List<WorldBlock>> shapes = RegionPartitioner.Partition(blocks, count, rules, rnd, FeatureSignatures(world, blocks));

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

        // For each block, which features cover it, as one comparable value: blocks with different values lie on different
        // sides of a feature's edge. Blocks covered by no feature get 0.
        private static Dictionary<WorldBlock, int> FeatureSignatures(World world, List<WorldBlock> blocks)
        {
            Dictionary<WorldBlock, int> signatures = new Dictionary<WorldBlock, int>();
            Dictionary<GeographicFeature, int> number = new Dictionary<GeographicFeature, int>();
            for (int i = 0; i < world.Features.Count; i++) number[world.Features[i]] = i + 1;
            foreach (WorldBlock block in blocks)
            {
                int signature = 0;
                foreach (GeographicFeature feature in world.FeaturesAt(block)) signature = unchecked(signature * 31 + number[feature]);
                signatures[block] = signature;
            }
            return signatures;
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
