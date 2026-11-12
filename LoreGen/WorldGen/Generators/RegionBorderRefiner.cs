using System;
using System.Collections.Generic;
using System.Linq;

using LoreGen.Features;

namespace LoreGen.WorldGen.Generators
{
    /// <summary>
    /// Redraws region borders at fine detail so they follow the fine terrain and feature edges, once both are known.
    ///
    /// Regions are shaped on the top-level blocks (<see cref="RegionPartitioner"/>), and at first each sub-block simply goes
    /// to the region of the nearest block (<see cref="CoastalDetail"/>), so a border crosses whatever fine terrain lies on
    /// it. Here, in every top-level block that a border runs through, the sub-blocks are given out again: each region keeps
    /// the middle sub-blocks of its own blocks (and everything in blocks the border does not touch), and spreads from
    /// there across the free sub-blocks in a race where every step has a cost, as on the top level: stepping into a
    /// different landscape costs RegionTerrainWeight (times how different), across a feature's edge RegionFeatureWeight
    /// (both scaled up for the shorter steps), up or down a slope RegionSlopeWeight, plus a little random unevenness. So a border settles where the landscape changes, along a desert's winding
    /// edge, at the foot of a range or along a river, wherever one is near. A final pass keeps every region in one piece.
    /// </summary>
    internal static class RegionBorderRefiner
    {
        private const int N = 10; // sub-blocks per side

        // A step between sub-blocks is a tenth as long as one between top-level blocks, so crossing an edge costs more
        // here, relative to distance, for edges to pull borders as far
        private const double EdgeScale = N / 2.0;

        public static void Apply(World world, NoiseGeneratorRules rules)
        {
            WorldBlock[,] grid = world.WorldBlock.ChildBlocks;
            int width = grid.GetLength(0), height = grid.GetLength(1);
            int fineWidth = width * N, fineHeight = height * N;

            // The fine map of regions as it stands (blocks without sub-blocks repeat their own region)
            Region[,] region = new Region[fineWidth, fineHeight];
            for (int bx = 0; bx < width; bx++)
                for (int by = 0; by < height; by++)
                    for (int i = 0; i < N; i++)
                        for (int j = 0; j < N; j++)
                            region[bx * N + i, by * N + j] = Cell(grid, bx, by, i, j)?.Status.Region;

            // Blocks a border runs through: with sub-blocks, and more than one region in or beside them
            bool[,] band = new bool[width, height];
            for (int bx = 0; bx < width; bx++)
                for (int by = 0; by < height; by++)
                {
                    if (grid[bx, by].ChildBlocks == null) continue;
                    Region first = null;
                    for (int fx = bx * N - 1; fx <= bx * N + N && !band[bx, by]; fx++)
                        for (int fy = by * N - 1; fy <= by * N + N && !band[bx, by]; fy++)
                        {
                            if (fx < 0 || fy < 0 || fx >= fineWidth || fy >= fineHeight || region[fx, fy] == null) continue;
                            if (first == null) first = region[fx, fy];
                            else if (!ReferenceEquals(first, region[fx, fy])) band[bx, by] = true;
                        }
                }

            // Free the band's sub-blocks, except the middle ones, which keep their region so every region keeps a foothold
            bool[,] free = new bool[fineWidth, fineHeight];
            for (int bx = 0; bx < width; bx++)
                for (int by = 0; by < height; by++)
                {
                    if (!band[bx, by]) continue;
                    for (int i = 0; i < N; i++)
                        for (int j = 0; j < N; j++)
                        {
                            bool middle = i >= N / 2 - 1 && i <= N / 2 && j >= N / 2 - 1 && j <= N / 2;
                            if (!middle && region[bx * N + i, by * N + j] != null) free[bx * N + i, by * N + j] = true;
                        }
                }

            // What the cost of a step looks at: landscape, features and height of each sub-block of the band and around it
            int[,] landscape = new int[fineWidth, fineHeight];
            int[,] features = new int[fineWidth, fineHeight];
            Dictionary<GeographicFeature, int> number = new Dictionary<GeographicFeature, int>();
            for (int i = 0; i < world.Features.Count; i++) number[world.Features[i]] = i + 1;
            Func<int, int, WorldBlock> cellAt = (fx, fy) => Cell(grid, fx / N, fy / N, fx % N, fy % N);
            for (int bx = 0; bx < width; bx++)
                for (int by = 0; by < height; by++)
                {
                    bool near = false;
                    for (int dx = -1; dx <= 1 && !near; dx++)
                        for (int dy = -1; dy <= 1 && !near; dy++)
                            near = bx + dx >= 0 && by + dy >= 0 && bx + dx < width && by + dy < height && band[bx + dx, by + dy];
                    if (!near) continue;
                    for (int i = 0; i < N; i++)
                        for (int j = 0; j < N; j++)
                        {
                            int fx = bx * N + i, fy = by * N + j;
                            WorldBlock cell = cellAt(fx, fy);
                            if (cell == null || region[fx, fy] == null) continue;
                            landscape[fx, fy] = RegionPartitioner.Landscape(cell.Status.Terrain);
                            int signature = 0;
                            foreach (GeographicFeature feature in world.FeaturesAt(cell)) signature = unchecked(signature * 31 + number[feature]);
                            features[fx, fy] = signature;
                        }
                }

            // The race: from every fixed sub-block next to a free one, cheapest first
            PriorityQueue<(int X, int Y, Region Region), (double Cost, long Order)> queue = new PriorityQueue<(int, int, Region), (double, long)>();
            long order = 0;
            int[] sx = { 1, -1, 0, 0 }, sy = { 0, 0, 1, -1 };
            for (int fy = 0; fy < fineHeight; fy++)
                for (int fx = 0; fx < fineWidth; fx++)
                {
                    if (free[fx, fy] || region[fx, fy] == null) continue;
                    bool besideFree = false;
                    for (int d = 0; d < 4 && !besideFree; d++)
                    {
                        int nx = fx + sx[d], ny = fy + sy[d];
                        besideFree = nx >= 0 && ny >= 0 && nx < fineWidth && ny < fineHeight && free[nx, ny];
                    }
                    if (besideFree) queue.Enqueue((fx, fy, region[fx, fy]), (0.0, order++));
                }

            bool[,] settled = new bool[fineWidth, fineHeight];
            while (queue.TryDequeue(out var item, out var priority))
            {
                if (free[item.X, item.Y])
                {
                    if (settled[item.X, item.Y]) continue;
                    settled[item.X, item.Y] = true;
                    region[item.X, item.Y] = item.Region;
                }
                for (int d = 0; d < 4; d++)
                {
                    int nx = item.X + sx[d], ny = item.Y + sy[d];
                    if (nx < 0 || ny < 0 || nx >= fineWidth || ny >= fineHeight || !free[nx, ny] || settled[nx, ny]) continue;
                    // Only within the region's own continent
                    if (!ReferenceEquals(cellAt(nx, ny).Status.Continent, item.Region.Continent)) continue;
                    // Edges cost EdgeScale times what they do between top-level blocks, so that an edge up to about half a block
                    // from where the border would otherwise fall draws it in
                    double step = 1.0
                        + EdgeScale * rules.RegionTerrainWeight * RegionPartitioner.LandscapeDifference(landscape[item.X, item.Y], landscape[nx, ny])
                        + EdgeScale * (features[item.X, item.Y] != features[nx, ny] ? rules.RegionFeatureWeight : 0.0)
                        + rules.RegionSlopeWeight * N * Math.Abs(Elevation(cellAt(item.X, item.Y)) - Elevation(cellAt(nx, ny)))
                        + rules.RegionJitter * Jitter(nx, ny);
                    queue.Enqueue((nx, ny, item.Region), (priority.Cost + step, order++));
                }
            }

            // A region must not be cut in two around blocks it holds outright. Where the race did that, the border blocks
            // around the cut go back to how they were (which was connected), and again until no such cut is left.
            for (int round = 0; ; round++)
            {
                List<(int X, int Y)> cut = CutOffCells(region, free);
                if (cut.Count == 0) break;
                HashSet<(int, int)> undo = new HashSet<(int, int)>();
                foreach ((int X, int Y) c in cut)
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            int bx = c.X / N + dx, by = c.Y / N + dy;
                            if (round < 4 ? bx >= 0 && by >= 0 && bx < width && by < height && band[bx, by] : false) undo.Add((bx, by));
                        }
                if (round >= 4) for (int bx = 0; bx < width; bx++) for (int by = 0; by < height; by++) if (band[bx, by]) undo.Add((bx, by));
                foreach ((int bx, int by) in undo)
                    for (int i = 0; i < N; i++)
                        for (int j = 0; j < N; j++)
                            region[bx * N + i, by * N + j] = Cell(grid, bx, by, i, j).Status.Region;
                if (round >= 4) break;
            }

            // Any other stray piece (made only of sub-blocks given out here) joins the region around it
            CoastalDetail.KeepRegionsConnected(region);

            for (int bx = 0; bx < width; bx++)
                for (int by = 0; by < height; by++)
                {
                    if (grid[bx, by].ChildBlocks == null) continue;
                    for (int i = 0; i < N; i++)
                        for (int j = 0; j < N; j++)
                        {
                            WorldBlock cell = grid[bx, by].ChildBlocks[i, j];
                            if (region[bx * N + i, by * N + j] != null && cell.Status.Region != null) cell.Status.Region = region[bx * N + i, by * N + j];
                        }
                }
        }

        /// <summary>
        /// The cells of pieces of a region, other than its largest, that touch another region and include a cell that was
        /// not given out (a block the region holds outright, or the middle of one of its blocks).
        /// </summary>
        private static List<(int X, int Y)> CutOffCells(Region[,] region, bool[,] free)
        {
            int width = region.GetLength(0), height = region.GetLength(1);
            int[,] piece = new int[width, height];
            for (int x = 0; x < width; x++) for (int y = 0; y < height; y++) piece[x, y] = -1;
            List<List<(int X, int Y)>> cells = new List<List<(int, int)>>();
            List<Region> owner = new List<Region>();
            List<bool> fixedCell = new List<bool>(), touches = new List<bool>();
            int[] sx = { 1, -1, 0, 0 }, sy = { 0, 0, 1, -1 };
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    if (region[x, y] == null || piece[x, y] >= 0) continue;
                    int label = cells.Count;
                    List<(int X, int Y)> list = new List<(int, int)> { (x, y) };
                    piece[x, y] = label;
                    bool anyFixed = false, anyTouch = false;
                    for (int k = 0; k < list.Count; k++)
                    {
                        (int cx, int cy) = list[k];
                        if (!free[cx, cy]) anyFixed = true;
                        for (int d = 0; d < 4; d++)
                        {
                            int nx = cx + sx[d], ny = cy + sy[d];
                            if (nx < 0 || ny < 0 || nx >= width || ny >= height || region[nx, ny] == null) continue;
                            if (!ReferenceEquals(region[nx, ny], region[x, y])) { anyTouch = true; continue; }
                            if (piece[nx, ny] >= 0) continue;
                            piece[nx, ny] = label;
                            list.Add((nx, ny));
                        }
                    }
                    cells.Add(list); owner.Add(region[x, y]); fixedCell.Add(anyFixed); touches.Add(anyTouch);
                }

            Dictionary<Region, int> main = new Dictionary<Region, int>();
            for (int p = 0; p < cells.Count; p++)
                if (!main.ContainsKey(owner[p]) || cells[p].Count > cells[main[owner[p]]].Count) main[owner[p]] = p;
            List<(int X, int Y)> cut = new List<(int, int)>();
            for (int p = 0; p < cells.Count; p++)
                if (main[owner[p]] != p && fixedCell[p] && touches[p]) cut.AddRange(cells[p]);
            return cut;
        }

        // Random unevenness (0 to 1) fixed by position, so borders through even ground aren't ruler-straight
        private static double Jitter(int fx, int fy)
        {
            uint h = unchecked((uint)(fx * 73856093) ^ (uint)(fy * 19349663));
            h ^= h >> 16; h = unchecked(h * 0x7feb352d); h ^= h >> 15; h = unchecked(h * 0x846ca68b); h ^= h >> 16;
            return h / (double)uint.MaxValue;
        }

        private static double Elevation(WorldBlock block)
        {
            return block?.Status.Elevation ?? 0.0;
        }

        // A sub-block, or the block itself where it has no sub-blocks
        private static WorldBlock Cell(WorldBlock[,] grid, int bx, int by, int i, int j)
        {
            WorldBlock block = grid[bx, by];
            return block.ChildBlocks != null ? block.ChildBlocks[i, j] : block;
        }
    }
}
