using System;
using System.Collections.Generic;

using LoreGen.Noise;
using LoreGen.Randomizer;
using LoreGen.Simulation;

namespace LoreGen.WorldGen.Generators
{
    /// <summary>
    /// Adds fine-grained coastlines and region borders to a world built by <see cref="NoiseWorldGenerator"/>.
    ///
    /// Only blocks that can contain a coastline or a region border get a grid of sub-blocks: those with both land and
    /// water somewhere in the 3x3 blocks around them, and land blocks with more than one region in the 3x3 around them.
    /// Interior land and open ocean stay as single blocks.
    ///
    /// Within those blocks the fine elevation is the coarse elevation smoothly interpolated across block boundaries,
    /// plus fractal wiggle that fades out away from the coast. It is one function of position over the whole world,
    /// so a coastline carries on unbroken from one block into the next. Two clean-up rules keep the fine map faithful
    /// to the coarse one: fine land must connect to coarse land, and fine water to coarse water, so detail only reshapes
    /// existing coasts and never creates stray lakes or specks of land: fine islets smaller than a handful of
    /// sub-blocks are dropped.
    ///
    /// Region borders are made to wind: each sub-block goes to the region of the nearest land block centre after its
    /// position has been nudged by smooth noise, and a final pass keeps every region in one piece.
    /// </summary>
    internal static class CoastalDetail
    {
        private const double Epsilon = 0.001;

        // Sub-blocks per side. WorldBlock builds 10x10 grids; this is checked when they are created.
        private const int SubBlocksPerSide = 10;

        public static void Apply(SimEngine engine, World world, NoiseGeneratorRules rules, bool[,] land, double[,] elevation, TerrainPicker terrain)
        {
            WorldBlock[,] grid = world.WorldBlock.ChildBlocks;
            int width = world.WorldBlock.Width, height = world.WorldBlock.Height;
            int n = SubBlocksPerSide;

            // 1. Which blocks get sub-blocks: those that can contain a coastline (both land and water somewhere in the 3x3
            // blocks around them) and those on a region border (land of more than one region in the 3x3 around them)
            bool[,] coastal = new bool[width, height];
            bool[,] regionMixed = new bool[width, height];
            bool[,] needsDetail = new bool[width, height];
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
                    needsDetail[x, y] = coastal[x, y] || (rules.RegionBorderDetail != 0 && land[x, y] && regionMixed[x, y]);
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
                                status.Terrain = terrain.Pick(status.Region.Biome, status.Elevation.Value);
                                status.Ocean = null;
                                status.Sea = null;
                            }
                            else
                            {
                                // Belongs to the nearest coarse water (this block itself, if it is water)
                                WorldBlockStatus owner = Nearest(grid, land, false, bx, by, i, j, n).Status;
                                status.Ocean = owner.Ocean;
                                status.Sea = owner.Sea;
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
        private static void KeepRegionsConnected(Region[,] fineRegion)
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
