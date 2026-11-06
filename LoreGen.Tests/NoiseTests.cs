using System;
using System.Collections.Generic;
using System.Linq;
using LoreGen.Noise;
using Xunit;

namespace LoreGen.Tests
{
    public class NoiseTests
    {
        private static IEnumerable<(double X, double Y)> Grid(int n, double step)
        {
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    yield return (i * step - 20.0, j * step - 20.0); // includes negative coordinates
        }

        // ---- SimplexNoise ---------------------------------------------------------------------

        [Fact]
        public void SameSeedGivesSameNoise()
        {
            var a = new SimplexNoise(123);
            var b = new SimplexNoise(123);
            Assert.All(Grid(40, 0.37), p => Assert.Equal(a.Noise2D(p.X, p.Y), b.Noise2D(p.X, p.Y)));
        }

        [Fact]
        public void DifferentSeedsGiveDifferentNoise()
        {
            var a = new SimplexNoise(1);
            var b = new SimplexNoise(2);
            int differing = Grid(40, 0.37).Count(p => a.Noise2D(p.X, p.Y) != b.Noise2D(p.X, p.Y));
            Assert.True(differing > 1000, "Only " + differing + " of 1600 samples differed.");
        }

        [Fact]
        public void NoiseIsIndependentOfOtherInstances()
        {
            var first = new SimplexNoise(5);
            double before = first.Noise2D(3.3, -7.1);

            var other = new SimplexNoise(99);
            other.Noise2D(1, 1);
            var again = new SimplexNoise(5);

            Assert.Equal(before, first.Noise2D(3.3, -7.1));
            Assert.Equal(before, again.Noise2D(3.3, -7.1));
        }

        [Fact]
        public void NoiseStaysInRangeAndVaries()
        {
            var noise = new SimplexNoise(42);
            var samples = Grid(200, 0.21).Select(p => noise.Noise2D(p.X, p.Y)).ToList();

            Assert.All(samples, v => Assert.InRange(v, -1.0, 1.0));
            Assert.True(samples.Max() > 0.7, "Never gets near +1: " + samples.Max());
            Assert.True(samples.Min() < -0.7, "Never gets near -1: " + samples.Min());
            double mean = samples.Average();
            Assert.InRange(mean, -0.1, 0.1);
        }

        [Fact]
        public void NoiseIsSmooth()
        {
            var noise = new SimplexNoise(7);
            const double eps = 1e-3;
            Assert.All(Grid(60, 0.31), p =>
                Assert.True(Math.Abs(noise.Noise2D(p.X + eps, p.Y) - noise.Noise2D(p.X, p.Y)) < 0.05, "Jump near " + p));
        }

        [Fact]
        public void ExtremeSeedsWork()
        {
            foreach (int seed in new[] { 0, -1, int.MinValue, int.MaxValue })
            {
                var noise = new SimplexNoise(seed);
                Assert.InRange(noise.Noise2D(0.5, 0.5), -1.0, 1.0);
            }
        }

        // ---- FractalNoise ---------------------------------------------------------------------

        [Fact]
        public void FractalMethodsStayInRange()
        {
            var fractal = new FractalNoise(new SimplexNoise(11), octaves: 6, frequency: 2.0);
            foreach (var p in Grid(80, 0.13))
            {
                Assert.InRange(fractal.Fbm(p.X, p.Y), -1.0, 1.0);
                Assert.InRange(fractal.Ridged(p.X, p.Y), -1.0, 1.0);
                Assert.InRange(fractal.Warped(p.X, p.Y, 1.5), -1.0, 1.0);
            }
        }

        [Fact]
        public void FractalNoiseIsDeterministic()
        {
            var a = new FractalNoise(new SimplexNoise(11), 5, 1.5);
            var b = new FractalNoise(new SimplexNoise(11), 5, 1.5);
            Assert.All(Grid(30, 0.4), p =>
            {
                Assert.Equal(a.Fbm(p.X, p.Y), b.Fbm(p.X, p.Y));
                Assert.Equal(a.Ridged(p.X, p.Y), b.Ridged(p.X, p.Y));
                Assert.Equal(a.Warped(p.X, p.Y, 2.0), b.Warped(p.X, p.Y, 2.0));
            });
        }

        [Fact]
        public void MoreOctavesAddFinerDetail()
        {
            // Output is normalized by total amplitude, so compare local change relative to the overall spread:
            // roughness = average change between neighbouring samples / standard deviation of the field
            Func<int, double> roughness = octaves =>
            {
                var f = new FractalNoise(new SimplexNoise(3), octaves, 1.0);
                var values = Grid(100, 0.05).Select(p => f.Fbm(p.X, p.Y)).ToList();
                double mean = values.Average();
                double spread = Math.Sqrt(values.Average(v => (v - mean) * (v - mean)));
                double change = Grid(100, 0.05).Average(p => Math.Abs(f.Fbm(p.X + 0.05, p.Y) - f.Fbm(p.X, p.Y)));
                return change / spread;
            };
            Assert.True(roughness(6) > roughness(1) * 1.3, "1 octave: " + roughness(1) + ", 6 octaves: " + roughness(6));
        }

        [Fact]
        public void RidgedNoisePeaksWhereBaseNoiseCrossesZero()
        {
            var simplex = new SimplexNoise(8);
            var ridged = new FractalNoise(simplex, 1, 1.0);
            // Single octave: ridged = 1 - 2|noise|, with the first octave's offset of 0
            Assert.All(Grid(30, 0.43), p =>
                Assert.Equal(1.0 - 2.0 * Math.Abs(simplex.Noise2D(p.X, p.Y)), ridged.Ridged(p.X, p.Y), 10));
        }

        [Fact]
        public void FractalNoiseRejectsBadSettings()
        {
            var s = new SimplexNoise(1);
            Assert.Throws<ArgumentNullException>(() => new FractalNoise(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FractalNoise(s, octaves: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FractalNoise(s, octaves: 17));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FractalNoise(s, frequency: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FractalNoise(s, lacunarity: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FractalNoise(s, persistence: double.NaN));
        }

        // ---- Heightmap ------------------------------------------------------------------------

        private static Heightmap NoiseMap(int w, int h, int seed = 21)
        {
            var f = new FractalNoise(new SimplexNoise(seed), 5, 3.0);
            return Heightmap.Sample(w, h, (u, v) => f.Fbm(u, v));
        }

        [Fact]
        public void SampleEvaluatesAtCellCentersAcrossUnitSquare()
        {
            var seen = new List<(double, double)>();
            Heightmap.Sample(4, 2, (u, v) => { seen.Add((u, v)); return 0; });
            Assert.Equal(8, seen.Count);
            Assert.Contains((0.125, 0.25), seen);
            Assert.Contains((0.875, 0.75), seen);
            Assert.All(seen, p => { Assert.InRange(p.Item1, 0.0, 1.0); Assert.InRange(p.Item2, 0.0, 1.0); });
        }

        [Fact]
        public void NormalizeMapsToZeroOne()
        {
            var map = NoiseMap(60, 40).Normalize();
            Assert.Equal(0.0, map.Min(), 12);
            Assert.Equal(1.0, map.Max(), 12);
            Assert.Equal(60, map.Width);
            Assert.Equal(40, map.Height);
        }

        [Fact]
        public void NormalizingAFlatMapDoesNotProduceNaN()
        {
            var flat = Heightmap.Sample(5, 5, (u, v) => 3.0).Normalize();
            Assert.Equal(0.0, flat.Min());
            Assert.Equal(0.0, flat.Max());
        }

        [Fact]
        public void PercentileHitsTheRequestedFraction()
        {
            var map = NoiseMap(100, 100);
            foreach (double fraction in new[] { 0.0, 0.1, 0.3, 0.5, 0.7, 0.95 })
            {
                double level = map.Percentile(fraction);
                int below = 0;
                for (int x = 0; x < map.Width; x++)
                    for (int y = 0; y < map.Height; y++)
                        if (map[x, y] < level) below++;
                Assert.Equal(fraction * 10000, below, 1.0); // within one cell, since noise values are distinct
            }
        }

        [Fact]
        public void PercentileRejectsBadFractions()
        {
            var map = NoiseMap(5, 5);
            Assert.Throws<ArgumentOutOfRangeException>(() => map.Percentile(-0.1));
            Assert.Throws<ArgumentOutOfRangeException>(() => map.Percentile(1.1));
            Assert.Throws<ArgumentOutOfRangeException>(() => map.Percentile(double.NaN));
        }

        [Fact]
        public void BlurSmoothsAndLeavesTheOriginalAlone()
        {
            var map = NoiseMap(50, 50);
            double originalMax = map.Max();
            var blurred = map.Blur(2);

            Assert.Equal(originalMax, map.Max());
            Assert.True(blurred.Max() < map.Max(), "Blur should flatten peaks.");
            Assert.True(blurred.Min() > map.Min(), "Blur should lift valleys.");
            Assert.Equal(50, blurred.Width);
        }

        [Fact]
        public void BlurRadiusZeroKeepsValuesAndBlurPreservesAFlatMap()
        {
            var map = NoiseMap(10, 10);
            var same = map.Blur(0);
            for (int x = 0; x < 10; x++)
                for (int y = 0; y < 10; y++)
                    Assert.Equal(map[x, y], same[x, y]);

            var flat = Heightmap.Sample(8, 6, (u, v) => 0.4).Blur(3);
            Assert.Equal(0.4, flat.Min(), 12);
            Assert.Equal(0.4, flat.Max(), 12);
        }

        [Fact]
        public void SeaLevelWorkflowGivesRequestedLandFraction()
        {
            // The pipeline Phase 2 will use: noise -> normalize -> sea level from a percentile -> land mask
            var map = NoiseMap(120, 90).Normalize();
            double seaLevel = map.Percentile(0.7);
            int land = 0;
            for (int x = 0; x < map.Width; x++)
                for (int y = 0; y < map.Height; y++)
                    if (map[x, y] >= seaLevel) land++;
            double fraction = land / (double)(map.Width * map.Height);
            Assert.InRange(fraction, 0.299, 0.301);
        }
    }
}
