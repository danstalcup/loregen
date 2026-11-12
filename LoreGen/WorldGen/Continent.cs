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
        public ILanguage RootLanguage;
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
            Name = RootLanguage.GetName(NameKind.Continent);
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
            foreach (WorldBlock wb in ContinentArea.Blocks().Where(cb => cb.Status.WaterStatus == WorldBlockWaterStatus.Coastline))
            {
                WorldTasks.ApplyCoastlineToBlock(wb, 10);
                //ErodeCoastlineBlock(wb);
            }
            foreach (WorldBlock wb in ContinentArea.Blocks().Where(cb => cb.Status.WaterStatus == WorldBlockWaterStatus.Coastline))
            {
                //WorldTasks.ApplyCoastlineToBlock(wb, 10);
                ErodeCoastlineBlock(wb);
            }
            NewNewGenerateRegions(NumRegions);
        }

        public void AddBlock(WorldBlock WorldBlock)
        {
            ContinentArea.Add(WorldBlock);
            WorldBlock.Status.Continent = this;
        }


        public Continent(World World)
        {
            this.World = World;
            World.Continents.Add(this);
            RootLanguage = SimEngine.Language.GetRandomLanguage();
            Name = RootLanguage.GetName(NameKind.Continent);
            WorldEntityType = WorldEntityType.Continent;
            Regions = new List<Region>();
            Seas = new List<Sea>();            
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
                    List<WorldBlock> nextSeaBlockPossibilities = nextEdgeBlock.SurroundingBlocksAsList().Where(sb => sb.Status.Continent == this && sb.Status.WaterStatus != WorldBlockWaterStatus.Water).ToList();
                    
                    if (nextSeaBlockPossibilities.Count == 0) continue;

                    WorldBlock nextSeaBlock = ListR<WorldBlock>.RandomFromList(nextSeaBlockPossibilities,Rnd);
                    nextSeaBlock.Status.WaterStatus = WorldBlockWaterStatus.Water;
                    seas[i].SeaBlock.Blocks().Add(nextSeaBlock);
                    nextSeaBlock.Status.Sea = seas[i];
                    WorldTasks.FixCoastlineForWaterBlock(nextSeaBlock);
                    
                    
                    
                }
            }


        }



        private void NewNewGenerateRegions(int NumRegions)
        {
            List<WorldBlock> AllContinentBlocks = ContinentArea.Blocks().ToList();
            List<WorldBlock> StartingBlockCandidates = ContinentArea.Blocks().Where(cb => cb.Status.WaterStatus == WorldBlockWaterStatus.Land || cb.Status.WaterStatus == WorldBlockWaterStatus.Coastline).ToList();
            if (StartingBlockCandidates.Count < NumRegions)
            {
                //This is kind of hacky
                if (StartingBlockCandidates.Count == 0)
                {
                    StartingBlockCandidates = new List<WorldBlock> { ContinentArea.Blocks().First() };
                }
                else
                {
                    NumRegions = StartingBlockCandidates.Count;
                }
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
                WorldBlock NextBlock = ListR<WorldBlock>.RandomFromList(AllContinentBlocks.Where(cb => cb.Status.Region == null && cb.SurroundingBlocksAsList().Where(sb => sb.Status.Region != null && sb.Status.Continent == this).Count() > 0).ToList(),Rnd);
                Region NextRegion = ListR<Region>.RandomFromList(NextBlock.SurroundingBlocksAsList().Where(sb => sb.Status.Region != null && sb.Status.Continent == this).Select(sb => sb.Status.Region).ToList(), Rnd);
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
        public WorldBlockArea OceanArea;

        /// <summary>
        /// The ocean as a geographic feature (with its bays and straits as parts), if the world's generator finds water
        /// features; otherwise null.
        /// </summary>
        public Features.GeographicFeature Feature;
        
        public Ocean(World World, WorldBlockArea OceanArea)
        {
            this.World = World;
            this.OceanArea = OceanArea;
            this.OceanArea.ParentBlock = World.WorldBlock;
            OceanArea.Entity = this;

            foreach(WorldBlock block in OceanArea.Blocks())
            {
                block.Status.Ocean = this;
            }
            WorldEntityType = WorldGen.WorldEntityType.Ocean;
            World.Oceans.Add(this);
            Name = World.Language.GetName(new NameContext(NameKind.Ocean)
            {
                Direction = NameHints.DirectionWithinWorld(OceanArea.Blocks(), World)
            });
        }                

        public void AddBlock(WorldBlock Block)
        {
            OceanArea.Add(Block);
            Block.Status.Ocean = this;
        }
    }
}
