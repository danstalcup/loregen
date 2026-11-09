# LoreGen / Worldbuilder

LoreGen generates fictional worlds: continents, seas and oceans, regions, biomes, terrain, and a distinct
procedurally generated language for each continent and region (so every place gets a name that sounds like it
belongs there, such as "Thurwold", "Zandabad" or "the Sea of Storms"). Worldbuilder is a browser app for exploring the results.

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

- **Options**: A panel under the header with the selected generator's settings, grouped, with sliders and checkboxes
  (the classic generator has none). Changed options are highlighted, each has a reset button, and **Reset all** clears
  them. They apply the next time you press Generate or New world, and World info lists the ones a world was made with.

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
Console.WriteLine(language.GetName(NameKind.Region));   // e.g. "Aldermere" or "the Ashen Fens"
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
5. Every continent has a root language. Each region gets a dialect of it (`CreateDialect`), which names the
   region. Changing one region's language doesn't affect the others.

## Languages and names

Every language implements [`ILanguage`](LoreGen/Lang/ILanguage.cs): `GetName` and `GetWord` take a
[`NameContext`](LoreGen/Lang/NameContext.cs), which says what is being named (`NameKind.Continent`, `Region`, `Sea`,
`Ocean`, `World` or `Language`) and can carry optional hints: the biome, which side of its parent the place lies on,
and the parent's name. A bare `NameKind` converts to a context with no hints. LoreGen ships two kinds of language:

- **`CommonLanguage`** (the default): names that read like those in fantasy and historical fiction. Each language
  blends one or two *flavours* (AngloNorse, Latinate, Celtic, Hellenic, Slavic, Persianate, AfroAsiatic,
  SinoTibetan). It uses only part of each flavour and favours some pieces, so two continents from the same flavour
  still sound different. Each language also has its own mix of proper names ("Valoria") and descriptive ones ("The
  Sea of Storms", "Greymoor"), with more descriptive names for oceans and fewer for continents. Flavours, templates and
  vocabulary are all data: see `CommonName*.csv` and `CommonLanguageRules.csv` in [`LoreGen/Resources`](LoreGen/Resources).
- **`PhoneticLanguage`**: the original generator, which builds names sound by sound from a real phonetic inventory
  and random rules. Its names sound foreign and unfamiliar.

Choose with `engine.Configuration.LanguageStyle` (`LanguageStyle.Common` or `LanguageStyle.Phonetic`), with
`languagestyle=Phonetic` in `loregen.config`, or with the Names drop-down in Worldbuilder. Languages and names draw
from their own random stream, so a seed gives the same land whichever style you pick. Names aren't repeated within
a world.

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
3. Specks of land too small to be an island are removed, land that only meets other land at a corner is joined into one
   landmass, and puddles too small to be a sea are filled in.
4. Continents are the connected pieces of land that remain, so the number of continents is a target, not a promise.
   Enclosed bodies of water become seas of the continent they touch most, and the rest is divided into oceans.
5. Climate is worked out for every block. Temperature is warmest at the equator (the middle of the map), falls toward
   the poles and with altitude, and varies a little at random. Moisture blends broad random patterns, nearness to
   water and a latitude pattern (wet equator, dry subtropics, wetter mid-latitudes, dry poles), is reduced where it is
   cold, and is stretched so every world spans the full range from arid to tropical.
6. Each continent is divided into connected regions that follow its geography. Region centres are spread apart, then each
   region spreads outward in a race where every step has a cost: steps are expensive across slopes, onto high ground and
   across changes of temperature or moisture, so regions fill valleys and plains first and meet at ridges and climate
   transitions rather than along arbitrary lines. Regions left very small are merged into a neighbour. A region's biome is the existing biome that best fits its average
   temperature, its average moisture and how rugged its terrain is, so polar land gets Polar Desert and Glacier, hot
   wet lowlands get Jungle Bog and Rainforest, and dry mid-latitudes get Steppe and Dunes. Each region also gets a
   language, and terrain drawn from its biome's usual mix, tilted so higher ground favours steeper terrain (Cliffs and
   Peaks) and lower ground favours Flats and Marsh.
7. Blocks that can contain a coastline (those with both land and water somewhere in the 3x3 blocks around them) get a
   10x10 grid of sub-blocks. Their shape comes from the same elevation, smoothly interpolated and then given fractal
   wiggle that fades away from the coast, so coastlines wind finely and carry on unbroken across block boundaries.
   Detail only reshapes existing coasts: fine land must connect to coarse land and fine water to coarse water, specks of
   land smaller than a dozen sub-blocks are dropped, and land of different continents never touches.
   Blocks on region borders get sub-blocks too, so borders wind: each sub-block goes to the region of the nearest land
   block after its position is nudged by smooth noise, and a final pass keeps every region in one piece (apart from
   small offshore islets). Interior land and open ocean stay as single blocks.
8. Every block, at every level, records its `Elevation` (0 at sea level, up to 1 on the highest land, down to -1 in the
   deepest water), `Temperature` and `Moisture` (both 0 to 1). The map shows them under "Colour by", and a block's details
   list them.

Its settings are in `NoiseGeneratorRules.csv` and can be changed per world through its options (below); world size and the
number of continents and regions come from `WorldRules.csv`. Set `CoastDetail` to 0 for a lighter world with no sub-blocks. The noise building blocks it uses
(`SimplexNoise`, `FractalNoise`, `Heightmap`) are in [`LoreGen/Noise`](LoreGen/Noise).

**Choosing one.** Set `engine.Configuration.GeneratorId`, or `generatormode=<id>` in `loregen.config`. Ids are
case-insensitive, and an unknown id fails with a message listing the available ones. The Worldbuilder app has a
Generator dropdown filled from the registry. A world records the `GeneratorId` and `GeneratorVersion` that made it, and
the seed, generator id and generator version together identify a world.

**Generator options.** A generator can describe its settings (name, description, group, range, step and default) so a user
interface can offer controls without knowing anything about it: `generator.GetOptions(engine.SimData)` returns a list of
`GeneratorOption`s, and the Worldbuilder options panel is built entirely from that list. To use a setting, put its value in
`engine.Configuration.GeneratorSettings` (names are not case-sensitive), or write `setting.Name=value` in `loregen.config`:

```csharp
var engine = new SimEngine(12345);
engine.Configuration.GeneratorId = "noise";
engine.Configuration.GeneratorSettings["LandFraction"] = 0.4;   // 40% land
engine.Configuration.GeneratorSettings["CoastDetail"] = 0;      // a lighter world without fine coastlines
engine.GenerateWorld();
// engine.World.GeneratorSettings records what was used
```

Settings a generator does not understand are rejected with an error that names them. Giving every option its default value
gives the same world as giving none. The noise generator's options are listed below.

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

Optionally, implement `GetOptions` to describe settings (it defaults to none), read them from
`engine.Configuration.GeneratorSettings`, and reject any you do not recognise. To ship a generator with LoreGen, add it to `WorldGeneratorRegistry.CreateDefault()`. That automatically puts it through
the contract tests in `GeneratorContractTests`. Generators keep their own settings in their own keyed rules file
(`engine.SimData.LoadRules("MyGeneratorRules.csv")`) so they never need to change the shared ones. If a generator throws,
`engine.World` keeps the previous world.

#### The noise generator's options

| Group | Option | Does |
| --- | --- | --- |
| Land | Land share | How much of the map is land (random by default) |
| | Land centres | How many places land gathers around. More centres spread land over more of the map; the number of continents that results varies, since land can merge or break apart |
| | Continent blobbiness | Noise-only scattered land, up to smooth blobs around the centres |
| | Fragmentation | Smaller, more numerous features |
| | Coast winding | How much coastlines twist |
| Coasts | Fine coastlines | Sub-blocks along coasts (off for a lighter world) |
| | Coast ruggedness | How rugged fine coastlines are |
| Climate | Global temperature | Ice age to hothouse |
| | Altitude chill, Cold dryness | How much colder high ground is, and how much drier cold places are |
| Regions and terrain | Regions (target) | About how many regions (random by default; very small ones are merged) |
| | Terrain-following borders | How strongly region borders stick to steep ground |
| | Winding region borders | Sub-blocks on region borders (off for a lighter world) |
| | Terrain follows height | How strongly high ground favours steep terrain |

## Tests

`dotnet test` runs the suite in [`LoreGen.Tests`](LoreGen.Tests). Besides basic loading and generation, it checks:

- generation succeeds across 150 seeds, and the same seed gives the same world even when other engines are created in between
- data loads correctly under a comma-decimal culture (`de-DE`)
- rule files accept reordered rows and report missing or unrecognized keys
- regions have their own languages, and seas are registered on their continent
- the noise generator: connected continents that never touch, an ocean border, a land share that follows its settings,
  consistent elevation, seas and oceans, and connected regions
- the noise generator's climate: warmer at the equator, colder at altitude by the lapse rate, wetter near water, drier
  where it is cold, biomes that fit their region's climate (polar land gets cold biomes, the tropics warm ones, deserts are
  drier than forests), and steeper terrain on higher ground
- the noise generator's regions: borders fall on steeper ground and at climate changes more than inside regions, centres
  are spread out, no region is tiny, each region is connected, and winding borders never cut a region into pieces
- the noise generator's fine coastlines: detail only near coasts, no stray islets or lakes, land of different continents
  never touching, exact coastline status at every resolution, and no seams at block boundaries
- generator options: descriptors that match the rules file, settings that do what they say (land share, number of regions,
  global temperature, detail switches), rejection of unknown and out-of-range settings, settings recorded on the world,
  `setting.` lines in the config file, and every option of every generator staying safe across its whole range
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
| `NoiseGeneratorRules.csv` | Settings for the noise generator: land share, noise detail, coastline winding, island size, coastal detail, climate, biome matching, terrain relief, how regions are shaped |
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
| `setting.<Name>` | A generator setting, for example `setting.LandFraction=0.4` (see Generator options) |

## Roadmap

The noise generator is feature-complete for its original plan: land, water, continents, seas, oceans, fine coastlines,
climate, climate-driven biomes, terrain-following regions, and per-world options. Ideas not yet done:

- Rain shadows behind mountains, and ocean currents, in the climate.
- A direct control over how many continents form. The number of land centres only changes how far the land spreads, because
  continents are found from the land that results rather than decided beforehand.
- Options for world size and for the biome and terrain matching, which currently live only in the data files.
- Sharing a world as a link (seed, generator, version and options).
