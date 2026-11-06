using System;

namespace LoreGen.Noise
{
    /// <summary>
    /// Layers several octaves of simplex noise into natural-looking fields: broad shapes with progressively finer
    /// detail. Every method returns a value in [-1, 1] and is a pure function of the coordinates and settings.
    /// </summary>
    public class FractalNoise
    {
        private const int MaxOctaves = 16;

        private readonly SimplexNoise noise;
        private readonly double amplitudeSum;

        /// <summary>
        /// Number of noise layers added together.
        /// </summary>
        public int Octaves { get; private set; }

        /// <summary>
        /// Frequency of the first (broadest) octave. Larger values give smaller features.
        /// </summary>
        public double Frequency { get; private set; }

        /// <summary>
        /// How much each octave's frequency grows over the previous one (typically 2).
        /// </summary>
        public double Lacunarity { get; private set; }

        /// <summary>
        /// How much each octave's amplitude shrinks relative to the previous one (typically 0.5). Lower is smoother.
        /// </summary>
        public double Persistence { get; private set; }

        /// <summary>
        /// Creates a fractal noise source.
        /// </summary>
        /// <param name="noise">Underlying noise field. It provides the seed.</param>
        /// <param name="octaves">Number of layers, 1 to 16</param>
        /// <param name="frequency">Frequency of the first octave, greater than 0</param>
        /// <param name="lacunarity">Frequency multiplier per octave, greater than 0</param>
        /// <param name="persistence">Amplitude multiplier per octave, greater than 0</param>
        public FractalNoise(SimplexNoise noise, int octaves = 5, double frequency = 1.0, double lacunarity = 2.0, double persistence = 0.5)
        {
            if (noise == null) throw new ArgumentNullException("noise");
            if (octaves < 1 || octaves > MaxOctaves) throw new ArgumentOutOfRangeException("octaves", "Octaves must be between 1 and " + MaxOctaves + ".");
            if (!(frequency > 0)) throw new ArgumentOutOfRangeException("frequency", "Frequency must be greater than 0.");
            if (!(lacunarity > 0)) throw new ArgumentOutOfRangeException("lacunarity", "Lacunarity must be greater than 0.");
            if (!(persistence > 0)) throw new ArgumentOutOfRangeException("persistence", "Persistence must be greater than 0.");

            this.noise = noise;
            Octaves = octaves;
            Frequency = frequency;
            Lacunarity = lacunarity;
            Persistence = persistence;

            double sum = 0.0, amplitude = 1.0;
            for (int o = 0; o < octaves; o++)
            {
                sum += amplitude;
                amplitude *= persistence;
            }
            amplitudeSum = sum;
        }

        /// <summary>
        /// Standard fractal Brownian motion: soft rolling shapes, good for continents and general elevation.
        /// </summary>
        /// <param name="x">X coordinate</param>
        /// <param name="y">Y coordinate</param>
        /// <returns>A value in [-1, 1]</returns>
        public double Fbm(double x, double y)
        {
            double total = 0.0, amplitude = 1.0, frequency = Frequency;
            for (int o = 0; o < Octaves; o++)
            {
                total += amplitude * noise.Noise2D(x * frequency + OctaveOffset(o), y * frequency + OctaveOffset(o) * 0.7);
                amplitude *= Persistence;
                frequency *= Lacunarity;
            }
            return Clamp(total / amplitudeSum);
        }

        /// <summary>
        /// Ridged noise: sharp crests where the underlying noise crosses zero. Good for mountain ranges.
        /// </summary>
        /// <param name="x">X coordinate</param>
        /// <param name="y">Y coordinate</param>
        /// <returns>A value in [-1, 1], highest along the ridge lines</returns>
        public double Ridged(double x, double y)
        {
            double total = 0.0, amplitude = 1.0, frequency = Frequency;
            for (int o = 0; o < Octaves; o++)
            {
                double n = noise.Noise2D(x * frequency + OctaveOffset(o), y * frequency + OctaveOffset(o) * 0.7);
                total += amplitude * (1.0 - 2.0 * Math.Abs(n));
                amplitude *= Persistence;
                frequency *= Lacunarity;
            }
            return Clamp(total / amplitudeSum);
        }

        /// <summary>
        /// Domain-warped fractal noise: the sample point is displaced by two other noise fields first, which bends
        /// straight features into winding, organic ones (coastlines, river-like valleys).
        /// </summary>
        /// <param name="x">X coordinate</param>
        /// <param name="y">Y coordinate</param>
        /// <param name="strength">How far the point can be displaced, in input coordinate units (before frequency)</param>
        /// <returns>A value in [-1, 1]</returns>
        public double Warped(double x, double y, double strength)
        {
            double qx = Fbm(x + 5.2, y + 1.3);
            double qy = Fbm(x + 8.3, y + 2.8);
            return Fbm(x + strength * qx, y + strength * qy);
        }

        // Shifts each octave to a different part of the field so the layers don't line up at the origin.
        private static double OctaveOffset(int octave)
        {
            return octave * 31.7;
        }

        private static double Clamp(double v)
        {
            if (v > 1.0) return 1.0;
            if (v < -1.0) return -1.0;
            return v;
        }
    }
}
