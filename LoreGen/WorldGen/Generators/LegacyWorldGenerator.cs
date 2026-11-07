using System;
using System.Collections.Generic;
using System.Linq;

using LoreGen.Randomizer;
using LoreGen.Simulation;

namespace LoreGen.WorldGen.Generators
{
    /// <summary>
    /// The original world generator. Continents are grown from random seed blocks across a partitioned grid, the
    /// edge of each continent is flooded to form oceans, seas are carved inland, coasts are eroded, and each
    /// continent is split into regions with latitude-based biomes.
    /// </summary>
    public class LegacyWorldGenerator : IWorldGenerator
    {
        /// <summary>
        /// Id of this generator.
        /// </summary>
        public const string GeneratorId = "legacy";

        public string Id { get { return GeneratorId; } }

        public string DisplayName { get { return "Classic"; } }

        public string Description
        {
            get { return "The original generator: continents are carved from a randomly partitioned grid, then eroded and divided into regions."; }
        }

        public string Version { get { return "1"; } }

        public World Generate(SimEngine engine)
        {
            if (engine.Configuration.GeneratorSettings.Count > 0)
                throw new ArgumentException("The '" + GeneratorId + "' generator has no settings, but these were given: "
                    + string.Join(", ", engine.Configuration.GeneratorSettings.Keys) + ".");

            Run run = new Run(new World(engine));
            run.GenerateWorldBlock();
            run.NewGenerateContinents();
            run.NewGenerateOceans(run.World.Continents.Count);
            return run.World;
        }

        // Working state for one generation, so the generator itself holds none.
        private class Run
        {
            public readonly World World;

            public Run(World world)
            {
                World = world;
            }

            private SimEngine SimEngine { get { return World.SimEngine; } }
            private Rnd Rnd { get { return SimEngine.Rnd; } }
            private WorldRules Rules { get { return SimEngine.SimData.WorldRules; } }
            private WorldRules WorldRules { get { return SimEngine.SimData.WorldRules; } }
            private List<Continent> Continents { get { return World.Continents; } }
            private WorldBlock WorldBlock
            {
                get { return World.WorldBlock; }
                set { World.WorldBlock = value; }
            }

            public void GenerateWorldBlock()
            {
                WorldGridBuilder.InitializeWorldBlock(World);
            }

            public void NewGenerateContinents()
            {
                //Figure out number of continents
                int numContinents = Rnd.Unweighted(WorldRules.ContinentsMin, WorldRules.ContinentsMax);
                List<WorldBlock> UnallocatedBlocks = WorldBlock.ChildBlocksAsList();            

                for(int i=0;i<numContinents;i++)
                {
                    WorldBlock continentStartingBlock = ListR<WorldBlock>.RandomFromList(UnallocatedBlocks, Rnd);
                    UnallocatedBlocks.Remove(continentStartingBlock);
                    Continent.GenerateContinentWithAreaFromStartingBlock(World, continentStartingBlock);
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

            public void NewGenerateOceans(int NumOceans)
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
                    if (CurrentOceans < 1) CurrentOceans = 1; // every body of water needs at least one ocean to grow from
                    if (CurrentOceans > ContiguousWaterBlocks.Count) CurrentOceans = ContiguousWaterBlocks.Count;

                    //Create Ocean Starting Points
                    Ocean[] Oceans = new Ocean[CurrentOceans];
                    List<WorldBlock> RemainingBlocks = ContiguousWaterBlocks.ToList();
                    for(int i=0; i<CurrentOceans; i++)
                    {
                        WorldBlock StartingBlock = ListR<WorldBlock>.RandomFromList(RemainingBlocks,Rnd);
                        WorldBlockArea OceanArea = new WorldBlockArea(new List<WorldBlock> { StartingBlock }, null);
                        Oceans[i] = new Ocean(World, OceanArea);
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
                // Iterative (not recursive) so large worlds can't overflow the stack
                Stack<WorldBlock> pending = new Stack<WorldBlock>();
                pending.Push(LatestBlock);
                while (pending.Count > 0)
                {
                    WorldBlock block = pending.Pop();
                    if (block == null || block.Status.WaterStatus != WorldBlockWaterStatus.Water || Contiguous.Contains(block)) continue;
                    Contiguous.Add(block);
                    RemainingWorldBlocks.Remove(block);
                    foreach (WorldBlock Neighbor in block.SurroundingBlocksAsList()) pending.Push(Neighbor);
                }
            }
        }
    }
}
