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

        /// <summary>
        /// Access to configuration, read from "loregen.config"
        /// </summary>
        public SimEngineConfiguration Configuration;

        /// <summary>
        /// Current version
        /// </summary>
        public const string Version = "0.1";

        /// <summary>
        /// Creates and returns a SimEngine, including loading all data and generating World
        /// </summary>
        /// <returns>an initialized SimEngine, default configuration, with a generated world</returns>
        public static SimEngine QuickStart()
        {            
            SimEngine simEngine = new SimEngine();
            simEngine.GenerateWorld();                        
            return simEngine;
        }

        /// <summary>
        /// Initializes the SimEngine and loads configuration data
        /// </summary>
        /// <param name="ConfigFilepath">filepath of configuration file</param>
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

        /// <summary>
        /// Generates a world using the current configuration
        /// </summary>
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
        /// <summary>
        /// True if the configuration file has been read without error, false otherwise
        /// </summary>
        public bool ConfigFileSuccessfullyRead;

        /// <summary>
        /// Folder from which the simulation reads data files
        /// </summary>
        public string DataFolder;

        /// <summary>
        /// Current version, as defined in the configuration file
        /// </summary>
        public string Version;

        /// <summary>
        /// True if the simulation is using a preset seed for randomization
        /// </summary>
        public bool UsePresetSeed;

        /// <summary>
        /// Seed used by the randomizer (if UsePresetSeed == true)
        /// </summary>
        public int PresetSeed;

        /// <summary>
        /// File path of the configuration file
        /// </summary>
        public string ConfigFilepath;

        /// <summary>
        /// Create the configuration object and set the path of the config file. (This does not actually load and read the configuration file.)
        /// </summary>
        /// <param name="ConfigFilepath">path of the config file</param>
        public SimEngineConfiguration(string ConfigFilepath)
        {
            ConfigFileSuccessfullyRead = false;
            DataFolder = "Default";
            Version = "0.12";
            UsePresetSeed = false;
            PresetSeed = 0;
            this.ConfigFilepath = ConfigFilepath;
        }


        /// <summary>
        /// Loads and processes the configuration file
        /// </summary>
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
