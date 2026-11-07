using System;
using System.Collections.Generic;

using LoreGen.Randomizer;
using LoreGen.Simulation;
using LoreGen.Terrains;

namespace LoreGen.WorldGen.Generators
{
    /// <summary>
    /// Picks the terrain for a block from its region's biome, leaning toward steeper terrain on higher ground.
    ///
    /// Each terrain type has a relief from 0 (flat) to 4 (peaks), and a block's height gives it a target relief. A
    /// terrain's chance in the biome's usual mix is multiplied by how close its relief is to that target, so a biome
    /// that mixes flats and peaks puts the peaks high up and the flats low down, while a biome with only one kind of
    /// terrain is unaffected.
    /// </summary>
    internal class TerrainPicker
    {
        private readonly Rnd rnd;
        private readonly double strength;
        private readonly double elevationPerRelief;
        private readonly Dictionary<long, WeightedRandomContainer<Terrain>> cache = new Dictionary<long, WeightedRandomContainer<Terrain>>();

        public TerrainPicker(SimEngine engine, NoiseGeneratorRules rules)
        {
            rnd = engine.Rnd;
            strength = rules.TerrainReliefStrength;
            elevationPerRelief = rules.ElevationPerRelief;
        }

        /// <summary>
        /// Terrain for a land block of the given elevation (above 0) in a region with the given biome.
        /// </summary>
        public Terrain Pick(Biome biome, double elevation)
        {
            if (strength <= 0) return biome.TerrainPattern.Get();

            // Targets are rounded to half steps so the weighted mixes can be built once and reused
            int bucket = (int)Math.Round(Math.Min(4.0, Math.Max(0.0, elevation / elevationPerRelief)) * 2.0);
            long key = (long)biome.ID * 100 + bucket;
            WeightedRandomContainer<Terrain> container;
            if (!cache.TryGetValue(key, out container))
            {
                double target = bucket / 2.0;
                container = new WeightedRandomContainer<Terrain>(rnd);
                List<Terrain> terrains = biome.TerrainPattern.GetAllElements();
                List<double> weights = biome.TerrainPattern.GetAllWeights();
                for (int i = 0; i < terrains.Count; i++)
                {
                    double gap = terrains[i].TerrainType.Relief - target;
                    container.Add(terrains[i], weights[i] * Math.Exp(-strength * gap * gap));
                }
                cache[key] = container;
            }
            return container.Get();
        }
    }
}
