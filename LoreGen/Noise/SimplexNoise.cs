using System;

namespace LoreGen.Noise
{
    /// <summary>
    /// Seeded 2D simplex noise (after Stefan Gustavson's reference implementation).
    ///
    /// Every instance owns its permutation table and uses no static or shared random state, so a given seed always
    /// produces the same field regardless of what else is running. Values are a pure function of (seed, x, y).
    /// </summary>
    public class SimplexNoise
    {
        private static readonly double F2 = 0.5 * (Math.Sqrt(3.0) - 1.0);
        private static readonly double G2 = (3.0 - Math.Sqrt(3.0)) / 6.0;

        // Gradient directions (the 12 edge midpoints of a cube, projected to 2D).
        private static readonly int[,] Gradients =
        {
            { 1, 1 }, { -1, 1 }, { 1, -1 }, { -1, -1 },
            { 1, 0 }, { -1, 0 }, { 1, 0 }, { -1, 0 },
            { 0, 1 }, { 0, -1 }, { 0, 1 }, { 0, -1 }
        };

        private readonly int[] perm = new int[512];

        /// <summary>
        /// Seed this noise field was built from.
        /// </summary>
        public int Seed { get; private set; }

        /// <summary>
        /// Creates a noise field from the given seed.
        /// </summary>
        /// <param name="seed">Any int. The same seed always gives the same field.</param>
        public SimplexNoise(int seed)
        {
            Seed = seed;

            // Fisher-Yates shuffle of 0..255 using a private generator
            int[] p = new int[256];
            for (int i = 0; i < 256; i++) p[i] = i;
            Random random = new Random(seed);
            for (int i = 255; i > 0; i--)
            {
                int j = random.Next(i + 1);
                int tmp = p[i];
                p[i] = p[j];
                p[j] = tmp;
            }
            for (int i = 0; i < 512; i++) perm[i] = p[i & 255];
        }

        /// <summary>
        /// Noise value at a point. The field is smooth and its features are roughly one unit across.
        /// </summary>
        /// <param name="x">X coordinate</param>
        /// <param name="y">Y coordinate</param>
        /// <returns>A value in [-1, 1]</returns>
        public double Noise2D(double x, double y)
        {
            // Skew the input space to find which simplex cell we're in
            double s = (x + y) * F2;
            int i = FastFloor(x + s);
            int j = FastFloor(y + s);
            double t = (i + j) * G2;
            double x0 = x - (i - t);
            double y0 = y - (j - t);

            // Which of the two triangles in the cell are we in?
            int i1, j1;
            if (x0 > y0) { i1 = 1; j1 = 0; }
            else { i1 = 0; j1 = 1; }

            double x1 = x0 - i1 + G2;
            double y1 = y0 - j1 + G2;
            double x2 = x0 - 1.0 + 2.0 * G2;
            double y2 = y0 - 1.0 + 2.0 * G2;

            int ii = i & 255;
            int jj = j & 255;

            double n = Corner(x0, y0, perm[ii + perm[jj]])
                     + Corner(x1, y1, perm[ii + i1 + perm[jj + j1]])
                     + Corner(x2, y2, perm[ii + 1 + perm[jj + 1]]);

            // Scale to roughly [-1, 1]; the clamp guards against tiny overshoot
            double result = 70.0 * n;
            if (result > 1.0) return 1.0;
            if (result < -1.0) return -1.0;
            return result;
        }

        private static double Corner(double x, double y, int hash)
        {
            double t = 0.5 - x * x - y * y;
            if (t < 0.0) return 0.0;
            int g = hash % 12;
            t *= t;
            return t * t * (Gradients[g, 0] * x + Gradients[g, 1] * y);
        }

        private static int FastFloor(double x)
        {
            int xi = (int)x;
            return x < xi ? xi - 1 : xi;
        }
    }
}
