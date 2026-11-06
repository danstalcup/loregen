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

        public const string Filename = "NoiseGeneratorRules.csv";

        /// <summary>
        /// Loads and validates the settings.
        /// </summary>
        /// <exception cref="InvalidDataException">A key is missing or unrecognized, or a value is out of range</exception>
        public static NoiseGeneratorRules Load(SimData data)
        {
            RulesFile r = data.LoadRules(Filename);
            NoiseGeneratorRules rules = new NoiseGeneratorRules();
            rules.LandFractionMean = r.D("LandFractionMean");
            rules.LandFractionStdDev = r.D("LandFractionStdDev");
            rules.LandFractionMin = r.D("LandFractionMin");
            rules.LandFractionMax = r.D("LandFractionMax");
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
            r.Finish();

            Require(rules.LandFractionMin > 0 && rules.LandFractionMin <= rules.LandFractionMax && rules.LandFractionMax < 1,
                "LandFractionMin and LandFractionMax must satisfy 0 < min <= max < 1");
            Require(rules.LandFractionStdDev >= 0, "LandFractionStdDev must not be negative");
            Require(rules.Octaves >= 1 && rules.Octaves <= 16, "Octaves must be between 1 and 16");
            Require(rules.Frequency > 0 && rules.Lacunarity > 0 && rules.Persistence > 0, "Frequency, Lacunarity and Persistence must be greater than 0");
            Require(rules.WarpStrength >= 0, "WarpStrength must not be negative");
            Require(rules.MaskStrength >= 0 && rules.MaskStrength <= 1, "MaskStrength must be between 0 and 1");
            Require(rules.MaskRadiusMin > 0 && rules.MaskRadiusMin <= rules.MaskRadiusMax, "MaskRadiusMin and MaskRadiusMax must satisfy 0 < min <= max");
            Require(rules.EdgeBorder > 0 && rules.EdgeBorder <= 0.5, "EdgeBorder must be greater than 0 and at most 0.5");
            Require(rules.MinLandmassBlocks >= 1 && rules.MinSeaBlocks >= 1, "MinLandmassBlocks and MinSeaBlocks must be at least 1");
            return rules;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidDataException(Filename + ": " + message);
        }
    }
}
