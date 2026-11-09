using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using LoreGen.Drawing;
using LoreGen.Randomizer;
using LoreGen.Simulation;
using LoreGen.WorldDisplay;

namespace LoreGen.WorldGen.Generators
{
    /// <summary>
    /// Builds the land from a simple model of plate tectonics (see <see cref="PlateModel"/>): plates of different sizes
    /// drift and meet, continents ride on some of them, and mountain belts, trenches, island arcs and rifts form along
    /// the boundaries. Mountain ranges therefore run along plate boundaries and the edges of continents, as on Earth,
    /// rather than sitting in the middle of round landmasses. Noise adds coastline detail and lesser hills.
    ///
    /// Everything after the elevation (sea level, continents and oceans, climate, features, regions, fine detail) is
    /// shared with <see cref="NoiseWorldGenerator"/>: see <see cref="ElevationWorldBuilder"/>. The plates are kept on the
    /// world (World.Plates and each top-level block's Status.Plate).
    ///
    /// Plate settings are in TectonicGeneratorRules.csv; the rest are the shared ones in NoiseGeneratorRules.csv, except
    /// the noise generator's own land-shaping settings, which this generator does not use.
    /// </summary>
    public class TectonicWorldGenerator : IWorldGenerator
    {
        /// <summary>
        /// Id of this generator.
        /// </summary>
        public const string GeneratorId = "tectonic";

        public string Id { get { return GeneratorId; } }

        public string DisplayName { get { return "Tectonic"; } }

        public string Description
        {
            get { return "Land is shaped by drifting plates: continents ride on them, and mountain belts, island arcs and rift seas form where plates meet."; }
        }

        public string Version { get { return "1"; } }

        /// <summary>
        /// Settings in NoiseGeneratorRules.csv that only shape the noise generator's land, so this generator rejects them.
        /// </summary>
        private static readonly HashSet<string> NoiseLandSettings = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ContinentCentres", "MaskStrength", "MaskRadiusMin", "MaskRadiusMax", "Octaves", "Frequency", "Lacunarity", "Persistence", "WarpStrength"
        };

        public IReadOnlyList<GeneratorOption> GetOptions(SimData data)
        {
            RulesFile noise = data.LoadRules(NoiseGeneratorRules.Filename);
            RulesFile plates = data.LoadRules(TectonicGeneratorRules.Filename);
            List<GeneratorOption> options = new List<GeneratorOption>
            {
                GeneratorOption.Number("LandFraction", "Land share", "How much of the map is land. Random varies it from world to world.", "Land", 0, 0.7, 0.01, noise.D("LandFraction"), "random"),
                GeneratorOption.Integer("Plates", "Plates", "How many plates the crust is broken into. More plates give more, smaller continents and more mountain belts.", "Land", 0, 20, plates.I("Plates"), "random"),
                GeneratorOption.Number("ContinentalShare", "Continental crust", "How much of the map is continental crust. Land share decides how much of it is above the sea.", "Land", 0.15, 0.7, 0.01, plates.D("ContinentalShare")),
                GeneratorOption.Number("PlateBoundaryRoughness", "Boundary wandering", "How much plate boundaries, and so continents and mountain belts, wander rather than run straight.", "Land", 0, 3, 0.1, plates.D("PlateBoundaryRoughness")),
                GeneratorOption.Number("CollisionUplift", "Collision mountains", "How high mountains rise where continents collide.", "Land", 0, 1.5, 0.05, plates.D("CollisionUplift")),
                GeneratorOption.Number("SubductionUplift", "Coastal mountains", "How high mountains rise where ocean floor dives under a continent.", "Land", 0, 1.5, 0.05, plates.D("SubductionUplift")),
                GeneratorOption.Number("IslandArcUplift", "Island arcs", "How high island arcs rise where ocean floor dives under ocean floor.", "Land", 0, 1, 0.05, plates.D("IslandArcUplift")),
                GeneratorOption.Number("RiftDepth", "Rifts", "How deep continents sink where they pull apart. Deep rifts flood into long seas.", "Land", 0, 1, 0.05, plates.D("RiftDepth")),
                GeneratorOption.Number("TerrainNoise", "Coast and hill noise", "How much noise is added to the plate-built land. 0 gives smooth, plate-shaped continents.", "Land", 0, 0.4, 0.01, plates.D("TerrainNoise")),
            };
            options.AddRange(ElevationWorldBuilder.SharedOptions(data));
            return options;
        }

        public World Generate(SimEngine engine)
        {
            // Each setting goes to the rules file that has it
            RulesFile plateFile = engine.SimData.LoadRules(TectonicGeneratorRules.Filename);
            Dictionary<string, double> plateSettings = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, double> sharedSettings = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, double> setting in engine.Configuration.GeneratorSettings)
            {
                if (NoiseLandSettings.Contains(setting.Key))
                    throw new InvalidDataException("The tectonic generator does not use the setting '" + setting.Key + "' (it shapes the noise generator's land).");
                if (plateFile.Has(setting.Key)) plateSettings[setting.Key] = setting.Value;
                else sharedSettings[setting.Key] = setting.Value; // unknown settings are rejected by NoiseGeneratorRules.Load
            }
            NoiseGeneratorRules rules = NoiseGeneratorRules.Load(engine.SimData, sharedSettings);
            TectonicGeneratorRules plateRules = TectonicGeneratorRules.Load(engine.SimData, plateSettings);
            Rnd rnd = engine.Rnd;

            World world = new World(engine);
            WorldGridBuilder.InitializeWorldBlock(world);
            WorldBlock[,] grid = world.WorldBlock.ChildBlocks;
            int width = world.WorldBlock.Width, height = world.WorldBlock.Height;

            double landFraction = ElevationWorldBuilder.ChooseLandFraction(rules, rnd);
            PlateModel model = PlateModel.Build(rnd, plateRules, rules.EdgeBorder, width, height);

            // The plates go on the world first, so sub-blocks made later inherit them
            for (int i = 0; i < model.Count; i++)
            {
                List<WorldBlock> blocks = new List<WorldBlock>();
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                        if (model.Owner[x, y] == i) blocks.Add(grid[x, y]);
                TectonicPlate plate = new TectonicPlate(world, i + 1, model.PlateContinental[i], model.VelocityX[i], model.VelocityY[i], blocks);
                Rnd colour = new Rnd(unchecked(rnd.Seed * 31 + i));
                plate.DisplayInfo = new WorldEntityDisplayInfo
                {
                    Entity = plate,
                    DisplayColor = Color.FromArgb(70 + colour.Unweighted(170), 70 + colour.Unweighted(170), 70 + colour.Unweighted(170))
                };
                foreach (WorldBlock block in blocks)
                {
                    block.Status.Plate = plate;
                    if (model.Boundary[block.X, block.Y] != PlateBoundaryKind.None) plate.SetBoundary(block, model.Boundary[block.X, block.Y]);
                }
                world.Plates.Add(plate);
            }

            ElevationWorldBuilder.Build(engine, world, rules, model.Field, landFraction);
            NamePlates(world);
            return world;
        }

        /// <summary>
        /// Plates are named after the continent with the most land on them ("the Valoria Plate"), or failing that the
        /// ocean with the most water on them. Where two plates would share a name, the side they lie on tells them apart.
        /// </summary>
        private static void NamePlates(World world)
        {
            foreach (TectonicPlate plate in world.Plates)
            {
                Continent continent = plate.Blocks.Where(b => b.Status.Continent != null)
                    .GroupBy(b => b.Status.Continent).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault();
                string name = continent?.Name ?? plate.Blocks.Where(b => b.Status.Ocean != null)
                    .GroupBy(b => b.Status.Ocean).OrderByDescending(g => g.Count()).Select(g => g.Key.Name).FirstOrDefault();
                if (name != null && name.StartsWith("The ")) name = name.Substring(4);
                plate.Name = name != null ? "The " + name + " Plate" : plate.Name;
            }
            foreach (IGrouping<string, TectonicPlate> same in world.Plates.GroupBy(p => p.Name).Where(g => g.Count() > 1).ToList())
            {
                List<WorldBlock> all = same.SelectMany(p => p.Blocks).ToList();
                foreach (TectonicPlate plate in same)
                {
                    Directions? side = NameHints.DirectionWithin(plate.Blocks, all);
                    string bare = plate.Name.Substring(4, plate.Name.Length - 4 - " Plate".Length);
                    plate.Name = "The " + (side.HasValue ? side.Value + " " : "") + bare + " Plate";
                }
                // Anything still shared gets its number
                foreach (IGrouping<string, TectonicPlate> still in same.GroupBy(p => p.Name).Where(g => g.Count() > 1))
                    foreach (TectonicPlate plate in still) plate.Name += " " + plate.Number;
            }
        }
    }
}
