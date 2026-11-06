using System;

namespace LoreGen.Noise
{
    /// <summary>
    /// A rectangular grid of values (typically elevation, moisture or temperature) with the helpers needed to turn a
    /// raw noise field into something usable: normalizing, finding a threshold for a target fraction (such as sea
    /// level for a given amount of land), and smoothing.
    ///
    /// Operations return a new Heightmap and leave the original unchanged.
    /// </summary>
    public class Heightmap
    {
        private readonly double[,] values;

        /// <summary>
        /// Number of columns.
        /// </summary>
        public int Width { get; private set; }

        /// <summary>
        /// Number of rows.
        /// </summary>
        public int Height { get; private set; }

        /// <summary>
        /// Creates a map filled with zeros.
        /// </summary>
        public Heightmap(int width, int height)
        {
            if (width < 1) throw new ArgumentOutOfRangeException("width");
            if (height < 1) throw new ArgumentOutOfRangeException("height");
            Width = width;
            Height = height;
            values = new double[width, height];
        }

        /// <summary>
        /// Value at a cell.
        /// </summary>
        public double this[int x, int y]
        {
            get { return values[x, y]; }
            set { values[x, y] = value; }
        }

        /// <summary>
        /// Builds a map by evaluating a function at the center of every cell.
        /// The function receives (u, v) in [0, 1) across the whole map, not cell indices, so the same function sampled
        /// at a higher resolution describes the same terrain with finer detail. For a non-square map, scale the
        /// coordinates yourself (for example multiply u by the aspect ratio) to avoid stretching the features.
        /// </summary>
        /// <param name="width">Number of columns</param>
        /// <param name="height">Number of rows</param>
        /// <param name="sampler">Function of (u, v)</param>
        public static Heightmap Sample(int width, int height, Func<double, double, double> sampler)
        {
            if (sampler == null) throw new ArgumentNullException("sampler");
            Heightmap map = new Heightmap(width, height);
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    map.values[x, y] = sampler((x + 0.5) / width, (y + 0.5) / height);
                }
            }
            return map;
        }

        /// <summary>
        /// Smallest value in the map.
        /// </summary>
        public double Min()
        {
            double min = double.PositiveInfinity;
            foreach (double v in values) if (v < min) min = v;
            return min;
        }

        /// <summary>
        /// Largest value in the map.
        /// </summary>
        public double Max()
        {
            double max = double.NegativeInfinity;
            foreach (double v in values) if (v > max) max = v;
            return max;
        }

        /// <summary>
        /// Rescales so the lowest value becomes 0 and the highest becomes 1. A perfectly flat map becomes all 0.
        /// </summary>
        public Heightmap Normalize()
        {
            double min = Min(), max = Max();
            double range = max - min;
            Heightmap result = new Heightmap(Width, Height);
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    result.values[x, y] = range > 0 ? (values[x, y] - min) / range : 0.0;
                }
            }
            return result;
        }

        /// <summary>
        /// The value that the given fraction of cells fall below. For example Percentile(0.7) is the sea level
        /// that leaves roughly 30% of the map as land.
        /// </summary>
        /// <param name="fraction">A fraction from 0 to 1</param>
        public double Percentile(double fraction)
        {
            if (double.IsNaN(fraction) || fraction < 0.0 || fraction > 1.0) throw new ArgumentOutOfRangeException("fraction", "Fraction must be between 0 and 1.");
            double[] sorted = new double[Width * Height];
            int i = 0;
            foreach (double v in values) sorted[i++] = v;
            Array.Sort(sorted);

            // Cells strictly below the returned value number floor(fraction * count); this is the lowest value that
            // leaves at least that many cells below it.
            int index = (int)Math.Floor(fraction * sorted.Length);
            if (index >= sorted.Length) index = sorted.Length - 1;
            return sorted[index];
        }

        /// <summary>
        /// Box blur: each cell becomes the average of the cells within the given radius. Edges use the cells that
        /// exist rather than wrapping.
        /// </summary>
        /// <param name="radius">Radius in cells; 0 returns an unchanged copy</param>
        public Heightmap Blur(int radius)
        {
            if (radius < 0) throw new ArgumentOutOfRangeException("radius");

            // Separable: blur rows, then columns
            Heightmap horizontal = new Heightmap(Width, Height);
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int lo = Math.Max(0, x - radius), hi = Math.Min(Width - 1, x + radius);
                    double sum = 0.0;
                    for (int k = lo; k <= hi; k++) sum += values[k, y];
                    horizontal.values[x, y] = sum / (hi - lo + 1);
                }
            }
            Heightmap result = new Heightmap(Width, Height);
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    int lo = Math.Max(0, y - radius), hi = Math.Min(Height - 1, y + radius);
                    double sum = 0.0;
                    for (int k = lo; k <= hi; k++) sum += horizontal.values[x, k];
                    result.values[x, y] = sum / (hi - lo + 1);
                }
            }
            return result;
        }
    }
}
