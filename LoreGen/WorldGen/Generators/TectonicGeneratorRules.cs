using System.Collections.Generic;
using System.IO;

using LoreGen.Simulation;

namespace LoreGen.WorldGen.Generators
{
    /// <summary>
    /// Plate settings for <see cref="TectonicWorldGenerator"/>, read from TectonicGeneratorRules.csv. (Everything after
    /// the elevation uses <see cref="NoiseGeneratorRules"/>.)
    /// </summary>
    public struct TectonicGeneratorRules
    {
        public int Plates;
        public int PlatesMin;
        public int PlatesMax;
        public double PlateSizeVariety;
        public double PlateBoundaryRoughness;
        public double ContinentalShare;
        public double ContinentalCoverMin;
        public double ContinentalCoverMax;
        public double DriftSpeedMin;
        public double BoundaryThreshold;
        public double ContinentalBase;
        public int CrustBlur;
        public double CollisionUplift;
        public double CollisionWidth;
        public double SubductionUplift;
        public double SubductionWidth;
        public double SubductionInland;
        public double TrenchDepth;
        public double IslandArcUplift;
        public double RidgeHeight;
        public double RiftDepth;
        public int UpliftReach;
        public double TerrainNoise;
        public double TerrainNoiseFrequency;
        public int TerrainNoiseOctaves;
        public double TerrainNoiseWarp;

        public const string Filename = "TectonicGeneratorRules.csv";

        /// <summary>
        /// Loads and validates the settings, with any user-supplied settings replacing the file's values.
        /// </summary>
        /// <exception cref="InvalidDataException">A key is missing or unrecognized, or a value is out of range</exception>
        public static TectonicGeneratorRules Load(SimData data, IReadOnlyDictionary<string, double> overrides = null)
        {
            RulesFile r = data.LoadRules(Filename);
            if (overrides != null)
                foreach (KeyValuePair<string, double> setting in overrides) r.Override(setting.Key, setting.Value);
            TectonicGeneratorRules rules = new TectonicGeneratorRules();
            rules.Plates = r.I("Plates");
            rules.PlatesMin = r.I("PlatesMin");
            rules.PlatesMax = r.I("PlatesMax");
            rules.PlateSizeVariety = r.D("PlateSizeVariety");
            rules.PlateBoundaryRoughness = r.D("PlateBoundaryRoughness");
            rules.ContinentalShare = r.D("ContinentalShare");
            rules.ContinentalCoverMin = r.D("ContinentalCoverMin");
            rules.ContinentalCoverMax = r.D("ContinentalCoverMax");
            rules.DriftSpeedMin = r.D("DriftSpeedMin");
            rules.BoundaryThreshold = r.D("BoundaryThreshold");
            rules.ContinentalBase = r.D("ContinentalBase");
            rules.CrustBlur = r.I("CrustBlur");
            rules.CollisionUplift = r.D("CollisionUplift");
            rules.CollisionWidth = r.D("CollisionWidth");
            rules.SubductionUplift = r.D("SubductionUplift");
            rules.SubductionWidth = r.D("SubductionWidth");
            rules.SubductionInland = r.D("SubductionInland");
            rules.TrenchDepth = r.D("TrenchDepth");
            rules.IslandArcUplift = r.D("IslandArcUplift");
            rules.RidgeHeight = r.D("RidgeHeight");
            rules.RiftDepth = r.D("RiftDepth");
            rules.UpliftReach = r.I("UpliftReach");
            rules.TerrainNoise = r.D("TerrainNoise");
            rules.TerrainNoiseFrequency = r.D("TerrainNoiseFrequency");
            rules.TerrainNoiseOctaves = r.I("TerrainNoiseOctaves");
            rules.TerrainNoiseWarp = r.D("TerrainNoiseWarp");
            r.Finish();

            Require(rules.Plates >= 0 && rules.Plates <= 100, "Plates must be 0 (random) or between 1 and 100");
            Require(rules.PlatesMin >= 1 && rules.PlatesMin <= rules.PlatesMax && rules.PlatesMax <= 100, "PlatesMin and PlatesMax must satisfy 1 <= min <= max <= 100");
            Require(rules.PlateSizeVariety >= 0 && rules.PlateSizeVariety <= 1, "PlateSizeVariety must be between 0 and 1");
            Require(rules.PlateBoundaryRoughness >= 0, "PlateBoundaryRoughness must not be negative");
            Require(rules.ContinentalShare > 0 && rules.ContinentalShare < 1, "ContinentalShare must be between 0 and 1");
            Require(rules.ContinentalCoverMin > 0 && rules.ContinentalCoverMin <= rules.ContinentalCoverMax && rules.ContinentalCoverMax <= 1,
                "ContinentalCoverMin and ContinentalCoverMax must satisfy 0 < min <= max <= 1");
            Require(rules.DriftSpeedMin >= 0 && rules.DriftSpeedMin <= 1, "DriftSpeedMin must be between 0 and 1");
            Require(rules.BoundaryThreshold >= 0, "BoundaryThreshold must not be negative");
            Require(rules.ContinentalBase > 0, "ContinentalBase must be greater than 0");
            Require(rules.CrustBlur >= 0 && rules.CrustBlur <= 5, "CrustBlur must be between 0 and 5");
            Require(rules.CollisionUplift >= 0 && rules.SubductionUplift >= 0 && rules.IslandArcUplift >= 0 && rules.RidgeHeight >= 0
                && rules.TrenchDepth >= 0 && rules.RiftDepth >= 0, "Uplifts, heights and depths must not be negative");
            Require(rules.CollisionWidth > 0 && rules.SubductionWidth > 0, "CollisionWidth and SubductionWidth must be greater than 0");
            Require(rules.SubductionInland >= 0, "SubductionInland must not be negative");
            Require(rules.UpliftReach >= 1 && rules.UpliftReach <= 10, "UpliftReach must be between 1 and 10");
            Require(rules.TerrainNoise >= 0 && rules.TerrainNoiseWarp >= 0, "TerrainNoise and TerrainNoiseWarp must not be negative");
            Require(rules.TerrainNoiseFrequency > 0, "TerrainNoiseFrequency must be greater than 0");
            Require(rules.TerrainNoiseOctaves >= 1 && rules.TerrainNoiseOctaves <= 16, "TerrainNoiseOctaves must be between 1 and 16");
            return rules;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidDataException(Filename + ": " + message);
        }
    }
}
