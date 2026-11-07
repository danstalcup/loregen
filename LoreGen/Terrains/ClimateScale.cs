using System;

namespace LoreGen.Terrains
{
    /// <summary>
    /// Turns continuous climate values (0 to 1) into the named classes biomes are described with.
    /// The five classes are equal-width bands: 0-0.2, 0.2-0.4, 0.4-0.6, 0.6-0.8 and 0.8-1.
    /// </summary>
    public static class ClimateScale
    {
        /// <summary>
        /// Temperature class for a temperature of 0 (coldest) to 1 (hottest). Values outside that range are clamped.
        /// </summary>
        public static BiomeTemperature Temperature(double temperature)
        {
            return (BiomeTemperature)Band(temperature);
        }

        /// <summary>
        /// Precipitation class for a moisture of 0 (driest) to 1 (wettest). Values outside that range are clamped.
        /// </summary>
        public static BiomePrecipitation Precipitation(double moisture)
        {
            return (BiomePrecipitation)Band(moisture);
        }

        private static int Band(double value)
        {
            if (double.IsNaN(value)) throw new ArgumentOutOfRangeException("value", "Climate value is not a number.");
            return Math.Max(0, Math.Min(4, (int)Math.Floor(value * 5.0)));
        }
    }
}
