using System;
using System.Collections.Generic;
using System.Linq;

using LoreGen.Noise;
using LoreGen.Terrains;

namespace LoreGen.WorldGen.Generators
{
    /// <summary>
    /// Makes the detail inside a block when someone zooms in below the detail made with the world, level by level down to
    /// 100 m blocks (<see cref="WorldBlock.FinestTrueSize"/>). Set on worlds by <see cref="ElevationWorldBuilder"/>.
    ///
    /// Each child is worked out from the 3x3 blocks around its parent (at the parent's depth) and the child's position:
    ///  - land and water: where the 3x3 is all land or all water, the child is the same. Otherwise land counts +1 and water
    ///    -1, blended between block centres, plus smooth noise (ZoomCoastWiggle); the child is water below 0. So coasts and
    ///    lake shores follow the coarser outline and get more ragged with every level. Water never joins two continents.
    ///  - height, temperature, moisture and hydration: blended between block centres, plus fractal rise and fall on land
    ///    (ZoomRelief, from 10 km blocks down, halving with each level and stronger on high ground). Land stays above 0 and
    ///    water below.
    ///  - continent and region (land), ocean, sea or lake (water): from the nearest block of its kind among the 3x3; region
    ///    borders wander, as each child takes the region of the nearest land block centre after its position has been nudged
    ///    by noise (ZoomBorderWarp).
    ///  - terrain (land): chosen by the terrain model from the child's own height, climate, water, features and plate
    ///    boundary. It carries on the terrain of one of the four blocks around it (among those around its parent), drawn by
    ///    weight with a number from patchy noise (see InheritedTerrain): that terrain's landscape wherever it still fits
    ///    nearly as well, and the terrain itself over part of the ground (ZoomTerrainKeep). So the coarser terrain blends
    ///    into the finer blocks in ragged patches instead of changing along block edges. The blocks around a parent are
    ///    made first for this.
    ///
    /// The 3x3 blocks may not all exist yet. Any that don't are worked out by the same rule from the level above (and
    /// remembered), and blocks made with the world are fixed. So a child depends only on the world and its position, never
    /// on what was zoomed into first, and the children of neighbouring blocks meet without seams.
    /// </summary>
    internal sealed class ZoomDetail : IBlockDetailer
    {
        private const double Epsilon = 0.001;
        private const int N = 10; // children per side, as WorldBlock makes them

        private readonly World world;
        private readonly NoiseGeneratorRules rules;
        private readonly TerrainModel terrain;
        private readonly WorldBlock[,] top;
        private readonly int topWidth, topHeight, topSize;
        private readonly FractalNoise coastNoise, reliefNoise, warpNoise, blendNoise, climateNoise;
        private readonly double blendSpread, coastSpread;
        // Statuses worked out for blocks that have not been made, by (depth, x, y) in blocks of that depth
        private readonly Dictionary<(int, int, int), WorldBlockStatus> derived = new Dictionary<(int, int, int), WorldBlockStatus>();

        public ZoomDetail(World world, NoiseGeneratorRules rules, TerrainModel terrain, int worldSeed)
        {
            this.world = world;
            this.rules = rules;
            this.terrain = terrain;
            top = world.WorldBlock.ChildBlocks;
            topWidth = top.GetLength(0);
            topHeight = top.GetLength(1);
            topSize = top[0, 0].TrueWidth;
            // Seeded from the world seed rather than drawn, so the world itself is unchanged
            // Each level adds variation only down to about two of its own blocks; finer variation comes from the levels below
            coastNoise = new FractalNoise(new SimplexNoise(unchecked(worldSeed * 40503 + 0x2007)), 3, 1.0, 2.0, 0.5);
            reliefNoise = new FractalNoise(new SimplexNoise(unchecked(worldSeed * 69069 + 0x2001)), 2, 1.0, 2.0, 0.5);
            warpNoise = new FractalNoise(new SimplexNoise(unchecked(worldSeed * 22695477 + 0x2003)), 2, 1.0, 2.0, 0.5);
            // Chooses which surrounding block's terrain carries on where several could: patchy at every scale down to a
            // few blocks, so the borders between them are ragged rather than straight. Its spread, measured once, turns its
            // values into evenly spread numbers.
            blendNoise = new FractalNoise(new SimplexNoise(unchecked(worldSeed * 1103515245 + 0x2009)), 2, 1.0, 2.0, 0.5);
            // Nudges where a child's climate is blended from, so thresholds in it don't follow the coarser blocks' grid
            climateNoise = new FractalNoise(new SimplexNoise(unchecked(worldSeed * 2147483629 + 0x200d)), 2, 1.0, 2.0, 0.55);
            double sum = 0;
            for (int i = 0; i < 4096; i++) { double v = blendNoise.Fbm(i * 0.6180339887 * 37.0, i * 0.4142135623 * 41.0); sum += v * v; }
            blendSpread = Math.Max(1e-6, Math.Sqrt(sum / 4096));
            // Coast noise is measured too, so ZoomCoastWiggle is how far coasts typically wander as a share of a block
            sum = 0;
            for (int i = 0; i < 4096; i++) { double v = coastNoise.Fbm(i * 0.6180339887 * 37.0, i * 0.4142135623 * 41.0); sum += v * v; }
            coastSpread = Math.Max(1e-6, Math.Sqrt(sum / 4096));
        }

        public void CreateChildren(WorldBlock block)
        {
            if (block.ChildBlocks != null) return;
            int depth = block.Depth + 1;
            int size = SizeAt(block.Depth);
            if (block.Depth < 1 || block.TrueWidth != size || block.TrueHeight != size || size % N != 0)
            {
                // Not the usual even layout (it never happens for worlds from ElevationWorldBuilder): plain copies
                block.CreateChildGrid();
                return;
            }

            int px = block.TrueX / size, py = block.TrueY / size;
            // The blocks around this one are made first (if they haven't been), as children take their terrain from them
            WorldBlock[,] around = Around(block.Depth, px, py);
            WorldBlockStatus[,] ring = Ring(block.Depth, px, py);
            block.CreateChildGrid();

            // Land and water, height, climate and ownership
            for (int i = 0; i < N; i++)
            {
                for (int j = 0; j < N; j++)
                {
                    var key = (depth, px * N + i, py * N + j);
                    WorldBlockStatus status;
                    if (derived.TryGetValue(key, out status)) derived.Remove(key);
                    else status = Child(ring, depth, px * N + i, py * N + j);
                    block.ChildBlocks[i, j].Status = status;
                }
            }

            MarkCoastlines(block, depth, px, py);

            // Terrain, once every child has its place (features are looked up by block). Each child carries on the terrain of
            // the block around it whose centre is nearest once its position has been nudged by noise, so the coarser
            // terrain's borders wind through the finer blocks rather than running along block edges.
            var covering = world.FeaturesAtChildren(block);
            for (int i = 0; i < N; i++)
                for (int j = 0; j < N; j++)
                    PickTerrain(around, block, depth, px, py, i, j, covering[i, j]);

            // Subregions: below 100 km, each land child belongs to the subregion (of its own region) of the block around its
            // parent nearest to its nudged position, so subregion borders wind like region borders. The 100 km blocks get
            // theirs when their region is divided (SubregionPlanner).
            if (block.Depth >= 2 && world.HasSubregions)
                for (int i = 0; i < N; i++)
                    for (int j = 0; j < N; j++)
                    {
                        WorldBlockStatus status = block.ChildBlocks[i, j].Status;
                        if (status.WaterStatus == WorldBlockWaterStatus.Water || status.Region == null) continue;
                        status.Subregion = InheritedSubregion(around, block, status.Region, depth, px * N + i, py * N + j);
                    }

            // Small features in the new detail: hills, forests, coves... (10 km) and knolls, groves, ponds... (1 km). The
            // children inside them choose their terrain again, so terrain that belongs to them (a beach, lava) shows here
            // and not only further in
            var found = FineFeatureDetector.Detect(world, block, (x, y) => StatusAt(depth, x, y), rules, covering);
            if (found.Count > 0)
            {
                // A crater lake may have turned land to water
                if (found.Any(f => f.Type.Category == Features.FeatureCategory.Water)) MarkCoastlines(block, depth, px, py);
                var within = new HashSet<WorldBlock>(found.SelectMany(f => f.Footprint.Blocks));
                for (int i = 0; i < N; i++)
                    for (int j = 0; j < N; j++)
                        if (within.Contains(block.ChildBlocks[i, j])) PickTerrain(around, block, depth, px, py, i, j, world.FeaturesAt(block.ChildBlocks[i, j]));
            }
        }

        // Coastlines: land beside water, looking across into the neighbouring blocks' children where need be
        private void MarkCoastlines(WorldBlock block, int depth, int px, int py)
        {
            for (int i = 0; i < N; i++)
            {
                for (int j = 0; j < N; j++)
                {
                    WorldBlockStatus status = block.ChildBlocks[i, j].Status;
                    if (status.WaterStatus == WorldBlockWaterStatus.Water) continue;
                    int cx = px * N + i, cy = py * N + j;
                    bool beside = IsWater(block, depth, cx - 1, cy) || IsWater(block, depth, cx + 1, cy)
                        || IsWater(block, depth, cx, cy - 1) || IsWater(block, depth, cx, cy + 1);
                    status.WaterStatus = beside ? WorldBlockWaterStatus.Coastline : WorldBlockWaterStatus.Land;
                }
            }
        }

        // Chooses the terrain of one land child (see CreateChildren)
        private void PickTerrain(WorldBlock[,] around, WorldBlock block, int depth, int px, int py, int i, int j, IReadOnlyList<Features.GeographicFeature> features)
        {
            WorldBlock child = block.ChildBlocks[i, j];
            WorldBlockStatus status = child.Status;
            if (status.WaterStatus == WorldBlockWaterStatus.Water || status.Region == null) return;
            Terrain parentTerrain = InheritedTerrain(around, block, depth, px * N + i, py * N + j);
            var boundary = terrain.BoundaryBlend(child);
            status.Terrain = terrain.PickZoomed(new TerrainSite
            {
                Elevation = status.Elevation.Value,
                Temperature = status.Temperature.Value,
                Moisture = status.Moisture.Value,
                Hydration = status.Hydration.Value,
                Features = features,
                Boundary = boundary.Kind,
                BoundaryStrength = boundary.Strength
            }, child, parentTerrain, rules.ZoomTerrainKeep);
        }

        // ---- Blocks by position ----------------------------------------------------------------

        // Width of a block at a depth, in true units (depth 1 is the top-level grid)
        private int SizeAt(int depth)
        {
            int size = topSize;
            for (int d = 1; d < depth; d++) size /= N;
            return size;
        }

        private int Across(int depth, bool wide)
        {
            int count = wide ? topWidth : topHeight;
            for (int d = 1; d < depth; d++) count *= N;
            return count;
        }

        // The block at a depth and position (in blocks of that depth) if it has been made, otherwise null
        private WorldBlock Existing(int depth, int x, int y)
        {
            int scale = 1;
            for (int d = 1; d < depth; d++) scale *= N;
            WorldBlock block = top[x / scale, y / scale];
            while (block.Depth < depth)
            {
                if (block.ChildBlocks == null) return null;
                scale /= N;
                block = block.ChildBlocks[(x / scale) % N, (y / scale) % N];
            }
            return block;
        }

        // The status of the block at a depth and position: the block's own if it has been made, otherwise worked out (and
        // remembered). Null off the map.
        private WorldBlockStatus StatusAt(int depth, int x, int y)
        {
            if (x < 0 || y < 0 || x >= Across(depth, true) || y >= Across(depth, false)) return null;
            WorldBlock block = Existing(depth, x, y);
            if (block != null) return block.Status;
            var key = (depth, x, y);
            WorldBlockStatus status;
            if (derived.TryGetValue(key, out status)) return status;
            status = Child(Ring(depth - 1, x / N, y / N), depth, x, y);
            derived[key] = status;
            return status;
        }

        // The block at a depth and position, making it (and the blocks above it) if need be; null off the map
        private WorldBlock Make(int depth, int x, int y)
        {
            if (x < 0 || y < 0 || x >= Across(depth, true) || y >= Across(depth, false)) return null;
            WorldBlock block = Existing(depth, x, y);
            if (block != null) return block;
            WorldBlock parent = Make(depth - 1, x / N, y / N);
            if (parent == null || !world.EnsureChildren(parent) || parent.ChildBlocks.GetLength(0) != N) return null;
            return parent.ChildBlocks[x % N, y % N];
        }

        // The 3x3 blocks around a block, [dx + 1, dy + 1], made if need be; null off the map
        private WorldBlock[,] Around(int depth, int x, int y)
        {
            WorldBlock[,] around = new WorldBlock[3, 3];
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    around[dx + 1, dy + 1] = Make(depth, x + dx, y + dy);
            return around;
        }

        /// <summary>
        /// The terrain a child carries on, from one of the four land blocks (among the 3x3 around its parent) whose centres
        /// surround the child's nudged position. Each is weighted by how near it is (the weights blend from one block centre
        /// to the next), and one is drawn with a number from patchy noise. So between two blocks of different terrain the
        /// share of each changes gradually across the whole gap between their centres, in ragged patches, rather than
        /// switching along a line. The parent's own if no land block around has terrain.
        /// </summary>
        private Terrain InheritedTerrain(WorldBlock[,] around, WorldBlock parent, int depth, int x, int y)
        {
            Terrain own = parent.Status.WaterStatus == WorldBlockWaterStatus.Water ? null : parent.Status.Terrain;
            var (wx, wy) = Nudged(depth, x, y);
            // The four centres around the position: centres are at dx + 0.5 for dx = -1, 0, 1
            int x0 = wx < 0.5 ? -1 : 0, y0 = wy < 0.5 ? -1 : 0;
            double tx = Math.Max(0, Math.Min(1, wx - (x0 + 0.5))), ty = Math.Max(0, Math.Min(1, wy - (y0 + 0.5)));
            Terrain[] terrains = new Terrain[4];
            double[] weights = new double[4];
            double total = 0;
            for (int k = 0; k < 4; k++)
            {
                int dx = x0 + (k & 1), dy = y0 + (k >> 1);
                WorldBlock b = around[dx + 1, dy + 1];
                if (b == null || b.Status.Terrain == null || Water(b.Status)) continue;
                double weight = ((k & 1) == 1 ? tx : 1 - tx) * ((k >> 1) == 1 ? ty : 1 - ty);
                terrains[k] = b.Status.Terrain;
                weights[k] = weight;
                total += weight;
            }
            if (total <= 0) return own;
            double gx = (x + 0.5) / N + 211.3 * depth, gy = (y + 0.5) / N - 137.9 * depth;
            // Finest wiggle about four children across, so the choice comes in clumps rather than speckles
            double roll = Math.Max(0, Math.Min(1 - 1e-12, NormalCdf(blendNoise.Fbm(gx * 1.2, gy * 1.2) / blendSpread))) * total;
            for (int k = 0; k < 4; k++)
            {
                if (terrains[k] == null) continue;
                roll -= weights[k];
                if (roll < 0) return terrains[k];
            }
            for (int k = 3; k >= 0; k--) if (terrains[k] != null) return terrains[k];
            return own;
        }

        // The standard normal distribution function (Abramowitz and Stegun 7.1.26)
        private static double NormalCdf(double z)
        {
            double t = 1 / (1 + 0.3275911 * Math.Abs(z) / Math.Sqrt(2));
            double erf = 1 - ((((1.061405429 * t - 1.453152027) * t + 1.421413741) * t - 0.284496736) * t + 0.254829592) * t * Math.Exp(-z * z / 2);
            return z >= 0 ? (1 + erf) / 2 : (1 - erf) / 2;
        }

        /// <summary>
        /// The subregion a land child of the given region belongs to: that of the block of the same region, among the 3x3
        /// around its parent, whose centre is nearest to the child's nudged position.
        /// </summary>
        private Subregion InheritedSubregion(WorldBlock[,] around, WorldBlock parent, Region region, int depth, int x, int y)
        {
            var (wx, wy) = Nudged(depth, x, y);
            Subregion best = null;
            double bestDistance = double.MaxValue;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    WorldBlock b = around[dx + 1, dy + 1];
                    if (b == null || b.Status.Region != region || Water(b.Status)) continue;
                    Subregion subregion = world.SubregionAt(b);
                    if (subregion == null) continue;
                    double cx = dx + 0.5, cy = dy + 0.5;
                    double distance = (wx - cx) * (wx - cx) + (wy - cy) * (wy - cy);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = subregion;
                    }
                }
            }
            return best ?? (parent.Status.Region == region ? world.SubregionAt(parent) : null);
        }

        /// <summary>
        /// A child's position inside its parent (0 to 1 each way), nudged by smooth noise by up to ZoomBorderWarp. One
        /// function of position over the whole world, so nudges agree across block edges.
        /// </summary>
        private (double X, double Y) Nudged(int depth, int x, int y)
        {
            double lx = (x % N + 0.5) / N, ly = (y % N + 0.5) / N;
            double nx = (x + 0.5) / N + 173.1 * depth, ny = (y + 0.5) / N - 89.7 * depth;
            return (lx + rules.ZoomBorderWarp * warpNoise.Fbm(nx * 2, ny * 2),
                    ly + rules.ZoomBorderWarp * warpNoise.Fbm(nx * 2 + 31.7, ny * 2 + 17.3));
        }

        // The 3x3 statuses around a block, [dx + 1, dy + 1]; off the map, the block's own
        private WorldBlockStatus[,] Ring(int depth, int x, int y)
        {
            WorldBlockStatus[,] ring = new WorldBlockStatus[3, 3];
            WorldBlockStatus centre = StatusAt(depth, x, y);
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    ring[dx + 1, dy + 1] = (dx == 0 && dy == 0 ? centre : StatusAt(depth, x + dx, y + dy)) ?? centre;
            return ring;
        }

        private bool IsWater(WorldBlock parent, int depth, int x, int y)
        {
            int px = x / N, py = y / N;
            WorldBlockStatus status = parent.TrueX / SizeAt(parent.Depth) == px && parent.TrueY / SizeAt(parent.Depth) == py
                && x >= 0 && y >= 0
                ? parent.ChildBlocks[x - px * N, y - py * N].Status
                : StatusAt(depth, x, y);
            return status != null && status.WaterStatus == WorldBlockWaterStatus.Water;
        }

        // ---- The rule --------------------------------------------------------------------------

        /// <summary>
        /// The status of the child at a depth and position (in blocks of that depth), from the 3x3 statuses around its
        /// parent. Its water status is Land or Water (coastlines are marked once the children are made) and it has no
        /// terrain.
        /// </summary>
        private WorldBlockStatus Child(WorldBlockStatus[,] ring, int depth, int x, int y)
        {
            WorldBlockStatus parent = ring[1, 1];
            double lx = (x % N + 0.5) / N, ly = (y % N + 0.5) / N; // position inside the parent, 0 to 1
            double gx = (x + 0.5) / N, gy = (y + 0.5) / N;           // position in parent-sized blocks
            // Each level samples its own stretch of noise
            double ox = 173.1 * depth, oy = -89.7 * depth;

            // Land or water
            bool parentWater = Water(parent);
            bool water = parentWater;
            bool mixed = false;
            foreach (WorldBlockStatus s in ring) if (Water(s) != parentWater) { mixed = true; break; }
            if (mixed)
            {
                double side = Blend(ring, lx, ly, s => Water(s) ? -1.0 : 1.0);
                // Land/water is +1/-1 blended between centres (changing by 2 over a block), so a wiggle of w moves the coast
                // by about w / 2 of a block; the noise is scaled to its typical size and capped
                double wiggle = Clamp(coastNoise.Fbm(gx * 1.5 + ox, gy * 1.5 + oy) / coastSpread, -2.5, 2.5);
                water = side + rules.ZoomCoastWiggle * wiggle < 0;
                // New land in water must not join two continents, and needs land nearby to belong to
                if (!water && parentWater && (LandContinents(ring) > 1 || Nearest(ring, lx, ly, false) == null)) water = true;
            }

            // Height and climate
            double elevation = Blend(ring, lx, ly, s => s.Elevation ?? 0);
            // From 10 km blocks down: 100 km blocks made by zooming in match those made with the world, which have none
            if (!water && rules.ZoomRelief > 0 && depth >= 3)
            {
                double amplitude = rules.ZoomRelief * Math.Pow(0.5, depth - 3) * (0.5 + Math.Min(1.5, 2 * Math.Max(0, elevation)));
                elevation += amplitude * reliefNoise.Fbm(gx * 2 + ox, gy * 2 + oy);
            }
            elevation = water ? Math.Min(-Epsilon, elevation) : Math.Max(Epsilon, Math.Min(1.0, elevation));

            // Climate is blended at a nudged position (up to a third of the parent away), for ragged rather than straight
            // lines where it crosses a threshold
            double cx = Clamp(lx + 0.35 * climateNoise.Fbm(gx * 1.5 + ox, gy * 1.5 + oy), -0.5, 1.5);
            double cy = Clamp(ly + 0.35 * climateNoise.Fbm(gx * 1.5 + ox + 41.7, gy * 1.5 + oy - 23.3), -0.5, 1.5);
            WorldBlockStatus status = new WorldBlockStatus
            {
                Elevation = elevation,
                Temperature = Clamp(Blend(ring, cx, cy, s => s.Temperature ?? parent.Temperature ?? 0.5), 0, 1),
                Moisture = Clamp(Blend(ring, cx, cy, s => s.Moisture ?? parent.Moisture ?? 0.5), 0, 1),
                Hydration = Clamp(Blend(ring, cx, cy, s => s.Hydration ?? parent.Hydration ?? s.Moisture ?? 0.5), 0, 1),
                Plate = parent.Plate,
                WaterStatus = water ? WorldBlockWaterStatus.Water : WorldBlockWaterStatus.Land
            };

            if (water)
            {
                WorldBlockStatus owner = Nearest(ring, lx, ly, true) ?? parent;
                status.Ocean = owner.Ocean;
                status.Sea = owner.Sea;
                status.Lake = owner.Lake;
            }
            else
            {
                WorldBlockStatus owner = Nearest(ring, lx, ly, false) ?? parent;
                status.Continent = owner.Continent;
                status.Region = WarpedRegion(ring, owner, depth, x, y);
                status.Terrain = owner.Terrain; // provisional: the terrain model chooses once the block is made
            }
            return status;
        }

        private static bool Water(WorldBlockStatus status)
        {
            return status.WaterStatus == WorldBlockWaterStatus.Water;
        }

        private static int LandContinents(WorldBlockStatus[,] ring)
        {
            Continent first = null;
            foreach (WorldBlockStatus s in ring)
            {
                if (Water(s) || s.Continent == null) continue;
                if (first == null) first = s.Continent;
                else if (!ReferenceEquals(first, s.Continent)) return 2;
            }
            return first == null ? 0 : 1;
        }

        /// <summary>
        /// A value blended between the centres of the 3x3 blocks, at a position inside the middle one (0 to 1 each way).
        /// </summary>
        private static double Blend(WorldBlockStatus[,] ring, double lx, double ly, Func<WorldBlockStatus, double> value)
        {
            // Centres are at 0.5 (the middle block) and -0.5 / 1.5 (its neighbours)
            int x0 = lx < 0.5 ? 0 : 1, y0 = ly < 0.5 ? 0 : 1;
            double tx = lx < 0.5 ? lx + 0.5 : lx - 0.5, ty = ly < 0.5 ? ly + 0.5 : ly - 0.5;
            return (1 - tx) * (1 - ty) * value(ring[x0, y0]) + tx * (1 - ty) * value(ring[x0 + 1, y0])
                + (1 - tx) * ty * value(ring[x0, y0 + 1]) + tx * ty * value(ring[x0 + 1, y0 + 1]);
        }

        /// <summary>
        /// The block of the wanted kind (water or land) nearest to a position inside the middle block, among the 3x3; the
        /// middle one itself if it is of that kind.
        /// </summary>
        private static WorldBlockStatus Nearest(WorldBlockStatus[,] ring, double lx, double ly, bool wantWater)
        {
            WorldBlockStatus best = null;
            double bestDistance = double.MaxValue;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    WorldBlockStatus s = ring[dx + 1, dy + 1];
                    if (Water(s) != wantWater) continue;
                    if (!wantWater && s.Region == null) continue;
                    double gapX = dx < 0 ? lx : dx > 0 ? 1.0 - lx : 0.0;
                    double gapY = dy < 0 ? ly : dy > 0 ? 1.0 - ly : 0.0;
                    double distance = gapX * gapX + gapY * gapY;
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = s;
                    }
                }
            }
            return best;
        }

        /// <summary>
        /// The region of the land block (of the owner's continent, among the 3x3) whose centre is nearest once the position
        /// has been nudged by noise.
        /// </summary>
        private Region WarpedRegion(WorldBlockStatus[,] ring, WorldBlockStatus owner, int depth, int x, int y)
        {
            Region only = null;
            bool several = false;
            foreach (WorldBlockStatus s in ring)
            {
                if (Water(s) || s.Region == null || !ReferenceEquals(s.Continent, owner.Continent)) continue;
                if (only == null) only = s.Region;
                else if (!ReferenceEquals(only, s.Region)) { several = true; break; }
            }
            if (!several || rules.ZoomBorderWarp <= 0) return owner.Region ?? only;

            var (wx, wy) = Nudged(depth, x, y);
            Region best = owner.Region;
            double bestDistance = double.MaxValue;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    WorldBlockStatus s = ring[dx + 1, dy + 1];
                    if (Water(s) || s.Region == null || !ReferenceEquals(s.Continent, owner.Continent)) continue;
                    double cx = dx + 0.5, cy = dy + 0.5;
                    double distance = (wx - cx) * (wx - cx) + (wy - cy) * (wy - cy);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = s.Region;
                    }
                }
            }
            return best;
        }

        private static double Clamp(double value, double low, double high)
        {
            return Math.Max(low, Math.Min(high, value));
        }
    }
}
