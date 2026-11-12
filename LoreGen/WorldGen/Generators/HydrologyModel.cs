using System;
using System.Collections.Generic;
using System.Linq;

namespace LoreGen.WorldGen.Generators
{
    /// <summary>
    /// Where a river ends.
    /// </summary>
    internal enum RiverEnd
    {
        /// <summary>It reaches an ocean or sea.</summary>
        Sea,
        /// <summary>It runs into a basin with no outlet (holding a salt lake, or dry).</summary>
        Basin,
        /// <summary>It joins a larger river.</summary>
        River
    }

    /// <summary>
    /// A hollow in the land with no outlet lower than its rim, and the lake it holds, if any.
    /// </summary>
    internal sealed class LakeBasin
    {
        /// <summary>The top-level blocks of the hollow (those below its rim), in scan order.</summary>
        public List<(int X, int Y)> Blocks = new List<(int, int)>();
        /// <summary>The lowest of them.</summary>
        public (int X, int Y) Lowest;
        /// <summary>The elevation of the lowest block, and of the rim (where water would spill out).</summary>
        public double Floor, Rim;
        /// <summary>Water running in, and the share of the lake's evaporation it makes up for (1 or more fills it).</summary>
        public double Inflow, Fill;
        /// <summary>Whether the lake fills to overflowing, so its water runs on (otherwise it has no outflow).</summary>
        public bool Open;
        /// <summary>Whether the basin holds a lake at all.</summary>
        public bool HasLake;
        /// <summary>How many sub-blocks the lake should cover.</summary>
        public int TargetCells;
        /// <summary>The sub-blocks made into lake water by the fine detail, as positions in the world's grid of sub-blocks.</summary>
        public List<(int X, int Y)> Cells = new List<(int, int)>();
    }

    /// <summary>
    /// A large river: the top-level blocks it runs through and its winding course.
    /// </summary>
    internal sealed class RiverCourse
    {
        /// <summary>The land blocks it runs through, from its source down.</summary>
        public List<(int X, int Y)> Blocks = new List<(int, int)>();
        public RiverEnd End;
        /// <summary>Where it ends: the water block it runs into, the basin's lowest block, or the block where it joins the larger river.</summary>
        public (int X, int Y) EndBlock;
        /// <summary>The river it joins (End is River).</summary>
        public RiverCourse Joins;
        /// <summary>The basin it ends in (End is Basin).</summary>
        public LakeBasin Basin;
        /// <summary>Water carried at the source and at the last block.</summary>
        public double SourceFlow, MouthFlow;
        /// <summary>Fixed per river: the seed of the river feature.</summary>
        public int Seed;
        /// <summary>
        /// The river's centre line, in true units (TrueX and TrueY), from source to end: a winding line through fixed points
        /// (a point in each block, and a crossing point on each block edge it crosses), made by repeatedly displacing the
        /// middle of each piece sideways.
        /// </summary>
        public List<(double X, double Y)> Path = new List<(double, double)>();
        /// <summary>Distance along the path to each of its points (true units).</summary>
        public List<double> Along = new List<double>();
        /// <summary>The sub-blocks the path crosses, in order, as positions in the world's grid of sub-blocks (edge-connected).</summary>
        public List<(int X, int Y)> Cells = new List<(int, int)>();
    }

    /// <summary>
    /// Runs the water that falls on a world's land off to the sea, over the world's top-level blocks.
    ///
    /// 1. Drainage: every land block drains to one neighbour (side or corner), found by flooding the land up from the sea
    ///    (lowest first); so water always finds a way out, through the lowest gap in the rim of any hollow. Land on the
    ///    edge of the map drains off it.
    /// 2. Runoff: each land block yields its moisture, less what warmth evaporates (RunoffEvaporation). Flow is the
    ///    runoff of a block and of everything upstream of it.
    /// 3. Lakes: a hollow (land below the level water would spill out at, by at least LakeDepthFrom) collects the
    ///    water running into it. A lake loses LakeEvaporation for each block of its basin; fed at least that much it
    ///    fills and overflows and its water runs on (a lake); fed less, it shrinks and the water goes no further (a salt
    ///    lake); fed less than LakeMinFill of it, the basin stays dry. A full lake covers LakeAreaShare of its hollow's
    ///    area, and less in proportion when not full.
    /// 4. Rivers: blocks carrying at least RiverFlowFrom are on a large river. Where rivers meet, the one carrying the
    ///    most water keeps going and the other ends there. Rivers shorter than RiverMinBlocks are dropped, unless
    ///    another river kept joins them.
    /// 5. Courses: each river's centre line winds through fixed points (see <see cref="RiverCourse.Path"/>) which depend
    ///    only on the world seed and where the blocks are, so rivers meet exactly where they join.
    ///
    /// Uses no random numbers: everything is fixed by the world seed and the land, so it does not change anything else
    /// about the world.
    /// </summary>
    internal sealed class HydrologyModel
    {
        public int Width, Height;
        /// <summary>The level water would have to rise to before spilling out (the elevation itself for land that drains freely).</summary>
        public double[,] Level;
        /// <summary>Each land block's downstream neighbour, or (-1,-1) where it drains into water or off the map.</summary>
        public (int X, int Y)[,] Receiver;
        public double[,] Runoff;
        public double[,] Flow;
        public List<LakeBasin> Basins = new List<LakeBasin>();
        public List<RiverCourse> Rivers = new List<RiverCourse>();
        /// <summary>Land blocks that need sub-blocks: those rivers run through, and lakes and the blocks around them.</summary>
        public bool[,] DetailBlocks;

        // Sub-blocks per side of a block (WorldBlock builds 10x10 grids; CoastalDetail checks this)
        private const int SubBlocksPerSide = 10;

        public static HydrologyModel Build(NoiseGeneratorRules rules, WorldBlock[,] grid, bool[,] land, double[,] elevation,
            double[,] moisture, double[,] temperature, int worldSeed, Features.FeatureType riverType)
        {
            int width = land.GetLength(0), height = land.GetLength(1);
            HydrologyModel model = new HydrologyModel
            {
                Width = width, Height = height,
                Level = new double[width, height],
                Receiver = new (int, int)[width, height],
                Runoff = new double[width, height],
                Flow = new double[width, height],
                DetailBlocks = new bool[width, height]
            };
            if (rules.Hydrology == 0) return model;

            List<(int X, int Y)> order = model.Drain(land, elevation);
            int[,] basinOf = model.FindBasins(rules, land, elevation);

            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    if (land[x, y]) model.Runoff[x, y] = Math.Max(0.0, moisture[x, y] * (1.0 - rules.RunoffEvaporation * temperature[x, y]));

            // Flow, with closed basins keeping their water. Whether a basin is closed depends on what flows into it, which
            // depends on whether the basins upstream are closed, so settle it in a few rounds (upstream first)
            bool[] closed = new bool[model.Basins.Count];
            for (int round = 0; round < 4; round++)
            {
                model.Accumulate(order, basinOf, closed);
                bool changed = false;
                for (int i = 0; i < model.Basins.Count; i++)
                {
                    LakeBasin basin = model.Basins[i];
                    basin.Inflow = 0;
                    foreach ((int X, int Y) b in basin.Blocks)
                    {
                        (int X, int Y) r = model.Receiver[b.X, b.Y];
                        if (r.X < 0 || basinOf[r.X, r.Y] != i) basin.Inflow += model.Flow[b.X, b.Y];
                    }
                    basin.Fill = basin.Inflow / (rules.LakeEvaporation * basin.Blocks.Count);
                    bool isClosed = basin.Fill < 1.0;
                    if (isClosed != closed[i]) { closed[i] = isClosed; changed = true; }
                }
                if (!changed) break;
            }
            model.Accumulate(order, basinOf, closed);
            foreach (LakeBasin basin in model.Basins)
            {
                basin.Open = basin.Fill >= 1.0;
                basin.HasLake = basin.Fill >= rules.LakeMinFill;
                basin.TargetCells = (int)Math.Round(rules.LakeAreaShare * Math.Min(1.0, basin.Fill) * basin.Blocks.Count * SubBlocksPerSide * SubBlocksPerSide);
                if (!basin.HasLake) continue;
                // The lake and the land around it get sub-blocks, so its shore can be drawn
                foreach ((int X, int Y) b in basin.Blocks)
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            int nx = b.X + dx, ny = b.Y + dy;
                            if (nx >= 0 && ny >= 0 && nx < width && ny < height && land[nx, ny]) model.DetailBlocks[nx, ny] = true;
                        }
            }

            model.TraceRivers(rules, grid, land, basinOf, worldSeed, riverType);
            return model;
        }

        // ---- Drainage -------------------------------------------------------------------------

        // Floods the land up from the water, lowest first. Returns the land blocks in the order reached (downstream first).
        private List<(int X, int Y)> Drain(bool[,] land, double[,] elevation)
        {
            bool[,] reached = new bool[Width, Height];
            PriorityQueue<(int X, int Y), (double, int)> pending = new PriorityQueue<(int, int), (double, int)>();
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    Receiver[x, y] = (-1, -1);
                    bool edge = x == 0 || y == 0 || x == Width - 1 || y == Height - 1;
                    // Water is where everything drains to; land on the map's edge drains off it
                    if (!land[x, y] || edge)
                    {
                        reached[x, y] = true;
                        Level[x, y] = elevation[x, y];
                        pending.Enqueue((x, y), (Level[x, y], y * Width + x));
                    }
                }
            }

            List<(int X, int Y)> order = new List<(int, int)>();
            while (pending.Count > 0)
            {
                (int X, int Y) c = pending.Dequeue();
                if (land[c.X, c.Y]) order.Add(c);
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = c.X + dx, ny = c.Y + dy;
                        if ((dx == 0 && dy == 0) || nx < 0 || ny < 0 || nx >= Width || ny >= Height || reached[nx, ny]) continue;
                        reached[nx, ny] = true;
                        Level[nx, ny] = Math.Max(elevation[nx, ny], Level[c.X, c.Y]);
                        Receiver[nx, ny] = land[c.X, c.Y] ? c : (-1, -1);
                        pending.Enqueue((nx, ny), (Level[nx, ny], ny * Width + nx));
                    }
                }
            }
            return order;
        }

        // Hollows: land below its spill level, grouped where they touch (side or corner), kept if deep enough
        private int[,] FindBasins(NoiseGeneratorRules rules, bool[,] land, double[,] elevation)
        {
            int[,] basinOf = new int[Width, Height];
            bool[,] hollow = new bool[Width, Height];
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                {
                    basinOf[x, y] = -1;
                    hollow[x, y] = land[x, y] && Level[x, y] - elevation[x, y] > 1e-12;
                }

            bool[,] seen = new bool[Width, Height];
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (!hollow[x, y] || seen[x, y]) continue;
                    List<(int X, int Y)> group = new List<(int, int)>();
                    Queue<(int X, int Y)> queue = new Queue<(int, int)>();
                    queue.Enqueue((x, y));
                    seen[x, y] = true;
                    while (queue.Count > 0)
                    {
                        (int X, int Y) c = queue.Dequeue();
                        group.Add(c);
                        for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int nx = c.X + dx, ny = c.Y + dy;
                                if (nx < 0 || ny < 0 || nx >= Width || ny >= Height || seen[nx, ny] || !hollow[nx, ny]) continue;
                                seen[nx, ny] = true;
                                queue.Enqueue((nx, ny));
                            }
                    }
                    group = group.OrderBy(g => g.Y).ThenBy(g => g.X).ToList();
                    (int X, int Y) lowest = group.OrderBy(g => elevation[g.X, g.Y]).ThenBy(g => g.Y).ThenBy(g => g.X).First();
                    double rim = group.Max(g => Level[g.X, g.Y]);
                    if (rim - elevation[lowest.X, lowest.Y] < rules.LakeDepthFrom) continue;

                    LakeBasin basin = new LakeBasin { Blocks = group, Lowest = lowest, Floor = elevation[lowest.X, lowest.Y], Rim = rim };
                    foreach ((int X, int Y) g in group) basinOf[g.X, g.Y] = Basins.Count;
                    Basins.Add(basin);
                }
            }
            return basinOf;
        }

        // Flow: each block's runoff plus everything draining into it; a closed basin passes nothing on
        private void Accumulate(List<(int X, int Y)> order, int[,] basinOf, bool[] closed)
        {
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    Flow[x, y] = Runoff[x, y];
            for (int i = order.Count - 1; i >= 0; i--)
            {
                (int X, int Y) c = order[i];
                (int X, int Y) r = Receiver[c.X, c.Y];
                if (r.X < 0) continue;
                int basin = basinOf[c.X, c.Y];
                if (basin >= 0 && basinOf[r.X, r.Y] != basin && closed[basin]) continue;
                Flow[r.X, r.Y] += Flow[c.X, c.Y];
            }
        }

        // ---- Rivers ---------------------------------------------------------------------------

        private void TraceRivers(NoiseGeneratorRules rules, WorldBlock[,] grid, bool[,] land, int[,] basinOf, int worldSeed, Features.FeatureType riverType)
        {
            Func<int, int, bool> onRiver = (x, y) => land[x, y] && Flow[x, y] >= rules.RiverFlowFrom;

            // Which upstream river block each river block continues from: the one carrying the most water
            (int X, int Y)[,] mainUpstream = new (int, int)[Width, Height];
            bool[,] hasUpstream = new bool[Width, Height];
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    // Rivers end in closed basins, so nothing continues out of one
                    if (!onRiver(x, y) || (basinOf[x, y] >= 0 && !Basins[basinOf[x, y]].Open)) continue;
                    (int X, int Y) r = Receiver[x, y];
                    if (r.X < 0 || !onRiver(r.X, r.Y) || ClosedExit(basinOf, x, y, r)) continue;
                    if (!hasUpstream[r.X, r.Y] || Flow[x, y] > Flow[mainUpstream[r.X, r.Y].X, mainUpstream[r.X, r.Y].Y])
                    {
                        mainUpstream[r.X, r.Y] = (x, y);
                        hasUpstream[r.X, r.Y] = true;
                    }
                }
            }

            // Each source starts a river, which runs down until it reaches water or a closed basin, or joins a river
            // carrying more water
            Dictionary<(int, int), RiverCourse> courseAt = new Dictionary<(int, int), RiverCourse>();
            List<RiverCourse> all = new List<RiverCourse>();
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (!onRiver(x, y) || hasUpstream[x, y]) continue;
                    RiverCourse course = new RiverCourse { SourceFlow = Flow[x, y] };
                    (int X, int Y) c = (x, y);
                    while (true)
                    {
                        course.Blocks.Add(c);
                        courseAt[c] = course;
                        int basin = basinOf[c.X, c.Y];
                        if (basin >= 0 && !Basins[basin].Open)
                        {
                            course.End = RiverEnd.Basin;
                            course.Basin = Basins[basin];
                            course.EndBlock = Basins[basin].Lowest;
                            break;
                        }
                        (int X, int Y) r = Receiver[c.X, c.Y];
                        if (r.X < 0)
                        {
                            course.End = RiverEnd.Sea;
                            course.EndBlock = WaterBeside(land, c);
                            break;
                        }
                        if (!onRiver(r.X, r.Y) || ClosedExit(basinOf, c.X, c.Y, r))
                        {
                            // Only a closed basin's exit stops a river short of water: treat it as ending in the basin
                            course.End = RiverEnd.Basin;
                            course.Basin = basin >= 0 ? Basins[basin] : null;
                            course.EndBlock = basin >= 0 ? Basins[basin].Lowest : c;
                            break;
                        }
                        if (mainUpstream[r.X, r.Y] != c)
                        {
                            course.End = RiverEnd.River;
                            course.EndBlock = r;
                            break;
                        }
                        c = r;
                    }
                    course.MouthFlow = Flow[course.Blocks[course.Blocks.Count - 1].X, course.Blocks[course.Blocks.Count - 1].Y];
                    all.Add(course);
                }
            }
            foreach (RiverCourse course in all)
                if (course.End == RiverEnd.River) course.Joins = courseAt[course.EndBlock];

            // Keep long enough rivers, and every river a kept one joins
            HashSet<RiverCourse> kept = new HashSet<RiverCourse>(all.Where(c => c.Blocks.Count >= rules.RiverMinBlocks));
            foreach (RiverCourse course in all.Where(kept.Contains).ToList())
                for (RiverCourse down = course.Joins; down != null && kept.Add(down); down = down.Joins) { }

            foreach (RiverCourse course in all.Where(kept.Contains))
            {
                (int X, int Y) source = course.Blocks[0];
                course.Seed = Features.GeographicFeature.SeedFor(worldSeed, riverType, grid[source.X, source.Y]);
                Lay(course, rules, grid, land, worldSeed);
                Rivers.Add(course);
            }
        }

        private bool ClosedExit(int[,] basinOf, int x, int y, (int X, int Y) r)
        {
            int basin = basinOf[x, y];
            return basin >= 0 && basinOf[r.X, r.Y] != basin && !Basins[basin].Open;
        }

        // The water block a block drains into: a side neighbour if there is one, else a corner
        private (int X, int Y) WaterBeside(bool[,] land, (int X, int Y) c)
        {
            (int X, int Y) best = (-1, -1);
            int bestRank = int.MaxValue;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = c.X + dx, ny = c.Y + dy;
                    if ((dx == 0 && dy == 0) || nx < 0 || ny < 0 || nx >= Width || ny >= Height || land[nx, ny]) continue;
                    int rank = Math.Abs(dx) + Math.Abs(dy);
                    if (rank < bestRank) { bestRank = rank; best = (nx, ny); }
                }
            if (best.X >= 0) return best;
            // Off the map's edge: the nearest point beyond it
            int ox = c.X == 0 ? -1 : c.X == Width - 1 ? Width : c.X;
            int oy = c.Y == 0 ? -1 : c.Y == Height - 1 ? Height : c.Y;
            return (ox, oy);
        }

        // ---- Courses --------------------------------------------------------------------------

        // Lays out a river's winding centre line and the sub-blocks it crosses
        private void Lay(RiverCourse course, NoiseGeneratorRules rules, WorldBlock[,] grid, bool[,] land, int worldSeed)
        {
            double size = grid[0, 0].TrueWidth;
            double originX = grid[0, 0].TrueX, originY = grid[0, 0].TrueY;

            // The blocks the line passes through, in order, ending in the end block (and for a basin, on to its lowest block)
            List<(int X, int Y)> blocks = new List<(int, int)>(course.Blocks);
            if (course.End == RiverEnd.Basin && course.Basin != null)
                blocks.AddRange(PathWithin(course.Basin, blocks[blocks.Count - 1]).Skip(1));
            else if (course.End != RiverEnd.Basin)
                blocks.Add(course.EndBlock);

            List<(double X, double Y)> fixedPoints = new List<(double, double)> { InBlock(blocks[0], worldSeed, size, originX, originY) };
            for (int i = 1; i < blocks.Count; i++)
            {
                fixedPoints.Add(Crossing(blocks[i - 1], blocks[i], worldSeed, size, originX, originY));
                fixedPoints.Add(InBlock(blocks[i], worldSeed, size, originX, originY));
            }

            // Wind the line between the fixed points
            double finest = size / SubBlocksPerSide / 40.0; // pieces of about 2.5 km for 1000 km blocks
            course.Path.Add(fixedPoints[0]);
            for (int i = 1; i < fixedPoints.Count; i++)
                Wind(course.Path, fixedPoints[i - 1], fixedPoints[i], rules.RiverMeander, finest, course.Seed, 0);

            course.Along.Add(0);
            for (int i = 1; i < course.Path.Count; i++)
                course.Along.Add(course.Along[i - 1] + Distance(course.Path[i - 1], course.Path[i]));

            // The sub-blocks crossed, edge to edge
            double cell = size / SubBlocksPerSide;
            (int X, int Y) last = (int.MinValue, int.MinValue);
            for (int i = 0; i < course.Path.Count; i++)
            {
                (double X, double Y) p = course.Path[i];
                (int X, int Y) at = ((int)Math.Floor((p.X - originX) / cell), (int)Math.Floor((p.Y - originY) / cell));
                if (at == last) continue;
                if (last.X != int.MinValue)
                {
                    // Steps cover at most one sub-block (pieces are much shorter), but may cut a corner: add the sub-block
                    // the line passed through between them
                    if (at.X != last.X && at.Y != last.Y)
                    {
                        (double X, double Y) q = course.Path[i - 1];
                        double bx = originX + Math.Max(at.X, last.X) * cell, by = originY + Math.Max(at.Y, last.Y) * cell;
                        double tx = p.X != q.X ? (bx - q.X) / (p.X - q.X) : double.MaxValue;
                        double ty = p.Y != q.Y ? (by - q.Y) / (p.Y - q.Y) : double.MaxValue;
                        course.Cells.Add(tx < ty ? (at.X, last.Y) : (last.X, at.Y));
                    }
                }
                course.Cells.Add(at);
                last = at;
            }

            // Every land block the line passes through needs sub-blocks
            foreach ((int X, int Y) c in course.Cells)
            {
                int bx = Math.Max(0, Math.Min(Width - 1, (int)Math.Floor((double)c.X / SubBlocksPerSide)));
                int by = Math.Max(0, Math.Min(Height - 1, (int)Math.Floor((double)c.Y / SubBlocksPerSide)));
                if (land[bx, by]) DetailBlocks[bx, by] = true;
            }
        }

        // A path from a block into a basin on to the basin's lowest block, through the basin's blocks
        private static List<(int X, int Y)> PathWithin(LakeBasin basin, (int X, int Y) from)
        {
            HashSet<(int, int)> inside = new HashSet<(int, int)>(basin.Blocks) { from };
            Dictionary<(int, int), (int, int)> previous = new Dictionary<(int, int), (int, int)>();
            Queue<(int X, int Y)> queue = new Queue<(int, int)>();
            queue.Enqueue(from);
            previous[from] = from;
            while (queue.Count > 0)
            {
                (int X, int Y) c = queue.Dequeue();
                if (c == basin.Lowest) break;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        (int, int) n = (c.X + dx, c.Y + dy);
                        if (!inside.Contains(n) || previous.ContainsKey(n)) continue;
                        previous[n] = c;
                        queue.Enqueue(n);
                    }
            }
            List<(int X, int Y)> path = new List<(int, int)>();
            if (!previous.ContainsKey(basin.Lowest)) return new List<(int, int)> { from };
            for ((int X, int Y) c = basin.Lowest; c != from; c = previous[c]) path.Add(c);
            path.Add(from);
            path.Reverse();
            return path;
        }

        // A fixed point inside a block: its centre, moved up to a quarter of the block each way. Every river through the
        // block passes it, so a river joining another meets it there.
        private static (double X, double Y) InBlock((int X, int Y) b, int worldSeed, double size, double originX, double originY)
        {
            double jx = Hash(worldSeed, b.X, b.Y, 1) - 0.5, jy = Hash(worldSeed, b.X, b.Y, 2) - 0.5;
            return (originX + (b.X + 0.5 + 0.5 * jx) * size, originY + (b.Y + 0.5 + 0.5 * jy) * size);
        }

        // Where a river crosses from one block to the next: a point along their shared edge, or their shared corner
        private static (double X, double Y) Crossing((int X, int Y) a, (int X, int Y) b, int worldSeed, double size, double originX, double originY)
        {
            int ex = Math.Max(a.X, b.X), ey = Math.Max(a.Y, b.Y);
            if (a.Y == b.Y)
                return (originX + ex * size, originY + (a.Y + 0.25 + 0.5 * Hash(worldSeed, ex, a.Y, 3)) * size);
            if (a.X == b.X)
                return (originX + (a.X + 0.25 + 0.5 * Hash(worldSeed, a.X, ey, 4)) * size, originY + ey * size);
            return (originX + ex * size, originY + ey * size);
        }

        // Adds the winding line from a to b (not a itself) by moving the middle of each piece sideways, a share of its
        // length set by `meander` and fixed by the seed and where the piece is
        private static void Wind(List<(double X, double Y)> path, (double X, double Y) a, (double X, double Y) b, double meander, double finest, int seed, int level)
        {
            double length = Distance(a, b);
            if (length <= finest || level > 16 || meander <= 0)
            {
                path.Add(b);
                return;
            }
            double offset = meander * length * (2.0 * Hash(seed, (int)Math.Round(a.X), (int)Math.Round(a.Y), (int)Math.Round(b.X) ^ ((int)Math.Round(b.Y) << 7)) - 1.0);
            (double X, double Y) middle = ((a.X + b.X) / 2 - (b.Y - a.Y) / length * offset, (a.Y + b.Y) / 2 + (b.X - a.X) / length * offset);
            Wind(path, a, middle, meander * 0.85, finest, seed, level + 1);
            Wind(path, middle, b, meander * 0.85, finest, seed, level + 1);
        }

        private static double Distance((double X, double Y) a, (double X, double Y) b)
        {
            return Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
        }

        // A number in [0, 1) fixed by the values (FNV-1a)
        private static double Hash(int a, int b, int c, int d)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (int value in new[] { a, b, c, d })
                    for (int i = 0; i < 4; i++)
                    {
                        hash ^= (uint)(value >> (8 * i)) & 0xFF;
                        hash *= 16777619;
                    }
                // Finish with a mix so nearby inputs give unrelated outputs
                hash ^= hash >> 15; hash *= 0x2c1b3c6d; hash ^= hash >> 12; hash *= 0x297a2d39; hash ^= hash >> 15;
                return hash / 4294967296.0;
            }
        }
    }
}
