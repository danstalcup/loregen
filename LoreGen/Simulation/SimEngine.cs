using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

using LoreGen.Randomizer;
using LoreGen.WorldGen;
using LoreGen.Lang;

namespace LoreGen.Simulation
{
    /// <summary>
    /// Runs the simulation. Conduit for sim info. Start here.
    /// </summary>
    public class SimEngine
    {
        /// <summary>
        /// Random number generater
        /// </summary>
        public Rnd Rnd;
        /// <summary>
        /// World simulated
        /// </summary>
        public World World;
        /// <summary>
        /// Reads and stores info for this simulation
        /// </summary>
        public SimData SimData;

        /// <summary>
        /// Access to Language-related tasks        
        /// </summary>
        public SimEngineLanguageTasks Language;

        public SimEngineConfiguration Configuration;

        public const string Version = "0.1";

        /// <summary>
        /// Creates and returns a SimEngine, including loading all data and generating World
        /// </summary>
        /// <returns>an initialized SimEngine</returns>
        public static SimEngine QuickStart()
        {            
            SimEngine simEngine = new SimEngine();
            simEngine.GenerateWorld();                        
            return simEngine;
        }

        public SimEngine(string ConfigFilepath="loregen.config")
        {
            Configuration = new SimEngineConfiguration(ConfigFilepath);
            Configuration.Configure();

            if (Configuration.UsePresetSeed)
            {
                Rnd = new Rnd(Configuration.PresetSeed);
            }
            else
            {
                Rnd = new Rnd();
            }
            SimData = new SimData(this);
            SimData.Initialize();
            Language = new SimEngineLanguageTasks(this);            
        }

        public void GenerateWorld()
        {
            World = World.GenerateWorld(this);
        }
        
        
    }

    /// <summary>
    /// Repository of tasks relating to Language for interfaces to use
    /// </summary>
    public class SimEngineLanguageTasks
    {
        SimEngine SimEngine;
        SimData Data
        {
            get
            {
                return SimEngine.SimData;
            }
        }
        /// <summary>
        /// Constructor for the repository
        /// </summary>
        /// <param name="SimEngine">Current simulation</param>
        public SimEngineLanguageTasks(SimEngine SimEngine)
        {
            this.SimEngine = SimEngine;
        }
        /// <summary>
        /// Returns a random language with a random phonetic vocabulary and random rules
        /// </summary>
        /// <returns>A randomly generated language</returns>
        public Language GetRandomLanguage()
        {
            return Language.RandomLanguageWithPhoneticInventory(ListR<PhoneticInventory>.RandomFromList(SimEngine.SimData.PhoneticInventories, SimEngine.Rnd), SimEngine);
        }        
    }

    public class SimEngineConfiguration
    {
        public bool ConfigFileSuccessfullyRead;
        public string DataFolder;
        public string Version;
        public bool UsePresetSeed;
        public int PresetSeed;
        public string ConfigFilepath;

        public SimEngineConfiguration(string ConfigFilepath)
        {
            ConfigFileSuccessfullyRead = false;
            DataFolder = "Default";
            Version = "0.1";
            UsePresetSeed = false;
            PresetSeed = 0;
            this.ConfigFilepath = ConfigFilepath;
        }

        public void Configure()
        {            
            try
            {
                string[] ConfigLines = File.ReadAllLines(ConfigFilepath);
                foreach (string ConfigLine in ConfigLines)
                {
                    string[] ConfigLineData = ConfigLine.Trim().Split(new char[] { '=' });
                    if (ConfigLineData[0].Trim().ToLower() == "version")
                    {
                        Version = ConfigLineData[1].Trim();
                    }
                    if (ConfigLineData[0].Trim().ToLower() == "datafolder")
                    {
                        DataFolder = ConfigLineData[1].Trim();
                    }
                    if (ConfigLineData[0].Trim().ToLower() == "usepresetseed")
                    {
                        UsePresetSeed = Convert.ToBoolean(ConfigLineData[1].Trim());
                    }
                    if (ConfigLineData[0].Trim().ToLower() == "presetseed")
                    {
                        PresetSeed = Convert.ToInt32(ConfigLineData[1].Trim());
                    }
                }
                ConfigFileSuccessfullyRead = true;
            }
            catch
            {
                ConfigFileSuccessfullyRead = false;
            }
        }
        
    }
}
