using System;
using System.Collections.Generic;
using System.Linq;

namespace LoreGen.WorldGen
{
    /// <summary>
    /// Works out hints for naming places (see <see cref="Lang.NameContext"/>) from where they lie.
    /// </summary>
    public static class NameHints
    {
        /// <summary>
        /// How far from the middle of its parent (as a share of the parent's width or height) a place has to lie before
        /// it counts as lying to one side.
        /// </summary>
        public const double SideThreshold = 0.2;

        /// <summary>
        /// The side of <paramref name="whole"/> that <paramref name="part"/> lies on, or null if it lies near the middle
        /// or either collection is empty.
        /// </summary>
        public static Directions? DirectionWithin(IEnumerable<WorldBlock> part, IEnumerable<WorldBlock> whole)
        {
            if (part == null || whole == null) return null;
            List<WorldBlock> wholeList = whole.ToList();
            if (wholeList.Count == 0) return null;
            return DirectionWithin(part,
                wholeList.Min(b => b.TrueX), wholeList.Max(b => b.TrueXMax),
                wholeList.Min(b => b.TrueY), wholeList.Max(b => b.TrueYMax));
        }

        /// <summary>
        /// The side of the world that <paramref name="part"/> lies on, or null if it lies near the middle.
        /// </summary>
        public static Directions? DirectionWithinWorld(IEnumerable<WorldBlock> part, World world)
        {
            if (part == null || world?.WorldBlock == null) return null;
            WorldBlock w = world.WorldBlock;
            return DirectionWithin(part, w.TrueX, w.TrueXMax, w.TrueY, w.TrueYMax);
        }

        private static Directions? DirectionWithin(IEnumerable<WorldBlock> part, double minX, double maxX, double minY, double maxY)
        {
            List<WorldBlock> partList = part.ToList();
            if (partList.Count == 0 || maxX <= minX || maxY <= minY) return null;

            double cx = partList.Average(b => b.TrueX + b.TrueWidth / 2.0);
            double cy = partList.Average(b => b.TrueY + b.TrueHeight / 2.0);
            double dx = (cx - (minX + maxX) / 2.0) / (maxX - minX);
            double dy = (cy - (minY + maxY) / 2.0) / (maxY - minY);

            if (Math.Max(Math.Abs(dx), Math.Abs(dy)) < SideThreshold) return null;
            if (Math.Abs(dx) >= Math.Abs(dy)) return dx < 0 ? Directions.West : Directions.East;
            return dy < 0 ? Directions.North : Directions.South; // y grows southwards
        }
    }
}
