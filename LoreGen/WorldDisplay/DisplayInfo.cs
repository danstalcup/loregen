using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

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
                DisplayColor = Color.FromRgb((byte)rnd.Unweighted(256), (byte)rnd.Unweighted(256), (byte)rnd.Unweighted(256))
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
        /// <summary>
        /// Named colors used by ClimateTypes.csv. SixLabors.ImageSharp has no name-based lookup like
        /// System.Drawing.Color.FromName, so the bounded set of names actually in the data is mapped by hand.
        /// </summary>
        private static readonly Dictionary<string, Color> NamedColors = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase)
        {
            { "White", Color.White },
            { "GreenYellow", Color.GreenYellow },
            { "DarkGreen", Color.DarkGreen },
            { "SaddleBrown", Color.SaddleBrown },
            { "Tan", Color.Tan },
            { "Gold", Color.Gold },
            { "Red", Color.Red },
        };

        public ClimateTypeDisplayInfo(ClimateType ClimateType, string ColorName)
        {
            this.ClimateType = ClimateType;
            if (!NamedColors.TryGetValue(ColorName, out this.Color))
            {
                throw new ArgumentException($"Unrecognized color name '{ColorName}' in climate type data. Add it to ClimateTypeDisplayInfo.NamedColors.");
            }
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
            
            Rgba32 colorA = Terrain.Climate.PrimaryClimateType.DisplayInfo.Color.ToPixel<Rgba32>();
            Rgba32 colorB = Terrain.Climate.SecondaryClimateType.DisplayInfo.Color.ToPixel<Rgba32>();

            byte r = (byte)((double)(colorA.R) * .75 + (double)(colorB.R) * .25);
            byte g = (byte)((double)(colorA.G) * .75 + (double)(colorB.G) * .25);
            byte b = (byte)((double)(colorA.B) * .75 + (double)(colorB.B) * .25);

            this.DisplayColor = Color.FromRgb(r, g, b);
        }

        public Terrain Terrain;
        public Color DisplayColor;        
        public int TileX;
        public int TileY;        
    }


}
