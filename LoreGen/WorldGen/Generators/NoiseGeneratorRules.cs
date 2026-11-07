using System.Collections.Generic;
using System.IO;

using LoreGen.Simulation;

namespace LoreGen.WorldGen.Generators
{
    /// <summary>
    /// Settings for <see cref="NoiseWorldGenerator"/>, read from NoiseGeneratorRules.csv.
    /// </summary>
    public struct NoiseGeneratorRules
    {
        public double LandFractionMean;
        public double LandFractionStdDev;
        public double LandFractionMin;
        public double LandFraction;
        public int ContinentCentres;
        public int RegionTarget;
        public double TemperatureOffset;
        public double LandFractionMax;
        public int Octaves;
        public double Frequency;
        public double Lacunarity;
        public double Persistence;
        public double WarpStrength;
        public double MaskStrength;
        public double MaskRadiusMin;
        public double MaskRadiusMax;
        public double EdgeBorder;
        public int MinLandmassBlocks;
        public int MinSeaBlocks;
        public int CoastDetail;
        public double DetailStrength;
        public double DetailRange;
        public double DetailFrequency;
        public int DetailOctaves;
        public int DetailMinIsletCells;
        public double ClimateFrequency;
        public int ClimateOctaves;
        public double TemperatureLapse;
        public double TemperatureNoise;
        public double MoistureNoiseWeight;
        public double MoistureCoastWeight;
        public double MoistureLatitudeWeight;
        public double MoistureReach;
        public double MoistureElevationGain;
        public double MoistureColdPenalty;
        public double ContourFlatFrom;
        public double ContourHillyFrom;
        public double ContourJaggedFrom;
        public double ContourMountainousFrom;
        public double BiomeTemperatureWeight;
        public double BiomePrecipitationWeight;
        public double BiomeContourWeight;
        public double TerrainReliefStrength;
        public double ElevationPerRelief;
        public int RegionSeedCandidates;
        public double RegionSlopeWeight;
        public double RegionHeightWeight;
        public double RegionClimateWeight;
        public double RegionJitter;
        public int RegionMinBlocks;
        public int RegionBorderDetail;
        public double RegionBorderWarp;
        public double RegionBorderFrequency;

        public const string Filename = "NoiseGeneratorRules.csv";

        /// <summary>
        /// Loads and validates the settings, with any user-supplied settings replacing the file's values.
        /// </summary>
        /// <param name="data">Where the rules file is read from</param>
        /// <param name="overrides">Settings by key (as in <see cref="SimEngineConfiguration.GeneratorSettings"/>), or null for none</param>
        /// <exception cref="InvalidDataException">A key is missing or unrecognized, or a value is out of range</exception>
        public static NoiseGeneratorRules Load(SimData data, IReadOnlyDictionary<string, double> overrides = null)
        {
            RulesFile r = data.LoadRules(Filename);
            if (overrides != null)
                foreach (KeyValuePair<string, double> setting in overrides) r.Override(setting.Key, setting.Value);
            NoiseGeneratorRules rules = new NoiseGeneratorRules();
            rules.LandFractionMean = r.D("LandFractionMean");
            rules.LandFractionStdDev = r.D("LandFractionStdDev");
            rules.LandFractionMin = r.D("LandFractionMin");
            rules.LandFractionMax = r.D("LandFractionMax");
            rules.LandFraction = r.D("LandFraction");
            rules.ContinentCentres = r.I("ContinentCentres");
            rules.RegionTarget = r.I("RegionTarget");
            rules.TemperatureOffset = r.D("TemperatureOffset");
            rules.Octaves = r.I("Octaves");
            rules.Frequency = r.D("Frequency");
            rules.Lacunarity = r.D("Lacunarity");
            rules.Persistence = r.D("Persistence");
            rules.WarpStrength = r.D("WarpStrength");
            rules.MaskStrength = r.D("MaskStrength");
            rules.MaskRadiusMin = r.D("MaskRadiusMin");
            rules.MaskRadiusMax = r.D("MaskRadiusMax");
            rules.EdgeBorder = r.D("EdgeBorder");
            rules.MinLandmassBlocks = r.I("MinLandmassBlocks");
            rules.MinSeaBlocks = r.I("MinSeaBlocks");
            rules.CoastDetail = r.I("CoastDetail");
            rules.DetailStrength = r.D("DetailStrength");
            rules.DetailRange = r.D("DetailRange");
            rules.DetailFrequency = r.D("DetailFrequency");
            rules.DetailOctaves = r.I("DetailOctaves");
            rules.DetailMinIsletCells = r.I("DetailMinIsletCells");
            rules.ClimateFrequency = r.D("ClimateFrequency");
            rules.ClimateOctaves = r.I("ClimateOctaves");
            rules.TemperatureLapse = r.D("TemperatureLapse");
            rules.TemperatureNoise = r.D("TemperatureNoise");
            rules.MoistureNoiseWeight = r.D("MoistureNoiseWeight");
            rules.MoistureCoastWeight = r.D("MoistureCoastWeight");
            rules.MoistureLatitudeWeight = r.D("MoistureLatitudeWeight");
            rules.MoistureReach = r.D("MoistureReach");
            rules.MoistureElevationGain = r.D("MoistureElevationGain");
            rules.MoistureColdPenalty = r.D("MoistureColdPenalty");
            rules.ContourFlatFrom = r.D("ContourFlatFrom");
            rules.ContourHillyFrom = r.D("ContourHillyFrom");
            rules.ContourJaggedFrom = r.D("ContourJaggedFrom");
            rules.ContourMountainousFrom = r.D("ContourMountainousFrom");
            rules.BiomeTemperatureWeight = r.D("BiomeTemperatureWeight");
            rules.BiomePrecipitationWeight = r.D("BiomePrecipitationWeight");
            rules.BiomeContourWeight = r.D("BiomeContourWeight");
            rules.TerrainReliefStrength = r.D("TerrainReliefStrength");
            rules.ElevationPerRelief = r.D("ElevationPerRelief");
            rules.RegionSeedCandidates = r.I("RegionSeedCandidates");
            rules.RegionSlopeWeight = r.D("RegionSlopeWeight");
            rules.RegionHeightWeight = r.D("RegionHeightWeight");
            rules.RegionClimateWeight = r.D("RegionClimateWeight");
            rules.RegionJitter = r.D("RegionJitter");
            rules.RegionMinBlocks = r.I("RegionMinBlocks");
            rules.RegionBorderDetail = r.I("RegionBorderDetail");
            rules.RegionBorderWarp = r.D("RegionBorderWarp");
            rules.RegionBorderFrequency = r.D("RegionBorderFrequency");
            r.Finish();

            Require(rules.LandFractionMin > 0 && rules.LandFractionMin <= rules.LandFractionMax && rules.LandFractionMax < 1,
                "LandFractionMin and LandFractionMax must satisfy 0 < min <= max < 1");
            Require(rules.LandFractionStdDev >= 0, "LandFractionStdDev must not be negative");
            Require(rules.LandFraction == 0 || (rules.LandFraction > 0 && rules.LandFraction < 1), "LandFraction must be 0 (random) or between 0 and 1");
            Require(rules.ContinentCentres >= 0 && rules.ContinentCentres <= 50, "ContinentCentres must be 0 (random) or between 1 and 50");
            Require(rules.RegionTarget >= 0 && rules.RegionTarget <= 1000, "RegionTarget must be 0 (random) or between 1 and 1000");
            Require(rules.TemperatureOffset >= -1 && rules.TemperatureOffset <= 1, "TemperatureOffset must be between -1 and 1");
            Require(rules.Octaves >= 1 && rules.Octaves <= 16, "Octaves must be between 1 and 16");
            Require(rules.Frequency > 0 && rules.Lacunarity > 0 && rules.Persistence > 0, "Frequency, Lacunarity and Persistence must be greater than 0");
            Require(rules.WarpStrength >= 0, "WarpStrength must not be negative");
            Require(rules.MaskStrength >= 0 && rules.MaskStrength <= 1, "MaskStrength must be between 0 and 1");
            Require(rules.MaskRadiusMin > 0 && rules.MaskRadiusMin <= rules.MaskRadiusMax, "MaskRadiusMin and MaskRadiusMax must satisfy 0 < min <= max");
            Require(rules.EdgeBorder > 0 && rules.EdgeBorder <= 0.5, "EdgeBorder must be greater than 0 and at most 0.5");
            Require(rules.MinLandmassBlocks >= 1 && rules.MinSeaBlocks >= 1, "MinLandmassBlocks and MinSeaBlocks must be at least 1");
            Require(rules.CoastDetail == 0 || rules.CoastDetail == 1, "CoastDetail must be 0 or 1");
            Require(rules.DetailStrength >= 0, "DetailStrength must not be negative");
            Require(rules.DetailRange > 0 && rules.DetailRange <= 1, "DetailRange must be greater than 0 and at most 1");
            Require(rules.DetailFrequency > 0, "DetailFrequency must be greater than 0");
            Require(rules.DetailOctaves >= 1 && rules.DetailOctaves <= 16, "DetailOctaves must be between 1 and 16");
            Require(rules.DetailMinIsletCells >= 1, "DetailMinIsletCells must be at least 1");
            Require(rules.ClimateFrequency > 0, "ClimateFrequency must be greater than 0");
            Require(rules.ClimateOctaves >= 1 && rules.ClimateOctaves <= 16, "ClimateOctaves must be between 1 and 16");
            Require(rules.TemperatureLapse >= 0 && rules.TemperatureNoise >= 0, "TemperatureLapse and TemperatureNoise must not be negative");
            Require(rules.MoistureNoiseWeight >= 0 && rules.MoistureCoastWeight >= 0 && rules.MoistureLatitudeWeight >= 0
                && rules.MoistureNoiseWeight + rules.MoistureCoastWeight + rules.MoistureLatitudeWeight > 0,
                "MoistureNoiseWeight, MoistureCoastWeight and MoistureLatitudeWeight must not be negative and must not all be 0");
            Require(rules.MoistureReach > 0, "MoistureReach must be greater than 0");
            Require(rules.MoistureElevationGain >= 0, "MoistureElevationGain must not be negative");
            Require(rules.MoistureColdPenalty >= 0 && rules.MoistureColdPenalty <= 1, "MoistureColdPenalty must be between 0 and 1");
            Require(rules.ContourFlatFrom >= 0 && rules.ContourFlatFrom <= rules.ContourHillyFrom
                && rules.ContourHillyFrom <= rules.ContourJaggedFrom && rules.ContourJaggedFrom <= rules.ContourMountainousFrom,
                "ContourFlatFrom, ContourHillyFrom, ContourJaggedFrom and ContourMountainousFrom must not be negative and must be in increasing order");
            Require(rules.BiomeTemperatureWeight >= 0 && rules.BiomePrecipitationWeight >= 0 && rules.BiomeContourWeight >= 0
                && rules.BiomeTemperatureWeight + rules.BiomePrecipitationWeight + rules.BiomeContourWeight > 0,
                "BiomeTemperatureWeight, BiomePrecipitationWeight and BiomeContourWeight must not be negative and must not all be 0");
            Require(rules.TerrainReliefStrength >= 0, "TerrainReliefStrength must not be negative");
            Require(rules.ElevationPerRelief > 0, "ElevationPerRelief must be greater than 0");
            Require(rules.RegionSeedCandidates >= 1, "RegionSeedCandidates must be at least 1");
            Require(rules.RegionSlopeWeight >= 0 && rules.RegionHeightWeight >= 0 && rules.RegionClimateWeight >= 0 && rules.RegionJitter >= 0,
                "RegionSlopeWeight, RegionHeightWeight, RegionClimateWeight and RegionJitter must not be negative");
            Require(rules.RegionMinBlocks >= 1, "RegionMinBlocks must be at least 1");
            Require(rules.RegionBorderDetail == 0 || rules.RegionBorderDetail == 1, "RegionBorderDetail must be 0 or 1");
            Require(rules.RegionBorderWarp >= 0 && rules.RegionBorderWarp <= 0.5, "RegionBorderWarp must be between 0 and 0.5");
            Require(rules.RegionBorderFrequency > 0, "RegionBorderFrequency must be greater than 0");
            return rules;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidDataException(Filename + ": " + message);
        }
    }
}
