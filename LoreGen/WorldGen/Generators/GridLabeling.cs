using System.Collections.Generic;

namespace LoreGen.WorldGen.Generators
{
    /// <summary>
    /// Finds the connected groups ("components") in a grid of true/false cells, treating cells as connected when they
    /// share an edge. Iterative, so large grids can't overflow the stack.
    /// </summary>
    internal class GridLabeling
    {
        /// <summary>
        /// Component number of each cell, or -1 for cells that are false. Components are numbered in scan order:
        /// top row first, left to right.
        /// </summary>
        public int[,] Labels;

        /// <summary>
        /// Number of cells in each component.
        /// </summary>
        public List<int> Sizes = new List<int>();

        /// <summary>
        /// Whether each component has a cell on the edge of the grid.
        /// </summary>
        public List<bool> TouchesEdge = new List<bool>();

        public int Count { get { return Sizes.Count; } }

        public static GridLabeling Label(bool[,] cells)
        {
            int width = cells.GetLength(0), height = cells.GetLength(1);
            GridLabeling result = new GridLabeling { Labels = new int[width, height] };
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    result.Labels[x, y] = -1;

            Stack<int> pending = new Stack<int>();
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!cells[x, y] || result.Labels[x, y] != -1) continue;

                    int label = result.Count;
                    int size = 0;
                    bool touchesEdge = false;
                    result.Labels[x, y] = label;
                    pending.Push(y * width + x);
                    while (pending.Count > 0)
                    {
                        int index = pending.Pop();
                        int cx = index % width, cy = index / width;
                        size++;
                        if (cx == 0 || cy == 0 || cx == width - 1 || cy == height - 1) touchesEdge = true;
                        Visit(cells, result.Labels, label, cx - 1, cy, width, height, pending);
                        Visit(cells, result.Labels, label, cx + 1, cy, width, height, pending);
                        Visit(cells, result.Labels, label, cx, cy - 1, width, height, pending);
                        Visit(cells, result.Labels, label, cx, cy + 1, width, height, pending);
                    }
                    result.Sizes.Add(size);
                    result.TouchesEdge.Add(touchesEdge);
                }
            }
            return result;
        }

        private static void Visit(bool[,] cells, int[,] labels, int label, int x, int y, int width, int height, Stack<int> pending)
        {
            if (x < 0 || y < 0 || x >= width || y >= height) return;
            if (!cells[x, y] || labels[x, y] != -1) return;
            labels[x, y] = label;
            pending.Push(y * width + x);
        }
    }
}
