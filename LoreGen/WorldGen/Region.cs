using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LoreGen.WorldDisplay;
using LoreGen.Terrains;
using LoreGen.Lang;
using LoreGen.Randomizer;

namespace LoreGen.WorldGen
{
    /// <summary>
    /// A portion of a continent with distinct features.
    /// </summary>
    public class Region : WorldEntity
    {
        /// <summary>
        /// Continent this region resides within
        /// </summary>
        public Continent Continent;
        /// <summary>
        /// Block representing this region
        /// </summary>
        //public WorldBlockRectangle RegionBlock;

        public WorldBlockArea RegionArea;

        public Biome Biome;

        public Language Language; // Add multiple languages per region

        /*public Region(Continent Continent, WorldBlockRectangle RegionBlock)
        {
            this.World = Continent.World;
            this.RegionBlock = RegionBlock;
            this.RegionBlock.Entity = this;
            this.WorldEntityType = WorldEntityType.Region;
            this.Continent = Continent;
            this.Language = Continent.RootLanguage.Skew();
            this.Name = this.Language.GetName();
            Continent.Regions.Add(this);
            SelectBiome();
            foreach (WorldBlock wb in RegionBlock.Blocks())
            {
                wb.Status.Region = this;
                if(wb.Status.WaterStatus != WorldBlockWaterStatus.Water)
                    wb.Status.Terrain = this.Biome.TerrainPattern.Get();
                foreach (WorldBlock child in wb.ChildBlocksAsList(true))
                {
                    child.Status.Region = this;
                    if (child.Status.WaterStatus != WorldBlockWaterStatus.Water)
                        child.Status.Terrain = this.Biome.TerrainPattern.Get();
                }
            }
        }*/

        public Region(Continent Continent, WorldBlockArea RegionArea)
        {
            this.World = Continent.World;
            this.RegionArea = RegionArea;
            this.RegionArea.Entity = this;
            this.WorldEntityType = WorldEntityType.Region;
            this.Continent = Continent;
            this.Language = Continent.RootLanguage.Skew();
            this.Name = this.Language.GetName();
            Continent.Regions.Add(this);
            SelectBiome();
            foreach (WorldBlock wb in RegionArea.Blocks())
            {
                wb.Status.Region = this;
                if (wb.Status.WaterStatus != WorldBlockWaterStatus.Water)
                    wb.Status.Terrain = this.Biome.TerrainPattern.Get();
                foreach (WorldBlock child in wb.ChildBlocksAsList(true))
                {
                    child.Status.Region = this;
                    if (child.Status.WaterStatus != WorldBlockWaterStatus.Water)
                        child.Status.Terrain = this.Biome.TerrainPattern.Get();
                }
            }
        }

        public void AddBlock(WorldBlock WorldBlock)
        {
            RegionArea.BlocksList.Add(WorldBlock);
            WorldBlock.Status.Region = this;
            if (WorldBlock.Status.WaterStatus != WorldBlockWaterStatus.Water)
            {
                WorldBlock.Status.Terrain = this.Biome.TerrainPattern.Get();
            }
            foreach(WorldBlock child in WorldBlock.ChildBlocksAsList(true))
            {
                child.Status.Region = this;
                if (child.Status.WaterStatus != WorldBlockWaterStatus.Water)
                    child.Status.Terrain = this.Biome.TerrainPattern.Get();
            }
        }

        /// <summary>
        /// Generates a region with the given rectangle in the given continent
        /// </summary>
        /// <param name="Continent">Continent that contains this region</param>
        /// <param name="RegionBlock">Area representing the region</param>
        /// <returns>Generated region</returns>
        /*public static Region GenerateRegion(Continent Continent, WorldBlockRectangle RegionBlock)
        {
            Region reg = new Region(Continent, RegionBlock);            
            
            reg.DisplayInfo = WorldEntityDisplayInfo.GenerateWithRandomColor(reg);                                    

            return reg;
        }*/

        public static Region GenerateRegion(Continent Continent, WorldBlockArea RegionArea)
        {
            Region reg = new Region(Continent, RegionArea);

            reg.DisplayInfo = WorldEntityDisplayInfo.GenerateWithRandomColor(reg);

            return reg;
        } 
       
        public List<Region> NeighboringRegions()
        {
            List<WorldBlock> allNeighboringBlocksWithRegions = RegionArea.Blocks().Where(wb => wb.Status.WaterStatus != WorldBlockWaterStatus.Water).SelectMany(block => block.SurroundingBlocksAsList()).Where(wb => wb.Status.Region != null).ToList();
            List<WorldBlock> blocksWithSubBlocks = allNeighboringBlocksWithRegions.Where(wb => wb.ChildBlocks != null).ToList();
            allNeighboringBlocksWithRegions.RemoveAll(wb => blocksWithSubBlocks.Contains(wb));
            allNeighboringBlocksWithRegions.AddRange(blocksWithSubBlocks.SelectMany(wb => wb.ChildBlocksAsList().Where(cb => cb.Status.WaterStatus != WorldBlockWaterStatus.Water)));
            return allNeighboringBlocksWithRegions.Select(block => block.Status.Region).Distinct().Where(r => r != this).ToList();
        }

        private void SelectBiome()
        {
            double medianY = RegionArea.BlocksList[0].TrueY;// ((double)this.RegionBlock.TrueY + (double)(0.5 * this.RegionBlock.TrueHeight));
            double globalHeight = (double)Continent.World.WorldBlock.TrueHeight;
            double ratio = medianY / globalHeight;
            double adjRatio = Math.Abs(ratio - 0.5) * 1.25;//!!!
            int tempRegion = 4 - (int)(adjRatio * 10);
            tempRegion = Math.Max(0, Math.Min(4, tempRegion));
            this.Biome = LoreGen.Randomizer.ListR<Biome>.RandomFromList(Continent.World.SimEngine.SimData.Biomes.Where(b => b.Temperature == (BiomeTemperature)tempRegion), Continent.World.SimEngine.Rnd);
        }

        public static void SmoothEdgesOfRegion(Region Region)
        {
            List<WorldBlock> BlocksToSmooth = Region.RegionArea.BlocksList.Where(wb => wb.SurroundingBlocksAsList().Where(sb => sb.Status.Region != null && sb.Status.Region != Region && !sb.Status.RegionBoundariesHaveBeenSmoothed).Count()>0).ToList();
            foreach(WorldBlock BlockToSmooth in BlocksToSmooth)
            {
                if (BlockToSmooth.SurroundingBlocksAsList().Where(sb => sb.Status.RegionBoundariesHaveBeenSmoothed).Count()>0) continue;
                SmoothEdgesOfRegionBlock(BlockToSmooth);
            }
        }

        private static void SmoothEdgesOfRegionBlock(WorldBlock WorldBlock)
        {
            Region region = WorldBlock.Status.Region;
            if(WorldBlock.ChildBlocks == null)
            {
                WorldBlock.InitializeChildBlocks();
            }

            WorldBlock[] neighbors = WorldBlock.SurroundingBlocks();
            Region[] neighborRegions = neighbors.Select(n => n==null ? region : n.Status.Region ?? region).ToArray();
            bool[] isDifferentRegion = neighborRegions.Select(nr => nr != region).ToArray();

            EdgePattern edgePattern = ListR<EdgePattern>.RandomFromList(EdgePattern.GetValidEdgePatterns(WorldBlock.Status.Continent.World.SimEngine.SimData.EdgePatterns, isDifferentRegion[0], isDifferentRegion[1], isDifferentRegion[3], isDifferentRegion[2],true), WorldBlock.Status.Continent.World.SimEngine.Rnd);

            for (int i = 0; i < 10;i++ )
            {
                for(int j=0; j<10; j++ )
                {
                    if(edgePattern.Pattern[i,j] == EdgeStatus.Off)
                    {
                        WorldBlock.ChildBlocks[i, j].Status.Region = neighborRegions[WorldTasks.ClosestEdge(isDifferentRegion, i, j)];
                    }                    
                }
            }

            WorldBlock.Status.RegionBoundariesHaveBeenSmoothed = true;            
        }
    }
    /// <summary>
    /// A water area on a continent. (Analagous to Region.)
    /// </summary>
    public class Sea : WorldEntity
    {
        /// <summary>
        /// Continent that the sea is on.
        /// </summary>
        public Continent Continent;
        /// <summary>
        /// Area representing the block.
        /// </summary>
        public string Name;
        public WorldBlockArea SeaBlock;
        /// <summary>
        /// Constructor for the sea. Sea is added to the Continent's collection of Seas.
        /// </summary>
        /// <param name="Continent">Continent the sea resides in</param>
        /// <param name="SeaBlocks">Collection of blocks representing the sea</param>
        public Sea(Continent Continent, List<WorldBlock> SeaBlocks)
        {
            SeaBlock = new WorldBlockArea(SeaBlocks, this);            
            this.Continent = Continent;            
            foreach (WorldBlock block in SeaBlocks)
            {
                block.Status.Sea = this;
            }
            Name = Continent.RootLanguage.GetName();
            this.WorldEntityType = WorldGen.WorldEntityType.Sea;
        }
    }




}
