using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Globalization;

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


        // Data files always use '.' decimals regardless of the machine/browser culture.
        private static double ToD(string s) { return double.Parse(s.Trim(), CultureInfo.InvariantCulture); }
        private static int ToI(string s) { return int.Parse(s.Trim(), CultureInfo.InvariantCulture); }
        private static int ToI(double d) { return Convert.ToInt32(d); }

        /// <summary>
        /// Folder to read data in from. "Default" by default.
        /// </summary>
        public string DataFolder = "Default";


        /// <summary>
        /// Loads a keyed rules file ("Key,Value[,Description]" rows) from the data folder, falling back to the copy
        /// embedded in LoreGen. Generators use this to read their own settings files.
        /// </summary>
        /// <param name="filename">File name, for example "WorldRules.csv"</param>
        public RulesFile LoadRules(string filename)
        {
            return new RulesFile(filename, ReadDataLines(filename));
        }

        /// <summary>
        /// Reads a data file as lines. Looks in the data folder on disk first (so data can be overridden),
        /// then falls back to the copy embedded in the LoreGen assembly.
        /// </summary>
        private string[] ReadDataLines(string filename)
        {
            string path = Path.Combine(DataFolder, filename);
            if (File.Exists(path)) return File.ReadAllLines(path);

            var assembly = typeof(SimData).Assembly;
            using (Stream stream = assembly.GetManifestResourceStream("LoreGen.Resources." + filename))
            {
                if (stream == null) throw new FileNotFoundException("Data file not found on disk or embedded: " + filename, path);
                using (var reader = new StreamReader(stream, true))
                {
                    var lines = new List<string>();
                    string line;
                    while ((line = reader.ReadLine()) != null) lines.Add(line);
                    return lines.ToArray();
                }
            }
        }

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
            string[] letterLines = ReadDataLines("LetterSounds.txt");
            foreach (string letterLine in letterLines)
            {
                string[] letterData = letterLine.Split();
                LetterSound letter = new LetterSound();
                letter.ID = ToI(letterData[0]);
                letter.IsVowel = (letterData[1] == "1");
                letter.IsSZLike = (letterData[2] == "1");
                letter.IsRLLike = (letterData[3] == "1");
                letter.Written = new List<string>();

                int numberOfWrittenRepresentations = ToI(letterData[4]);

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
            string[] inventoryLines = ReadDataLines("PhoneticInventories.csv");
            foreach (string inventoryLine in inventoryLines)
            {
                string[] inventoryData = inventoryLine.Split(new char[] { ',' });                
                List<LetterSound> letters = new List<LetterSound>();
                for (int i = 1; i < inventoryData.Count(); i++)
                {
                    if (inventoryData[i].Length > 0) // no empty entries
                    {
                        int id = ToI(inventoryData[i]);
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
            var r = LoadRules("LanguageRules.csv");
            LanguageRules = new LanguageRules();
            LanguageRules.LongRimeProbability = r.D("LongRimeProbability");
            LanguageRules.LongRimeMin = r.D("LongRimeMin");
            LanguageRules.LongRimeMax = r.D("LongRimeMax");
            LanguageRules.LongRimeSkew = r.D("LongRimeSkew");
            LanguageRules.ComplexPreOnsetProbability = r.D("ComplexPreOnsetProbability");
            LanguageRules.ComplexPreOnsetMin = r.D("ComplexPreOnsetMin");
            LanguageRules.ComplexPreOnsetMax = r.D("ComplexPreOnsetMax");
            LanguageRules.ComplexPreOnsetSkew = r.D("ComplexPreOnsetSkew");
            LanguageRules.ComplexPostOnsetProbability = r.D("ComplexPostOnsetProbability");
            LanguageRules.ComplexPostOnsetMin = r.D("ComplexPostOnsetMin");
            LanguageRules.ComplexPostOnsetMax = r.D("ComplexPostOnsetMax");
            LanguageRules.ComplexPostOnsetSkew = r.D("ComplexPostOnsetSkew");
            LanguageRules.CodaProbability = r.D("CodaProbability");
            LanguageRules.CodaMin = r.D("CodaMin");
            LanguageRules.CodaMax = r.D("CodaMax");
            LanguageRules.CodaSkew = r.D("CodaSkew");
            LanguageRules.ComplexCodaProbability = r.D("ComplexCodaProbability");
            LanguageRules.ComplexCodaMin = r.D("ComplexCodaMin");
            LanguageRules.ComplexCodaMax = r.D("ComplexCodaMax");
            LanguageRules.ComplexCodaSkew = r.D("ComplexCodaSkew");
            LanguageRules.HyphenProbability = r.D("HyphenProbability");
            LanguageRules.HyphenMin = r.D("HyphenMin");
            LanguageRules.HyphenMax = r.D("HyphenMax");
            LanguageRules.HyphenSkew = r.D("HyphenSkew");
            LanguageRules.ApostropheProbability = r.D("ApostropheProbability");
            LanguageRules.FirstConsonantSkippedMin = r.D("FirstConsonantSkippedMin");
            LanguageRules.FirstConsonantSkippedMax = r.D("FirstConsonantSkippedMax");
            LanguageRules.FirstConsonantSkippedSkew = r.D("FirstConsonantSkippedSkew");
            LanguageRules.SyllablesMeanMin = r.D("SyllablesMeanMin");
            LanguageRules.SyllablesMeanMax = r.D("SyllablesMeanMax");
            LanguageRules.SyllablesMeanSkew = r.D("SyllablesMeanSkew");
            LanguageRules.SyllablesStdDevMin = r.D("SyllablesStdDevMin");
            LanguageRules.SyllablesStdDevMax = r.D("SyllablesStdDevMax");
            LanguageRules.SyllablesStdDevSkew = r.D("SyllablesStdDevSkew");
            LanguageRules.WordsMeanMin = r.D("WordsMeanMin");
            LanguageRules.WordsMeanMax = r.D("WordsMeanMax");
            LanguageRules.WordsMeanSkew = r.D("WordsMeanSkew");
            LanguageRules.WordsStdDevMin = r.D("WordsStdDevMin");
            LanguageRules.WordsStdDevMax = r.D("WordsStdDevMax");
            LanguageRules.WordsStdDevSkew = r.D("WordsStdDevSkew");
            LanguageRules.ExoticnessMeanMin = r.D("ExoticnessMeanMin");
            LanguageRules.ExoticnessMeanMax = r.D("ExoticnessMeanMax");
            LanguageRules.ExoticnessMeanSkew = r.D("ExoticnessMeanSkew");
            LanguageRules.ExoticnessStdDevMin = r.D("ExoticnessStdDevMin");
            LanguageRules.ExoticnessStdDevMax = r.D("ExoticnessStdDevMax");
            LanguageRules.ExoticnessStdDevSkew = r.D("ExoticnessStdDevSkew");
            LanguageRules.MaxSkewedElements = r.I("MaxSkewedElements");
            r.Finish();
        }

        /// <summary>
        /// Loads in world rule data from "WorldRules.csv"
        /// </summary>
        public void InitializeWorldRules()
        {
            var r = LoadRules("WorldRules.csv");
            WorldRules = new WorldRules();
            WorldRules.ContinentsMin = r.I("ContinentsMin");
            WorldRules.ContinentsMax = r.I("ContinentsMax");
            WorldRules.WorldLengthMean = r.D("WorldLengthMean");
            WorldRules.WorldLengthStdDev = r.D("WorldLengthStdDev");
            WorldRules.WorldLengthMin = r.I("WorldLengthMin");
            WorldRules.WorldLengthMax = r.I("WorldLengthMax");
            WorldRules.WorldHeightLengthRatio = r.D("WorldHeightLengthRatio");
            WorldRules.SplitBlockMinimumPercentage = r.D("SplitBlockMinimumPercentage");
            WorldRules.RegionsMean = r.D("RegionsMean");
            WorldRules.RegionsStdDev = r.D("RegionsStdDev");
            WorldRules.RegionsMin = r.I("RegionsMin");
            WorldRules.RegionsMax = r.I("RegionsMax");
            WorldRules.ZoomScaleLarge = r.I("ZoomScaleLarge");
            WorldRules.ZoomScaleSmall = r.I("ZoomScaleSmall");
            WorldRules.CoastlineBorderMean = r.D("CoastlineBorderMean");
            WorldRules.CoastlineBorderStdDev = r.D("CoastlineBorderStdDev");
            WorldRules.CoastlineBorderMin = r.D("CoastlineBorderMin");
            WorldRules.CoastlineBorderMax = r.D("CoastlineBorderMax");
            WorldRules.SeasPerContinentMin = r.I("SeasPerContinentMin");
            WorldRules.SeasPerContinentMax = r.I("SeasPerContinentMax");
            WorldRules.ContinentPerSeaMin = r.D("ContinentPerSeaMin");
            WorldRules.ContinentPerSeaMax = r.D("ContinentPerSeaMax");
            WorldRules.ContinentInSeasTotalMin = r.D("ContinentInSeasTotalMin");
            WorldRules.ContinentInSeasTotalMax = r.D("ContinentInSeasTotalMax");
            WorldRules.MediterraneanSeaProbability = r.D("MediterraneanSeaProbability");
            WorldRules.ContinentCoastErosionMin = r.D("ContinentCoastErosionMin");
            WorldRules.ContinentCoastErosionMax = r.D("ContinentCoastErosionMax");
            r.Finish();
        }

        /// <summary>
        /// Loads in edge patterns from EdgePatterns.csv
        /// </summary>
        public void InitializeEdgePatterns()
        {
            EdgePatterns = new List<EdgePattern>();
            string[] patternLines = ReadDataLines("EdgePatterns.csv");

            int i = 0;
            while(i < patternLines.Length)
            {
                //read in the size of edge pattern
                string[] sizeLine = patternLines[i++].Split(new char[] { ',' });
                
                EdgePattern pattern = new EdgePattern(ToI(sizeLine[0]),sizeLine[1].ToUpper() == "YES");                

                //read in which sides are edges
                string[] coastsLine = patternLines[i++].Split(new char[] { ',' });

                int numCoasts = ToI(coastsLine[0]);
                
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
            string[] climateTypeLines = ReadDataLines("ClimateTypes.csv");

            foreach(string climateTypeLine in climateTypeLines)
            {
                string[] line = climateTypeLine.Split(new char[] { ',' });
                int id = ToI(line[1]);
                ClimateTypes.Add(new ClimateType(line[0], id,line[2], line[3]));
            }

        }

        /// <summary>
        /// Loads in climates from "Climates.csv". ClimateTypes must be loaded.
        /// </summary>
        private void InitializeClimates()
        {
            Climates = new List<Climate>();
            string[] climateLines = ReadDataLines("Climates.csv");

            foreach(string climateLine in climateLines)
            {
                string[] line = climateLine.Split(new char[] { ',' });
                ClimateType primary = ClimateTypes.Where(ct => ct.ID == ToI(line[1])).Single();
                ClimateType secondary = ClimateTypes.Where(ct => ct.ID == ToI(line[2])).Single();
                Climate climate = new Climate (ToI(line[0]), primary , secondary , line[3], line[4] );
                Climates.Add(climate);
            }
        }

        /// <summary>
        /// Loads in biomes from "Biomes.txt"
        /// </summary>
        private void InitializeBiomes()
        {
            Biomes = new List<Biome>();
            string[] biomeLines = ReadDataLines("Biomes.txt");

            foreach (string biomeLine in biomeLines)
            {
                string[] line = biomeLine.Split(new char[] { ',' });
                Biomes.Add(new Biome(ToI(line[0]), line[1], line[2], line[3]));
            }
        }

        /// <summary>
        /// Loads in terrain types from "TerrainTypes.csv."
        /// </summary>
        private void InitializeTerrainTypes()
        {
            TerrainTypes = new List<TerrainType>();
            string[] terrainTypeLines = ReadDataLines("TerrainTypes.csv");

            foreach(string terrainTypeLine in terrainTypeLines)
            {
                string[] line = terrainTypeLine.Split(new char[] { ',' });
                TerrainTypes.Add(new TerrainType(ToI(line[1]), line[0], line[2], line.Length > 3 && line[3].Trim().Length > 0 ? ToI(line[3]) : TerrainType.DefaultRelief));
            }
        }

        /// <summary>
        /// Loads in terrains from "Terrains.csv." Terrain types and climates must be loaded.
        /// </summary>
        private void InitializeTerrains()
        {
            Terrains = new List<Terrain>();
            string[] terrainLines = ReadDataLines("Terrains.csv");

            foreach(string terrainLine in terrainLines)
            {
                string[] line = terrainLine.Split(new char[] { ',' });
                Climate climate = Climates.Where(c => c.ID == ToI(line[1])).Single();
                TerrainType terrainType = TerrainTypes.Where(tt => tt.ID == ToI(line[2])).Single();
                Terrain terrain = new Terrain(ToI(line[0]), climate, terrainType, line[3], string.Empty);//line[4]);
                Terrains.Add(terrain);
            }

        }

        /// <summary>
        /// Loads biome terrain generation patterns from "BiomePatterns.csv." Biomes, climates, and terrain types must be loaded.
        /// </summary>
        private void InitializeBiomePatterns()
        {
            string[] biomePatternLines = ReadDataLines("BiomePatterns.csv");

            for (int i = 0; i < biomePatternLines.Length; i += 3)
            {
                Biome biome = Biomes.Where(b => b.ID == ToI(biomePatternLines[i])).Single();
                string[] climateTypeWeights = biomePatternLines[i+1].Split(new char[] { ',' });
                for(int j=0;j<climateTypeWeights.Length;j+=2)
                {
                    ClimateType climateType = ClimateTypes.Where(ct => ct.ID == ToI(climateTypeWeights[j])).Single();
                    biome.ClimateTypeWeights.Add(new BiomeClimateTypeWeight { ClimateType = climateType, Weight = ToD(climateTypeWeights[j + 1]) });
                }

                string[] terrainTypeWeights = biomePatternLines[i + 2].Split(new char[] { ',' });
                for (int j = 0; j < terrainTypeWeights.Length; j += 2)
                {
                    TerrainType terrainType = TerrainTypes.Where(tt => tt.ID == ToI(terrainTypeWeights[j])).Single();
                    biome.TerrainTypeWeights.Add(new BiomeTerrainTypeWeight { TerrainType = terrainType, Weight = ToD(terrainTypeWeights[j + 1]) });
                }
                biome.InitializeTerrainPattern(SimEngine);
            }
        }

        /// <summary>
        /// Loads terrain rules from TerrainRules.csv. 
        /// </summary>
        public void InitializeTerrainRules()
        {
            var r = LoadRules("TerrainRules.csv");
            TerrainRules = new TerrainRules();
            TerrainRules.CombinationWeightReducer = r.D("CombinationWeightReducer");
            r.Finish();
        }

    }
}
