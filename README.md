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
  general, continents, regions, biomes and terrains. "High detail" shows the finer coastline and border blocks.
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

### How a world is built

1. Continents are grown from random seed blocks across the world grid.
2. Each continent's edge becomes coastline, and coastal blocks become water to form oceans.
3. Seas (up to one per continent, set by `WorldRules.csv`) are carved into each continent's own land and
   registered in `Continent.Seas`. Coasts are eroded, and coastline blocks get a finer sub-grid shaped by the
   patterns in `EdgePatterns.csv`.
4. Each continent is divided into regions. A region's biome depends on its latitude, and its terrain is drawn
   from weighted patterns in the data files.
5. Every continent has a root language. Each region gets its own copy of it, shifted slightly, which names the
   region. Changing one region's language doesn't affect the others.

### Generator mode

`SimEngine.Configuration.GeneratorMode` (or `generatormode=` in `loregen.config`) selects the land generator.
`Legacy` is the default and is the algorithm described above. `Noise` is reserved for an upcoming height-field
generator and currently throws `NotImplementedException`.

## Tests

`dotnet test` runs the suite in [`LoreGen.Tests`](LoreGen.Tests). Besides basic loading and generation, it checks:

- generation succeeds across 150 seeds, and the same seed gives the same world even when other engines are created in between
- data loads correctly under a comma-decimal culture (`de-DE`)
- rule files accept reordered rows and report missing or unrecognized keys
- regions have their own languages, and seas are registered on their continent
- the default generator is `Legacy`, and an unimplemented one fails without leaving a half-built world

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
| `generatormode` | `Legacy` or `Noise` |

## Roadmap

The land generator is being replaced with a height-field one based on fractal noise (elevation, climate and biomes
derived from the same fields, with regions that follow the terrain). `GeneratorMode` is the switch that lets the old
and new generators coexist while that is built.
