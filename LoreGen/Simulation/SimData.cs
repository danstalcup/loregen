using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

using LoreGen.Lang;
using LoreGen.WorldGen;
using LoreGen.Terrains;

namespace LoreGen.Simulation
{
    /// <summary>
    /// Loads and provides raw data from data files
    /// </summary>
    public class SimData
    {
        /// <summary>
        /// The simulation engine that is using this data processor
        /// </summary>
        public SimEngine SimEngine;

        /// <summary>
        /// Collection of Letter-Sounds, processed from "LetterSounds.txt" in the RawData folder
        /// </summary>       
        public List<LetterSound> LetterSounds;

        /// <summary>
        /// Collection of Phonetic Inventories, processed from "PhoneticInventories.csv"
        /// </summary>
        public List<PhoneticInventory> PhoneticInventories;

        /// <summary>
        /// Collection of language randomization rules, processed from "LanguageRules.csv"
        /// </summary>
        public LanguageRules LanguageRules;

        /// <summary>
        /// Collection of world generation rules, processed from "WorldRules.csv"
        /// </summary>
        public WorldRules WorldRules;

        /// <summary>
        /// Collection of edge patterns, processed from "EdgePatterns.csv"
        /// </summary>
        public List<EdgePattern> EdgePatterns;

        /// <summary>
        /// Collection of climate types, processed from "ClimateTypes.csv"
        /// </summary>
        public List<ClimateType> ClimateTypes;

        /// <summary>
        /// Collection of climates, processed from "Climates.csv"
        /// </summary>
        public List<Climate> Climates;

        /// <summary>
        /// Collection of biomes, processed from "Biomes.txt"
        /// </summary>
        public List<Biome> Biomes;

        /// <summary>
        /// Collection of terrain types, processed from "TerrainTypes.csv"
        /// </summary>
        public List<TerrainType> TerrainTypes;

        /// <summary>
        /// Collection of terrains, processed from "Terrains.csv"
        /// </summary>
        public List<Terrain> Terrains;

        /// <summary>
        /// Collection of terrain generation rules, processed form "TerrainRules.csv"
        /// </summary>
        public TerrainRules TerrainRules;
        
        /// <summary>
        /// Initialize the data processor
        /// </summary>
        /// <param name="SimEngine">The engine using this data processor</param>
        public SimData(SimEngine SimEngine)
        {
            this.SimEngine = SimEngine;
            this.DataFolder = SimEngine.Configuration.DataFolder;
        }

        /// <summary>
        /// Folder to read data in from. "Default" by default.
        /// </summary>
        public string DataFolder = "Default";

        /// <summary>
        /// Loads in all data
        /// </summary>
        public void Initialize()
        {
            InitializeLetterSounds();
            InitializePhoneticInventories();
            InitializeLanguageRules();
            InitializeWorldRules();
            InitializeEdgePatterns();
            InitializeClimateTypes();
            InitializeClimates();
            InitializeBiomes();
            InitializeTerrainTypes();
            InitializeTerrains();
            InitializeTerrainRules();
            InitializeBiomePatterns();            
        }        


        /// <summary>
        /// Loads in letter/sound data from "LetterSounds.txt" in the data folder.        
        /// </summary>
        public void InitializeLetterSounds()
        {
            LetterSounds = new List<LetterSound>();
            string[] letterLines = File.ReadAllLines(DataFolder+"/LetterSounds.txt");
            foreach (string letterLine in letterLines)
            {
                string[] letterData = letterLine.Split();
                LetterSound letter = new LetterSound();
                letter.ID = Convert.ToInt32(letterData[0]);
                letter.IsVowel = (letterData[1] == "1");
                letter.IsSZLike = (letterData[2] == "1");
                letter.IsRLLike = (letterData[3] == "1");
                letter.Written = new List<string>();

                int numberOfWrittenRepresentations = Convert.ToInt32(letterData[4]);

                for (int i = 0; i < numberOfWrittenRepresentations; i++)
                {
                    letter.Written.Add(letterData[5 + i]);
                }
                LetterSounds.Add(letter);
            }
        }

        /// <summary>
        /// Loads in letter/sound data from "PhoneticInventories.csv." Letter-Sounds must be loaded.
        /// </summary>
        public void InitializePhoneticInventories()
        {
            PhoneticInventories = new List<PhoneticInventory>();
            string[] inventoryLines = File.ReadAllLines(DataFolder + "/PhoneticInventories.csv");
            foreach (string inventoryLine in inventoryLines)
            {
                string[] inventoryData = inventoryLine.Split(new char[] { ',' });                
                List<LetterSound> letters = new List<LetterSound>();
                for (int i = 1; i < inventoryData.Count(); i++)
                {
                    if (inventoryData[i].Length > 0) // no empty entries
                    {
                        int id = Convert.ToInt32(inventoryData[i]);
                        letters.Add(LetterSounds.Where(ls => ls.ID == id).Single());
                    }
                }
                PhoneticInventory inventory = new PhoneticInventory(letters,inventoryData[0]);
                PhoneticInventories.Add(inventory);
            }
        }

        /// <summary>
        /// Loads in language rule data from "LanguageRules.csv"
        /// </summary>
        public void InitializeLanguageRules()
        {
            LanguageRules = new LanguageRules();
            string[] languageLines = File.ReadAllLines(DataFolder + "/LanguageRules.csv");
            double[] languageInfo = new double[languageLines.Length];
            for (int i =0; i<languageLines.Length;i++)
            {
                string[] line = languageLines[i].Split(new char[]{','});
                languageInfo[i] = Convert.ToDouble(line[1]);
            }
            
            LanguageRules.LongRimeProbability = languageInfo[0];
            LanguageRules.LongRimeMin = languageInfo[1];
            LanguageRules.LongRimeMax = languageInfo[2];
            LanguageRules.LongRimeSkew = languageInfo[3];
            LanguageRules.ComplexPreOnsetProbability = languageInfo[4];
            LanguageRules.ComplexPreOnsetMin = languageInfo[5];
            LanguageRules.ComplexPreOnsetMax = languageInfo[6];
            LanguageRules.ComplexPreOnsetSkew = languageInfo[7];
            LanguageRules.ComplexPostOnsetProbability = languageInfo[8];
            LanguageRules.ComplexPostOnsetMin = languageInfo[9];
            LanguageRules.ComplexPostOnsetMax = languageInfo[10];
            LanguageRules.ComplexPostOnsetSkew = languageInfo[11];
            LanguageRules.CodaProbability = languageInfo[12];
            LanguageRules.CodaMin = languageInfo[13];
            LanguageRules.CodaMax = languageInfo[14];
            LanguageRules.CodaSkew = languageInfo[15];
            LanguageRules.ComplexCodaProbability = languageInfo[16];
            LanguageRules.ComplexCodaMin = languageInfo[17];
            LanguageRules.ComplexCodaMax = languageInfo[18];
            LanguageRules.ComplexCodaSkew = languageInfo[19];
            LanguageRules.HyphenProbability = languageInfo[20];
            LanguageRules.HyphenMin = languageInfo[21];
            LanguageRules.HyphenMax = languageInfo[22];
            LanguageRules.HyphenSkew = languageInfo[23];
            LanguageRules.ApostropheProbability = languageInfo[24];
            LanguageRules.FirstConsonantSkippedMin = languageInfo[25];
            LanguageRules.FirstConsonantSkippedMax = languageInfo[26];
            LanguageRules.FirstConsonantSkippedSkew = languageInfo[27];
            LanguageRules.SyllablesMeanMin = languageInfo[28];
            LanguageRules.SyllablesMeanMax = languageInfo[29];
            LanguageRules.SyllablesMeanSkew = languageInfo[30];
            LanguageRules.SyllablesStdDevMin = languageInfo[31];
            LanguageRules.SyllablesStdDevMax = languageInfo[32];
            LanguageRules.SyllablesStdDevSkew = languageInfo[33];            
            LanguageRules.WordsMeanMin = languageInfo[34];
            LanguageRules.WordsMeanMax = languageInfo[35];
            LanguageRules.WordsMeanSkew = languageInfo[36];
            LanguageRules.WordsStdDevMin = languageInfo[37];
            LanguageRules.WordsStdDevMax = languageInfo[38];
            LanguageRules.WordsStdDevSkew = languageInfo[39];
            LanguageRules.ExoticnessMeanMin = languageInfo[40];
            LanguageRules.ExoticnessMeanMax = languageInfo[41];
            LanguageRules.ExoticnessMeanSkew = languageInfo[42];
            LanguageRules.ExoticnessStdDevMin = languageInfo[43];
            LanguageRules.ExoticnessStdDevMax = languageInfo[44];
            LanguageRules.ExoticnessStdDevSkew = languageInfo[45];
            LanguageRules.MaxSkewedElements = Convert.ToInt32(languageInfo[46]);
                 }

        /// <summary>
        /// Loads in world rule data from "WorldRules.csv"
        /// </summary>
        public void InitializeWorldRules()
        {
            WorldRules = new WorldRules();
            string[] worldLines = File.ReadAllLines(DataFolder + "/WorldRules.csv");
            double[] worldInfo = new double[worldLines.Length];
            for (int i = 0; i < worldLines.Length; i++)
            {
                string[] line = worldLines[i].Split(new char[] { ',' });
                worldInfo[i] = Convert.ToDouble(line[1]);
            }

            WorldRules.ContinentsMin = Convert.ToInt32(worldInfo[0]);
            WorldRules.ContinentsMax = Convert.ToInt32(worldInfo[1]);
            WorldRules.WorldLengthMean = worldInfo[2];
            WorldRules.WorldLengthStdDev = worldInfo[3];
            WorldRules.WorldLengthMin = Convert.ToInt32(worldInfo[4]);
            WorldRules.WorldLengthMax = Convert.ToInt32(worldInfo[5]);
            WorldRules.WorldHeightLengthRatio = worldInfo[6];
            WorldRules.SplitBlockMinimumPercentage = worldInfo[7];
            WorldRules.RegionsMean = worldInfo[8];
            WorldRules.RegionsStdDev = worldInfo[9];
            WorldRules.RegionsMin = Convert.ToInt32(worldInfo[10]);
            WorldRules.RegionsMax = Convert.ToInt32(worldInfo[11]);
            WorldRules.ZoomScaleLarge = Convert.ToInt32(worldInfo[12]);
            WorldRules.ZoomScaleSmall = Convert.ToInt32(worldInfo[13]);
            WorldRules.CoastlineBorderMean = worldInfo[14];
            WorldRules.CoastlineBorderStdDev = worldInfo[15];
            WorldRules.CoastlineBorderMin = worldInfo[16];
            WorldRules.CoastlineBorderMax = worldInfo[17];
            WorldRules.SeasPerContinentMin = Convert.ToInt32(worldInfo[18]);
            WorldRules.SeasPerContinentMax = Convert.ToInt32(worldInfo[19]);
            WorldRules.ContinentPerSeaMin = worldInfo[20];
            WorldRules.ContinentPerSeaMax = worldInfo[21];
            WorldRules.ContinentInSeasTotalMin = worldInfo[22];
            WorldRules.ContinentInSeasTotalMax = worldInfo[23];
            WorldRules.MediterraneanSeaProbability = worldInfo[24];
            WorldRules.ContinentCoastErosionMin = worldInfo[25];
            WorldRules.ContinentCoastErosionMax = worldInfo[26];
        }

        /// <summary>
        /// Loads in edge patterns from EdgePatterns.csv
        /// </summary>
        public void InitializeEdgePatterns()
        {
            EdgePatterns = new List<EdgePattern>();
            string[] patternLines = File.ReadAllLines(DataFolder + "/EdgePatterns.csv");

            int i = 0;
            while(i < patternLines.Length)
            {
                //read in the size of edge pattern
                string[] sizeLine = patternLines[i++].Split(new char[] { ',' });
                
                EdgePattern pattern = new EdgePattern(Convert.ToInt32(sizeLine[0]),sizeLine[1].ToUpper() == "YES");                

                //read in which sides are edges
                string[] coastsLine = patternLines[i++].Split(new char[] { ',' });

                int numCoasts = Convert.ToInt32(coastsLine[0]);
                
                for (int j = 1; j <= numCoasts; j++)
                {
                    if (coastsLine[j] == "N")
                    {
                        pattern.NorthEdge = true;
                    }
                    if (coastsLine[j] == "S")
                    {
                        pattern.SouthEdge = true;
                    }
                    if (coastsLine[j] == "E")
                    {
                        pattern.EastEdge = true;
                    }
                    if (coastsLine[j] == "W")
                    {
                        pattern.WestEdge = true;
                    }
                }

                //read in the actual pattern
                for (int j = 0; j < pattern.Length; j++)
                {
                    string[] patternLine = patternLines[i++].Split(new char[] { ',' });
                    for (int k = 0; k < pattern.Length; k++)
                    {
                        if (patternLine[k] == ".")
                        {
                            pattern.Pattern[k, j] = EdgeStatus.Off;
                        }
                        if (patternLine[k] == "O")
                        {
                            pattern.Pattern[k, j] = EdgeStatus.Edge;
                        }
                        if (patternLine[k] == "X")
                        {
                            pattern.Pattern[k, j] = EdgeStatus.On;
                        }
                    }
                }
                EdgePatterns.Add(pattern);

            }
        }

        /// <summary>
        /// Loads in climate types from ClimateTypes.csv
        /// </summary>
        private void InitializeClimateTypes()
        {
            ClimateTypes = new List<ClimateType>();
            string[] climateTypeLines = File.ReadAllLines(DataFolder + "/ClimateTypes.csv");

            foreach(string climateTypeLine in climateTypeLines)
            {
                string[] line = climateTypeLine.Split(new char[] { ',' });
                int id = Convert.ToInt32(line[1]);
                ClimateTypes.Add(new ClimateType(line[0], id,line[2], line[3]));
            }

        }

        /// <summary>
        /// Loads in climates from "Climates.csv". ClimateTypes must be loaded.
        /// </summary>
        private void InitializeClimates()
        {
            Climates = new List<Climate>();
            string[] climateLines = File.ReadAllLines(DataFolder + "/Climates.csv");

            foreach(string climateLine in climateLines)
            {
                string[] line = climateLine.Split(new char[] { ',' });
                ClimateType primary = ClimateTypes.Where(ct => ct.ID == Convert.ToInt32(line[1])).Single();
                ClimateType secondary = ClimateTypes.Where(ct => ct.ID == Convert.ToInt32(line[2])).Single();
                Climate climate = new Climate (Convert.ToInt32(line[0]), primary , secondary , line[3], line[4] );
                Climates.Add(climate);
            }
        }

        /// <summary>
        /// Loads in biomes from "Biomes.txt"
        /// </summary>
        private void InitializeBiomes()
        {
            Biomes = new List<Biome>();
            string[] biomeLines = File.ReadAllLines(DataFolder + "/Biomes.txt");

            foreach (string biomeLine in biomeLines)
            {
                string[] line = biomeLine.Split(new char[] { ',' });
                Biomes.Add(new Biome(Convert.ToInt32(line[0]), line[1], line[2], line[3]));
            }
        }

        /// <summary>
        /// Loads in terrain types from "TerrainTypes.csv."
        /// </summary>
        private void InitializeTerrainTypes()
        {
            TerrainTypes = new List<TerrainType>();
            string[] terrainTypeLines = File.ReadAllLines(DataFolder + "/TerrainTypes.csv");

            foreach(string terrainTypeLine in terrainTypeLines)
            {
                string[] line = terrainTypeLine.Split(new char[] { ',' });
                TerrainTypes.Add(new TerrainType(Convert.ToInt32(line[1]), line[0], line[2]));
            }
        }

        /// <summary>
        /// Loads in terrains from "Terrains.csv." Terrain types and climates must be loaded.
        /// </summary>
        private void InitializeTerrains()
        {
            Terrains = new List<Terrain>();
            string[] terrainLines = File.ReadAllLines(DataFolder + "/Terrains.csv");

            foreach(string terrainLine in terrainLines)
            {
                string[] line = terrainLine.Split(new char[] { ',' });
                Climate climate = Climates.Where(c => c.ID == Convert.ToInt32(line[1])).Single();
                TerrainType terrainType = TerrainTypes.Where(tt => tt.ID == Convert.ToInt32(line[2])).Single();
                Terrain terrain = new Terrain(Convert.ToInt32(line[0]), climate, terrainType, line[3], string.Empty);//line[4]);
                Terrains.Add(terrain);
            }

        }

        /// <summary>
        /// Loads biome terrain generation patterns from "BiomePatterns.csv." Biomes, climates, and terrain types must be loaded.
        /// </summary>
        private void InitializeBiomePatterns()
        {
            string[] biomePatternLines = File.ReadAllLines(DataFolder + "/BiomePatterns.csv");

            for (int i = 0; i < biomePatternLines.Length; i += 3)
            {
                Biome biome = Biomes.Where(b => b.ID == Convert.ToInt32(biomePatternLines[i])).Single();
                string[] climateTypeWeights = biomePatternLines[i+1].Split(new char[] { ',' });
                for(int j=0;j<climateTypeWeights.Length;j+=2)
                {
                    ClimateType climateType = ClimateTypes.Where(ct => ct.ID == Convert.ToInt32(climateTypeWeights[j])).Single();
                    biome.ClimateTypeWeights.Add(new BiomeClimateTypeWeight { ClimateType = climateType, Weight = Convert.ToDouble(climateTypeWeights[j + 1]) });
                }

                string[] terrainTypeWeights = biomePatternLines[i + 2].Split(new char[] { ',' });
                for (int j = 0; j < terrainTypeWeights.Length; j += 2)
                {
                    TerrainType terrainType = TerrainTypes.Where(tt => tt.ID == Convert.ToInt32(terrainTypeWeights[j])).Single();
                    biome.TerrainTypeWeights.Add(new BiomeTerrainTypeWeight { TerrainType = terrainType, Weight = Convert.ToDouble(terrainTypeWeights[j + 1]) });
                }
                biome.InitializeTerrainPattern(SimEngine);
            }
        }

        /// <summary>
        /// Loads terrain rules from TerrainRules.csv. 
        /// </summary>
        private void InitializeTerrainRules()
        {
            TerrainRules = new TerrainRules();
            string[] terrainRulesLines = File.ReadAllLines(DataFolder + "/TerrainRules.csv");
            double[] terrainInfo = new double[terrainRulesLines.Length];
            for (int i = 0; i < terrainRulesLines.Length; i++)
            {
                string[] line = terrainRulesLines[i].Split(new char[] { ',' });
                terrainInfo[i] = Convert.ToDouble(line[1]);
            }

            TerrainRules.CombinationWeightReducer = terrainInfo[0];
        }

    }
}
