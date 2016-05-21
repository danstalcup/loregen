using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;

using LoreGen.Randomizer;

namespace LoreGen.WorldGen
{
    /// <summary>
    /// A class of static methods to assist in world generation and simulation.
    /// </summary>
    public class WorldTasks
    {
        /// <summary>
        /// Apply a random coastline to the given block (by generating child blocks).
        /// </summary>
        /// <param name="Block">Block for coastline</param>
        /// <param name="Length">Length of the grid of subblocks</param>
        public static void ApplyCoastlineToBlock(WorldBlock Block, int Length)
        {
            //NSEW
            WorldBlock[] neighbors = Block.SurroundingBlocks();

            bool n = neighbors[0] == null || neighbors[0].Status.WaterStatus == WorldBlockWaterStatus.Water;
            bool s = neighbors[1] == null || neighbors[1].Status.WaterStatus == WorldBlockWaterStatus.Water;
            bool e = neighbors[2] == null || neighbors[2].Status.WaterStatus == WorldBlockWaterStatus.Water;
            bool w = neighbors[3] == null || neighbors[3].Status.WaterStatus == WorldBlockWaterStatus.Water;

            List<EdgePattern> ValidEdgePatterns = EdgePattern.GetValidEdgePatterns(Block.SimEngine.SimData.EdgePatterns,n,s,w,e).ToList();

            if (ValidEdgePatterns.Count > 0)
            {
                ApplyCoastlineToBlock(Block, ListR<EdgePattern>.RandomFromList(ValidEdgePatterns, Block.SimEngine.Rnd), 10);
                //ApplyFakeCoastlineToBlock(Block, n, s, e, w, 10);
            }
            else
            {
                return;
            }
        }
        /// <summary>
        /// Apply the given coastline to the block (by generating child blocks).
        /// </summary>
        /// <param name="Block">Block for coastline</param>
        /// <param name="Pattern">Coastline pattern</param>
        /// <param name="Length">Length of grid of subblocks</param>
        public static void ApplyCoastlineToBlock(WorldBlock Block, EdgePattern Pattern, int Length)
        {
            Block.Height = Length;
            Block.Width = Length;
            Block.InitializeChildBlocks();
            ApplyCoastlineToBlock(Block, Pattern);
        }

        public static void ApplyFakeCoastlineToBlock(WorldBlock Block, bool n, bool s, bool e, bool w, int Length)
        {
            Block.Height = Length;
            Block.Width = Length;
            Block.InitializeChildBlocks();
            foreach(WorldBlock wb in Block.ChildBlocksAsList())
            {
                wb.Status.WaterStatus = WorldBlockWaterStatus.Land;
            }
            if(n)
                foreach(WorldBlock subblock in Block.ChildBlocksAsList().Where(cb => cb.Y==0))
                {
                    subblock.Status.WaterStatus = WorldBlockWaterStatus.Coastline;
                }
            if (s)
                foreach (WorldBlock subblock in Block.ChildBlocksAsList().Where(cb => cb.Y == 9))
                {
                    subblock.Status.WaterStatus = WorldBlockWaterStatus.Coastline;
                }
            if (e)
                foreach (WorldBlock subblock in Block.ChildBlocksAsList().Where(cb => cb.X == 9))
                {
                    subblock.Status.WaterStatus = WorldBlockWaterStatus.Coastline;
                }
            if (w)
                foreach (WorldBlock subblock in Block.ChildBlocksAsList().Where(cb => cb.X == 0))
                {
                    subblock.Status.WaterStatus = WorldBlockWaterStatus.Coastline;
                }
        }

        /// <summary>
        /// Apply the given coastline to the block (assuming child blocks already exist).
        /// </summary>
        /// <param name="Block">Block for coastline</param>
        /// <param name="Pattern">Coastline pattern</param>
        public static void ApplyCoastlineToBlock(WorldBlock Block, EdgePattern Pattern)
        {
            if (Block.Height != Block.Width)
                throw new Exception("Coastline pattern does not support non-square blocks just yet!");
            EdgePattern fit = Pattern.FitToLength(Block.Height);
            for (int i = 0; i < Block.Width; i++)
            {
                for (int j = 0; j < Block.Height; j++)
                {
                    if (fit.Pattern[i, j] == EdgeStatus.On)
                        Block.ChildBlocks[i, j].Status.WaterStatus = WorldBlockWaterStatus.Land;
                    else if (fit.Pattern[i, j] == EdgeStatus.Off)
                    {                        
                        Block.ChildBlocks[i, j].Status.WaterStatus = WorldBlockWaterStatus.Water;
                        //FixCoastlineForWaterBlock(Block.ChildBlocks[i, j]);
                    }
                    else if (fit.Pattern[i, j] == EdgeStatus.Edge)
                        Block.ChildBlocks[i, j].Status.WaterStatus = WorldBlockWaterStatus.Coastline;
                }
            }
            List<WorldBlock> WaterBlocks = Block.ChildBlocksAsList().Where(cb => cb.Status.WaterStatus == WorldBlockWaterStatus.Water).ToList();
            foreach(WorldBlock block in WaterBlocks)
            {
                FixCoastlineForWaterBlock(block);
            }

        }


        public static int ClosestEdge(bool[] NSEWIsEdge, int X, int Y)
        {
            List<int> EdgesNSEW = new List<int>();
            for (int i = 0; i < 4; i++) if (NSEWIsEdge[i]) EdgesNSEW.Add(i);  
          
            if(EdgesNSEW.Contains(0) && EdgesNSEW.Contains(1))
            {
                if (Y < 5) EdgesNSEW.Remove(1);
                else EdgesNSEW.Remove(0);
            }

            if (EdgesNSEW.Contains(2) && EdgesNSEW.Contains(3))
            {
                if (X < 5) EdgesNSEW.Remove(2);
                else EdgesNSEW.Remove(3);
            }

            if (EdgesNSEW.Count == 1) return EdgesNSEW[0];

            int[] DistanceFrom = new int[4];
            DistanceFrom[0] = Y;
            DistanceFrom[1] = 10 - Y;
            DistanceFrom[2] = 10 - X;
            DistanceFrom[3] = X;
            
            if(DistanceFrom[EdgesNSEW[0]] < DistanceFrom[EdgesNSEW[1]])
            {
                return EdgesNSEW[0];
            }
            else
            {
                return EdgesNSEW[1];
            }
        }

        /// <summary>
        /// Makes sure the surrounding blocks are coastline
        /// </summary>
        /// <param name="block"></param>
        public static List<WorldBlock> FixCoastlineForWaterBlock(WorldBlock block)
        {
            List<WorldBlock> NewCoastlineBlocks = new List<WorldBlock>();
            WorldBlock[] surrounding = block.SurroundingBlocks(true);
            for (int j = 0; j < 4; j++)
            {
                if (surrounding[j] == null)
                    continue;
                if (surrounding[j].Status.WaterStatus == WorldBlockWaterStatus.Land)
                {
                    surrounding[j].Status.WaterStatus = WorldBlockWaterStatus.Coastline;
                    NewCoastlineBlocks.Add(surrounding[j]);
                }
            }
            return NewCoastlineBlocks;
        }

        public static WorldBlock GetBlockAtPoint(WorldBlock[,] Grid, Point Point, Rectangle DisplayRectangle)
        {
            int x = Point.X;
            int y = Point.Y;

            double xDivisor = ((double)(DisplayRectangle.Width)) / (double)(Grid.GetUpperBound(0)+1);
            double yDivisor = ((double)(DisplayRectangle.Height)) / (double)(Grid.GetUpperBound(1) + 1);

            int outputX = (int)(x / xDivisor);
            int outputY = (int)(y / yDivisor);

            return Grid[outputX, outputY];
        }
    }
}
