using System;
using System.Collections.Generic;
using System.Linq;

namespace LoreGen.WorldGen.Generators
{
    /// <summary>
    /// Grid helpers shared by the feature detectors.
    /// </summary>
    internal static class FeatureGeometry
    {
        // How much more a spine step costs per block when it jumps a gap rather than moving to a touching block
        private const double JumpPenalty = 1.5;

        /// <summary>
        /// A block's column and row in a grid of all blocks at its depth across the whole world (a top-level block's own X
        /// and Y; a sub-block's are its parent's times 10, plus its own).
        /// </summary>
        public static (int X, int Y) Coord(WorldBlock block)
        {
            if (block.ParentBlock == null || block.ParentBlock.ParentBlock == null) return (block.X, block.Y);
            var parent = Coord(block.ParentBlock);
            return (parent.X * block.ParentBlock.Width + block.X, parent.Y * block.ParentBlock.Height + block.Y);
        }

        /// <summary>
        /// The path through a group of blocks (all at one depth) between its two ends: the block farthest from the first
        /// block, and the block farthest from that. Steps join blocks within `reach` of each other, so the path can jump
        /// passes, though it prefers to step between touching blocks. Ties go to the earlier block. The group must be
        /// connected within `reach`.
        /// </summary>
        public static List<WorldBlock> Spine(IReadOnlyList<WorldBlock> group, int reach)
        {
            if (group.Count == 1) return new List<WorldBlock>(group);
            var coords = group.Select(Coord).ToList();
            int[] previous;
            double[] distance = ShortestPaths(coords, 0, reach, out previous);
            int a = Farthest(distance);
            distance = ShortestPaths(coords, a, reach, out previous);
            int b = Farthest(distance);

            List<WorldBlock> path = new List<WorldBlock>();
            for (int i = b; i != -1; i = previous[i]) path.Add(group[i]);
            path.Reverse();
            return path;
        }

        /// <summary>
        /// Groups blocks (all at one depth) that touch at a side or corner, in order of each group's first block; blocks
        /// keep their order within a group.
        /// </summary>
        public static List<List<WorldBlock>> TouchingGroups(IReadOnlyList<WorldBlock> blocks)
        {
            Dictionary<(int, int), int> at = new Dictionary<(int, int), int>();
            for (int i = 0; i < blocks.Count; i++) at[Coord(blocks[i])] = i;
            int[] group = Enumerable.Repeat(-1, blocks.Count).ToArray();
            List<List<WorldBlock>> groups = new List<List<WorldBlock>>();
            for (int i = 0; i < blocks.Count; i++)
            {
                if (group[i] >= 0) continue;
                List<int> members = new List<int> { i };
                group[i] = groups.Count;
                Stack<int> pending = new Stack<int>();
                pending.Push(i);
                while (pending.Count > 0)
                {
                    var c = Coord(blocks[pending.Pop()]);
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            int j;
                            if (!at.TryGetValue((c.X + dx, c.Y + dy), out j) || group[j] >= 0) continue;
                            group[j] = groups.Count;
                            members.Add(j);
                            pending.Push(j);
                        }
                }
                members.Sort();
                groups.Add(members.Select(m => blocks[m]).ToList());
            }
            return groups;
        }

        private static int Farthest(double[] distance)
        {
            int best = 0;
            for (int i = 1; i < distance.Length; i++)
                if (!double.IsInfinity(distance[i]) && distance[i] > distance[best] + 1e-9) best = i;
            return best;
        }

        private static double[] ShortestPaths(List<(int X, int Y)> coords, int start, int reach, out int[] previous)
        {
            int n = coords.Count;
            double[] distance = Enumerable.Repeat(double.PositiveInfinity, n).ToArray();
            previous = Enumerable.Repeat(-1, n).ToArray();
            bool[] done = new bool[n];
            distance[start] = 0;
            PriorityQueue<int, (double, int)> queue = new PriorityQueue<int, (double, int)>();
            queue.Enqueue(start, (0.0, start));
            while (queue.TryDequeue(out int i, out _))
            {
                if (done[i]) continue;
                done[i] = true;
                for (int j = 0; j < n; j++)
                {
                    if (done[j]) continue;
                    int dx = coords[i].X - coords[j].X, dy = coords[i].Y - coords[j].Y;
                    if (Math.Abs(dx) > reach || Math.Abs(dy) > reach) continue;
                    double step = Math.Sqrt(dx * dx + dy * dy);
                    double d = distance[i] + (Math.Abs(dx) > 1 || Math.Abs(dy) > 1 ? step * JumpPenalty : step);
                    if (d < distance[j] - 1e-9)
                    {
                        distance[j] = d;
                        previous[j] = i;
                        queue.Enqueue(j, (d, j));
                    }
                }
            }
            return distance;
        }
    }
}
