using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LoreGen.Randomizer;
using LoreGen.Simulation;
using LoreGen.Lang;
using LoreGen.WorldDisplay;

namespace LoreGen.WorldGen
{
    /// <summary>
    /// A simulated continent
    /// </summary>
    public class Continent : WorldEntity
    {
        /// <summary>
        /// Root language of this continent, used to generate languages in its.
        /// </summary>
        public Language RootLanguage;
        /// <summary>
        /// Block representing this region
        /// </summary>
        public WorldBlockRectangle ContinentBlock;

        public WorldBlockArea ContinentArea;

        /// <summary>
        /// The regions in this continent.
        /// </summary>
        public List<Region> Regions;
        /// <summary>
        /// The seas in this continent.
        /// </summary>
        public List<Sea> Seas;

        public WorldEntityDisplayInfo DisplayInfo;

        private SimEngine SimEngine
        {
            get
            {
                return World.SimEngine;
            }
        }
        private Rnd Rnd
        {
            get
            {
                return SimEngine.Rnd;
            }
        }
        private WorldRules Rules
        {
            get
            {
                return World.SimEngine.SimData.WorldRules;
            }
        }
        /// <summary>
        /// Creates a continent (without generating any further continent, such as regions or seas).
        /// </summary>
        /// <param name="World">World for this continent</param>
        /// <param name="ContinentBlock">Block for this continent</param>
        public Continent(World World, WorldBlockRectangle ContinentBlock)
        {
            this.World = World;
            World.Continents.Add(this);
            RootLanguage = SimEngine.Language.GetRandomLanguage();
            Name = RootLanguage.GetName(1);
            this.ContinentBlock = ContinentBlock;
            WorldEntityType = WorldEntityType.Continent;
            Regions = new List<Region>();
            Seas = new List<Sea>();
            ContinentBlock.Entity = this;            

            foreach (WorldBlock block in ContinentBlock.BlocksAsGrid())
            {
                block.Status.Continent = this;                
            }
        }

        /// <summary>
        /// Generates a continent
        /// </summary>
        /// <param name="World">World in which continent resides</param>
        /// <param name="ContinentBlock">Block representing this continent</param>
        /// <param name="NumRegions">Number of regions in this continent</param>
        /// <returns>Generated continent</returns>
        public static Continent GenerateContinentWithAreaFromStartingBlock(World World, WorldBlock StartingBlock)//, int NumRegions)
        {
            Continent cont = new Continent(World);

            cont.DisplayInfo = WorldEntityDisplayInfo.GenerateWithRandomColor(cont);
            cont.ContinentArea = new WorldBlockArea(new List<WorldBlock> { StartingBlock }, cont);
            StartingBlock.Status.Continent = cont;
                

            //generate seas
            //cont.GenerateSeas();

            /*foreach (WorldBlock wb in cont.ContinentArea.Blocks().Where(cb => cb.Status.WaterStatus == WorldBlockWaterStatus.Coastline))
            {
                WorldTasks.ApplyCoastlineToBlock(wb, 10);                
                //cont.ErodeCoastlineBlock(wb);
            }*/

            //cont.ErodeCoastlineAllBlocks();
            //generate regions
            //cont.NewNewGenerateRegions(NumRegions);                        

            return cont;

        }

        public void InitializeContinent(int NumRegions)
        {
            //generate seas
            GenerateSeas();
            
            NewErodeCoasts();
            NewNewGenerateRegions(NumRegions);
            foreach (WorldBlock wb in ContinentArea.Blocks().Where(cb => cb.Status.WaterStatus == WorldBlockWaterStatus.Coastline))
            {
                WorldTasks.ApplyCoastlineToBlock(wb, 10);
                ErodeCoastlineBlock(wb);
            }        
        }

        public void AddBlock(WorldBlock WorldBlock)
        {
            ContinentArea.BlocksList.Add(WorldBlock);
            WorldBlock.Status.Continent = this;
        }


        public Continent(World World)
        {
            this.World = World;
            World.Continents.Add(this);
            RootLanguage = SimEngine.Language.GetRandomLanguage();
            Name = RootLanguage.GetName(1);            
            WorldEntityType = WorldEntityType.Continent;
            Regions = new List<Region>();
            Seas = new List<Sea>();            
        }

        /// <summary>
        /// Generates a continent
        /// </summary>
        /// <param name="World">World in which continent resides</param>
        /// <param name="ContinentBlock">Block representing this continent</param>
        /// <param name="NumRegions">Number of regions in this continent</param>
        /// <returns>Generated continent</returns>
        public static Continent GenerateContinent(World World, WorldBlockRectangle ContinentBlock, int NumRegions)
        {
            Continent cont = new Continent(World, ContinentBlock);

            cont.DisplayInfo = WorldEntityDisplayInfo.GenerateWithRandomColor(cont);

            cont.ErodeCoasts();

            //generate seas
            //cont.GenerateSeas();

            foreach (WorldBlock wb in ContinentBlock.Blocks().Where(cb => cb.Status.WaterStatus == WorldBlockWaterStatus.Coastline))
            {
                WorldTasks.ApplyCoastlineToBlock(wb, 10);
                //cont.ErodeCoastlineBlock(wb);
            }

            cont.ErodeCoastlineAllBlocks();
            //generate regions
            cont.NewNewGenerateRegions(NumRegions);

            return cont;

        }

        private void ErodeCoastlineAllBlocks()
        {
            //int CIRC_FACT = 10;
            double erosionPercent = Rnd.Unweighted(Rules.ContinentCoastErosionMin, Rules.ContinentCoastErosionMax);
            int continentCircumference = ContinentBlock.Blocks().SelectMany(b => b.ChildBlocksAsList()).Where(cb => cb != null && cb.Status.WaterStatus == WorldBlockWaterStatus.Coastline).Count();
            int estimateCoastlineArea = ContinentBlock.Blocks().SelectMany(b => b.ChildBlocksAsList()).Where(cb => cb != null && cb.Status.WaterStatus != WorldBlockWaterStatus.Water).Count();
            int erosionCount = (int)Math.Round(erosionPercent * estimateCoastlineArea, MidpointRounding.AwayFromZero);

            List<WorldBlock> erosionCandidates = ContinentBlock.Blocks().SelectMany(b => b.ChildBlocksAsList()).Where(cb => cb != null && cb.Status.WaterStatus == WorldBlockWaterStatus.Coastline).ToList();

            for (int i = 0; i < erosionCount; i++)
            {
                WorldBlock erosionBlock = ListR<WorldBlock>.RandomFromList(erosionCandidates, Rnd);//ListR<WorldBlock>.RandomFromList(ContinentBlock.Blocks().SelectMany(b => b.ChildBlocksAsList()).Where(cb => cb != null && cb.Status.WaterStatus == WorldBlockWaterStatus.Coastline).ToList(), Rnd);
                erosionBlock.Status.WaterStatus = WorldBlockWaterStatus.Water;
                erosionCandidates.Remove(erosionBlock);
                erosionCandidates.AddRange(WorldTasks.FixCoastlineForWaterBlock(erosionBlock));
            } 
        }


        private void ErodeCoasts()
        {
            double erosionPercent = Rnd.Unweighted(Rules.ContinentCoastErosionMin, Rules.ContinentCoastErosionMax);
            int erosionCount = (int)Math.Round(erosionPercent * ContinentBlock.Area(), MidpointRounding.AwayFromZero);
            for (int i = 0; i < erosionCount; i++)
            {
                WorldBlock erosionBlock = ListR<WorldBlock>.RandomFromList(ContinentBlock.Blocks().Where(wb => wb.Status.WaterStatus == WorldBlockWaterStatus.Coastline).ToList(), Rnd);
                erosionBlock.Status.WaterStatus = WorldBlockWaterStatus.Water;
                WorldTasks.FixCoastlineForWaterBlock(erosionBlock);
            } 
        }


        private void NewErodeCoasts()
        {
            double erosionPercent = Rnd.Unweighted(Rules.ContinentCoastErosionMin, Rules.ContinentCoastErosionMax) * 0.5;
            int erosionCount = (int)Math.Round(erosionPercent * ContinentArea.Area(), MidpointRounding.AwayFromZero);
            for (int i = 0; i < erosionCount; i++)
            {
                List<WorldBlock> unerodedCoastline = ContinentArea.Blocks().Where(wb => wb.Status.WaterStatus == WorldBlockWaterStatus.Coastline).ToList();
                if (unerodedCoastline.Count <= 1) return;
                WorldBlock erosionBlock = ListR<WorldBlock>.RandomFromList(unerodedCoastline, Rnd);
                erosionBlock.Status.WaterStatus = WorldBlockWaterStatus.Water;
                WorldTasks.FixCoastlineForWaterBlock(erosionBlock);
            }
        }
        

        private void ErodeCoastlineBlock(WorldBlock b)
        {
            double erosionPercent = Rnd.Unweighted(Rules.ContinentCoastErosionMin, Rules.ContinentCoastErosionMax);

            int erosionCount = (int)Math.Round(erosionPercent * b.ChildBlocksAsList().Where(cb => cb.Status.WaterStatus != WorldBlockWaterStatus.Water).Count(), MidpointRounding.AwayFromZero);
            for (int i = 0; i < erosionCount; i++)
            {
                WorldBlock erosionBlock = ListR<WorldBlock>.RandomFromList(b.ChildBlocksAsList().Where(wb => wb.Status.WaterStatus == WorldBlockWaterStatus.Coastline).ToList(),Rnd);
                erosionBlock.Status.WaterStatus = WorldBlockWaterStatus.Water;
                WorldTasks.FixCoastlineForWaterBlock(erosionBlock);
            }

        }


        private void GenerateSeas()
        {
            int numSeas = Rnd.Unweighted(Rules.SeasPerContinentMin, Rules.SeasPerContinentMax);

            int[] seaSizes = new int[numSeas];
            WorldBlock[] seaHeads = new WorldBlock[numSeas];
            Sea[] seas = new Sea[numSeas];

            double totalPercentOfContinentInSeas = 0.0;
            List<WorldBlock> ContBlocks = ContinentArea.Blocks();

            for (int i = 0; i < numSeas; i++)
            {

                if (Rnd.R01() < Rules.MediterraneanSeaProbability)
                {
                    List<WorldBlock> landBlocks = ContBlocks.Where(cb => cb.Status.WaterStatus == WorldBlockWaterStatus.Land).ToList();
                    if(landBlocks.Count == 0) continue; 
                    seaHeads[i] = ListR<WorldBlock>.RandomFromList(landBlocks,Rnd);
                }
                else
                {
                    List<WorldBlock> coastBlocks =ContBlocks.Where(cb => cb.Status.WaterStatus == WorldBlockWaterStatus.Coastline).ToList();
                    if(coastBlocks.Count == 0) continue;
                    seaHeads[i] = ListR<WorldBlock>.RandomFromList(coastBlocks,Rnd);
                }      
                double percentOfContinent = Rnd.Unweighted(Rules.ContinentPerSeaMin, Rules.ContinentPerSeaMax);
                totalPercentOfContinentInSeas += percentOfContinent;
                seaSizes[i] = (int)Math.Round(percentOfContinent * ContinentArea.Area(), MidpointRounding.AwayFromZero);
                seas[i] = new Sea(this,new WorldBlock[]{ seaHeads[i]}.ToList());
                seaHeads[i].Status.WaterStatus = WorldBlockWaterStatus.Water;
                WorldTasks.FixCoastlineForWaterBlock(seaHeads[i]);
            }

            //make sure percent of continent devoted to seas is appropriate
            if (totalPercentOfContinentInSeas > Rules.ContinentInSeasTotalMax)
            {
                double scalar = Rules.ContinentInSeasTotalMax / totalPercentOfContinentInSeas;
                for (int i = 0; i < numSeas; i++)
                {
                    seaSizes[i] = (int)Math.Round(seaSizes[i] * scalar);
                }
            }
            else if (totalPercentOfContinentInSeas < Rules.ContinentInSeasTotalMin)
            {
                double scalar = Rules.ContinentInSeasTotalMin / totalPercentOfContinentInSeas;
                for (int i = 0; i < numSeas; i++)
                {
                    seaSizes[i] = (int)Math.Round(seaSizes[i] * scalar);
                }
            }

            bool anySeasShouldExpand = true;

            while (anySeasShouldExpand)
            {
                anySeasShouldExpand = false;
                for (int i = 0; i < numSeas; i++)
                {
                    if (seaSizes[i] == 0) continue;
                    
                    if(seaSizes[i] > 1)
                    {
                        anySeasShouldExpand = true;
                    }
                    seaSizes[i]--;

                    List<WorldBlock> seaBlockEdge = seas[i].SeaBlock.Edge();

                    if (seaBlockEdge.Count == 0) throw new Exception("No edge to sea... this shouldn't be happening...");

                    WorldBlock nextEdgeBlock = ListR<WorldBlock>.RandomFromList(seaBlockEdge,Rnd);
                    //List<WorldBlock> nextSeaBlockPossibilities = nextEdgeBlock.SurroundingBlocks().Where(wb => wb.Status.WaterStatus == WorldBlockWaterStatus.Coastline).ToList();
                    List<WorldBlock> nextSeaBlockPossibilities = nextEdgeBlock.SurroundingBlocksAsList();
                    
                    if (nextSeaBlockPossibilities.Count == 0) continue;

                    WorldBlock nextSeaBlock = ListR<WorldBlock>.RandomFromList(nextSeaBlockPossibilities,Rnd);
                    nextSeaBlock.Status.WaterStatus = WorldBlockWaterStatus.Water;
                    seas[i].SeaBlock.Blocks().Add(nextSeaBlock);
                    nextSeaBlock.Status.Sea = seas[i];
                    WorldTasks.FixCoastlineForWaterBlock(nextSeaBlock);
                    
                    
                    
                }
            }


        }

        /*private void GenerateRegions(int NumRegions)
        {
            List<WorldBlockRectangle> RegionBlocks = new List<WorldBlockRectangle>();
            WorldBlockRectangle FirstRegion = new WorldBlockRectangle { ParentBlock = World.WorldBlock, Height = ContinentBlock.Height, Width = ContinentBlock.Width, X = ContinentBlock.X, Y = ContinentBlock.Y };
            RegionBlocks.Add(FirstRegion);

            for(int r=1;r<NumRegions;r++)
            {
                WorldBlockRectangle LargestRegion = RegionBlocks.OrderByDescending(wbr => wbr.Area()).First();
                if(LargestRegion.Width > LargestRegion.Height)
                {
                    int divider = (int)Rnd.Unweighted(Rules.SplitBlockMinimumPercentage * LargestRegion.Width, (1.0 - Rules.SplitBlockMinimumPercentage) * LargestRegion.Width);
                    if (divider == 0 || divider == LargestRegion.Width) break;
                    WorldBlockRectangle newRegion = new WorldBlockRectangle { Height = LargestRegion.Height, Width = divider, ParentBlock = LargestRegion.ParentBlock, X = LargestRegion.X, Y = LargestRegion.Y };
                    LargestRegion.Width -= divider;
                    LargestRegion.X += divider;
                    RegionBlocks.Add(newRegion);
                }
                else
                {
                    int divider = (int)Rnd.Unweighted(Rules.SplitBlockMinimumPercentage * LargestRegion.Height, (1.0 - Rules.SplitBlockMinimumPercentage) * LargestRegion.Height);
                    if (divider == 0 || divider == LargestRegion.Height) break;
                    WorldBlockRectangle newRegion = new WorldBlockRectangle { Width = LargestRegion.Width, Height = divider, ParentBlock = LargestRegion.ParentBlock, X = LargestRegion.X, Y = LargestRegion.Y };
                    LargestRegion.Height -= divider;
                    LargestRegion.Y += divider;
                    RegionBlocks.Add(newRegion);
                }
            }

            foreach( WorldBlockRectangle regionBlock in RegionBlocks)
            {
                Region.GenerateRegion(this, regionBlock);
            }

        }*/

        private void NewGenerateRegions(int NumRegions)
        {
            List<WorldBlock> StartingBlockCandidates = ContinentBlock.Blocks().Where(cb => cb.Status.WaterStatus == WorldBlockWaterStatus.Land || cb.Status.WaterStatus == WorldBlockWaterStatus.Coastline).ToList();            
            if(StartingBlockCandidates.Count < NumRegions)
            {
                NumRegions = StartingBlockCandidates.Count;
            }

            //List<WorldBlock> UnallocatedContinentBlocks = ContinentBlock.Blocks();
            //List<WorldBlock> AllocatedContinentBlocks = new List<WorldBlock>();
            //HashSet<WorldBlock> PossibleNextRegionBlocks = new HashSet<WorldBlock>();

            WorldBlockArea[] RegionAreas = new WorldBlockArea[NumRegions];
            WorldBlock[] StartingBlocks = new WorldBlock[NumRegions];
            Region[] GeneratedRegions = new Region[NumRegions];
            for (int i = 0; i < NumRegions; i++ )
            {
                //PossibleNextRegionBlocks.RemoveWhere(pnr => AllocatedContinentBlocks.Contains(pnr));
                StartingBlocks[i] = ListR<WorldBlock>.RandomFromList(StartingBlockCandidates, Rnd);
                StartingBlockCandidates.Remove(StartingBlocks[i]);
                //UnallocatedContinentBlocks.Remove(StartingBlocks[i]);
                //AllocatedContinentBlocks.Add(StartingBlocks[i]);
                /*foreach(WorldBlock wb in StartingBlocks[i].SurroundingBlocks())
                {
                    if(wb != null)// && !AllocatedContinentBlocks.Contains(wb))
                    {
                        PossibleNextRegionBlocks.Add(wb);
                    }
                }*/
                RegionAreas[i] = new WorldBlockArea(new List<WorldBlock> { StartingBlocks[i] }, null);
                GeneratedRegions[i] = Region.GenerateRegion(this, RegionAreas[i]);
            }

            while(ContinentBlock.Blocks().Where(cb => cb.Status.Region == null).Count() > 0)
            {
                //PossibleNextRegionBlocks.RemoveWhere(pnr => AllocatedContinentBlocks.Contains(pnr));
                WorldBlock NextAllocatedBlock = ListR<WorldBlock>.RandomFromList(ContinentBlock.Blocks().Where(cb => cb.Status.Region == null).ToList(), Rnd);
                List<WorldBlock> PossibleRegionConnectors = NextAllocatedBlock.SurroundingBlocks().Where(nb => nb != null && nb.Status.Region != null && nb.Status.WaterStatus != WorldBlockWaterStatus.Water).ToList(); //NextAllocatedBlock.SurroundingBlocks().Where(wb => wb != null && AllocatedContinentBlocks.Contains(wb) && wb.Status.WaterStatus != WorldBlockWaterStatus.Water).ToList();
                if (PossibleRegionConnectors.Count == 0)
                {
                    PossibleRegionConnectors = NextAllocatedBlock.SurroundingBlocks().Where(nb => nb != null && nb.Status.Region != null).ToList();
                }
                Region RegionConnector = ListR<WorldBlock>.RandomFromList(PossibleRegionConnectors,Rnd).Status.Region;
                //WorldBlockArea NewRegionArea = RegionAreas.Where(ra => ra.BlocksList.Contains(RegionConnector)).Single();
                //NewRegionArea.BlocksList.Add(NextAllocatedBlock);
                //PossibleNextRegionBlocks.Remove(NextAllocatedBlock);
                //UnallocatedContinentBlocks.Remove(NextAllocatedBlock);
                //AllocatedContinentBlocks.Add(NextAllocatedBlock);
                /*foreach (WorldBlock wb in NextAllocatedBlock.SurroundingBlocks())
                {
                    if (wb != null)// && !AllocatedContinentBlocks.Contains(wb))
                    {
                        PossibleNextRegionBlocks.Add(wb);
                    }
                }*/
                RegionConnector.AddBlock(NextAllocatedBlock);
            }

            /*for (int r = 1; r < NumRegions; r++)
            {
                WorldBlockRectangle LargestRegion = RegionAreas.OrderByDescending(wbr => wbr.Area()).First();
                if (LargestRegion.Width > LargestRegion.Height)
                {
                    int divider = (int)Rnd.Unweighted(Rules.SplitBlockMinimumPercentage * LargestRegion.Width, (1.0 - Rules.SplitBlockMinimumPercentage) * LargestRegion.Width);
                    if (divider == 0 || divider == LargestRegion.Width) break;
                    WorldBlockRectangle newRegion = new WorldBlockRectangle { Height = LargestRegion.Height, Width = divider, ParentBlock = LargestRegion.ParentBlock, X = LargestRegion.X, Y = LargestRegion.Y };
                    LargestRegion.Width -= divider;
                    LargestRegion.X += divider;
                    RegionAreas.Add(newRegion);
                }
                else
                {
                    int divider = (int)Rnd.Unweighted(Rules.SplitBlockMinimumPercentage * LargestRegion.Height, (1.0 - Rules.SplitBlockMinimumPercentage) * LargestRegion.Height);
                    if (divider == 0 || divider == LargestRegion.Height) break;
                    WorldBlockRectangle newRegion = new WorldBlockRectangle { Width = LargestRegion.Width, Height = divider, ParentBlock = LargestRegion.ParentBlock, X = LargestRegion.X, Y = LargestRegion.Y };
                    LargestRegion.Height -= divider;
                    LargestRegion.Y += divider;
                    RegionAreas.Add(newRegion);
                }
            }*/

            foreach (WorldBlockArea regionArea in RegionAreas)
            {
                Region.GenerateRegion(this, regionArea);
            }
        }

        private void NewNewGenerateRegions(int NumRegions)
        {
            List<WorldBlock> AllContinentBlocks = ContinentArea.Blocks().ToList();
            List<WorldBlock> StartingBlockCandidates = ContinentArea.Blocks().Where(cb => cb.Status.WaterStatus == WorldBlockWaterStatus.Land || cb.Status.WaterStatus == WorldBlockWaterStatus.Coastline).ToList();
            if (StartingBlockCandidates.Count < NumRegions)
            {
                NumRegions = StartingBlockCandidates.Count;
            }      

            WorldBlockArea[] RegionAreas = new WorldBlockArea[NumRegions];
            WorldBlock[] StartingBlocks = new WorldBlock[NumRegions];
            
            for (int i = 0; i < NumRegions; i++)
            {                
                StartingBlocks[i] = ListR<WorldBlock>.RandomFromList(StartingBlockCandidates, Rnd);
                StartingBlockCandidates.Remove(StartingBlocks[i]);                             
                Region.GenerateRegion(this, new WorldBlockArea(new List<WorldBlock> { StartingBlocks[i] }, null));
            }

            while(AllContinentBlocks.Where(cb => cb.Status.Region == null).Count() > 0)
            {
                WorldBlock NextBlock = ListR<WorldBlock>.RandomFromList(AllContinentBlocks.Where(cb => cb.Status.Region == null && cb.SurroundingBlocksAsList().Where(sb => sb.Status.Region != null).Count() > 0).ToList(),Rnd);
                Region NextRegion = ListR<Region>.RandomFromList(NextBlock.SurroundingBlocksAsList().Where(sb => sb.Status.Region != null).Select(sb => sb.Status.Region).ToList(), Rnd);
                NextRegion.AddBlock(NextBlock);
            }

            //Interesting Region Edges

            foreach(Region region in Regions)
            {
                Region.SmoothEdgesOfRegion(region);
            }
            
        }
    }
   

    public class Ocean : WorldEntity
    {        
        public WorldBlockRectangle OceanBlock;
        public World World;
        

        public Ocean(World World, WorldBlockRectangle OceanBlock)
        {
            this.World = World;
            this.OceanBlock = OceanBlock;
            this.OceanBlock.ParentBlock = World.WorldBlock;
            OceanBlock.Entity = this;

            foreach (WorldBlock block in OceanBlock.Blocks())
            {
                block.Status.Ocean = this;
                block.Status.WaterStatus = WorldBlockWaterStatus.Water;
            }
            
            World.Oceans.Add(this);
            WorldEntityType = WorldGen.WorldEntityType.Ocean;
            Name = World.SimEngine.Language.GetRandomLanguage().GetName();
        }
        

    }
}
