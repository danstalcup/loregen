using System;
using System.Collections.Generic;

using LoreGen.WorldGen;

namespace LoreGen.Features
{
    /// <summary>
    /// Shapes a river below the depth it is defined at: a finer block is part of the river when the river's winding centre
    /// line passes within half the river's width of it. The width grows from the source to the mouth. A river is never
    /// narrower than one block at any depth (the line passes through every block it is part of) and never leaves the
    /// blocks of its footprint, so it stays one connected ribbon however far in it is drawn.
    ///
    /// The centre line is fixed when the world is made (from the world seed and where the river runs), so the answer
    /// depends only on the block's position, as <see cref="IFeatureRefiner"/> requires.
    /// </summary>
    public sealed class RiverRefiner : IFeatureRefiner
    {
        private readonly IReadOnlyList<(double X, double Y)> path;
        private readonly IReadOnlyList<double> along;
        private readonly double sourceHalfWidth, mouthHalfWidth;
        private readonly double cell;
        private readonly Dictionary<(long, long), List<int>> segmentsAt = new Dictionary<(long, long), List<int>>();

        /// <summary>
        /// Creates a refiner for a river.
        /// </summary>
        /// <param name="path">The centre line, in true units (TrueX, TrueY), from source to end</param>
        /// <param name="along">The distance along the line to each of its points</param>
        /// <param name="sourceWidth">How wide the river is at its source, in true units</param>
        /// <param name="mouthWidth">How wide it is at its end, in true units</param>
        /// <param name="cellSize">The size of the blocks of the river's footprint, in true units (used to find the line quickly)</param>
        public RiverRefiner(IReadOnlyList<(double X, double Y)> path, IReadOnlyList<double> along, double sourceWidth, double mouthWidth, double cellSize)
        {
            if (path == null) throw new ArgumentNullException("path");
            if (along == null || along.Count != path.Count) throw new ArgumentException("There must be a distance for every point of the path.", "along");
            if (cellSize <= 0) throw new ArgumentOutOfRangeException("cellSize");
            this.path = path;
            this.along = along;
            sourceHalfWidth = Math.Max(0, sourceWidth) / 2;
            mouthHalfWidth = Math.Max(0, mouthWidth) / 2;
            cell = cellSize;

            // Index each piece of the line under every footprint-sized cell its bounding box (plus the widest half-width) touches
            double pad = Math.Max(sourceHalfWidth, mouthHalfWidth);
            for (int i = 1; i < path.Count; i++)
            {
                long x0 = (long)Math.Floor((Math.Min(path[i - 1].X, path[i].X) - pad) / cell), x1 = (long)Math.Floor((Math.Max(path[i - 1].X, path[i].X) + pad) / cell);
                long y0 = (long)Math.Floor((Math.Min(path[i - 1].Y, path[i].Y) - pad) / cell), y1 = (long)Math.Floor((Math.Max(path[i - 1].Y, path[i].Y) + pad) / cell);
                for (long x = x0; x <= x1; x++)
                    for (long y = y0; y <= y1; y++)
                    {
                        List<int> list;
                        if (!segmentsAt.TryGetValue((x, y), out list)) segmentsAt[(x, y)] = list = new List<int>();
                        list.Add(i);
                    }
            }
        }

        /// <summary>How wide the river is at a distance along its centre line, in true units.</summary>
        public double WidthAt(double distance)
        {
            double total = along.Count > 0 ? along[along.Count - 1] : 0;
            double t = total > 0 ? Math.Max(0, Math.Min(1, distance / total)) : 0;
            return 2 * (sourceHalfWidth + (mouthHalfWidth - sourceHalfWidth) * t);
        }

        public bool Includes(GeographicFeature feature, WorldBlock block, bool coarseMember)
        {
            if (!coarseMember) return false;
            return Touches(block.TrueX, block.TrueY, block.TrueX + block.TrueWidth, block.TrueY + block.TrueHeight);
        }

        /// <summary>
        /// Whether the river (its centre line, widened by half its width) touches a rectangle in true units.
        /// </summary>
        public bool Touches(double left, double top, double right, double bottom)
        {
            HashSet<int> checkedSegments = new HashSet<int>();
            long x0 = (long)Math.Floor(left / cell), x1 = (long)Math.Floor((right - 1e-9) / cell);
            long y0 = (long)Math.Floor(top / cell), y1 = (long)Math.Floor((bottom - 1e-9) / cell);
            for (long x = x0; x <= x1; x++)
            {
                for (long y = y0; y <= y1; y++)
                {
                    List<int> list;
                    if (!segmentsAt.TryGetValue((x, y), out list)) continue;
                    foreach (int i in list)
                    {
                        if (!checkedSegments.Add(i)) continue;
                        double halfWidth = WidthAt((along[i - 1] + along[i]) / 2) / 2;
                        if (SegmentToRectangle(path[i - 1], path[i], left, top, right, bottom) <= halfWidth) return true;
                    }
                }
            }
            return false;
        }

        // Distance from a line segment to a rectangle (0 if they meet)
        private static double SegmentToRectangle((double X, double Y) a, (double X, double Y) b, double left, double top, double right, double bottom)
        {
            if (Inside(a, left, top, right, bottom) || Inside(b, left, top, right, bottom)) return 0;
            (double X, double Y)[] corners = { (left, top), (right, top), (right, bottom), (left, bottom) };
            for (int i = 0; i < 4; i++)
                if (SegmentsCross(a, b, corners[i], corners[(i + 1) % 4])) return 0;
            double best = double.MaxValue;
            foreach (var corner in corners) best = Math.Min(best, PointToSegment(corner, a, b));
            best = Math.Min(best, PointToRectangle(a, left, top, right, bottom));
            best = Math.Min(best, PointToRectangle(b, left, top, right, bottom));
            return best;
        }

        private static bool Inside((double X, double Y) p, double left, double top, double right, double bottom)
        {
            return p.X >= left && p.X < right && p.Y >= top && p.Y < bottom;
        }

        private static double PointToRectangle((double X, double Y) p, double left, double top, double right, double bottom)
        {
            double dx = Math.Max(Math.Max(left - p.X, 0), p.X - right);
            double dy = Math.Max(Math.Max(top - p.Y, 0), p.Y - bottom);
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static double PointToSegment((double X, double Y) p, (double X, double Y) a, (double X, double Y) b)
        {
            double vx = b.X - a.X, vy = b.Y - a.Y;
            double lengthSquared = vx * vx + vy * vy;
            double t = lengthSquared > 0 ? Math.Max(0, Math.Min(1, ((p.X - a.X) * vx + (p.Y - a.Y) * vy) / lengthSquared)) : 0;
            double cx = a.X + t * vx - p.X, cy = a.Y + t * vy - p.Y;
            return Math.Sqrt(cx * cx + cy * cy);
        }

        private static bool SegmentsCross((double X, double Y) a, (double X, double Y) b, (double X, double Y) c, (double X, double Y) d)
        {
            double d1 = Cross(c, d, a), d2 = Cross(c, d, b), d3 = Cross(a, b, c), d4 = Cross(a, b, d);
            return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
        }

        private static double Cross((double X, double Y) o, (double X, double Y) a, (double X, double Y) b)
        {
            return (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
        }
    }
}
