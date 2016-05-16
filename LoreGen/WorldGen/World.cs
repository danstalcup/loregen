using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LoreGen.Randomizer;
using LoreGen.Simulation;

namespace LoreGen.WorldGen
{
    /// <summary>
    /// A simulated world
    /// </summary>
    public class World : WorldEntity
    {
        /// <summary>
        /// World name
        /// </summary>
        public SimEngine SimEngine;
        /// <summary>
        /// Collection of the world's continents
        /// </summary>
        public List<Continent> Continents;
        
        /// <summary>
        /// Collection of the world's oceans
        /// </summary>
        public List<Ocean> Oceans;

        /// <summary>
        /// Block encompassing the whole simulated world
        /// </summary>
        public WorldBlock WorldBlock;

        private WorldRules Rules
        {
            get
            {
                return SimEngine.SimData.WorldRules;
            }
        }

        /// <summary>
        /// Just does basic initialization. To generate a random world, use the static GenerateWorld method
        /// </summary>
        /// <param name="SimEngine">Sim engine</param>
        public World(SimEngine SimEngine)
        {
            this.SimEngine = SimEngine;
            this.SimEngine.World = this;
            this.WorldEntityType = WorldGen.WorldEntityType.World;
            DisplayInfo = new WorldDisplay.WorldEntityDisplayInfo();
            Oceans = new List<Ocean>();
            Continents = new List<Continent>();
        }
        

        /// <summary>
        /// Returns a newly generated world for the provided engine
        /// </summary>
        /// <param name="simEngine">Engine running the simulation</param>
        /// <returns></returns>
        public static World GenerateWorld(SimEngine simEngine)
        {
            //Create world
            World world = new World(simEngine);                               
                        
            //Generate continent regions
            world.GenerateWorldBlock();            
            //world.GenerateOceansAndContinents();
            world.NewGenerateContinents();
            world.NewGenerateOceans(world.Continents.Count);

            return world;
        }        

        private void GenerateWorldBlock()
        {
            //Determine world length
            int length = Rnd.NormalAsInt(Rules.WorldLengthMean, Rules.WorldLengthStdDev);
            length = Math.Min(length, Rules.WorldLengthMax);
            length = Math.Max(length, Rules.WorldLengthMin);
            int trueWidth = length;
            trueWidth = (trueWidth / Rules.ZoomScaleLarge) * Rules.ZoomScaleLarge;
            int trueHeight = (int)(length * Rules.WorldHeightLengthRatio);
            trueHeight = (trueHeight / Rules.ZoomScaleLarge) * Rules.ZoomScaleLarge;

            //Create world block
            WorldBlock = new WorldBlock(this, trueWidth, trueHeight);
            WorldBlock.InitializeChildBlocks();
        }

       /* private int[] GenerateOuterOceans()
        {
            double OuterOceanPercentage = Rnd.Normal(Rules.CoastlineBorderMean, Rules.CoastlineBorderStdDev);
            if (OuterOceanPercentage < Rules.CoastlineBorderMin) OuterOceanPercentage = Rules.CoastlineBorderMin;
            if (OuterOceanPercentage > Rules.CoastlineBorderMax) OuterOceanPercentage = Rules.CoastlineBorderMax;

            int OuterOceanBlocksY = (int)(OuterOceanPercentage * WorldBlock.Height);
            int OuterOceanBlocksX = (int)(OuterOceanPercentage * WorldBlock.Width);

            if (OuterOceanBlocksY < 1) OuterOceanBlocksY = 1;
            if (OuterOceanBlocksX < 1) OuterOceanBlocksX = 1;

            Ocean NorthOcean = new Ocean(this, new WorldBlockRectangle { X = 0, Y = 0, Width = WorldBlock.Width, Height = OuterOceanBlocksY });
            Ocean SouthOcean = new Ocean(this, new WorldBlockRectangle { X = 0, Y = WorldBlock.Height - OuterOceanBlocksY, Width = WorldBlock.Width, Height = OuterOceanBlocksY });
            Ocean WestOcean = new Ocean(this, new WorldBlockRectangle { X = 0, Y = OuterOceanBlocksY, Width = OuterOceanBlocksX, Height = WorldBlock.Height - 2 * OuterOceanBlocksY });
            Ocean EastOcean = new Ocean(this, new WorldBlockRectangle { X = WorldBlock.Width - OuterOceanBlocksX, Y = OuterOceanBlocksY, Width = OuterOceanBlocksX, Height = WorldBlock.Height - 2 * OuterOceanBlocksY });

            return new int[]{OuterOceanBlocksX, OuterOceanBlocksY};
        }*/
  
        private void NewGenerateContinents()
        {
            //Figure out number of continents
            int numContinents = Rnd.Unweighted(WorldRules.ContinentsMin, WorldRules.ContinentsMax);
            List<WorldBlock> UnallocatedBlocks = WorldBlock.ChildBlocksAsList();            

            for(int i=0;i<numContinents;i++)
            {
                WorldBlock continentStartingBlock = ListR<WorldBlock>.RandomFromList(UnallocatedBlocks, Rnd);
                UnallocatedBlocks.Remove(continentStartingBlock);
                Continent.GenerateContinentWithAreaFromStartingBlock(this, continentStartingBlock);
            }

            while(UnallocatedBlocks.Count > 0)
            {
                WorldBlock nextBlock = ListR<WorldBlock>.RandomFromList(UnallocatedBlocks.Where(b => b.SurroundingBlocksAsList().Any(sb => sb.Status.Continent != null)).ToList(),Rnd);
                Continent newContinent = ListR<WorldBlock>.RandomFromList(nextBlock.SurroundingBlocksAsList().Where(sb => sb.Status.Continent != null).ToList(), Rnd).Status.Continent;
                newContinent.AddBlock(nextBlock);
                UnallocatedBlocks.Remove(nextBlock);
            }
            
            foreach(Continent continent in Continents)
            {
                List<WorldBlock> edge = continent.ContinentArea.Edge();
                
                foreach(WorldBlock block in edge)
                {
                    block.Status.WaterStatus = WorldBlockWaterStatus.Coastline;                    
                }
                List<WorldBlock> nonEdge = continent.ContinentArea.BlocksList.Except(edge).ToList();
                foreach(WorldBlock block in nonEdge)
                {
                    block.Status.WaterStatus = WorldBlockWaterStatus.Land;
                }
            }

            bool BorderOcean = false;

            if(BorderOcean)
            {
                List<WorldBlock> BorderBlocks = WorldBlock.ChildBlocksAsList().Where(cb => cb.X == 0 || cb.Y == 0 || cb.X==WorldBlock.Width-1 || cb.Y==WorldBlock.Height-1).ToList();
                List<WorldBlock> SecondLevelBorderBlocks = WorldBlock.ChildBlocksAsList().Where(cb => cb.X ==1 || cb.Y == 1 || cb.X == WorldBlock.Width - 2 || cb.Y == WorldBlock.Height - 2).ToList();
                SecondLevelBorderBlocks = SecondLevelBorderBlocks.Where(slbb => !BorderBlocks.Contains(slbb)).ToList();
                foreach(WorldBlock block in BorderBlocks)
                {
                    block.Status.WaterStatus = WorldBlockWaterStatus.Water;                    
                }
                foreach(WorldBlock block in SecondLevelBorderBlocks)
                {
                    block.Status.WaterStatus = WorldBlockWaterStatus.Coastline;
                }
            }

            int LAYERS_OF_OCEAN=1;
            for(int i=0;i<LAYERS_OF_OCEAN;i++)
            {
                foreach (Continent continent in Continents)
                {
                    List<WorldBlock> coastlineBlocks = continent.ContinentArea.Blocks().Where(wb => wb.Status.WaterStatus == WorldBlockWaterStatus.Coastline).ToList();
                    foreach(WorldBlock block in coastlineBlocks)
                    {
                        block.Status.WaterStatus = WorldBlockWaterStatus.Water;
                        WorldTasks.FixCoastlineForWaterBlock(block);
                    }
                }
            }
            int totalArea = (from continent in Continents select continent.ContinentArea.Area()).Sum();
            int numRegions = (int)Rnd.Normal(WorldRules.RegionsMean, WorldRules.RegionsStdDev);
            numRegions = Math.Min(numRegions, WorldRules.RegionsMax);
            numRegions = Math.Max(numRegions, WorldRules.RegionsMin);
            foreach (Continent continent in Continents)
            {
                int regionCount = (int)(((double)(continent.ContinentArea.Area())) / ((double)(totalArea)) * numRegions);
                //all continents must have at least one region
                if (regionCount == 0)
                {
                    regionCount = 1;
                }
                continent.InitializeContinent(regionCount);
            }

        }

        private void NewGenerateOceans(int NumOceans)
        {
            List<WorldBlock> WaterBlocks = WorldBlock.ChildBlocksAsList().Where(wb => wb.Status.WaterStatus == WorldBlockWaterStatus.Water).ToList();
            double TotalWaterSize = WaterBlocks.Count;
            while(WaterBlocks.Count > 0)
            {
                //Find contiguous water range
                WorldBlock FirstBlock = ListR<WorldBlock>.RandomFromList(WaterBlocks, Rnd);
                HashSet<WorldBlock> ContiguousWaterBlocksHashSet = new HashSet<WorldBlock>();
                ExpandContiguousWaterArea(ContiguousWaterBlocksHashSet,FirstBlock,WaterBlocks);
                List<WorldBlock> ContiguousWaterBlocks = ContiguousWaterBlocksHashSet.ToList();                

                //Figure out number of oceans for this contiguous range
                double CurrentWaterSize = ContiguousWaterBlocks.Count;
                double CurrentWaterPercent = CurrentWaterSize / TotalWaterSize;
                int CurrentOceans = (int)(Math.Round(NumOceans * CurrentWaterPercent,MidpointRounding.AwayFromZero));
                if (CurrentOceans > ContiguousWaterBlocks.Count) CurrentOceans = ContiguousWaterBlocks.Count;

                //Create Ocean Starting Points
                Ocean[] Oceans = new Ocean[CurrentOceans];
                List<WorldBlock> RemainingBlocks = ContiguousWaterBlocks.ToList();
                for(int i=0; i<CurrentOceans; i++)
                {
                    WorldBlock StartingBlock = ListR<WorldBlock>.RandomFromList(RemainingBlocks,Rnd);
                    WorldBlockArea OceanArea = new WorldBlockArea(new List<WorldBlock> { StartingBlock }, null);
                    Oceans[i] = new Ocean(this, OceanArea);
                    RemainingBlocks.Remove(StartingBlock);
                }
                
                //Expand Oceans
                while(RemainingBlocks.Count > 0)
                {
                    WorldBlock nextBlock = ListR<WorldBlock>.RandomFromList(RemainingBlocks.Where(b => b.SurroundingBlocksAsList().Any(sb => sb.Status.Ocean != null)).ToList(), Rnd);
                    Ocean newOcean = ListR<WorldBlock>.RandomFromList(nextBlock.SurroundingBlocksAsList().Where(sb => sb.Status.Ocean != null).ToList(), Rnd).Status.Ocean;
                    newOcean.AddBlock(nextBlock);
                    RemainingBlocks.Remove(nextBlock);
                }
            }
        }

        private void ExpandContiguousWaterArea(HashSet<WorldBlock> Contiguous, WorldBlock LatestBlock, List<WorldBlock> RemainingWorldBlocks)
        {
            if(LatestBlock == null || LatestBlock.Status.WaterStatus != WorldBlockWaterStatus.Water || Contiguous.Contains(LatestBlock))
            {
                return;
            }
            else
            {
                Contiguous.Add(LatestBlock);
                RemainingWorldBlocks.Remove(LatestBlock);
                List<WorldBlock> NeighboringBlocks = LatestBlock.SurroundingBlocksAsList();
                foreach(WorldBlock Neighbor in NeighboringBlocks)
                {
                    ExpandContiguousWaterArea(Contiguous, Neighbor,RemainingWorldBlocks);
                }
            }
        }

        private Rnd Rnd
        {
            get
            {
                return SimEngine.Rnd;
            }
        }

        private WorldRules WorldRules
        {
            get
            {
                return SimEngine.SimData.WorldRules;
            }
        }

    }

    public struct WorldRules
    {
        public int ContinentsMin;
        public int ContinentsMax;
        public double WorldLengthMean;
        public double WorldLengthStdDev;
        public int WorldLengthMin;
        public int WorldLengthMax;
        public double WorldHeightLengthRatio;
        public double SplitBlockMinimumPercentage;
        public double RegionsMean;
        public double RegionsStdDev;
        public int RegionsMin;
        public int RegionsMax;
        public int ZoomScaleLarge;
        public int ZoomScaleSmall;
        public double CoastlineBorderMean;
        public double CoastlineBorderStdDev;
        public double CoastlineBorderMin;
        public double CoastlineBorderMax;
        public int SeasPerContinentMin;
        public int SeasPerContinentMax;
        public double ContinentPerSeaMin;
        public double ContinentPerSeaMax;
        public double ContinentInSeasTotalMin;
        public double ContinentInSeasTotalMax;
        public double MediterraneanSeaProbability;
        public double ContinentCoastErosionMin;
        public double ContinentCoastErosionMax;
    }

    public enum Directions
    {
        North, East, South, West
    }

}
