using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LoreGen.Randomizer;

namespace LoreGen.WorldGen
{
    /// <summary>
    /// A collection of WorldBlocks combined as one area. This is an abstract class that can either be neatly shaped or irregularly shaped.
    /// </summary>
    public abstract class WorldBlockCollection
    {
        /// <summary>
        /// Parent block containing all of these blocks.
        /// </summary>
        public WorldBlock ParentBlock;
        /// <summary>
        /// Entity that this collection represents.
        /// </summary>
        public WorldEntity Entity;
        /// <summary>
        /// The total calculated area of this (in blocks on this order of magnitude).
        /// </summary>
        /// <returns>The area</returns>
        public abstract int Area();
        /// <summary>
        /// Is the block with this X and this Y in this area?
        /// </summary>
        /// <param name="XCoord">X value</param>
        /// <param name="YCoord">Y value</param> 
        /// <returns>In this area?</returns>
        public abstract bool InArea(int XCoord, int YCoord);
        /// <summary>
        /// Is the block with this X and this Y on the edge?
        /// </summary>
        /// <param name="XCoord">X value</param>
        /// <param name="YCoord">Y value</param>
        /// <returns>Is it on the edge?</returns>
        public abstract bool OnEdge(int XCoord, int YCoord);
        /// <summary>
        /// Is this block in this area?
        /// </summary>
        /// <param name="Block">The block</param>
        /// <returns>Is it in the area?</returns>
        public abstract bool InArea(WorldBlock Block);
        /// <summary>
        /// Is this block on the edge of this area?
        /// </summary>
        /// <param name="Block">The block</param>
        /// <returns>Is it on the edge?</returns>
        public abstract bool OnEdge(WorldBlock Block);
        /// <summary>
        /// A list of blocks that represent the edge of this area.
        /// </summary>
        /// <returns>Blocks of the edge of this area</returns>
        public abstract List<WorldBlock> Edge();
        /// <summary>
        /// A list of the blocks that represent this area.
        /// </summary>
        /// <returns>Blocks of this area</returns>
        public abstract List<WorldBlock> Blocks();
    }
    /// <summary>
    /// A collection of blocks are shaped as a rectangle.
    /// </summary>
    public class WorldBlockRectangle : WorldBlockCollection
    {
        public int TrueX
        { 
            get
            {
                return ParentBlock.ChildBlocks[X, Y].TrueX;
            }        
        }
        public int TrueY
        {
            get
            {
                return ParentBlock.ChildBlocks[X, Y].TrueY;
            }
        }
        public int TrueWidth
        {
            get
            {
                return ParentBlock.ChildBlocks[X, Y].TrueWidth * Width;
            }
        }
        public int TrueHeight
        {
            get
            {
                return ParentBlock.ChildBlocks[X, Y].TrueHeight * Height;
            }
        }
        /// <summary>
        /// X value of the origin of this rectangle (within the parent block).
        /// </summary>
        public int X;
        /// <summary>
        /// Y value of the origin of this rectangle (within the parent block).
        /// </summary>
        public int Y;
        /// <summary>
        /// Width of this rectangle (in blocks).
        /// </summary>
        public int Width;
        /// <summary>
        /// Height of this rectangle (in blocks).
        /// </summary>
        public int Height;
        /// <summary>
        /// Returns a 2D array containing the blocks in this rectangle
        /// </summary>
        /// <returns>A 2D array containing the blocks in this rectangle</returns>
        public WorldBlock[,] BlocksAsGrid()
        {
            WorldBlock[,] Blocks = new WorldBlock[Width, Height];
            for (int i = 0; i < Width; i++)
            {
                for (int j = 0; j < Height; j++)
                {
                    Blocks[i, j] = ParentBlock.ChildBlocks[X + i, Y + j];
                }
            }
            return Blocks;
        }
        /// <summary>
        /// Returns a list of the blocks in the rectangle.
        /// </summary>
        /// <returns>List of the blocks in the rectangle.</returns>
        public override List<WorldBlock> Blocks()
        {
            List<WorldBlock> blocks = new List<WorldBlock>();
            foreach (WorldBlock block in BlocksAsGrid())
            {
                blocks.Add(block);
            }
            return blocks;
        }
        /// <summary>
        /// Checks whether a block with a given X and Y is on the edge of this rectangle.
        /// </summary>
        /// <param name="XCoord">X of that block</param>
        /// <param name="YCoord">Y of that block</param>
        /// <returns>A boolean representing whether the block is on the edge</returns>
        public override bool OnEdge(int XCoord, int YCoord)
        {
            if (XCoord < X || XCoord >= X + Width || YCoord < Y || YCoord >= Y + Height)
                return false;
            if (XCoord == X || XCoord == X + Width - 1 || YCoord == Y || YCoord == Y + Height - 1)
                return true;
            return false;
        }
        /// <summary>
        /// Checks whether a block with a given X and Y is in this rectangle
        /// </summary>
        /// <param name="XCoord">X of that block</param>
        /// <param name="YCoord">Y of that block</param>
        /// <returns>A boolean representing whether the block is in the rectangle</returns>
        public override bool InArea(int XCoord, int YCoord)
        {
            if (XCoord < X || XCoord >= X + Width || YCoord < Y || YCoord >= Y + Height)
                return false;
            return true;
        }

        /// <summary>
        /// Checks whether a given block is on the edge of this rectangle
        /// </summary>
        /// <param name="Block">The block</param>
        /// <returns>A boolean representing whether that block is on the edge</returns>
        public override bool OnEdge(WorldBlock Block)
        {
            return OnEdge(Block.X, Block.Y);
        }

        /// <summary>
        /// Checks whether the given block is in this rectangle
        /// </summary>
        /// <param name="Block">The block</param>
        /// <returns>A boolean representing whether that block is on the edge</returns>
        public override bool InArea(WorldBlock Block)
        {
            return InArea(Block.X, Block.Y);
        }

        /// <summary>
        /// Returns the equivalent of this rectangle as a WorldBlockArea
        /// </summary>
        /// <returns>The equivalent of this rectangle as a WorldBlockArea</returns>
        public WorldBlockArea ToWorldBlockArea()
        {          
            return new WorldBlockArea ( Blocks(), this.Entity );
        }
        /// <summary>
        /// Returns the area of this rectangle
        /// </summary>
        /// <returns>Area of this rectangle (in blocks)</returns>
        public override int Area()
        {
            return Width * Height;
        }
        /// <summary>
        /// Returns a list of the blocks on the edge of this rectangle
        /// </summary>
        /// <returns>A list of the blocks on the edge of this rectangle</returns>
        public override List<WorldBlock> Edge()
        {
            return Blocks().Where(wb => wb.X == X || wb.X == X + Width - 1 || wb.Y == Y || wb.Y == Y + Height - 1).ToList();
        }

    }
    /// <summary>
    /// A collection of blocks represented as an open-ended list
    /// </summary>
    public class WorldBlockArea : WorldBlockCollection
    {
        public List<WorldBlock> BlocksList;
        /// <summary>
        /// Constructor that uses the given Blocks as the area and the given WorldEntity as the entity
        /// </summary>
        /// <param name="Blocks">Blocks representing the area</param>
        /// <param name="Entity">Entity this area represents</param>
        public WorldBlockArea(List<WorldBlock> Blocks, WorldEntity Entity)
        {
            this.Entity = Entity;
            BlocksList = Blocks;
            ParentBlock = BlocksList[0].ParentBlock;
        }

        public override bool InArea(int XCoord, int YCoord)
        {
            return BlocksList.Contains(ParentBlock.ChildBlocks[XCoord, YCoord]);
        }
        public override bool OnEdge(int XCoord, int YCoord)
        {
            return OnEdge(ParentBlock.ChildBlocks[XCoord, YCoord]);
        }
        public override bool OnEdge(WorldBlock Block)
        {
            return Edge().Contains(Block);
        }

        public override bool InArea(WorldBlock Block)
        {
            return BlocksList.Contains(Block);
        }
        public override int Area()
        {
            return BlocksList.Count();
        }
        public override List<WorldBlock> Blocks()
        {
            return BlocksList;
        }
        public override List<WorldBlock> Edge()
        {
            List<WorldBlock> edge = new List<WorldBlock>();
            foreach (WorldBlock block in BlocksList)
            {
                WorldBlock[] surrounding = block.SurroundingBlocks();
                bool onEdge = false;
                for (int i = 0; i < 4; i++)
                {
                    if (!InArea(surrounding[i]))
                    {
                        onEdge = true;
                    }
                }
                if (onEdge)
                {
                    edge.Add(block);
                }
            }
            return edge;
        }
        public List<List<WorldBlock>> ContiguousAreas()
        {            
            List<HashSet<WorldBlock>> ContiguousAreas = new List<HashSet<WorldBlock>>();
            List<WorldBlock> CountedBlocks = new List<WorldBlock>();
            List<WorldBlock> UncountedBlocks = BlocksList.Select(b => b).ToList();
            
            while(UncountedBlocks.Count > 0)
            {
                HashSet<WorldBlock> contiguousArea = new HashSet<WorldBlock>();
                WorldBlock start = UncountedBlocks.First();
                FindContiguousArea(contiguousArea,start,UncountedBlocks);
                ContiguousAreas.Add(contiguousArea);
            }

            return ContiguousAreas.Select(ca => ca.ToList()).ToList();
        }

        private void FindContiguousArea(HashSet<WorldBlock> contiguousArea, WorldBlock start, List<WorldBlock> uncountedBlocks)
        {
            if(contiguousArea.Contains(start)) return;
            contiguousArea.Add(start);
            uncountedBlocks.Remove(start);
            List<WorldBlock> neighborsInRegion = start.SurroundingBlocksAsList().Where(sb => uncountedBlocks.Contains(sb)).ToList();
            foreach(WorldBlock neighbor in neighborsInRegion)
            {
                FindContiguousArea(contiguousArea, neighbor, uncountedBlocks);
            }
        }
    }
}
