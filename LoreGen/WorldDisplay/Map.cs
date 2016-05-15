using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;

using LoreGen.WorldGen;

namespace LoreGen.WorldDisplay
{  
    public class Map
    {
        public static Color GetColor(WorldBlock Block, MapContext MapContext)
        {
            //
            //if (null != Block.Status.Sea) return Color.DarkBlue;

            if (Block.Status.WaterStatus == WorldBlockWaterStatus.Water) return Color.Blue;

            if(MapContext == MapContext.Continents)
            {                
                if (null != Block.Status.Continent)
                    return Block.Status.Continent.DisplayInfo.DisplayColor;
                //else
                //    MapContext = MapContext.General;

            }

            if(MapContext == MapContext.Regions)
            {
                if (null != Block.Status.Region)
                    return Block.Status.Region.DisplayInfo.DisplayColor;
                //else
                    //MapContext = MapContext.General;
            }

            if (MapContext == MapContext.General)
            {                
                if (Block.Status.WaterStatus == WorldBlockWaterStatus.Land) return Color.Green;
                if (Block.Status.WaterStatus == WorldBlockWaterStatus.Coastline) return Color.Yellow;
                if (Block.Status.WaterStatus == WorldBlockWaterStatus.Water) return Color.Blue;
            }

            if(MapContext == MapContext.Biomes)
            {
                if(null != Block.Status.Region)
                {
                    switch(Block.Status.Region.Biome.Temperature)
                    {
                        case Terrains.BiomeTemperature.Frigid:
                            return Color.DarkBlue;                            
                        case Terrains.BiomeTemperature.Cold:
                            return Color.LightBlue;                            
                        case Terrains.BiomeTemperature.Temperate:
                            return Color.Green;                            
                        case Terrains.BiomeTemperature.Warm:
                            return Color.Yellow;                            
                        case Terrains.BiomeTemperature.Sweltering:
                            return Color.Red;                            
                    }
                    
                }
            }

            if(MapContext == MapContext.Terrains)
            {
                if (null != Block.Status.Terrain)
                    return Block.Status.Terrain.DisplayInfo.DisplayColor;                
            }

            return Color.Black;
        }
    }
    public enum MapContext
    {
        General, Continents, Regions, Biomes, Terrains, TerrainTiles
    }
}
