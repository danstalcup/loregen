using System;
using System.Collections.Generic;

using LoreGen.Randomizer;
using LoreGen.Terrains;

namespace LoreGen.WorldGen.Generators
{
    /// <summary>
    /// Chooses the biome that best fits a climate. The biome data covers only some combinations of temperature,
    /// precipitation and contour, so this picks the nearest one that exists.
    /// </summary>
    internal static class BiomeSelector
    {
        /// <summary>
        /// The contour class for a region from its terrain roughness (its mean elevation plus the spread of its elevations).
        /// </summary>
        public static BiomeContour Contour(double roughness, NoiseGeneratorRules rules)
        {
            if (roughness >= rules.ContourMountainousFrom) return BiomeContour.Mountainous;
            if (roughness >= rules.ContourJaggedFrom) return BiomeContour.Jagged;
            if (roughness >= rules.ContourHillyFrom) return BiomeContour.Hilly;
            if (roughness >= rules.ContourFlatFrom) return BiomeContour.Flat;
            return BiomeContour.Lowlands;
        }

        /// <summary>
        /// The closest biome by weighted distance in (temperature, precipitation, contour) steps. If several are equally
        /// close, one is picked at random.
        /// </summary>
        public static Biome Choose(IList<Biome> biomes, BiomeTemperature temperature, BiomePrecipitation precipitation, BiomeContour contour, NoiseGeneratorRules rules, Rnd rnd)
        {
            if (biomes == null || biomes.Count == 0) throw new InvalidOperationException("There are no biomes to choose from.");

            List<Biome> closest = new List<Biome>();
            double best = double.MaxValue;
            foreach (Biome biome in biomes)
            {
                double distance = rules.BiomeTemperatureWeight * Math.Abs((int)temperature - (int)biome.Temperature)
                                + rules.BiomePrecipitationWeight * Math.Abs((int)precipitation - (int)biome.Precipitation)
                                + rules.BiomeContourWeight * Math.Abs((int)contour - (int)biome.Contour);
                if (distance < best - 1e-9)
                {
                    best = distance;
                    closest.Clear();
                    closest.Add(biome);
                }
                else if (distance <= best + 1e-9)
                {
                    closest.Add(biome);
                }
            }
            return closest[rnd.Unweighted(closest.Count)];
        }
    }
}
