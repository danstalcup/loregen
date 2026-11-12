using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

using LoreGen.Randomizer;
using LoreGen.WorldGen;
using LoreGen.WorldGen.Generators;
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
        /// Random number generator for languages and names, seeded from <see cref="Rnd"/>'s seed. Keeping naming on its own
        /// stream means the shape of a world does not depend on how its places are named: the same seed gives the same
        /// land whichever <see cref="SimEngineConfiguration.LanguageStyle"/> is used.
        /// </summary>
        public Rnd NameRnd;
        /// <summary>
        /// World simulated
        /// </summary>
        public World World;
        /// <summary>
        /// Reads and stores info for this simulation
        /// </summary>
        public SimData SimData;

        /// <summary>
        /// The world generators this engine can use. Starts with the ones that ship with LoreGen; register your own to
        /// make them selectable by Id.
        /// </summary>
        public WorldGeneratorRegistry Generators;

        /// <summary>
        /// If set, this generator is used instead of the one named by Configuration.GeneratorId.
        /// </summary>
        public IWorldGenerator WorldGenerator;

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
        public SimEngine(string ConfigFilepath="loregen.config") : this(ConfigFilepath, null)
        {
        }

        /// <summary>
        /// Initializes the SimEngine with a specific random seed (no configuration file is needed)
        /// </summary>
        /// <param name="Seed">seed for the random number generator, or null for a clock-based seed</param>
        public SimEngine(int? Seed) : this(null, Seed)
        {
        }

        private SimEngine(string ConfigFilepath, int? Seed)
        {
            Configuration = new SimEngineConfiguration(ConfigFilepath);
            if (ConfigFilepath != null) Configuration.Configure();
            if (Seed.HasValue)
            {
                Configuration.UsePresetSeed = true;
                Configuration.PresetSeed = Seed.Value;
            }

            if (Configuration.UsePresetSeed)
            {
                Rnd = new Rnd(Configuration.PresetSeed);
            }
            else
            {
                Rnd = new Rnd();
            }
            NameRnd = new Rnd(unchecked(Rnd.Seed * 486187739 + 0x4E414D45));
            Generators = WorldGeneratorRegistry.CreateDefault();
            SimData = new SimData(this);
            SimData.Initialize();
            Language = new SimEngineLanguageTasks(this);            
        }

        /// <summary>
        /// Generates a world using the generator named in the configuration (or <see cref="WorldGenerator"/> if set).
        /// If generation fails, the exception propagates and <see cref="World"/> keeps whatever it held before.
        /// </summary>
        public void GenerateWorld()
        {
            IWorldGenerator generator = WorldGenerator ?? Generators.Get(Configuration.GeneratorId);
            Language.Names.Clear();
            Finish(generator, generator.Generate(this));
        }

        /// <summary>
        /// Starts generating a world with the generator named in the configuration (or <see cref="WorldGenerator"/> if set),
        /// to be run one step at a time with <see cref="WorldGeneration.RunNextStep"/>. Gives the same world as
        /// <see cref="GenerateWorld"/>. <see cref="World"/> changes only once the last step has run.
        /// </summary>
        public WorldGeneration BeginWorldGeneration()
        {
            IWorldGenerator generator = WorldGenerator ?? Generators.Get(Configuration.GeneratorId);
            Language.Names.Clear();
            WorldBuildPlan plan = generator.Plan(this);
            if (plan == null)
                throw new InvalidOperationException("World generator '" + generator.Id + "' returned no plan.");
            return new WorldGeneration(plan, world => Finish(generator, world));
        }

        private void Finish(IWorldGenerator generator, World generated)
        {
            if (generated == null)
                throw new InvalidOperationException("World generator '" + generator.Id + "' returned no world.");
            generated.GeneratorId = generator.Id;
            generated.GeneratorVersion = generator.Version;
            generated.GeneratorSettings = new Dictionary<string, double>(Configuration.GeneratorSettings, StringComparer.OrdinalIgnoreCase);
            generated.LanguageStyle = Configuration.LanguageStyle;
            World = generated;
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
        /// Names given out in the current world. Cleared whenever a world is generated.
        /// </summary>
        public NameRegistry Names { get; } = new NameRegistry();

        /// <summary>
        /// Returns a random language in the style set by <see cref="SimEngineConfiguration.LanguageStyle"/>.
        /// </summary>
        /// <returns>A randomly generated language</returns>
        public ILanguage GetRandomLanguage()
        {
            return GetRandomLanguage(SimEngine.Configuration.LanguageStyle);
        }

        /// <summary>
        /// Returns a random language in the given style.
        /// </summary>
        public ILanguage GetRandomLanguage(LanguageStyle style)
        {
            switch (style)
            {
                case LanguageStyle.Phonetic: return GetRandomPhoneticLanguage();
                case LanguageStyle.Common: return GetRandomCommonLanguage();
            }
            throw new ArgumentOutOfRangeException("style", style, "Unknown language style.");
        }

        /// <summary>
        /// Returns a random language with a random phonetic vocabulary and random rules
        /// </summary>
        public PhoneticLanguage GetRandomPhoneticLanguage()
        {
            return PhoneticLanguage.RandomLanguageWithPhoneticInventory(ListR<PhoneticInventory>.RandomFromList(Data.PhoneticInventories, SimEngine.NameRnd), SimEngine);
        }

        /// <summary>
        /// Returns a random blend of the naming flavours in "CommonNameFlavors.csv"
        /// </summary>
        public CommonLanguage GetRandomCommonLanguage()
        {
            return CommonLanguage.Random(SimEngine);
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
        /// Id of the world generator to use ("generatormode" in the config file). Resolved when a world is generated.
        /// </summary>
        public string GeneratorId = WorldGeneratorRegistry.DefaultId;

        /// <summary>
        /// Settings for the world generator, by name (see IWorldGenerator.GetOptions for what a generator offers). In the
        /// config file, write them as "setting.Name=value". Names are not case-sensitive.
        /// </summary>
        public Dictionary<string, double> GeneratorSettings = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// How the world's places are named ("languagestyle" in the config file): Common (the default) or Phonetic.
        /// </summary>
        public LanguageStyle LanguageStyle = LanguageStyle.Common;

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
            DataFolder = "Resources";
            Version = "0.12";
            UsePresetSeed = false;
            PresetSeed = 0;
            this.ConfigFilepath = ConfigFilepath;
        }

        /// <summary>
        /// Resolves a (possibly relative) path against the running assembly's base directory, so data/config
        /// lookups don't depend on the process's current working directory.
        /// </summary>
        private static string ResolveAgainstBaseDirectory(string path)
        {
            return Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path);
        }

        /// <summary>
        /// Loads and processes the configuration file
        /// </summary>
        public void Configure()
        {
            try
            {
                string[] ConfigLines = File.ReadAllLines(ResolveAgainstBaseDirectory(ConfigFilepath));
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
                    if (ConfigLineData[0].Trim().StartsWith("setting.", StringComparison.OrdinalIgnoreCase))
                    {
                        GeneratorSettings[ConfigLineData[0].Trim().Substring("setting.".Length)] =
                            double.Parse(ConfigLineData[1].Trim(), System.Globalization.CultureInfo.InvariantCulture);
                    }
                    if (ConfigLineData[0].Trim().ToLower() == "generatormode")
                    {
                        GeneratorId = ConfigLineData[1].Trim();
                    }
                    if (ConfigLineData[0].Trim().ToLower() == "languagestyle")
                    {
                        LanguageStyle = (LanguageStyle)Enum.Parse(typeof(LanguageStyle), ConfigLineData[1].Trim(), true);
                    }
                    if (ConfigLineData[0].Trim().ToLower() == "presetseed")
                    {
                        PresetSeed = int.Parse(ConfigLineData[1].Trim(), System.Globalization.CultureInfo.InvariantCulture);
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
