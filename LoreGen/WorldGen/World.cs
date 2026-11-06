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
        /// Id of the world generator that produced this world (see IWorldGenerator.Id). Set by SimEngine.
        /// </summary>
        public string GeneratorId { get; internal set; }

        /// <summary>
        /// Version of the world generator that produced this world. The seed, GeneratorId and GeneratorVersion together
        /// identify the world. Set by SimEngine.
        /// </summary>
        public string GeneratorVersion { get; internal set; }

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
