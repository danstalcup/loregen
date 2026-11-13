using System;
using System.Collections.Generic;
using System.Linq;

using LoreGen.Features;
using LoreGen.Noise;
using LoreGen.Randomizer;
using LoreGen.Simulation;

namespace LoreGen.WorldGen.Generators
{
    /// <summary>
    /// Adds fine-grained coastlines and region borders to a world built by <see cref="NoiseWorldGenerator"/>.
    ///
    /// Only blocks that can contain a coastline, a region border or a feature's edge get a grid of sub-blocks: those with
    /// both land and water somewhere in the 3x3 blocks around them, land blocks with more than one region in the 3x3
    /// around them, and land blocks on or beside the edge of a geographic feature (and every block of a mountain range,
    /// so its crests can be found, and of a desert when its parts are to be found). Interior land and open ocean stay as single blocks.
    ///
    /// Within those blocks the fine elevation is the coarse elevation smoothly interpolated across block boundaries,
    /// plus fractal wiggle that fades out away from the coast. It is one function of position over the whole world,
    /// so a coastline carries on unbroken from one block into the next. Two clean-up rules keep the fine map faithful
    /// to the coarse one: fine land must connect to coarse land, and fine water to coarse water, so detail only reshapes
    /// existing coasts and never creates stray lakes or specks of land: fine islets smaller than a handful of
    /// sub-blocks are dropped.
    ///
    /// High ground gets ridged relief on top, fading in from half the mountain height, which gives mountain ranges their
    /// crests, and desert ground gets gentle rises and hollows (so it has basins). Neither turns land into water or water into
    /// land.
    ///
    /// Region borders are made to wind: each sub-block goes to the region of the nearest land block centre after its
    /// position has been nudged by smooth noise, and a final pass keeps every region in one piece.
    /// </summary>
    internal static class CoastalDetail
    {
        private const double Epsilon = 0.001;

        // Sub-blocks per side. WorldBlock builds 10x10 grids; this is checked when they are created.
        private const int SubBlocksPerSide = 10;

        public static void Apply(SimEngine engine, World world, NoiseGeneratorRules rules, bool[,] land, double[,] elevation, HydrologyModel hydrology = null)
        {
            WorldBlock[,] grid = world.WorldBlock.ChildBlocks;
            int width = world.WorldBlock.Width, height = world.WorldBlock.Height;
            int n = SubBlocksPerSide;

            // 1. Which blocks get sub-blocks: those that can contain a coastline (both land and water somewhere in the 3x3
            // blocks around them) and those on a region border (land of more than one region in the 3x3 around them)
            bool[,] coastal = new bool[width, height];
            bool[,] regionMixed = new bool[width, height];
            bool[,] needsDetail = new bool[width, height];
            bool[,] featureDetail = rules.FeatureDetail != 0 ? FeatureDetailBlocks(world, rules, land) : new bool[width, height];
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    bool anyLand = false, anyWater = false;
                    Region first = null;
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                            if (!land[nx, ny]) { anyWater = true; continue; }
                            anyLand = true;
                            Region region = grid[nx, ny].Status.Region;
                            if (first == null) first = region;
                            else if (!ReferenceEquals(first, region)) regionMixed[x, y] = true;
                        }
                    }
                    coastal[x, y] = rules.CoastDetail != 0 && anyLand && anyWater;
                    needsDetail[x, y] = coastal[x, y] || (rules.RegionBorderDetail != 0 && land[x, y] && regionMixed[x, y]) || featureDetail[x, y]
                        || (hydrology != null && hydrology.DetailBlocks[x, y] && land[x, y]);
                }
            }

            // 2. Fine elevation over the whole world. Blocks without detail simply repeat their coarse value.
            int fineWidth = width * n, fineHeight = height * n;
            bool[,] fineLand = new bool[fineWidth, fineHeight];
            double[,] fineElevation = new double[fineWidth, fineHeight];

            FractalNoise wiggle = new FractalNoise(new SimplexNoise(engine.Rnd.Unweighted(int.MaxValue)),
                rules.DetailOctaves, rules.DetailFrequency, 2.0, 0.5);
            FractalNoise borderWarp = new FractalNoise(new SimplexNoise(engine.Rnd.Unweighted(int.MaxValue)),
                2, rules.RegionBorderFrequency, 2.0, 0.5);
            FractalNoise ridges = new FractalNoise(new SimplexNoise(engine.Rnd.Unweighted(int.MaxValue)),
                4, rules.MountainReliefFrequency, 2.0, 0.5);
            double reliefFrom = rules.MountainElevationFrom * 0.5, reliefFull = rules.MountainElevationFrom;

            // Gentle rises and hollows on desert ground, fading out across the desert's edge. The noise seed comes from the
            // world seed rather than a draw, so worlds without deserts, or with desert detail off, are unchanged.
            double[,] desertWeight = new double[width, height];
            bool anyDesert = false;
            if (rules.FeatureDetail != 0 && rules.DesertParts != 0 && rules.DesertRelief > 0)
            {
                foreach (GeographicFeature feature in world.Features)
                {
                    if (feature.Scope != FeatureScope.Continental || feature.Type.Category != FeatureCategory.Desert) continue;
                    foreach (WorldBlock block in feature.Footprint.Blocks) { desertWeight[block.X, block.Y] = 1; anyDesert = true; }
                }
            }
            FractalNoise dunes = new FractalNoise(new SimplexNoise(unchecked(engine.Rnd.Seed * 92821 + 4519)),
                3, rules.DesertReliefFrequency, 2.0, 0.5);
            // Bends coastlines into bays and headlands at every scale (see CoastShift). Seeded from the world seed rather than
            // drawn, so the world's other random numbers are unchanged. Its spread, measured once, scales it to typical size.
            FractalNoise coastBend = new FractalNoise(new SimplexNoise(unchecked(engine.Rnd.Seed * 39916801 + 0x4c0a57)),
                6, 0.5, 2.0, 0.6);
            double bendSpread = 0;
            for (int i = 0; i < 4096; i++) { double v = coastBend.Fbm(i * 0.6180339887 * 37.0, i * 0.4142135623 * 41.0); bendSpread += v * v; }
            bendSpread = Math.Max(1e-6, Math.Sqrt(bendSpread / 4096));

            for (int fx = 0; fx < fineWidth; fx++)
            {
                for (int fy = 0; fy < fineHeight; fy++)
                {
                    int bx = fx / n, by = fy / n;
                    double e = elevation[bx, by];
                    if (needsDetail[bx, by])
                    {
                        // Position in coarse-block units, with block centres at whole numbers
                        double px = (fx + 0.5) / n - 0.5, py = (fy + 0.5) / n - 0.5;
                        double smooth = Interpolate(elevation, px, py, width, height);
                        if (coastal[bx, by] && rules.CoastWarp > 0)
                        {
                            // Height near a coast, divided by how steeply it changes, is about the distance to the shore
                            // (in blocks). Moving that distance by noise moves the shore by a set amount whatever the
                            // slope: bays and headlands up to about CoastWarp blocks deep. The change fades out two blocks
                            // from the shore, so land and sea well away from it stay as they were.
                            double gxSlope = (Interpolate(elevation, px + 0.05, py, width, height) - Interpolate(elevation, px - 0.05, py, width, height)) / 0.1;
                            double gySlope = (Interpolate(elevation, px, py + 0.05, width, height) - Interpolate(elevation, px, py - 0.05, width, height)) / 0.1;
                            double slope = Math.Max(1e-4, Math.Sqrt(gxSlope * gxSlope + gySlope * gySlope));
                            double distance = smooth / slope;
                            double fade = Math.Max(0.0, Math.Min(1.0, 2.0 - Math.Abs(distance)));
                            double bend = Math.Max(-2.5, Math.Min(2.5, coastBend.Fbm(px + 0.5, py + 0.5) / bendSpread));
                            smooth += rules.CoastWarp * 0.5 * bend * slope * fade;
                        }
                        if (coastal[bx, by])
                        {
                            double window = Math.Max(0.0, 1.0 - Math.Abs(smooth) / rules.DetailRange);
                            e = smooth + rules.DetailStrength * wiggle.Fbm(px + 0.5, py + 0.5) * window;
                        }
                        else
                        {
                            // Not near a coast: smooth the height out, but land stays land and water stays water
                            e = land[bx, by] ? Math.Max(Epsilon, smooth) : Math.Min(-Epsilon, smooth);
                        }
                        if (e > 0 && rules.MountainRelief > 0)
                        {
                            // Ridged relief on high ground; land stays land
                            double fade = Smoothstep(reliefFrom, reliefFull, smooth);
                            if (fade > 0) e = Math.Min(1.0, Math.Max(Epsilon, e + rules.MountainRelief * fade * ridges.Ridged(px + 0.5, py + 0.5)));
                        }
                        if (e > 0 && anyDesert)
                        {
                            // Desert rises and hollows; land stays land
                            double weight = Interpolate(desertWeight, px, py, width, height);
                            if (weight > 0) e = Math.Min(1.0, Math.Max(Epsilon, e + rules.DesertRelief * weight * dunes.Fbm(px + 0.5, py + 0.5)));
                        }
                    }
                    fineLand[fx, fy] = e > 0;
                    fineElevation[fx, fy] = e;
                }
            }

            // 3. Detail only reshapes coasts: drop fine land and fill fine water that has lost its connection to the coarse map
            DropUnanchored(fineLand, fineElevation, land, n, true, rules.DetailMinIsletCells);
            DropUnanchored(fineLand, fineElevation, land, n, false, 1);

            // Safety net: smooth interpolation can bridge two continents across a thin gap. Land of different continents
            // must never touch, so cut any such contact (the weaker cell becomes water) and drop whatever that strands.
            if (SeparateContinents(grid, land, fineLand, fineElevation, n))
                DropUnanchored(fineLand, fineElevation, land, n, true, rules.DetailMinIsletCells);

            // Lakes: hollows the hydrology filled become fine water up to the lake's level
            bool[,] fineLake = new bool[fineWidth, fineHeight];
            if (hydrology != null)
                foreach (LakeBasin basin in hydrology.Basins)
                    if (basin.HasLake) CarveLake(basin, needsDetail, fineLand, fineElevation, fineLake, n, rules.LakeMinBlocks, rules.DetailMinIsletCells, wiggle);

            // 4. Which region each piece of fine land belongs to. Region borders wind: a sub-block goes to the region of the
            // land block whose centre is nearest after the sub-block's position is nudged by smooth noise.
            Region[,] fineRegion = new Region[fineWidth, fineHeight];
            for (int fx = 0; fx < fineWidth; fx++)
            {
                for (int fy = 0; fy < fineHeight; fy++)
                {
                    if (!fineLand[fx, fy]) continue;
                    int bx = fx / n, by = fy / n;
                    if (!needsDetail[bx, by])
                    {
                        fineRegion[fx, fy] = grid[bx, by].Status.Region;
                        continue;
                    }
                    WorldBlock owner = Nearest(grid, land, true, bx, by, fx % n, fy % n, n);
                    fineRegion[fx, fy] = rules.RegionBorderDetail != 0 && regionMixed[bx, by]
                        ? WarpedRegion(grid, land, owner, bx, by, (fx % n + 0.5) / n, (fy % n + 0.5) / n, rules.RegionBorderWarp, borderWarp)
                        : owner.Status.Region;
                }
            }
            if (rules.RegionBorderDetail != 0) KeepRegionsConnected(fineRegion);

            // 5. Build the sub-blocks and describe them
            for (int bx = 0; bx < width; bx++)
            {
                for (int by = 0; by < height; by++)
                {
                    if (!needsDetail[bx, by]) continue;
                    WorldBlock block = grid[bx, by];
                    block.InitializeChildBlocks();
                    if (block.ChildBlocks.GetLength(0) != n || block.ChildBlocks.GetLength(1) != n)
                        throw new InvalidOperationException("WorldBlock builds " + block.ChildBlocks.GetLength(0) + "x" + block.ChildBlocks.GetLength(1) + " sub-blocks but CoastalDetail expects " + n + "x" + n + ".");

                    for (int i = 0; i < n; i++)
                    {
                        for (int j = 0; j < n; j++)
                        {
                            int fx = bx * n + i, fy = by * n + j;
                            bool isLand = fineLand[fx, fy];
                            WorldBlockStatus status = block.ChildBlocks[i, j].Status;

                            status.Elevation = isLand ? Math.Max(Epsilon, fineElevation[fx, fy]) : Math.Min(-Epsilon, fineElevation[fx, fy]);
                            status.WaterStatus = !isLand ? WorldBlockWaterStatus.Water
                                : TouchesWater(fineLand, fx, fy, fineWidth, fineHeight) ? WorldBlockWaterStatus.Coastline
                                : WorldBlockWaterStatus.Land;

                            if (isLand)
                            {
                                // Belongs to the nearest coarse land (this block itself, if it is land)
                                WorldBlockStatus owner = Nearest(grid, land, true, bx, by, i, j, n).Status;
                                status.Continent = owner.Continent;
                                status.Region = fineRegion[fx, fy];
                                // For now the terrain of the land it belongs to; TerrainModel.ApplyFine chooses its own once
                                // every feature is known
                                status.Terrain = owner.Terrain;
                                status.Ocean = null;
                                status.Sea = null;
                            }
                            else
                            {
                                // Belongs to the nearest coarse water (this block itself, if it is water)
                                // Lake water belongs to no ocean or sea
                                WorldBlockStatus owner = fineLake[fx, fy] ? null : Nearest(grid, land, false, bx, by, i, j, n).Status;
                                status.Ocean = owner?.Ocean;
                                status.Sea = owner?.Sea;
                                status.Continent = null;
                                status.Region = null;
                                status.Terrain = null;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Land blocks that need sub-blocks for the world's features: every block of a mountain range (for its crests) and of a
        /// desert (for its ergs, salt flats and oases) when desert parts are on, the
        /// edge blocks of other features, and the blocks beside any feature's edge (where its outline can spread).
        /// </summary>
        private static bool[,] FeatureDetailBlocks(World world, NoiseGeneratorRules rules, bool[,] land)
        {
            int width = land.GetLength(0), height = land.GetLength(1);
            bool[,] detail = new bool[width, height];
            Action<WorldBlock> mark = b => { if (b != null && b.Depth == 1 && land[b.X, b.Y]) detail[b.X, b.Y] = true; };
            foreach (GeographicFeature feature in world.Features)
            {
                if (feature.Scope != FeatureScope.Continental) continue;
                foreach (WorldBlock block in feature.Footprint.Blocks)
                {
                    bool edge = feature.Footprint.IsEdge(block);
                    if (edge || WholeFeatureNeedsDetail(feature, rules)) mark(block);
                    if (!edge) continue;
                    foreach (WorldBlock beside in block.SurroundingBlocksAsList())
                        if (!feature.Footprint.IsMember(beside)) mark(beside);
                }
            }
            return detail;
        }

        /// <summary>
        /// Makes a lake in the fine land of its hollow: starting from the lowest sub-block of the hollow's lowest block, it
        /// spreads (across sides) into whichever neighbouring sub-block comes next by height, nearness to the start and the
        /// coastline noise, so it fills the bottom of the hollow first in a rounded, ragged-shored shape, until it covers its
        /// target area. It stays within the hollow's blocks and those around them, and off the sea (never within two
        /// sub-blocks of other water). A lake smaller than the minimum is left dry. Specks of land it cuts off go under too.
        /// Records the cells on the basin.
        /// </summary>
        private static void CarveLake(LakeBasin basin, bool[,] needsDetail, bool[,] fineLand, double[,] fineElevation, bool[,] fineLake, int n, int minimum, int isletMinimum, FractalNoise wiggle)
        {
            int fineWidth = fineLand.GetLength(0), fineHeight = fineLand.GetLength(1);
            int width = needsDetail.GetLength(0), height = needsDetail.GetLength(1);
            if (basin.TargetCells < minimum || !needsDetail[basin.Lowest.X, basin.Lowest.Y]) return;
            HashSet<(int, int)> area = new HashSet<(int, int)>();
            foreach ((int X, int Y) b in basin.Blocks)
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int x = b.X + dx, y = b.Y + dy;
                        if (x >= 0 && y >= 0 && x < width && y < height && needsDetail[x, y]) area.Add((x, y));
                    }

            Func<int, int, bool> allowed = (fx, fy) =>
            {
                if (fx < 0 || fy < 0 || fx >= fineWidth || fy >= fineHeight || !fineLand[fx, fy] || !area.Contains((fx / n, fy / n))) return false;
                for (int dx = -2; dx <= 2; dx++)
                    for (int dy = -2; dy <= 2; dy++)
                    {
                        int x = fx + dx, y = fy + dy;
                        if (x >= 0 && y >= 0 && x < fineWidth && y < fineHeight && !fineLand[x, y] && !fineLake[x, y]) return false;
                    }
                return true;
            };

            // Start from the lowest allowed sub-block of the lowest block
            (int X, int Y) start = (-1, -1);
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    int fx = basin.Lowest.X * n + i, fy = basin.Lowest.Y * n + j;
                    if (allowed(fx, fy) && (start.X < 0 || fineElevation[fx, fy] < fineElevation[start.X, start.Y])) start = (fx, fy);
                }
            if (start.X < 0) return;

            // Spread lowest-first. Heights count as a share of the hollow's depth, so shallow and deep hollows fill alike.
            double depth = Math.Max(1e-6, basin.Rim - basin.Floor);
            Func<int, int, double> cost = (fx, fy) =>
            {
                double px = (fx + 0.5) / n, py = (fy + 0.5) / n;
                double distance = Math.Sqrt((fx - start.X) * (fx - start.X) + (fy - start.Y) * (fy - start.Y));
                return (fineElevation[fx, fy] - basin.Floor) / depth + 0.06 * distance + 0.6 * wiggle.Fbm(px + 0.5, py + 0.5);
            };
            List<(int X, int Y)> cells = new List<(int, int)>();
            HashSet<(int, int)> seen = new HashSet<(int, int)> { start };
            PriorityQueue<(int X, int Y), (double, int)> frontier = new PriorityQueue<(int, int), (double, int)>();
            frontier.Enqueue(start, (0.0, start.Y * fineWidth + start.X));
            int[] sx = { 1, -1, 0, 0 }, sy = { 0, 0, 1, -1 };
            while (frontier.Count > 0 && cells.Count < basin.TargetCells)
            {
                (int X, int Y) c = frontier.Dequeue();
                cells.Add(c);
                for (int d = 0; d < 4; d++)
                {
                    (int X, int Y) next = (c.X + sx[d], c.Y + sy[d]);
                    if (seen.Add(next) && allowed(next.X, next.Y)) frontier.Enqueue(next, (cost(next.X, next.Y), next.Y * fineWidth + next.X));
                }
            }
            if (cells.Count < minimum) return;

            // Land the lake cuts off into a speck too small to be an islet goes under the water too
            HashSet<(int, int)> lake = new HashSet<(int, int)>(cells);
            foreach ((int X, int Y) c in cells.ToList())
                for (int d = 0; d < 4; d++)
                {
                    (int X, int Y) next = (c.X + sx[d], c.Y + sy[d]);
                    if (next.X < 0 || next.Y < 0 || next.X >= fineWidth || next.Y >= fineHeight || !fineLand[next.X, next.Y] || lake.Contains(next)) continue;
                    List<(int X, int Y)> speck = SmallPiece(fineLand, lake, next, isletMinimum);
                    if (speck == null) continue;
                    foreach ((int X, int Y) s in speck) if (lake.Add(s)) cells.Add(s);
                }

            foreach ((int X, int Y) c in cells)
            {
                fineLand[c.X, c.Y] = false;
                fineLake[c.X, c.Y] = true;
                fineElevation[c.X, c.Y] = -Epsilon;
            }
            basin.Cells = cells.OrderBy(c => c.Y).ThenBy(c => c.X).ToList();
        }

        // The piece of fine land (connected at the sides, not counting the lake being made) that a cell is in, if it has fewer
        // than `limit` cells; otherwise null
        private static List<(int X, int Y)> SmallPiece(bool[,] fineLand, HashSet<(int, int)> lake, (int X, int Y) start, int limit)
        {
            int fineWidth = fineLand.GetLength(0), fineHeight = fineLand.GetLength(1);
            List<(int X, int Y)> piece = new List<(int, int)>();
            HashSet<(int, int)> seen = new HashSet<(int, int)> { start };
            Queue<(int X, int Y)> queue = new Queue<(int, int)>();
            queue.Enqueue(start);
            int[] sx = { 1, -1, 0, 0 }, sy = { 0, 0, 1, -1 };
            while (queue.Count > 0)
            {
                (int X, int Y) c = queue.Dequeue();
                piece.Add(c);
                if (piece.Count >= limit) return null;
                for (int d = 0; d < 4; d++)
                {
                    (int X, int Y) next = (c.X + sx[d], c.Y + sy[d]);
                    if (next.X < 0 || next.Y < 0 || next.X >= fineWidth || next.Y >= fineHeight || !fineLand[next.X, next.Y] || lake.Contains(next)) continue;
                    if (seen.Add(next)) queue.Enqueue(next);
                }
            }
            return piece;
        }

        // Features whose parts are found in the fine detail need sub-blocks throughout, not just at their edges
        private static bool WholeFeatureNeedsDetail(GeographicFeature feature, NoiseGeneratorRules rules)
        {
            switch (feature.Type.Category)
            {
                case FeatureCategory.Mountain: return true;
                case FeatureCategory.Desert: return rules.DesertParts != 0;
                default: return false;
            }
        }

        private static double Smoothstep(double from, double to, double x)
        {
            if (to <= from) return x >= to ? 1.0 : 0.0;
            double t = Math.Min(1.0, Math.Max(0.0, (x - from) / (to - from)));
            return t * t * (3.0 - 2.0 * t);
        }

        /// <summary>
        /// The region of the land block (of the owner's continent, among the 3x3 around the sub-block's own block) whose
        /// centre is nearest once the sub-block's position has been nudged by noise. Positions are in block units.
        /// </summary>
        private static Region WarpedRegion(WorldBlock[,] grid, bool[,] land, WorldBlock owner, int bx, int by, double localX, double localY, double warp, FractalNoise noise)
        {
            double x = bx + localX, y = by + localY;
            double wx = x + warp * noise.Fbm(x, y);
            double wy = y + warp * noise.Fbm(x + 31.7, y + 17.3);

            int width = grid.GetLength(0), height = grid.GetLength(1);
            Region best = owner.Status.Region;
            double bestDistance = double.MaxValue;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int cx = bx + dx, cy = by + dy;
                    if (cx < 0 || cy < 0 || cx >= width || cy >= height || !land[cx, cy]) continue;
                    if (!ReferenceEquals(grid[cx, cy].Status.Continent, owner.Status.Continent)) continue;
                    double distance = (wx - (cx + 0.5)) * (wx - (cx + 0.5)) + (wy - (cy + 0.5)) * (wy - (cy + 0.5));
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = grid[cx, cy].Status.Region;
                    }
                }
            }
            return best;
        }

        /// <summary>
        /// Keeps each region in one piece at fine resolution. If winding borders have cut a region into several pieces, only
        /// its largest piece stays; each other piece is given to the neighbouring region it touches most. (A piece with no
        /// neighbouring region, cut off by water, is left alone.)
        /// </summary>
        internal static void KeepRegionsConnected(Region[,] fineRegion)
        {
            int width = fineRegion.GetLength(0), height = fineRegion.GetLength(1);
            int[] dx = { 1, -1, 0, 0 }, dy = { 0, 0, 1, -1 };

            for (int pass = 0; pass < 5; pass++)
            {
                // Label connected pieces of equal region
                int[,] piece = new int[width, height];
                List<Region> pieceRegion = new List<Region>();
                List<List<int>> pieceCells = new List<List<int>>();
                for (int x = 0; x < width; x++)
                    for (int y = 0; y < height; y++)
                        piece[x, y] = -1;

                Stack<int> pending = new Stack<int>();
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (fineRegion[x, y] == null || piece[x, y] != -1) continue;
                        int label = pieceRegion.Count;
                        pieceRegion.Add(fineRegion[x, y]);
                        List<int> cells = new List<int>();
                        pieceCells.Add(cells);
                        piece[x, y] = label;
                        pending.Push(y * width + x);
                        while (pending.Count > 0)
                        {
                            int index = pending.Pop();
                            cells.Add(index);
                            int cx = index % width, cy = index / width;
                            for (int d = 0; d < 4; d++)
                            {
                                int nx = cx + dx[d], ny = cy + dy[d];
                                if (nx < 0 || ny < 0 || nx >= width || ny >= height || piece[nx, ny] != -1) continue;
                                if (!ReferenceEquals(fineRegion[nx, ny], fineRegion[x, y])) continue;
                                piece[nx, ny] = label;
                                pending.Push(ny * width + nx);
                            }
                        }
                    }
                }

                // Each region's largest piece (the first, if tied) is its main body
                Dictionary<Region, int> mainPiece = new Dictionary<Region, int>();
                for (int p = 0; p < pieceRegion.Count; p++)
                    if (!mainPiece.ContainsKey(pieceRegion[p]) || pieceCells[p].Count > pieceCells[mainPiece[pieceRegion[p]]].Count)
                        mainPiece[pieceRegion[p]] = p;

                bool changed = false;
                for (int p = 0; p < pieceRegion.Count; p++)
                {
                    if (mainPiece[pieceRegion[p]] == p) continue;

                    List<Region> touching = new List<Region>();
                    List<int> contact = new List<int>();
                    foreach (int index in pieceCells[p])
                    {
                        int cx = index % width, cy = index / width;
                        for (int d = 0; d < 4; d++)
                        {
                            int nx = cx + dx[d], ny = cy + dy[d];
                            if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                            Region other = fineRegion[nx, ny];
                            if (other == null || ReferenceEquals(other, pieceRegion[p])) continue;
                            int at = touching.IndexOf(other);
                            if (at < 0) { touching.Add(other); contact.Add(1); }
                            else contact[at]++;
                        }
                    }
                    if (touching.Count == 0) continue;

                    int best = 0;
                    for (int t = 1; t < touching.Count; t++)
                        if (contact[t] > contact[best]) best = t;
                    foreach (int index in pieceCells[p]) fineRegion[index % width, index / width] = touching[best];
                    changed = true;
                }
                if (!changed) return;
            }
        }

        /// <summary>
        /// Smooth (bilinear) value of a grid at a position given in cell units, with cell centres at whole numbers.
        /// </summary>
        private static double Interpolate(double[,] values, double x, double y, int width, int height)
        {
            int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
            double tx = x - x0, ty = y - y0;
            double a = values[Clamp(x0, width), Clamp(y0, height)];
            double b = values[Clamp(x0 + 1, width), Clamp(y0, height)];
            double c = values[Clamp(x0, width), Clamp(y0 + 1, height)];
            double d = values[Clamp(x0 + 1, width), Clamp(y0 + 1, height)];
            return (a * (1 - tx) + b * tx) * (1 - ty) + (c * (1 - tx) + d * tx) * ty;
        }

        private static int Clamp(int i, int size)
        {
            return Math.Max(0, Math.Min(size - 1, i));
        }

        /// <summary>
        /// Flips every fine land (or water) region that contains no cell inside a coarse block of the same kind, and every
        /// one smaller than the given number of cells (specks of land too small to be an islet).
        /// </summary>
        private static void DropUnanchored(bool[,] fineLand, double[,] fineElevation, bool[,] coarseLand, int n, bool land, int minimumCells)
        {
            int fineWidth = fineLand.GetLength(0), fineHeight = fineLand.GetLength(1);
            bool[,] cells = new bool[fineWidth, fineHeight];
            for (int x = 0; x < fineWidth; x++)
                for (int y = 0; y < fineHeight; y++)
                    cells[x, y] = fineLand[x, y] == land;

            GridLabeling regions = GridLabeling.Label(cells);
            bool[] anchored = new bool[regions.Count];
            for (int x = 0; x < fineWidth; x++)
            {
                for (int y = 0; y < fineHeight; y++)
                {
                    int label = regions.Labels[x, y];
                    if (label >= 0 && coarseLand[x / n, y / n] == land) anchored[label] = true;
                }
            }

            for (int x = 0; x < fineWidth; x++)
            {
                for (int y = 0; y < fineHeight; y++)
                {
                    int label = regions.Labels[x, y];
                    if (label < 0 || (anchored[label] && regions.Sizes[label] >= minimumCells)) continue;
                    fineLand[x, y] = !land;
                    fineElevation[x, y] = land ? Math.Min(fineElevation[x, y], -Epsilon) : Math.Max(fineElevation[x, y], Epsilon);
                }
            }
        }

        private static bool SeparateContinents(WorldBlock[,] grid, bool[,] land, bool[,] fineLand, double[,] fineElevation, int n)
        {
            int fineWidth = fineLand.GetLength(0), fineHeight = fineLand.GetLength(1);
            bool cut = false;
            for (int x = 0; x < fineWidth; x++)
            {
                for (int y = 0; y < fineHeight; y++)
                {
                    if (!fineLand[x, y]) continue;
                    for (int side = 0; side < 2 && fineLand[x, y]; side++)
                    {
                        int nx = x + (side == 0 ? 1 : 0), ny = y + (side == 1 ? 1 : 0);
                        if (nx >= fineWidth || ny >= fineHeight || !fineLand[nx, ny]) continue;
                        if (ReferenceEquals(Owner(grid, land, x, y, n).Continent, Owner(grid, land, nx, ny, n).Continent)) continue;

                        bool cutThis = fineElevation[x, y] <= fineElevation[nx, ny];
                        int cx = cutThis ? x : nx, cy = cutThis ? y : ny;
                        fineLand[cx, cy] = false;
                        fineElevation[cx, cy] = Math.Min(fineElevation[cx, cy], -Epsilon);
                        cut = true;
                    }
                }
            }
            return cut;
        }

        // Status of the coarse land block that owns a fine land cell
        private static WorldBlockStatus Owner(WorldBlock[,] grid, bool[,] land, int fx, int fy, int n)
        {
            return Nearest(grid, land, true, fx / n, fy / n, fx % n, fy % n, n).Status;
        }

        private static bool TouchesWater(bool[,] fineLand, int x, int y, int width, int height)
        {
            return (x > 0 && !fineLand[x - 1, y]) || (x < width - 1 && !fineLand[x + 1, y])
                || (y > 0 && !fineLand[x, y - 1]) || (y < height - 1 && !fineLand[x, y + 1]);
        }

        /// <summary>
        /// The coarse block of the wanted kind (land or water) closest to a sub-block, among the 3x3 blocks around its own.
        /// </summary>
        private static WorldBlock Nearest(WorldBlock[,] grid, bool[,] land, bool wantLand, int bx, int by, int i, int j, int n)
        {
            int width = grid.GetLength(0), height = grid.GetLength(1);
            double lx = (i + 0.5) / n, ly = (j + 0.5) / n; // position inside its own block, 0 to 1
            WorldBlock best = null;
            double bestDistance = double.MaxValue;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int x = bx + dx, y = by + dy;
                    if (x < 0 || y < 0 || x >= width || y >= height || land[x, y] != wantLand) continue;
                    double gapX = dx < 0 ? lx : dx > 0 ? 1.0 - lx : 0.0;
                    double gapY = dy < 0 ? ly : dy > 0 ? 1.0 - ly : 0.0;
                    double distance = gapX * gapX + gapY * gapY;
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = grid[x, y];
                    }
                }
            }
            return best;
        }
    }
}
