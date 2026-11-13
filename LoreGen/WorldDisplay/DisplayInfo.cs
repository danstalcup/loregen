using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LoreGen.Drawing;

using LoreGen.WorldGen;
using LoreGen.Randomizer;
using LoreGen.Terrains;

namespace LoreGen.WorldDisplay
{
    public class WorldEntityDisplayInfo
    {
        public WorldEntity Entity;
        public Color DisplayColor;

        public static int ColorCount = 0;

        public static WorldEntityDisplayInfo GenerateWithRandomColor(WorldEntity Entity)
        {
            Rnd rnd = Entity.World.SimEngine.Rnd;
            WorldEntityDisplayInfo displayInfo = new WorldEntityDisplayInfo
            {
                Entity = Entity,
                DisplayColor = Color.FromArgb(rnd.Unweighted(256), rnd.Unweighted(256), rnd.Unweighted(256))
                //DisplayColor = GetNextColor()
            };
            return displayInfo;
        }

        public static Color GetNextColor()
        {
            ColorCount++;
            Color[] Colors = ColorList();
            return Colors[ColorCount % Colors.Length];
        }

        private static Color[] ColorList()
        {
            return new Color[]{
                Color.Black,
                Color.Orange,
                Color.Green,
                Color.DarkRed,
                Color.Yellow,
                Color.Purple,
                Color.Red,
                Color.DarkGreen,
                Color.White,
                Color.SpringGreen,
                Color.OliveDrab,
                Color.Tan
            };
        }
    }

    public class ClimateTypeDisplayInfo
    {
        public ClimateTypeDisplayInfo(ClimateType ClimateType, string ColorName)
        {
            this.ClimateType = ClimateType;
            this.Color = Color.FromName(ColorName);
        }
        public ClimateType ClimateType;
        public Color Color;
    }

    public class TerrainDisplayInfo
    {

        public TerrainDisplayInfo(Terrain Terrain)
        {
            this.Terrain = Terrain;
            this.TileX = Terrain.TerrainType.ID;
            this.TileY = Terrain.Climate.ID;
            
            Color colorA = Terrain.Climate.PrimaryClimateType.DisplayInfo.Color;
            Color colorB = Terrain.Climate.SecondaryClimateType.DisplayInfo.Color;
            
            int r = (int)((double)(colorA.R) * .75 + (double)(colorB.R) * .25);
            int g = (int)((double)(colorA.G) * .75 + (double)(colorB.G) * .25);
            int b = (int)((double)(colorA.B) * .75 + (double)(colorB.B) * .25);

            // Terrain types with a colour of their own (a beach, lava) show mostly that, tinted by the climate
            if (Terrain.TerrainType.Color.HasValue)
            {
                Color own = Terrain.TerrainType.Color.Value;
                r = (int)(own.R * 0.75 + r * 0.25);
                g = (int)(own.G * 0.75 + g * 0.25);
                b = (int)(own.B * 0.75 + b * 0.25);
            }
            this.DisplayColor = Color.FromArgb(r, g, b);
        }

        public Terrain Terrain;
        public Color DisplayColor;        
        public int TileX;
        public int TileY;        
    }


}
