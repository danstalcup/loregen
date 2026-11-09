using System.Collections.Generic;

using LoreGen.Features;
using LoreGen.Lang;
using LoreGen.Simulation;

namespace LoreGen.WorldGen.Generators
{
    /// <summary>
    /// Finds continental deserts in the climate of a world's top-level blocks.
    ///
    /// Desert is land at or below <see cref="NoiseGeneratorRules.DesertMoistureTo"/> moisture and at or above
    /// <see cref="NoiseGeneratorRules.DesertTemperatureFrom"/> temperature (cold dry land is tundra and ice, not desert).
    /// Desert blocks that share a side form one desert; deserts smaller than
    /// <see cref="NoiseGeneratorRules.DesertMinBlocks"/> are left out. Deserts may overlap mountain ranges. Below the top
    /// level their edges are given fractal detail (NoiseEdgeRefiner).
    ///
    /// Uses no random numbers except for naming, so it does not change anything else about the world.
    /// </summary>
    internal static class DesertDetector
    {
        public const string TypeId = "Desert";

        public static List<GeographicFeature> Detect(SimEngine engine, World world, NoiseGeneratorRules rules)
        {
            List<GeographicFeature> found = new List<GeographicFeature>();
            if (rules.Deserts == 0) return found;

            FeatureType type = engine.SimData.FeatureType(TypeId);
            WorldBlock[,] grid = world.WorldBlock.ChildBlocks;
            int width = grid.GetLength(0), height = grid.GetLength(1);

            bool[,] dry = new bool[width, height];
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    WorldBlockStatus s = grid[x, y].Status;
                    dry[x, y] = s.WaterStatus != WorldBlockWaterStatus.Water && s.Continent != null
                        && s.Moisture.HasValue && s.Temperature.HasValue
                        && s.Moisture.Value <= rules.DesertMoistureTo && s.Temperature.Value >= rules.DesertTemperatureFrom;
                }
            }

            // Land of different continents never shares a side, so each group lies on one continent
            GridLabeling labels = GridLabeling.Label(dry);
            List<List<WorldBlock>> groups = new List<List<WorldBlock>>();
            for (int i = 0; i < labels.Count; i++) groups.Add(new List<WorldBlock>());
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    if (labels.Labels[x, y] >= 0) groups[labels.Labels[x, y]].Add(grid[x, y]);

            foreach (List<WorldBlock> group in groups)
            {
                if (group.Count < rules.DesertMinBlocks) continue;

                Continent continent = group[0].Status.Continent;
                int seed = GeographicFeature.SeedFor(engine.Rnd.Seed, type, group[0]);
                GeographicFeature desert = new GeographicFeature(world, type, FeatureScope.Continental, group, seed)
                {
                    Refiner = new NoiseEdgeRefiner(seed, rules.FeatureEdgeStrength, rules.FeatureEdgeFrequency)
                };
                desert.Name = continent.RootLanguage.GetName(new NameContext(type.NameKind)
                {
                    ParentName = continent.Name,
                    Direction = NameHints.DirectionWithin(group, continent.ContinentArea.Blocks())
                });
                world.AddFeature(desert);
                found.Add(desert);
            }
            return found;
        }
    }
}
