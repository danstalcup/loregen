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
    ///  6b. Hydrology: where the rain runs off to, which hollows hold lakes, and the courses of the large rivers.
    ///  7. Geographic features (mountain ranges, deserts) are found.
    ///  8. Every land block's terrain is chosen from its height, climate, water (rain, rivers and lakes), the features on
    ///     it and its plate boundary (TerrainModel).
    ///  9. Each continent is divided into regions along its landscapes and features (RegionPartitioner), each with a biome
    ///     fitting its terrain and climate.
    ///  10. Blocks along coasts, region borders and feature edges (and all mountain blocks) get sub-blocks (CoastalDetail).
    ///  11. The parts of features are found in the fine detail: subranges, massifs, peaks and passes; ergs, salt flats and oases.
    ///  12. Lakes, large rivers, and the oceans and seas with their bays and straits become features.
    ///  13. Every land sub-block's terrain is chosen the same way as in step 8, now with all the features known.
    ///  14. Region borders through sub-blocks are redrawn to follow the fine terrain and feature edges (RegionBorderRefiner).
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

                GeneratorOption.Number("RainShadow", "Rain shadows", "How much drier land is downwind of mountains. Winds blow from the east in the tropics and near the poles, and from the west in between.", "Climate", 0, 1, 0.05, usual("RainShadow")),

                GeneratorOption.Number("TerrainReliefStrength", "Terrain follows height", "How strongly high ground favours steep terrain such as peaks. 0 ignores height.", "Regions and terrain", 0, 2, 0.1, usual("TerrainReliefStrength")),
                GeneratorOption.Number("WetlandFrom", "Wetlands", "How much water flat ground must hold (rain plus river and lake water) before it turns to marsh. Lower gives more marshes and ponds.", "Regions and terrain", 0.3, 0.95, 0.05, usual("WetlandFrom")),
                GeneratorOption.Number("TreeMoistureFrom", "Forests", "How much water ground must hold before trees grow. Lower gives more woodland and forest.", "Regions and terrain", 0, 0.6, 0.05, usual("TreeMoistureFrom")),
                GeneratorOption.Number("TerrainPatchSize", "Terrain patch size", "About how wide patches of one terrain are, in thousands of kilometres. 0 picks each place's terrain on its own, for a speckled look.", "Regions and terrain", 0, 12, 0.5, usual("TerrainPatchSize")),
                GeneratorOption.Integer("RegionTarget", "Regions (target)", "About how many regions the world is divided into. Very small regions are merged, so the final number is a little lower.", "Regions and terrain", 0, 150, (int)usual("RegionTarget"), "random"),
                GeneratorOption.Number("RegionTerrainWeight", "Terrain-defined regions", "How strongly regions keep to one landscape (the climate, ruggedness and tree cover of their terrain), meeting where the landscape changes. 0 ignores the terrain.", "Regions and terrain", 0, 12, 0.5, usual("RegionTerrainWeight")),
                GeneratorOption.Number("RegionSlopeWeight", "Slope-following borders", "How strongly region borders stick to steep ground. 0 ignores the lie of the land.", "Regions and terrain", 0, 30, 1, usual("RegionSlopeWeight")),
                GeneratorOption.Number("RegionFeatureWeight", "Feature-following borders", "How strongly regions keep to one set of features, meeting at the edges of features such as mountain ranges and deserts. 0 ignores features.", "Regions and terrain", 0, 10, 0.5, usual("RegionFeatureWeight")),
                GeneratorOption.Toggle("RegionBorderDetail", "Winding region borders", "Gives blocks on region borders a finer grid, where borders wind and follow the fine terrain and feature edges. Switch off for a lighter world.", "Regions and terrain", usual("RegionBorderDetail") != 0),

                GeneratorOption.Toggle("MountainRanges", "Mountain ranges", "Gathers high ground into named mountain ranges.", "Features", usual("MountainRanges") != 0),
                GeneratorOption.Number("MountainElevationFrom", "Mountain height", "How high land must be to count as part of a mountain range, as a share of the highest point. Lower gives more and bigger ranges.", "Features", 0.2, 0.95, 0.05, usual("MountainElevationFrom")),
                GeneratorOption.Toggle("SubRanges", "Subranges and massifs", "Finds the main crests within each mountain range and names them as subranges (long crests) and massifs (compact knots of peaks). Needs fine feature edges.", "Features", usual("SubRanges") != 0),
                GeneratorOption.Integer("PeaksPerRange", "Peaks per range", "How many of each mountain range's highest summits are named as peaks. Needs fine feature edges.", "Features", 0, 8, (int)usual("PeaksPerRange")),
                GeneratorOption.Integer("PassesPerRange", "Passes per range", "How many of the deepest saddles between each mountain range's summits are named as passes. Needs fine feature edges.", "Features", 0, 8, (int)usual("PassesPerRange")),
                GeneratorOption.Number("MountainRelief", "Mountain ruggedness", "How much ridged relief high ground has at fine detail. 0 is smooth.", "Features", 0, 0.5, 0.05, usual("MountainRelief")),
                GeneratorOption.Toggle("Deserts", "Deserts", "Gathers dry, warm land into named deserts.", "Features", usual("Deserts") != 0),
                GeneratorOption.Number("DesertMoistureTo", "Desert dryness", "How dry land must be to count as desert. Higher gives more and bigger deserts.", "Features", 0.02, 0.5, 0.01, usual("DesertMoistureTo")),
                GeneratorOption.Toggle("DesertParts", "Ergs, salt flats and oases", "Gives desert ground gentle rises and hollows, and finds and names its sand seas (ergs), salt-crusted basins (salt flats) and oases. Needs fine feature edges.", "Features", usual("DesertParts") != 0),
                GeneratorOption.Toggle("Hydrology", "Lakes and rivers", "Runs the rain off the land: hollows fill into lakes (salt lakes where too little water comes in), and the biggest flows become named rivers, drawn on a finer grid. Switch off for a lighter world.", "Water", usual("Hydrology") != 0),
                GeneratorOption.Number("RiverFlowFrom", "River size", "How much water a river must carry to be named. Lower gives more, shorter rivers.", "Water", 0.3, 4, 0.1, usual("RiverFlowFrom")),
                GeneratorOption.Number("LakeEvaporation", "Lake evaporation", "How much water lakes lose to the air. Higher gives fewer, smaller lakes and more salt lakes.", "Water", 0.1, 3, 0.1, usual("LakeEvaporation")),
                GeneratorOption.Toggle("WaterFeatures", "Bays and straits", "Makes the oceans and seas features, and finds and names their bays and straits.", "Water", usual("WaterFeatures") != 0),
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
            WorldBuildPlan plan = new WorldBuildPlan();
            AddSteps(plan, engine, world, rules, () => field, () => landFraction);
            return plan.RunAll();
        }

        /// <summary>How many steps the fine terrain is split into, so progress keeps moving through it.</summary>
        private const int FineTerrainParts = 4;

        /// <summary>
        /// Adds the steps that turn an elevation field into a finished world to a plan, and sets the plan's result. Step
        /// weights are roughly the milliseconds each step takes for a typical world. The
        /// field and land share are read when the first of these steps runs, so earlier steps of the plan can make them.
        /// </summary>
        /// <param name="plan">The plan to add to</param>
        /// <param name="engine">The engine</param>
        /// <param name="world">The world, with its top-level grid of blocks already made</param>
        /// <param name="rules">Settings</param>
        /// <param name="field">Gives the elevation for every top-level block, in any units: only the order of values matters</param>
        /// <param name="landFraction">Gives the share of the map that is to be land</param>
        public static void AddSteps(WorldBuildPlan plan, SimEngine engine, World world, NoiseGeneratorRules rules,
            Func<Heightmap> field, Func<double> landFraction)
        {
            Rnd rnd = engine.Rnd;
            WorldBlock[,] grid = world.WorldBlock.ChildBlocks;
            int width = world.WorldBlock.Width, height = world.WorldBlock.Height;
            bool[,] land = new bool[width, height];
            double[,] elevation = new double[width, height];
            ClimateField climate = null;
            HydrologyModel hydrology = null;
            bool waterDetail = false;
            TerrainModel terrain = null;
            double[,] hydration = null;

            plan.Add("Land and sea", () =>
            {
                // 1. The sea level that gives the target share of land
                Heightmap heights = field();
                double seaLevel = heights.Percentile(1.0 - landFraction());
                double low = heights.Min(), high = heights.Max();
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        double value = heights[x, y];
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
            }, 1);

            plan.Add("Continents, oceans and seas", () =>
            {
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
            }, 1);

            plan.Add("Climate", () =>
            {
                // 6. Climate: temperature and moisture for every block
                climate = ClimateModel.Compute(engine, rules, land, elevation);
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        grid[x, y].Status.Temperature = climate.Temperature[x, y];
                        grid[x, y].Status.Moisture = climate.Moisture[x, y];
                    }
                }
            }, 1);

            plan.Add("Drainage", () =>
            {
                // 6b. Hydrology: where the rain runs off to, which hollows hold lakes, and the courses of the large rivers.
                // Lakes and rivers are made at fine detail (step 10), so for now this only says where they will be.
                hydrology = HydrologyModel.Build(rules, grid, land, elevation, climate.Moisture, climate.Temperature,
                    engine.Rnd.Seed, engine.SimData.FeatureType(WaterFeatureDetector.RiverTypeId));
                for (int x = 0; x < width && !waterDetail; x++)
                    for (int y = 0; y < height && !waterDetail; y++)
                        waterDetail = hydrology.DetailBlocks[x, y];
            }, 3);

            plan.Add("Mountain ranges and deserts", () =>
            {
                // 7. Geographic features, found in the land's shape and climate
                MountainRangeDetector.Detect(engine, world, rules);
                DesertDetector.Detect(engine, world, rules);
            }, 1);

            plan.Add("Terrain", () =>
            {
                // 8. Terrain for every land block, from its height, climate, water, features and plate boundary
                terrain = new TerrainModel(engine, rules);
                hydration = terrain.CoarseHydration(land, climate.Moisture, hydrology);
                terrain.ApplyCoarse(world, land, hydration);
            }, 3);

            // 9. Regions: each continent is divided up along its landscapes and features, and each region gets a biome
            // fitting its terrain and climate, and a language
            plan.Add("Regions", () => CreateRegions(engine, world, rules), 9);

            plan.Add("Fine coastlines and borders", () =>
            {
                // 10. Fine-grained coastlines, region borders and feature edges, ridged relief on high ground, and lakes
                if (rules.CoastDetail != 0 || rules.RegionBorderDetail != 0 || rules.FeatureDetail != 0 || waterDetail)
                    CoastalDetail.Apply(engine, world, rules, land, elevation, hydrology);
            }, 35);

            plan.Add("Peaks, passes, ergs and oases", () =>
            {
                // 11. Features found in the fine detail: the crests, peaks and passes of each mountain range, and the ergs,
                // salt flats and oases of each desert
                SubRangeDetector.Detect(engine, world, rules);
                PeakPassDetector.Detect(engine, world, rules);
                DesertPartDetector.Detect(engine, world, rules);
            }, 17);

            plan.Add("Lakes, rivers, bays and straits", () =>
            {
                // 12. Water features: lakes, large rivers, and the oceans and seas with their bays and straits
                WaterFeatureDetector.DetectLakes(engine, world, hydrology);
                WaterFeatureDetector.DetectRivers(engine, world, rules, hydrology);
                WaterFeatureDetector.DetectBodies(engine, world, rules);
            }, 31);

            // 13. Terrain for the fine detail, now that every feature (peaks, ergs, oases, rivers, lakes) is known
            // in parts, as it is the longest step
            for (int part = 0; part < FineTerrainParts; part++)
            {
                int from = part * width / FineTerrainParts, to = (part + 1) * width / FineTerrainParts;
                plan.Add("Fine terrain (" + (part + 1) + " of " + FineTerrainParts + ")",
                    () => terrain.ApplyFine(world, climate.Temperature, climate.Moisture, hydration, from, to), 92.0 / FineTerrainParts);
            }

            // 14. Region borders at fine detail, redrawn to follow the fine terrain and feature edges
            if (rules.RegionBorderDetail != 0) plan.Add("Fine region borders", () => RegionBorderRefiner.Apply(world, rules), 30);

            plan.Result = () => world;
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

        private static void CreateRegions(SimEngine engine, World world, NoiseGeneratorRules rules)
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

                // Work out the regions' shapes from the landscapes and features first...
                Dictionary<WorldBlock, int> landscapes = blocks.ToDictionary(b => b, b => RegionPartitioner.Landscape(b.Status.Terrain));
                List<List<WorldBlock>> shapes = RegionPartitioner.Partition(blocks, count, rules, rnd, FeatureSignatures(world, blocks), landscapes);

                // ...then, knowing each region's terrain and climate, choose its biome and create it. Creating a region deals
                // its blocks terrain from the biome's usual mix, so the terrain already chosen is put back.
                foreach (List<WorldBlock> shape in shapes)
                {
                    Biome biome = ChooseBiome(engine, rules, shape, rnd);
                    Dictionary<WorldBlock, Terrain> chosen = shape.ToDictionary(b => b, b => b.Status.Terrain);
                    Region.GenerateRegion(continent, new WorldBlockArea(shape, null), biome);
                    foreach (WorldBlock block in shape) block.Status.Terrain = chosen[block];
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

            // How much of the region's terrain each biome's usual mix includes
            Func<Biome, double> terrainFit = biome =>
            {
                HashSet<Terrain> usual = new HashSet<Terrain>(biome.TerrainPattern.GetAllElements());
                return blocks.Count(b => b.Status.Terrain != null && usual.Contains(b.Status.Terrain)) / (double)blocks.Count;
            };

            return BiomeSelector.Choose(engine.SimData.Biomes, ClimateScale.Temperature(temperature), ClimateScale.Precipitation(moisture),
                BiomeSelector.Contour(meanElevation + spread, rules), rules, rnd, terrainFit);
        }
    }
}
