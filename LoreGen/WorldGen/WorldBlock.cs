using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;

using LoreGen.Simulation;
using LoreGen.Randomizer;
using LoreGen.Terrains;

namespace LoreGen.WorldGen
{
    /// <summary>
    /// A block of area in the simulated world   
    /// </summary>
    public class WorldBlock
    {
        /// <summary>
        /// Global X coordinate (in 100m blocks)
        /// </summary>
        public int TrueX;
        /// <summary>
        /// Global Y coordinate (in 100m blocks)
        /// </summary>
        public int TrueY;
        /// <summary>
        /// X coordinate within the parent block's children
        /// </summary>
        public int X;
        /// <summary>
        /// Y coordinate within the parent block's children
        /// </summary>
        public int Y;
        /// <summary>
        /// Depth level of this block. The global block = 0.
        /// </summary>
        public int Depth;
        /// <summary>
        /// Height of the grid of children blocks.
        /// </summary>
        public int Height;
        /// <summary>
        /// Width of the grid of children blocks.
        /// </summary>
        public int Width;
        /// <summary>
        /// Area of the grid of children blocks.
        /// </summary>
        public int Area
        {
            get
            {
                return Height * Width;
            }
        }
        /// <summary>
        /// Height of the block in 100m blocks.
        /// </summary>
        public int TrueHeight;
        /// <summary>
        /// Width of the block in 100m blocks.
        /// </summary>
        public int TrueWidth;
        /// <summary>
        /// The global max X coordinate of this block (in 100m blocks).
        /// </summary>
        public int TrueXMax
        {
            get
            {
                return TrueX + TrueWidth;
            }
        }
        /// <summary>
        /// The global max Y coordinate of this block (in 100m blocks).
        /// </summary>
        public int TrueYMax
        {
            get
            {
                return TrueY + TrueHeight;
            }
        }
        /// <summary>
        /// The area of this block in 100m x 100m segments.
        /// </summary>
        public long TrueArea
        {
            get
            {
                return ((long)TrueWidth) * ((long)TrueHeight);
            }
        }
        /// <summary>
        /// Parent block of this block.
        /// </summary>
        public WorldBlock ParentBlock;
        /// <summary>
        /// Children blocks of this block.
        /// </summary>
        public WorldBlock[,] ChildBlocks;
        /// <summary>
        /// Status/information of this block.
        /// </summary>
        public WorldBlockStatus Status;
        /// <summary>
        /// ID number of this block.
        /// </summary>
        public int ID;
        /// <summary>
        /// Entity this block represents.
        /// </summary>
        public WorldEntity Entity;
        /// <summary>
        /// Simulation engine.
        /// </summary>
        public SimEngine SimEngine;        
        private WorldRules Rules
        {
            get
            {
                return SimEngine.SimData.WorldRules;
            }
        }
        private Rnd Rnd
        {
            get
            {
                return SimEngine.Rnd;
            }
        }

        /*public WorldBlock(WorldBlock ParentBlock)
        {
            this.ParentBlock = ParentBlock;
            this.SimEngine = ParentBlock.SimEngine;
            this.Status = ParentBlock.Status.Copy();
            Depth = ParentBlock.Depth + 1;             
        }*/

        /// <summary>
        /// General constructor
        /// </summary>
        /// <param name="ParentBlock">Parent block</param>
        /// <param name="X">X among the parent block's children</param>
        /// <param name="Y">Y among the parent block's children</param>
        public WorldBlock(WorldBlock ParentBlock, int X, int Y)
        {
            this.ParentBlock = ParentBlock;
            this.SimEngine = ParentBlock.SimEngine;
            this.Status = ParentBlock.Status.Copy();
            Depth = ParentBlock.Depth + 1;
            this.X = X;
            this.Y = Y;
            this.Width = 10;
            this.Height = 10;
            TrueWidth = ParentBlock.TrueWidth / ParentBlock.Width;
            TrueHeight = ParentBlock.TrueHeight / ParentBlock.Height;
            TrueX = ParentBlock.TrueX + (X * TrueWidth);
            TrueY = ParentBlock.TrueY + (Y * TrueHeight);
            if (X == ParentBlock.Width - 1)
            {
                TrueWidth += ParentBlock.TrueWidth % ParentBlock.Width;
            }
            if (Y == ParentBlock.Height - 1)
            {
                TrueHeight += ParentBlock.TrueHeight % ParentBlock.Height;
            }
        }

        /// <summary>
        /// Constructor for the main world block ONLY
        /// </summary>
        /// <param name="World"></param>
        /// <param name="TrueWidth"></param>
        /// <param name="TrueHeight"></param>
        public WorldBlock(World World, int TrueWidth, int TrueHeight)
        {
            SimEngine = World.SimEngine;
            Entity = World;
            Status = new WorldBlockStatus();
            Depth = 0;
            X = 0;
            Y = 0;
            TrueX = 0;
            TrueY = 0;
            this.TrueHeight = TrueHeight;
            this.TrueWidth = TrueWidth;
            Width = TrueWidth / SimEngine.SimData.WorldRules.ZoomScaleLarge;
            Height = TrueHeight / SimEngine.SimData.WorldRules.ZoomScaleLarge;
            ID = 1;
            Status.WaterStatus = WorldBlockWaterStatus.Coastline;
        }

        public void InitializeChildBlocks()
        {
            ChildBlocks = new WorldBlock[Width, Height];
            for (int i = 0; i < Width; i++)
            {
                for (int j = 0; j < Height; j++)
                {
                    ChildBlocks[i, j] = new WorldBlock(this, i, j);     
                    if(Status.Region != null && Status.Region.Biome != null)
                    {
                        ChildBlocks[i, j].Status.Terrain = Status.Region.Biome.TerrainPattern.Get();
                    }
                }                
            }
            foreach(WorldBlock wb in ChildBlocksAsList().Where(cb => cb.Status.WaterStatus == WorldBlockWaterStatus.Land))
            {
                WorldBlock[] Neighbors = wb.SurroundingBlocks();
                foreach(WorldBlock Neighbor in Neighbors)
                {
                    if(Neighbor != null && Neighbor.Status.WaterStatus == WorldBlockWaterStatus.Water)
                    {
                        wb.Status.WaterStatus = WorldBlockWaterStatus.Coastline;
                    }
                }
            }
            /*
            if (SurroundingBlocksAsList().Any(wb => wb.Status.WaterStatus != WorldBlockWaterStatus.Water))
            {
                foreach (WorldBlock wb in ChildBlocksAsList().Where(cb => cb.Status.WaterStatus == WorldBlockWaterStatus.Water))
                {
                    WorldTasks.FixCoastlineForWaterBlock(wb);
                }
            }*/
        }

        /// <summary>
        /// Provides a list containing the IDs of this block and all of the parent blocks. This set of values is unique to this block.
        /// </summary>
        /// <returns>A list of ID values of blocks, starting with the parent-most block and ending with this block</returns>
        public List<int> FullID()
        {

            List<int> FullIDTemp;

            if (ParentBlock == null)
            {
                FullIDTemp = new List<int>();
            }
            else
            {
                FullIDTemp = ParentBlock.FullID();
            }
            FullIDTemp.Add(ID);
            return FullIDTemp;

        }

        public WorldBlock North(bool ForceChildBlockCreation=false)
        {
            if (ParentBlock == null)
            {
                return null;
            }
            if (Y != 0)
            {
                return ParentBlock.ChildBlocks[X, Y - 1];
            }
            if (ParentBlock.ParentBlock == null)
            {
                return null;
            }
            WorldBlock b = ParentBlock.North();
            if (b == null)
            {
                return null;
            }
            if (b == null) 
            {
                return null;
            }
            if( b.ChildBlocks == null) 
            {
                if(!ForceChildBlockCreation)
                {
                    return null;
                }
                else
                {
                    b.InitializeChildBlocks();                    
                }
            }
            if (b.Width != ParentBlock.Width || b.TrueWidth != ParentBlock.TrueWidth)
            {
                throw new Exception("Widths do not match up! Cannot get southern block!");
            }
            return b.ChildBlocks[X, b.Height - 1];            
        }

        public WorldBlock South(bool ForceChildBlockCreation = false)
        {
            if (ParentBlock == null)
            {
                return null;
            }
            if (Y != ParentBlock.Height-1)
            {
                return ParentBlock.ChildBlocks[X, Y + 1];
            }
            if (ParentBlock.ParentBlock == null)
            {
                return null;
            }
            WorldBlock b = ParentBlock.South();
            if (b == null)
            {
                return null;
            }
            if (b.ChildBlocks == null)
            {
                if (!ForceChildBlockCreation)
                {
                    return null;
                }
                else
                {
                    b.InitializeChildBlocks();                    
                }
            }
            if (b.Width != ParentBlock.Width || b.TrueWidth != ParentBlock.TrueWidth)
            {
                throw new Exception("Widths do not match up! Cannot get northern block!");
            }
            return b.ChildBlocks[X, 0];
        }

        public WorldBlock West(bool ForceChildBlockCreation = false)
        {
            if (ParentBlock == null)
            {
                return null;
            }
            if (X != 0)
            {
                return ParentBlock.ChildBlocks[X - 1, Y];
            }
            if (ParentBlock.ParentBlock == null)
            {
                return null;
            }
            WorldBlock b = ParentBlock.West();
            if (b == null)
            {
                return null;
            }
            if (b.ChildBlocks == null)
            {
                if (!ForceChildBlockCreation)
                {
                    return null;
                }
                else
                {
                    b.InitializeChildBlocks();                    
                }
            }
            if( b.Height != ParentBlock.Height || b.TrueHeight != ParentBlock.TrueHeight )
            {
                throw new Exception("Heights do not match up! Cannot get western block");
            }
            return b.ChildBlocks[b.Width-1, Y];
        }

        public WorldBlock East(bool ForceChildBlockCreation = false)
        {
            if (ParentBlock == null)
            {
                return null;
            }
            if (X != ParentBlock.Width-1)
            {
                return ParentBlock.ChildBlocks[X + 1, Y];
            }
            if (ParentBlock.ParentBlock == null)
            {
                return null;
            }
            WorldBlock b = ParentBlock.East();
            if(b == null)
            {
                return null;
            }
            if (b.ChildBlocks == null)
            {
                if (!ForceChildBlockCreation)
                {
                    return null;
                }
                else
                {
                    b.InitializeChildBlocks();
                }
            }
            if (b.Height != ParentBlock.Height || b.TrueHeight != ParentBlock.TrueHeight)
            {
                throw new Exception("Heights do not match up! Cannot get eastern block");
            }
            return b.ChildBlocks[0, Y];
        }

        public WorldBlock[] SurroundingBlocks(bool ForceSubblockCreation=false)
        {
            WorldBlock[] surrounding = new WorldBlock[4];
            surrounding[0] = North(ForceSubblockCreation);
            surrounding[1] = South(ForceSubblockCreation);
            surrounding[2] = East(ForceSubblockCreation);
            surrounding[3] = West(ForceSubblockCreation);
            return surrounding;
        }

        public List<WorldBlock> SurroundingBlocksAsList(bool ForceSubblockCreation=false)
        {
            return SurroundingBlocks(ForceSubblockCreation).Where(sb => sb != null).ToList();
        }

        /// <summary>
        /// Returns this block as a rectangle using the X, Y, Width, and Height values of this block.
        /// </summary>
        /// <returns>A rectangle representing this block</returns>
        public Rectangle AsRectangle()
        {
            return new Rectangle(TrueX, TrueY, TrueWidth, TrueHeight);
        }

        /// <summary>
        /// Returns this block as a rectangle scaled by the provided factors.
        /// </summary>
        /// <param name="widthScalar">Horizontal scalar</param>
        /// <param name="heightScalar">Vertical scalar</param>
        /// <returns>A rectangle representing this block</returns>
        public Rectangle AsRectangle(double widthScalar, double heightScalar)
        {
            return new Rectangle((int)(TrueX * widthScalar), (int)(TrueY * heightScalar), (int)(TrueWidth * widthScalar), (int)(TrueHeight * heightScalar));
            //return new Rectangle((int)Math.Round(X * widthScalar, MidpointRounding.AwayFromZero), (int)Math.Round(Y * heightScalar, MidpointRounding.AwayFromZero), (int)Math.Round(Width * widthScalar, MidpointRounding.AwayFromZero), (int)Math.Round(Height * heightScalar, MidpointRounding.AwayFromZero));
        }

        public Rectangle AsRectangle(Rectangle OutputRectangle)
        {
            return AsRectangle(((double)OutputRectangle.Width) / ((double)SimEngine.World.WorldBlock.TrueWidth), ((double)OutputRectangle.Height) / ((double)SimEngine.World.WorldBlock.TrueHeight));
        }

        public Rectangle AsRectangle(double widthScalar, double heightScalar, Rectangle InputRectangle)
        {                     
            Point rectangleStartingPoint = ProjectPoint(new Point(TrueX, TrueY),InputRectangle,widthScalar,heightScalar);
            Point rectangleEndingPoint = ProjectPoint(new Point(TrueX+TrueWidth, TrueY+TrueHeight),InputRectangle,widthScalar,heightScalar);
            Size size = new Size(rectangleEndingPoint.X-rectangleStartingPoint.X, rectangleEndingPoint.Y-rectangleStartingPoint.Y);
            return new Rectangle(rectangleStartingPoint, size);
        }

        private Point ProjectPoint(Point Point, Rectangle StartingRectangle, double widthScalar, double heightScalar)
        {            
            int X = (int)((Point.X - StartingRectangle.X) * widthScalar);
            int Y = (int)((Point.Y - StartingRectangle.Y) * heightScalar);
            return new Point(X, Y);
        }
        
        public WorldBlock ChildBlockAtPoint(Point Point, Rectangle DisplayRectangle)
        {
            int x = Point.X;
            int y = Point.Y;

            double xDivisor = ((double)(DisplayRectangle.Width)) / (double)Width;
            double yDivisor = ((double)(DisplayRectangle.Height)) / (double)Height;

            int outputX = (int)(x / xDivisor);
            int outputY = (int)(y / yDivisor);

            return ChildBlocks[outputX, outputY];
            
        }

        public List<WorldBlock> ChildBlocksAsList(bool IncludeAllLevels=false)
        {
            List<WorldBlock> children = new List<WorldBlock>();
            if(ChildBlocks == null)
                return children;
            for (int i = 0; i < Width; i++)
            {
                for (int j = 0; j < Height; j++)
                {
                    children.Add(ChildBlocks[i, j]);
                    if (IncludeAllLevels) children.AddRange(ChildBlocks[i, j].ChildBlocksAsList(true));
                }
            }
            return children;
        }
    }
    /// <summary>
    /// Information about the status of this block
    /// </summary>
    public class WorldBlockStatus
    {        
        /// <summary>
        /// Continent in which this block resides.
        /// </summary>
        public Continent Continent;
        public Ocean Ocean;
        public Sea Sea;
        public Region Region;
        public Terrain Terrain;
        public bool RegionBoundariesHaveBeenSmoothed;

        public WorldBlockWaterStatus WaterStatus;

        /// <summary>
        /// Returns a copy of this status
        /// </summary>
        /// <returns>Copy of this status</returns>
        public WorldBlockStatus Copy()
        {
            return new WorldBlockStatus
            {
                Continent = this.Continent,
                Ocean = this.Ocean,
                Sea = this.Sea,
                Region = this.Region,
                Terrain = this.Terrain,
                WaterStatus = this.WaterStatus,
                RegionBoundariesHaveBeenSmoothed = false
            };
        }        

        
    }

    /// <summary>
    /// Is this on or near a major body of water?
    /// </summary>
    public enum WorldBlockWaterStatus
    {
        Land, Water, Coastline
    }    

}
