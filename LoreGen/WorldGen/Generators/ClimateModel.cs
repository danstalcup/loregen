using System;
using System.Collections.Generic;

using LoreGen.Noise;
using LoreGen.Randomizer;
using LoreGen.Simulation;

namespace LoreGen.WorldGen.Generators
{
    /// <summary>
    /// Temperature and moisture for every block of a world.
    /// </summary>
    internal class ClimateField
    {
        /// <summary>0 (coldest) to 1 (hottest)</summary>
        public double[,] Temperature;

        /// <summary>0 (driest land) to 1 (wettest); water is 1</summary>
        public double[,] Moisture;
    }

    /// <summary>
    /// Works out the climate from land, water and elevation.
    ///
    /// Temperature is warmest at the equator (the middle row of the map) and falls toward the poles (the top and bottom
    /// rows), falls with altitude, and varies a little at random.
    ///
    /// Moisture blends broad random patterns, nearness to water, and a latitude pattern (wet at the equator, dry in the
    /// subtropics, wet again in the mid-latitudes and dry at the poles), less of it where it is cold, plus a little extra on
    /// high ground. Land in the lee of high ground gets less (a rain shadow): winds blow from the east in the tropics and
    /// near the poles and from the west in the mid-latitudes, and air that has crossed mountains on its way from the sea
    /// has rained out on their windward side. It is then stretched so the driest land in each world is about 0 and the wettest about 1 (the 2nd and
    /// 98th percentiles map to 0 and 1), so every world has a full range of moisture to work with.
    /// </summary>
    internal static class ClimateModel
    {
        public static ClimateField Compute(SimEngine engine, NoiseGeneratorRules rules, bool[,] land, double[,] elevation)
        {
            Rnd rnd = engine.Rnd;
            int width = land.GetLength(0), height = land.GetLength(1);
            FractalNoise temperatureNoise = new FractalNoise(new SimplexNoise(rnd.Unweighted(int.MaxValue)), rules.ClimateOctaves, rules.ClimateFrequency);
            FractalNoise moistureNoise = new FractalNoise(new SimplexNoise(rnd.Unweighted(int.MaxValue)), rules.ClimateOctaves, rules.ClimateFrequency);

            int[,] distanceToWater = DistanceToWater(land);
            double[,] shadow = RainShadow(land, elevation, rules.RainShadowReach);
            double weightSum = rules.MoistureNoiseWeight + rules.MoistureCoastWeight + rules.MoistureLatitudeWeight;
            double aspect = (double)width / height;

            ClimateField field = new ClimateField { Temperature = new double[width, height], Moisture = new double[width, height] };
            double[,] rawMoisture = new double[width, height];
            List<double> landMoisture = new List<double>();

            for (int y = 0; y < height; y++)
            {
                double v = (y + 0.5) / height;
                double warmth = 1.0 - Math.Abs(2.0 * v - 1.0);              // 1 at the equator, 0 at the poles
                double degrees = (1.0 - warmth) * 90.0;                      // latitude
                double latitudeWetness = 0.5 + 0.5 * Math.Cos(2.0 * Math.PI * degrees / 60.0);

                for (int x = 0; x < width; x++)
                {
                    double u = (x + 0.5) / width;
                    double altitude = land[x, y] ? Math.Max(0.0, elevation[x, y]) : 0.0;

                    field.Temperature[x, y] = Clamp01(warmth + rules.TemperatureOffset - rules.TemperatureLapse * altitude
                        + rules.TemperatureNoise * temperatureNoise.Fbm(u * aspect, v));

                    if (!land[x, y]) continue;
                    double coastWetness = 1.0 - Math.Min(distanceToWater[x, y], rules.MoistureReach) / rules.MoistureReach;
                    double noiseWetness = (moistureNoise.Fbm(u * aspect, v) + 1.0) * 0.5;
                    double raw = (rules.MoistureNoiseWeight * noiseWetness + rules.MoistureCoastWeight * coastWetness
                        + rules.MoistureLatitudeWeight * latitudeWetness) / weightSum;
                    raw *= 1.0 - rules.MoistureColdPenalty * (1.0 - field.Temperature[x, y]); // cold air holds little moisture
                    raw += rules.MoistureElevationGain * altitude;
                    raw *= 1.0 - rules.RainShadow * shadow[x, y];
                    rawMoisture[x, y] = raw;
                    landMoisture.Add(raw);
                }
            }

            // Stretch between the 2nd and 98th percentile, so a few extreme blocks don't squash everything else
            landMoisture.Sort();
            double lowest = landMoisture.Count > 0 ? landMoisture[(int)(0.02 * (landMoisture.Count - 1))] : 0.0;
            double highest = landMoisture.Count > 0 ? landMoisture[(int)(0.98 * (landMoisture.Count - 1))] : 1.0;
            double range = highest - lowest;
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    field.Moisture[x, y] = !land[x, y] ? 1.0 : range > 0 ? Clamp01((rawMoisture[x, y] - lowest) / range) : 0.5;

            return field;
        }

        /// <summary>
        /// The prevailing wind's direction along x at a row: -1 where it blows toward the west (it comes from the east: the
        /// tropical trade winds and the polar easterlies), +1 where it blows toward the east (the mid-latitude westerlies).
        /// </summary>
        public static int WindTowardX(int y, int height)
        {
            double v = (y + 0.5) / height;
            double degrees = Math.Abs(2.0 * v - 1.0) * 90.0;
            return degrees >= 30.0 && degrees < 60.0 ? 1 : -1;
        }

        /// <summary>
        /// How deep in a rain shadow each land block lies, 0 to 1: how far the highest land the wind has crossed since it
        /// last left water, within `reach` blocks upwind, rises above the block itself.
        /// </summary>
        internal static double[,] RainShadow(bool[,] land, double[,] elevation, int reach)
        {
            int width = land.GetLength(0), height = land.GetLength(1);
            double[,] shadow = new double[width, height];
            for (int y = 0; y < height; y++)
            {
                int upwind = -WindTowardX(y, height);
                for (int x = 0; x < width; x++)
                {
                    if (!land[x, y]) continue;
                    double barrier = 0.0;
                    for (int k = 1; k <= reach; k++)
                    {
                        int ux = x + upwind * k;
                        if (ux < 0 || ux >= width || !land[ux, y]) break; // the air picked up moisture over water
                        barrier = Math.Max(barrier, elevation[ux, y]);
                    }
                    shadow[x, y] = Clamp01(barrier - Math.Max(0.0, elevation[x, y]));
                }
            }
            return shadow;
        }

        /// <summary>
        /// Number of steps (across block edges) from each block to the nearest water; 0 for water itself.
        /// </summary>
        private static int[,] DistanceToWater(bool[,] land)
        {
            int width = land.GetLength(0), height = land.GetLength(1);
            int[,] distance = new int[width, height];
            Queue<int> queue = new Queue<int>();
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    distance[x, y] = land[x, y] ? int.MaxValue : 0;
                    if (!land[x, y]) queue.Enqueue(y * width + x);
                }
            }

            int[] dx = { 1, -1, 0, 0 }, dy = { 0, 0, 1, -1 };
            while (queue.Count > 0)
            {
                int index = queue.Dequeue();
                int cx = index % width, cy = index / width;
                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + dx[d], ny = cy + dy[d];
                    if (nx < 0 || ny < 0 || nx >= width || ny >= height || distance[nx, ny] != int.MaxValue) continue;
                    distance[nx, ny] = distance[cx, cy] + 1;
                    queue.Enqueue(ny * width + nx);
                }
            }

            // A world with no water at all: treat everything as far from it
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    if (distance[x, y] == int.MaxValue) distance[x, y] = width + height;
            return distance;
        }

        private static double Clamp01(double v)
        {
            return Math.Max(0.0, Math.Min(1.0, v));
        }
    }
}
