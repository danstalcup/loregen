using System;
using System.Collections.Generic;
using System.Linq;

using LoreGen.Noise;
using LoreGen.Randomizer;

namespace LoreGen.WorldGen.Generators
{
    /// <summary>
    /// A simple model of plate tectonics on the grid of top-level blocks, giving an elevation field shaped by plates
    /// rather than by noise alone.
    ///
    ///  1. Plates: a handful of well-spread seed blocks grow outward at once, each at its own rate, over ground made
    ///     uneven by noise, so plates come in different sizes and their boundaries wander. Every plate is in one piece.
    ///  2. Crust: some plates carry a continent. A continental plate's continent covers the part of the plate nearest
    ///     its seed (from about half the plate to all of it); the rest of it, and every other plate, is ocean floor.
    ///  3. Motion: every plate drifts in a random direction at a random speed. Across each boundary, the plates either
    ///     close (convergent), open (divergent) or slide past each other (transform).
    ///  4. Uplift: what a boundary does depends on its motion and the crust on either side:
    ///      - two continents colliding raise a broad mountain belt on both sides (like the Himalaya);
    ///      - ocean floor diving under a continent leaves a trench offshore and raises mountains a little inland (like the Andes);
    ///      - ocean floor diving under ocean floor leaves a trench and an arc of islands (like Japan);
    ///      - a continent pulling apart sinks into a rift valley, which can flood into a long sea (like the Red Sea);
    ///      - ocean floor pulling apart rises into a mid-ocean ridge.
    ///  5. Elevation: continental crust (with its edges smoothed into a slope) plus the uplift, plus noise for coastlines
    ///     and lesser hills, sinking to ocean at the map edges.
    ///
    /// All randomness comes from the given generator, in a fixed order.
    /// </summary>
    internal class PlateModel
    {
        public int Width, Height, Count;

        /// <summary>The plate each block is on (0 to Count - 1).</summary>
        public int[,] Owner;

        /// <summary>Whether each block has continental crust.</summary>
        public bool[,] ContinentalCrust;

        /// <summary>Whether each plate carries a continent.</summary>
        public bool[] PlateContinental;

        public double[] VelocityX, VelocityY;

        /// <summary>The most active kind of boundary each block lies on (None inside a plate).</summary>
        public PlateBoundaryKind[,] Boundary;

        /// <summary>The resulting elevation, in the units of TectonicGeneratorRules (only the order of values matters).</summary>
        public Heightmap Field;

        private const double DeepOcean = -0.3;
        private const double IslandArcInland = 1.0;
        private const int SeedCandidates = 6;

        // Closing (or opening) speed at which a boundary has its full effect; slower boundaries have proportionally less
        private const double FullStrengthClosing = 0.6;

        public static PlateModel Build(Rnd rnd, TectonicGeneratorRules rules, double edgeBorder, int width, int height)
        {
            PlateModel m = new PlateModel { Width = width, Height = height };

            // Random choices, always made in this order (and always made, even when a setting then overrides them, so that
            // giving a setting its usual value gives the same world as leaving it out)
            int count = rules.PlatesMax > rules.PlatesMin ? rnd.Unweighted(rules.PlatesMin, rules.PlatesMax + 1) : rules.PlatesMin;
            if (rules.Plates > 0) count = rules.Plates;
            count = Math.Max(1, Math.Min(count, width * height));
            m.Count = count;

            FractalNoise rough = new FractalNoise(new SimplexNoise(rnd.Unweighted(int.MaxValue)), 4, 5.0, 2.0, 0.6);
            FractalNoise terrain = new FractalNoise(new SimplexNoise(rnd.Unweighted(int.MaxValue)), rules.TerrainNoiseOctaves, rules.TerrainNoiseFrequency, 2.0, 0.55);

            List<(int X, int Y)> seeds = ChooseSeeds(rnd, count, width, height);
            double[] growth = new double[count], cover = new double[count], age = new double[count];
            m.VelocityX = new double[count];
            m.VelocityY = new double[count];
            for (int i = 0; i < count; i++)
            {
                growth[i] = 1.0 + rules.PlateSizeVariety * 0.6 * (2.0 * rnd.R01() - 1.0);
                cover[i] = rnd.Unweighted(rules.ContinentalCoverMin, rules.ContinentalCoverMax);
                age[i] = rnd.R01();
                double angle = rnd.Unweighted(2.0 * Math.PI);
                double speed = rnd.Unweighted(rules.DriftSpeedMin, 1.0);
                m.VelocityX[i] = speed * Math.Cos(angle);
                m.VelocityY[i] = speed * Math.Sin(angle);
            }
            int[] order = Enumerable.Range(0, count).ToArray();
            for (int i = count - 1; i > 0; i--)
            {
                int j = rnd.Unweighted(i + 1);
                int t = order[i]; order[i] = order[j]; order[j] = t;
            }

            double aspect = (double)width / height;
            double[,] distance = m.GrowPlates(seeds, growth, rules.PlateBoundaryRoughness,
                (x, y) => rough.Fbm((x + 0.5) / width * aspect, (y + 0.5) / height));
            m.AssignCrust(order, cover, distance, rules.ContinentalShare);
            m.FindBoundaries(rules.BoundaryThreshold);

            double[,] uplift = new double[width, height], sink = new double[width, height];
            m.ApplyBoundaryEffects(rules, age, uplift, sink);

            Heightmap crust = new Heightmap(width, height);
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    crust[x, y] = m.ContinentalCrust[x, y] ? 1.0 : 0.0;
            crust = crust.Blur(rules.CrustBlur);

            m.Field = new Heightmap(width, height);
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    double u = (x + 0.5) / width, v = (y + 0.5) / height;
                    double value = rules.ContinentalBase * crust[x, y] + uplift[x, y] + sink[x, y]
                                 + rules.TerrainNoise * terrain.Warped(u * aspect, v, rules.TerrainNoiseWarp);
                    double edge = Math.Min(Math.Min(u, 1.0 - u), Math.Min(v, 1.0 - v));
                    double fade = Smoothstep(0.0, edgeBorder, edge);
                    m.Field[x, y] = fade * value + (1.0 - fade) * DeepOcean;
                }
            }
            return m;
        }

        // Well-spread seed blocks: each new seed is the farthest from the others of a few random candidates
        private static List<(int X, int Y)> ChooseSeeds(Rnd rnd, int count, int width, int height)
        {
            List<(int X, int Y)> seeds = new List<(int X, int Y)>();
            HashSet<(int, int)> taken = new HashSet<(int, int)>();
            while (seeds.Count < count)
            {
                (int X, int Y) best = (-1, -1);
                double bestDistance = -1;
                int candidates = seeds.Count == 0 ? 1 : SeedCandidates;
                for (int k = 0; k < candidates; k++)
                {
                    var c = (X: rnd.Unweighted(width), Y: rnd.Unweighted(height));
                    if (taken.Contains(c)) continue;
                    double nearest = seeds.Count == 0 ? 0 : seeds.Min(s => (double)(s.X - c.X) * (s.X - c.X) + (double)(s.Y - c.Y) * (s.Y - c.Y));
                    if (nearest > bestDistance) { bestDistance = nearest; best = c; }
                }
                if (best.X < 0) continue; // every candidate was taken; try again
                taken.Add(best);
                seeds.Add(best);
            }
            return seeds;
        }

        // All plates spread from their seeds at once; a block goes to whichever plate reaches it most cheaply. A step
        // costs its length, scaled up or down by the ground noise (-1 to 1) so boundaries wander, and less for
        // fast-growing plates. Returns each block's cost from its seed.
        private double[,] GrowPlates(List<(int X, int Y)> seeds, double[] growth, double roughness, Func<int, int, double> ground)
        {
            Owner = new int[Width, Height];
            double[,] distance = new double[Width, Height];
            bool[,] done = new bool[Width, Height];
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                {
                    Owner[x, y] = -1;
                    distance[x, y] = double.PositiveInfinity;
                }

            PriorityQueue<(int X, int Y, int Plate), (double, long)> queue = new PriorityQueue<(int X, int Y, int Plate), (double, long)>();
            long order = 0;
            for (int i = 0; i < seeds.Count; i++)
            {
                distance[seeds[i].X, seeds[i].Y] = 0;
                queue.Enqueue((seeds[i].X, seeds[i].Y, i), (0.0, order++));
            }
            while (queue.TryDequeue(out var item, out var priority))
            {
                if (done[item.X, item.Y]) continue;
                done[item.X, item.Y] = true;
                Owner[item.X, item.Y] = item.Plate;
                distance[item.X, item.Y] = priority.Item1;
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int nx = item.X + dx, ny = item.Y + dy;
                        if ((dx == 0 && dy == 0) || nx < 0 || ny < 0 || nx >= Width || ny >= Height || done[nx, ny]) continue;
                        double step = (dx != 0 && dy != 0 ? Math.Sqrt(2.0) : 1.0) * Math.Exp(roughness * ground(nx, ny)) / growth[item.Plate];
                        queue.Enqueue((nx, ny, item.Plate), (priority.Item1 + step, order++));
                    }
                }
            }
            return distance;
        }

        // Plates in the given order carry continents until the target share of the map is continental crust. Each
        // continent covers its plate's blocks nearest the seed.
        private void AssignCrust(int[] order, double[] cover, double[,] distance, double share)
        {
            ContinentalCrust = new bool[Width, Height];
            PlateContinental = new bool[Count];
            List<(int X, int Y)>[] blocks = new List<(int X, int Y)>[Count];
            for (int i = 0; i < Count; i++) blocks[i] = new List<(int X, int Y)>();
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    blocks[Owner[x, y]].Add((x, y));

            double target = share * Width * Height;
            int continental = 0;
            foreach (int plate in order)
            {
                if (continental >= target && PlateContinental.Any(c => c)) break;
                PlateContinental[plate] = true;
                int take = Math.Max(1, (int)Math.Round(cover[plate] * blocks[plate].Count));
                // Nearest the seed first; ties in scan order (the list is in scan order and OrderBy is stable)
                foreach (var b in blocks[plate].OrderBy(b => distance[b.X, b.Y]).Take(take))
                {
                    ContinentalCrust[b.X, b.Y] = true;
                    continental++;
                }
            }
        }

        // How fast two neighbouring blocks on different plates close (positive) or open (negative)
        private double Closing(int ax, int ay, int bx, int by)
        {
            int a = Owner[ax, ay], b = Owner[bx, by];
            return (VelocityX[a] - VelocityX[b]) * (bx - ax) + (VelocityY[a] - VelocityY[b]) * (by - ay);
        }

        private static readonly int[] SideX = { 1, -1, 0, 0 }, SideY = { 0, 0, 1, -1 };

        private void FindBoundaries(double threshold)
        {
            Boundary = new PlateBoundaryKind[Width, Height];
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    for (int s = 0; s < 4; s++)
                    {
                        int nx = x + SideX[s], ny = y + SideY[s];
                        if (nx < 0 || ny < 0 || nx >= Width || ny >= Height || Owner[nx, ny] == Owner[x, y]) continue;
                        double closing = Closing(x, y, nx, ny);
                        PlateBoundaryKind kind = closing > threshold ? PlateBoundaryKind.Convergent
                            : closing < -threshold ? PlateBoundaryKind.Divergent : PlateBoundaryKind.Transform;
                        if (kind > Boundary[x, y]) Boundary[x, y] = kind;
                    }
                }
            }
        }

        // For every block beside a boundary, works out what the boundary does to its own plate's side, and spreads that
        // over the nearby blocks of its plate. Rises keep the highest effect reaching a block, and sinks the deepest, so a
        // long boundary does not pile up.
        private void ApplyBoundaryEffects(TectonicGeneratorRules rules, double[] age, double[,] uplift, double[,] sink)
        {
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    for (int s = 0; s < 4; s++)
                    {
                        int nx = x + SideX[s], ny = y + SideY[s];
                        if (nx < 0 || ny < 0 || nx >= Width || ny >= Height || Owner[nx, ny] == Owner[x, y]) continue;
                        double closing = Closing(x, y, nx, ny);
                        double strength = Math.Min(1.0, Math.Abs(closing) / FullStrengthClosing);
                        bool here = ContinentalCrust[x, y], there = ContinentalCrust[nx, ny];

                        if (closing > rules.BoundaryThreshold)
                        {
                            if (here && there)
                                Spread(x, y, rules.CollisionUplift * strength, 0.0, rules.CollisionWidth, rules.UpliftReach, uplift, sink);
                            else if (here)
                                Spread(x, y, rules.SubductionUplift * strength, rules.SubductionInland, rules.SubductionWidth, rules.UpliftReach, uplift, sink);
                            else if (there || age[Owner[x, y]] > age[Owner[nx, ny]])
                                Spread(x, y, -rules.TrenchDepth * strength, 0.0, 0.8, rules.UpliftReach, uplift, sink);  // this side dives
                            else
                                Spread(x, y, rules.IslandArcUplift * strength, IslandArcInland, 0.8, rules.UpliftReach, uplift, sink);
                        }
                        else if (closing < -rules.BoundaryThreshold)
                        {
                            if (here && there)
                                Spread(x, y, -rules.RiftDepth * strength, 0.0, 1.0, rules.UpliftReach, uplift, sink);
                            else if (!here && !there)
                                Spread(x, y, rules.RidgeHeight * strength, 0.0, 1.0, rules.UpliftReach, uplift, sink);
                            // A continent pulling away from ocean floor is a quiet (passive) margin: nothing happens
                        }
                    }
                }
            }
        }

        // Adds a bump (or dip, if amount is negative) peaking `offset` blocks in from the boundary beside (x, y), falling
        // away over `width` blocks, to the blocks of (x, y)'s plate within `reach`
        private void Spread(int x, int y, double amount, double offset, double width, int reach, double[,] uplift, double[,] sink)
        {
            if (amount == 0) return;
            int plate = Owner[x, y];
            for (int qx = Math.Max(0, x - reach); qx <= Math.Min(Width - 1, x + reach); qx++)
            {
                for (int qy = Math.Max(0, y - reach); qy <= Math.Min(Height - 1, y + reach); qy++)
                {
                    if (Owner[qx, qy] != plate) continue;
                    double d = Math.Sqrt((qx - x) * (qx - x) + (qy - y) * (qy - y)) + 0.5; // from the boundary line
                    if (d > reach + 0.5) continue;
                    double t = (d - offset) / width;
                    double value = amount * Math.Exp(-t * t);
                    if (value > 0) uplift[qx, qy] = Math.Max(uplift[qx, qy], value);
                    else sink[qx, qy] = Math.Min(sink[qx, qy], value);
                }
            }
        }

        private static double Smoothstep(double from, double to, double x)
        {
            double t = Math.Min(1.0, Math.Max(0.0, (x - from) / (to - from)));
            return t * t * (3.0 - 2.0 * t);
        }
    }
}
