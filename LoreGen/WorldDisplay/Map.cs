using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LoreGen.Drawing;

using LoreGen.WorldGen;

namespace LoreGen.WorldDisplay
{  
    public class Map
    {
        /// <summary>
        /// The feature to show for a block: the finest-scoped feature covering it (a sub-range rather than the range it is
        /// part of), the first added if several tie; or null if none covers it.
        /// </summary>
        public static Features.GeographicFeature FeatureToShow(WorldBlock Block)
        {
            var features = Block.World?.FeaturesAt(Block);
            if (features == null || features.Count == 0) return null;
            var best = features[0];
            foreach (var feature in features)
                if (feature.Scope > best.Scope) best = feature;
            return best;
        }

        /// <summary>
        /// Name of the elevation band a block falls in, or null if the block has no elevation.
        /// </summary>
        public static string ElevationBand(WorldBlock Block)
        {
            double? e = Block.Status.Elevation;
            if (!e.HasValue) return null;
            if (e < -0.5) return "Deep water";
            if (e < -0.2) return "Water";
            if (e < 0) return "Shallows";
            if (e < 0.25) return "Lowland";
            if (e < 0.5) return "Upland";
            if (e < 0.75) return "Highland";
            return "Mountain";
        }

        private static Color ElevationColor(WorldBlock Block)
        {
            switch (ElevationBand(Block))
            {
                case "Deep water": return Color.FromArgb(10, 40, 110);
                case "Water": return Color.FromArgb(25, 85, 160);
                case "Shallows": return Color.FromArgb(40, 130, 200);
                case "Lowland": return Color.FromArgb(110, 170, 80);
                case "Upland": return Color.FromArgb(175, 175, 90);
                case "Highland": return Color.FromArgb(145, 110, 80);
                case "Mountain": return Color.FromArgb(235, 235, 240);
            }
            return Color.Black;
        }

        /// <summary>
        /// Name of the temperature class of a block, or null if the block has no temperature.
        /// </summary>
        public static string TemperatureBand(WorldBlock Block)
        {
            double? t = Block.Status.Temperature;
            return t.HasValue ? Terrains.Biome.TraitName(Terrains.ClimateScale.Temperature(t.Value)) : null;
        }

        /// <summary>
        /// Name of the moisture (precipitation) class of a block, or null if the block has no moisture.
        /// </summary>
        public static string MoistureBand(WorldBlock Block)
        {
            double? m = Block.Status.Moisture;
            return m.HasValue ? Terrains.Biome.TraitName(Terrains.ClimateScale.Precipitation(m.Value)) : null;
        }

        private static readonly Color[] TemperatureColors =
        {
            Color.FromArgb(80, 110, 210), Color.FromArgb(120, 190, 225), Color.FromArgb(110, 190, 100),
            Color.FromArgb(235, 200, 70), Color.FromArgb(215, 80, 50)
        };

        private static readonly Color[] MoistureColors =
        {
            Color.FromArgb(214, 190, 130), Color.FromArgb(190, 200, 120), Color.FromArgb(110, 180, 100),
            Color.FromArgb(50, 140, 110), Color.FromArgb(20, 95, 120)
        };

        /// <summary>
        /// Label for a block in the Plates view: the kind of plate boundary it lies on, or its plate's name.
        /// </summary>
        public static string PlateLabel(WorldBlock Block)
        {
            var plate = Block.Status.Plate;
            if (plate == null) return "No plate";
            var boundary = plate.BoundaryAt(Block);
            return boundary == PlateBoundaryKind.None ? plate.Name : boundary + " boundary";
        }

        private static Color PlateColor(WorldBlock Block)
        {
            var plate = Block.Status.Plate;
            if (plate == null) return Color.Black;
            switch (plate.BoundaryAt(Block))
            {
                case PlateBoundaryKind.Convergent: return Color.FromArgb(200, 40, 40);
                case PlateBoundaryKind.Divergent: return Color.FromArgb(40, 110, 230);
                case PlateBoundaryKind.Transform: return Color.FromArgb(235, 200, 60);
            }
            return plate.DisplayInfo.DisplayColor;
        }

        public static Color GetColor(WorldBlock Block, MapContext MapContext)
        {
            if (MapContext == MapContext.Elevation) return ElevationColor(Block);
            if (MapContext == MapContext.Plates) return PlateColor(Block);

            //
            //if (null != Block.Status.Sea) return Color.DarkBlue;

            if (Block.Status.WaterStatus == WorldBlockWaterStatus.Water) return Color.Blue;

            if (MapContext == MapContext.Temperature && Block.Status.Temperature.HasValue)
                return TemperatureColors[(int)Terrains.ClimateScale.Temperature(Block.Status.Temperature.Value)];

            if (MapContext == MapContext.Moisture && Block.Status.Moisture.HasValue)
                return MoistureColors[(int)Terrains.ClimateScale.Precipitation(Block.Status.Moisture.Value)];

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

            if (MapContext == MapContext.Features)
            {
                // The finest feature covering the block, or a muted land colour where there is none
                var feature = FeatureToShow(Block);
                return feature != null ? feature.DisplayInfo.DisplayColor : Color.FromArgb(150, 165, 140);
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
        General, Continents, Regions, Biomes, Terrains, TerrainTiles, Elevation, Temperature, Moisture, Features, Plates
    }
}
