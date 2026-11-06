# LoreGen / Worldbuilder

LoreGen is a world-generation library (continents, regions, biomes, terrain, and procedural languages).
Worldbuilder.Web is a browser UI for it, built with Blazor WebAssembly.

| Project | What it is |
| --- | --- |
| `LoreGen/` | Core library (`net10.0`). Default data files are embedded as resources. |
| `LoreGen.Tests/` | xunit tests |
| `Worldbuilder.Web/` | Blazor WebAssembly app: map, world info, language generator, raw data |

## Run the web UI

```
dotnet run --project Worldbuilder.Web
```

Then open the URL it prints (usually http://localhost:5000). To publish a static site:
`dotnet publish Worldbuilder.Web -c Release` (output in `bin/Release/net10.0/publish/wwwroot`).

## Tests

```
dotnet test
```

## Data

LoreGen reads its data (`*.csv`, `*.txt`) from a data folder on disk if one exists, otherwise from the
copies embedded in the assembly (`LoreGen/Resources`). See `LoreGen/Resources/README.txt` for the formats.
