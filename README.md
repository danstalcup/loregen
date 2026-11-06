# LoreGen / Worldbuilder

LoreGen generates fictional worlds: continents, seas and oceans, regions, biomes, terrain, and a distinct
procedurally generated language for each continent and region (so every place gets a name that sounds like it
belongs there). Worldbuilder is a browser app for exploring the results.

Worlds are reproducible: the same seed produces the same world, no matter how many other engines are running
or what language/region the machine is set to.

## What's in the repo

| Project | What it is |
| --- | --- |
| [`LoreGen/`](LoreGen) | The core library (`net10.0`). Default data files are embedded as resources. |
| [`LoreGen.Tests/`](LoreGen.Tests) | xunit tests. |
| [`Worldbuilder.Web/`](Worldbuilder.Web) | Blazor WebAssembly app: map, world info, language generator, raw data. |

## Getting started

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```
dotnet run --project Worldbuilder.Web
```

Open the URL it prints (usually http://localhost:5000). To publish a static site you can host anywhere:

```
dotnet publish Worldbuilder.Web -c Release
```

The site is written to `Worldbuilder.Web/bin/Release/net10.0/publish/wwwroot`.

Run the tests with:

```
dotnet test
```

## Using the Worldbuilder app

- **Map**: Click a block for details. Double-click a continent to zoom in, then a region. "Colour by" switches between
  general, continents, regions, biomes and terrains (plus elevation, for generators that model it). "High detail" shows the finer coastline and border blocks.
- **World info**: The seed, the oceans, and a table of regions (biome, language, size) for each continent.
- **Language generator**: Generate a random language, inspect its rules, and produce sample names and words.
- **Raw data**: The climate, terrain and biome data the simulation loaded.

Enter a seed and press **Generate** to recreate a world, or press **New world** for a random one. If a world fails
to generate, the app shows an error and keeps the previous world.

## Using the library

```csharp
using LoreGen.Simulation;

var engine = new SimEngine(12345);   // fixed seed; `new SimEngine()` uses loregen.config or a clock-based seed
engine.GenerateWorld();

foreach (var continent in engine.World.Continents)
    Console.WriteLine($"{continent.Name}: {continent.Regions.Count} regions");

var language = engine.Language.GetRandomLanguage();
Console.WriteLine(language.GetName());   // e.g. a place name in that language
```

`SimEngine` is the entry point. It owns the random number generator, the loaded data (`SimData`), and the generated
`World`. A world is a tree of `WorldBlock`s (a coarse grid whose blocks can hold finer sub-grids) that carry a
`Status` saying which continent, ocean, sea, region and terrain each block belongs to.

### How a world is built (classic generator)

1. Continents are grown from random seed blocks across the world grid.
2. Each continent's edge becomes coastline, and coastal blocks become water to form oceans.
3. Seas (up to one per continent, set by `WorldRules.csv`) are carved into each continent's own land and
   registered in `Continent.Seas`. Coasts are eroded, and coastline blocks get a finer sub-grid shaped by the
   patterns in `EdgePatterns.csv`.
4. Each continent is divided into regions. A region's biome depends on its latitude, and its terrain is drawn
   from weighted patterns in the data files.
5. Every continent has a root language. Each region gets its own copy of it, shifted slightly, which names the
   region. Changing one region's language doesn't affect the others.

## World generators

How land and water are shaped is pluggable. A generator is any class implementing
[`IWorldGenerator`](LoreGen/WorldGen/Generators/IWorldGenerator.cs): given a `SimEngine`, it returns a complete `World`.
The interface's documentation is the contract: what a world must contain, and the rules for implementers.

| Id | Name | |
| --- | --- | --- |
| `legacy` | Classic | The original algorithm, described above. The default. |
| `noise` | Noise | Land comes from an elevation map of fractal noise. See below. |

#### The noise generator

1. An elevation value is computed for every block from fractal noise, bent by domain warping so coastlines wind. It is
   pulled up around a few randomly placed continent centres and down toward the map edge, so there is always an ocean
   border.
2. Sea level is chosen so that a target share of the map is land (about a third by default, varying per world).
3. Specks of land too small to be an island are removed, and puddles too small to be a sea are filled in.
4. Continents are the connected pieces of land that remain, so the number of continents is a target, not a promise.
   Enclosed bodies of water become seas of the continent they touch most, and the rest is divided into oceans.
5. Each continent is divided into connected regions, with a biome, language and terrain, as in the classic generator.
6. Every block records its `Elevation` (0 at sea level, up to 1 on the highest land, down to -1 in the deepest water),
   which the map shows under "Colour by" -> Elevation.

Its settings are in `NoiseGeneratorRules.csv`; world size and the number of continents and regions come from
`WorldRules.csv`. The noise building blocks it uses (`SimplexNoise`, `FractalNoise`, `Heightmap`) are in
[`LoreGen/Noise`](LoreGen/Noise). Coastlines are currently as coarse as the world grid; finer detail is planned.

**Choosing one.** Set `engine.Configuration.GeneratorId`, or `generatormode=<id>` in `loregen.config`. Ids are
case-insensitive, and an unknown id fails with a message listing the available ones. The Worldbuilder app has a
Generator dropdown filled from the registry. A world records the `GeneratorId` and `GeneratorVersion` that made it, and
the seed, generator id and generator version together identify a world.

**Adding one.**

```csharp
public class MyGenerator : IWorldGenerator
{
    public string Id => "mine";
    public string DisplayName => "My generator";
    public string Description => "What makes its worlds different.";
    public string Version => "1";            // bump when a seed would give a different world

    public World Generate(SimEngine engine)
    {
        var world = new World(engine);
        // ... fill in world.WorldBlock, Continents, Oceans, Regions using engine.Rnd for all randomness ...
        return world;
    }
}

engine.Generators.Register(new MyGenerator());      // selectable by id (config file, dropdown)
engine.Configuration.GeneratorId = "mine";
// or skip the registry:  engine.WorldGenerator = new MyGenerator();
```

To ship a generator with LoreGen, add it to `WorldGeneratorRegistry.CreateDefault()`. That automatically puts it through
the contract tests in `GeneratorContractTests`. Generators keep their own settings in their own keyed rules file
(`engine.SimData.LoadRules("MyGeneratorRules.csv")`) so they never need to change the shared ones. If a generator throws,
`engine.World` keeps the previous world.

## Tests

`dotnet test` runs the suite in [`LoreGen.Tests`](LoreGen.Tests). Besides basic loading and generation, it checks:

- generation succeeds across 150 seeds, and the same seed gives the same world even when other engines are created in between
- data loads correctly under a comma-decimal culture (`de-DE`)
- rule files accept reordered rows and report missing or unrecognized keys
- regions have their own languages, and seas are registered on their continent
- the noise generator: connected continents that never touch, an ocean border, a land share that follows its settings,
  consistent elevation, seas and oceans, and connected regions
- every registered generator meets the generator contract (see World generators): well-formed worlds, the same seed
  gives the same world, independence from other engines, and reusability
- the default generator is `legacy`, unknown ids are reported, and a failed generation keeps the previous world

## Data files

World and language behaviour is driven by data, not code. LoreGen reads its files (`*.csv`, `*.txt`) from a data
folder on disk if one exists, and otherwise from the copies embedded in the assembly
([`LoreGen/Resources`](LoreGen/Resources)). To customize, copy the files you want to change into a folder and point the
`datafolder` setting at it. Files you leave out fall back to the defaults. (The browser app always uses the embedded
copies.)

| File | Controls |
| --- | --- |
| `WorldRules.csv` | World size, number of continents and regions, seas, erosion |
| `LanguageRules.csv` | How languages are generated and how much regional languages drift |
| `TerrainRules.csv` | How terrain weights combine |
| `ClimateTypes.csv`, `Climates.csv` | Climate definitions |
| `TerrainTypes.csv`, `Terrains.csv` | Terrain definitions |
| `Biomes.txt`, `BiomePatterns.csv` | Biomes and which climates and terrains they contain |
| `NoiseGeneratorRules.csv` | Settings for the noise generator: land share, noise detail, coastline winding, island size |
| `EdgePatterns.csv` | Hand-drawn coastline and border shapes |
| `LetterSounds.txt`, `PhoneticInventories.csv` | The sounds and spellings languages are built from |

The three `*Rules.csv` files use a `Key,Value,Description` format. Rows can appear in any order, `#` starts a comment,
decimals always use `.`, and a missing or misspelled key fails with a message naming the file and key. The full format
of every file is documented in [`LoreGen/Resources/README.txt`](LoreGen/Resources/README.txt).

### Configuration file

`new SimEngine()` reads an optional `loregen.config` file of `name=value` lines:

| Setting | Meaning |
| --- | --- |
| `datafolder` | Folder to read data files from (default `Default`) |
| `usepresetseed` | `true` to use `presetseed` instead of a clock-based seed |
| `presetseed` | The seed to use |
| `generatormode` | Id of the world generator to use (default `legacy`) |

## Roadmap

The noise generator is being built up in stages. Land, water, continents, seas and oceans work now. Still to come:
finer coastline detail when zooming in, biomes derived from climate (temperature and moisture) instead of latitude alone,
and regions that follow the terrain instead of growing at random.
