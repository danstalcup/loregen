using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LoreGen.Simulation;
using LoreGen.Randomizer;

namespace LoreGen.Terrains
{
    public class Biome
    {
        public BiomeTemperature Temperature;
        public BiomePrecipitation Precipitation;
        public BiomeContour Contour;
        public BiomeSeasonsTemperature SeasonsTemp;
        public BiomeSeasonsPrecipitation SeasonsPrecip;
        public BiomeHabitability Habitability;
        public int ID;
        public string Name;
        public string Description;
        public List<BiomeClimateTypeWeight> ClimateTypeWeights;
        public List<BiomeTerrainTypeWeight> TerrainTypeWeights;
        public List<BiomeClimateWeight> ClimateWeights;
        public WeightedRandomContainer<Terrain> TerrainPattern;
        public SimEngine SimEngine;

        public Biome(int ID, string ParametersCode, string Name, string Description)
        {
            this.ID = ID;
            this.Name = Name;
            this.Description = Description;
            ClimateTypeWeights = new List<BiomeClimateTypeWeight>();
            TerrainTypeWeights = new List<BiomeTerrainTypeWeight>();
            ClimateWeights = new List<BiomeClimateWeight>();
            Temperature = (BiomeTemperature)Convert.ToInt32(""+ParametersCode[0]);
            Precipitation = (BiomePrecipitation)Convert.ToInt32("" + ParametersCode[1]);
            Contour = (BiomeContour)Convert.ToInt32("" + ParametersCode[2]);
            SeasonsTemp = (BiomeSeasonsTemperature)Convert.ToInt32("" + ParametersCode[3]);
            SeasonsPrecip = (BiomeSeasonsPrecipitation)Convert.ToInt32("" + ParametersCode[4]);
            Habitability = (BiomeHabitability)Convert.ToInt32("" + ParametersCode[5]);
        }

        public static string TraitName(BiomeTemperature BiomeTemperature)
        {
            switch(BiomeTemperature)
            {
                case Terrains.BiomeTemperature.Frigid:
                    return "Frigid";
                case Terrains.BiomeTemperature.Cold:
                    return "Cold";
                case Terrains.BiomeTemperature.Temperate:
                    return "Temperate";
                case Terrains.BiomeTemperature.Warm:
                    return "Warm";
                case Terrains.BiomeTemperature.Sweltering:
                    return "Sweltering";
            }
            return "ERROR";
        }

        public static string TraitName(BiomePrecipitation BiomePrecipitation)
        {
            switch (BiomePrecipitation)
            {
                case Terrains.BiomePrecipitation.Arid:
                    return "Arid";
                case Terrains.BiomePrecipitation.Dry:
                    return "Dry";
                case Terrains.BiomePrecipitation.Moderate:
                    return "Moderate";
                case Terrains.BiomePrecipitation.Wet:
                    return "Wet";
                case Terrains.BiomePrecipitation.Tropical:
                    return "Tropical";
            }
            return "ERROR";
        }

        public static string TraitName(BiomeContour BiomeContour)
        {
            switch (BiomeContour)
            {
                case Terrains.BiomeContour.Lowlands:
                    return "Lowlands";
                case Terrains.BiomeContour.Flat:
                    return "Flat";
                case Terrains.BiomeContour.Hilly:
                    return "Hilly";
                case Terrains.BiomeContour.Jagged:
                    return "Jagged";
                case Terrains.BiomeContour.Mountainous:
                    return "Mountainous";
            }
            return "ERROR";
        }

        public static string TraitName(BiomeSeasonsTemperature BiomeSeasonsTemperature)
        {
            switch (BiomeSeasonsTemperature)
            {
                case Terrains.BiomeSeasonsTemperature.NoSeasons:
                    return "No Temp Seasons";
                case Terrains.BiomeSeasonsTemperature.ColdWinter:
                    return "Cold Winter";
            }
            return "ERROR";
        }

        public static string TraitName(BiomeSeasonsPrecipitation BiomeSeasonsPrecipitation)
        {
            switch (BiomeSeasonsPrecipitation)
            {
                case Terrains.BiomeSeasonsPrecipitation.NoSeasons:
                    return "No Precip Seasons";
                case Terrains.BiomeSeasonsPrecipitation.WetSummers:
                    return "Wet Summers";
            }
            return "ERROR";
        }

        public static string TraitName(BiomeHabitability BiomeHabitability)
        {
            switch (BiomeHabitability)
            {
                case Terrains.BiomeHabitability.Inhabitable:
                    return "Inhabitable";
                case Terrains.BiomeHabitability.Harsh:
                    return "Harsh";
                case Terrains.BiomeHabitability.Unfavorable:
                    return "Unfavorable";
                case Terrains.BiomeHabitability.Livable:
                    return "Livable";
                case Terrains.BiomeHabitability.Comfortable:
                    return "Comfortable";
                case Terrains.BiomeHabitability.Lush:
                    return "Lush";
            }
            return "ERROR";
        }

        public void InitializeTerrainPattern(SimEngine SimEngine)
        {
            foreach (BiomeClimateTypeWeight bctw in ClimateTypeWeights)
            {
                BiomeClimateWeight bcw = new BiomeClimateWeight();
                bcw.Climate = SimEngine.SimData.Climates.Where(c => c.PrimaryClimateType == bctw.ClimateType && c.SecondaryClimateType == bctw.ClimateType).Single();
                bcw.Weight = bctw.Weight;
                ClimateWeights.Add(bcw);
            }

            for (int i = 0; i < ClimateTypeWeights.Count - 1; i++)
            {
                BiomeClimateTypeWeight first = ClimateTypeWeights[i];
                for (int j = i + 1; j < ClimateTypeWeights.Count; j++)
                {
                    BiomeClimateTypeWeight second = ClimateTypeWeights[j];

                    BiomeClimateTypeWeight larger = (first.Weight >= second.Weight ? first : second);
                    BiomeClimateTypeWeight smaller = (first.Weight >= second.Weight ? second : first);


                    Climate newClimate1 = SimEngine.SimData.Climates.Where(c => c.PrimaryClimateType == larger.ClimateType && c.SecondaryClimateType == smaller.ClimateType).SingleOrDefault();
                    Climate newClimate2 = SimEngine.SimData.Climates.Where(c => c.PrimaryClimateType == smaller.ClimateType && c.SecondaryClimateType == larger.ClimateType).SingleOrDefault();
                    if (null != newClimate1)
                    {
                        BiomeClimateWeight bcw = new BiomeClimateWeight();
                        bcw.Climate = newClimate1;
                        bcw.Weight = larger.Weight + smaller.Weight - SimEngine.SimData.TerrainRules.CombinationWeightReducer;
                        ClimateWeights.Add(bcw);
                    }
                    if (null != newClimate2)
                    {
                        BiomeClimateWeight bcw = new BiomeClimateWeight();
                        bcw.Climate = newClimate2;
                        bcw.Weight = (larger.Weight + smaller.Weight - SimEngine.SimData.TerrainRules.CombinationWeightReducer) * (smaller.Weight / larger.Weight);
                        ClimateWeights.Add(bcw);
                    }
                }
            }
            TerrainPattern = new WeightedRandomContainer<Terrain>(SimEngine.Rnd);
            
            foreach(BiomeClimateWeight bcw in ClimateWeights)
            {
                foreach(BiomeTerrainTypeWeight bttw in TerrainTypeWeights)
                {
                    Terrain terr = SimEngine.SimData.Terrains.Where(t => t.Climate == bcw.Climate && t.TerrainType == bttw.TerrainType).SingleOrDefault();
                    if(null != terr)
                    {
                        TerrainPattern.Add(terr, Math.Pow(2, bcw.Weight) * Math.Pow(2, bttw.Weight));
                    }
                }
            }
        }
        

    }
    public enum BiomeTemperature
    {
        Frigid,Cold,Temperate,Warm,Sweltering
    }
    public enum BiomePrecipitation
    {
        Arid,Dry,Moderate,Wet,Tropical
    }
    public enum BiomeContour
    {
        Lowlands,Flat,Hilly,Jagged,Mountainous
    }
    public enum BiomeSeasonsTemperature
    {
        NoSeasons,ColdWinter
    }
    public enum BiomeSeasonsPrecipitation
    {
        NoSeasons,WetSummers
    }
    public enum BiomeHabitability
    {
        Inhabitable,Harsh,Unfavorable,Livable,Comfortable,Lush
    }
    public struct BiomeClimateTypeWeight
    {
        public ClimateType ClimateType;
        public double Weight;
    }
    public struct BiomeTerrainTypeWeight
    {
        public TerrainType TerrainType;
        public double Weight;
    }
    public struct BiomeClimateWeight
    {
        public Climate Climate;
        public double Weight;
    }
}
