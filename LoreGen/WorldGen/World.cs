using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LoreGen.Randomizer;
using LoreGen.Simulation;

namespace LoreGen.WorldGen
{
    /// <summary>
    /// A simulated world
    /// </summary>
    public class World : WorldEntity
    {
        /// <summary>
        /// World name
        /// </summary>
        public SimEngine SimEngine;
        /// <summary>
        /// Collection of the world's continents
        /// </summary>
        public List<Continent> Continents;
        
        /// <summary>
        /// Collection of the world's oceans
        /// </summary>
        public List<Ocean> Oceans;

        /// <summary>
        /// Block encompassing the whole simulated world
        /// </summary>
        public WorldBlock WorldBlock;

        /// <summary>
        /// The plates of the world's crust, for generators that model plate tectonics (empty for those that don't).
        /// </summary>
        public List<TectonicPlate> Plates = new List<TectonicPlate>();

        private readonly List<Features.GeographicFeature> features = new List<Features.GeographicFeature>();
        private Features.FeatureIndex featureIndex;

        /// <summary>
        /// The world's geographic features (mountain ranges, deserts, lakes...), in the order they were added. A world may
        /// have none. Add features with <see cref="AddFeature"/>.
        /// </summary>
        public IReadOnlyList<Features.GeographicFeature> Features { get { return features; } }

        /// <summary>
        /// Adds a geographic feature to the world.
        /// </summary>
        /// <exception cref="ArgumentException">The feature belongs to another world or was already added</exception>
        public void AddFeature(Features.GeographicFeature feature)
        {
            if (feature == null) throw new ArgumentNullException("feature");
            if (feature.World != this) throw new ArgumentException("That feature belongs to another world.", "feature");
            if (features.Contains(feature)) throw new ArgumentException("That feature was already added.", "feature");
            features.Add(feature);
            featureIndex = null;
        }

        /// <summary>
        /// The features covering a block, at any depth, in the order they were added. For a block coarser than a feature's
        /// scope, a feature counts if any of it lies inside the block (see <see cref="Features.FeatureFootprint.Contains"/>).
        /// </summary>
        public IReadOnlyList<Features.GeographicFeature> FeaturesAt(WorldBlock block)
        {
            if (block == null || features.Count == 0) return Array.Empty<Features.GeographicFeature>();
            if (featureIndex == null) featureIndex = new Features.FeatureIndex(features);
            return featureIndex.At(block);
        }

        /// <summary>
        /// Id of the world generator that produced this world (see IWorldGenerator.Id). Set by SimEngine.
        /// </summary>
        public string GeneratorId { get; internal set; }

        /// <summary>
        /// Version of the world generator that produced this world. The seed, GeneratorId and GeneratorVersion together
        /// identify the world. Set by SimEngine.
        /// </summary>
        public string GeneratorVersion { get; internal set; }

        /// <summary>
        /// The generator settings this world was made with (empty if the generator's defaults were used). The seed, generator
        /// Id and Version, and these settings together identify the world. Set by SimEngine.
        /// </summary>
        public IReadOnlyDictionary<string, double> GeneratorSettings { get; internal set; } = new Dictionary<string, double>();

        /// <summary>
        /// How the places in this world were named.
        /// </summary>
        public Lang.LanguageStyle LanguageStyle { get; internal set; }

        /// <summary>
        /// World-wide language, used to name the oceans and the world itself.
        /// </summary>
        public Lang.ILanguage Language;

        /// <summary>
        /// Just does basic initialization. To generate a world, use SimEngine.GenerateWorld
        /// </summary>
        /// <param name="SimEngine">Sim engine</param>
        public World(SimEngine SimEngine)
        {
            this.SimEngine = SimEngine;
            this.WorldEntityType = WorldGen.WorldEntityType.World;
            DisplayInfo = new WorldDisplay.WorldEntityDisplayInfo();
            Oceans = new List<Ocean>();
            Continents = new List<Continent>();
            Language = SimEngine.Language.GetRandomLanguage();
            Name = Language.GetName(Lang.NameKind.World);
        }
    }

    public struct WorldRules
    {
        public int ContinentsMin;
        public int ContinentsMax;
        public double WorldLengthMean;
        public double WorldLengthStdDev;
        public int WorldLengthMin;
        public int WorldLengthMax;
        public double WorldHeightLengthRatio;
        public double SplitBlockMinimumPercentage;
        public double RegionsMean;
        public double RegionsStdDev;
        public int RegionsMin;
        public int RegionsMax;
        public int ZoomScaleLarge;
        public int ZoomScaleSmall;
        public double CoastlineBorderMean;
        public double CoastlineBorderStdDev;
        public double CoastlineBorderMin;
        public double CoastlineBorderMax;
        public int SeasPerContinentMin;
        public int SeasPerContinentMax;
        public double ContinentPerSeaMin;
        public double ContinentPerSeaMax;
        public double ContinentInSeasTotalMin;
        public double ContinentInSeasTotalMax;
        public double MediterraneanSeaProbability;
        public double ContinentCoastErosionMin;
        public double ContinentCoastErosionMax;
    }

    public enum Directions
    {
        North, East, South, West
    }

}
