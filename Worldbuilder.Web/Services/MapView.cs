using LoreGen.Drawing;
using LoreGen.Simulation;
using LoreGen.WorldDisplay;
using LoreGen.WorldGen;
using LoreGen.Terrains;

namespace Worldbuilder.Web.Services;

public enum MapZoom { World, Continent, Region }

public record LegendEntry(string Label, string Hex);

public record InfoLine(string Label, string Value);

/// <summary>
/// State and geometry of the map being displayed: what is zoomed, which colour mode, and how to
/// turn the world's blocks into rectangles for the canvas.
/// </summary>
public class MapView
{
    private readonly WorldSession session;

    public MapContext Context { get; set; } = MapContext.General;
    public bool HiDef { get; set; } = true;

    /// <summary>Draw terrain tiles from the tileset image instead of flat colours. Water and tile-less blocks still use colours.</summary>
    public bool UseTiles { get; set; }

    /// <summary>The colour mode actually used: tile mode always shows terrain.</summary>
    private MapContext EffectiveContext => UseTiles ? MapContext.Terrains : Context;
    public MapZoom Zoom { get; private set; } = MapZoom.World;
    public Continent CurrentContinent { get; private set; }
    public LoreGen.WorldGen.Region CurrentRegion { get; private set; }
    public WorldBlock Selected { get; private set; }

    private static readonly MapContext[] BaseContexts =
    {
        MapContext.General, MapContext.Continents, MapContext.Regions, MapContext.Biomes, MapContext.Terrains
    };

    /// <summary>The colour modes to offer. Elevation appears only if the world's generator produced elevation data.</summary>
    public IReadOnlyList<MapContext> AvailableContexts
    {
        get
        {
            var contexts = BaseContexts.ToList();
            if (HasElevation) contexts.Add(MapContext.Elevation);
            if (HasClimate) contexts.AddRange(new[] { MapContext.Temperature, MapContext.Moisture });
            return contexts;
        }
    }

    /// <summary>Whether the world's generator produced temperature and moisture data.</summary>
    public bool HasClimate =>
        session.Engine.World.WorldBlock.ChildBlocks[0, 0].Status.Temperature.HasValue;

    public bool HasElevation =>
        session.Engine.World.WorldBlock.ChildBlocks[0, 0].Status.Elevation.HasValue;

    public MapView(WorldSession session) { this.session = session; }

    public string Title => Zoom switch
    {
        MapZoom.Continent => CurrentContinent?.Name ?? "Continent",
        MapZoom.Region => $"{CurrentRegion?.Name} ({CurrentContinent?.Name})",
        _ => "World"
    };

    // ---- Navigation -------------------------------------------------------------------------

    public bool CanZoomOut => Zoom != MapZoom.World;

    public void ZoomOut()
    {
        Selected = null;
        if (Zoom == MapZoom.Region) Zoom = MapZoom.Continent;
        else if (Zoom == MapZoom.Continent) Zoom = MapZoom.World;
    }

    public void ZoomInto(WorldBlock block)
    {
        var status = block?.Status;
        if (status == null) return;
        if (Zoom == MapZoom.World && status.Continent != null)
        {
            CurrentContinent = status.Continent;
            CurrentRegion = null;
            Zoom = MapZoom.Continent;
            Selected = null;
        }
        else if (Zoom == MapZoom.Continent && status.Region != null)
        {
            CurrentRegion = status.Region;
            Zoom = MapZoom.Region;
            Selected = null;
        }
    }

    public void Select(WorldBlock block) => Selected = block;

    // ---- Geometry ---------------------------------------------------------------------------

    private WorldBlock[,] GetGrid()
    {
        WorldBlock[,] grid = null;
        if (Zoom == MapZoom.Continent && CurrentContinent != null)
            grid = CurrentContinent.ContinentArea.BlocksAsGrid();
        else if (Zoom == MapZoom.Region && CurrentRegion != null)
            grid = CurrentRegion.RegionArea.BlocksAsGrid();

        if (grid == null || grid.Length == 0)
            grid = session.Engine.World.WorldBlock.ChildBlocks;
        return grid;
    }

    private static IEnumerable<WorldBlock> Cells(WorldBlock[,] grid)
    {
        foreach (var b in grid) if (b != null) yield return b;
    }

    /// <summary>The area covered by the current view, in world units.</summary>
    public (int X, int Y, int Width, int Height) GetBounds()
    {
        var blocks = Cells(GetGrid()).ToList();
        int minX = blocks.Min(b => b.TrueX), minY = blocks.Min(b => b.TrueY);
        int maxX = blocks.Max(b => b.TrueXMax), maxY = blocks.Max(b => b.TrueYMax);
        return (minX, minY, maxX - minX, maxY - minY);
    }

    private IEnumerable<WorldBlock> Leaves(WorldBlock[,] grid)
    {
        foreach (var block in Cells(grid))
        {
            if (HiDef && block.ChildBlocks != null)
            {
                foreach (var child in block.ChildBlocks) if (child != null) yield return child;
            }
            else yield return block;
        }
    }

    /// <summary>
    /// Flat array of rectangles to draw: x, y, width, height (world units, relative to the view origin), rgb, tile.
    /// The tile is (climate ID &lt;&lt; 8) | terrain type ID, the cell in the tileset image, or -1 to draw the colour.
    /// Also fills in a legend of the colours used.
    /// </summary>
    public (int[] Rects, List<LegendEntry> Legend) Build()
    {
        var grid = GetGrid();
        var b = GetBounds();
        var rects = new List<int>();
        var legend = new Dictionary<int, string>();
        foreach (var leaf in Leaves(grid))
        {
            int rgb = Map.GetColor(leaf, EffectiveContext).ToRgb();
            rects.Add(leaf.TrueX - b.X);
            rects.Add(leaf.TrueY - b.Y);
            rects.Add(leaf.TrueWidth);
            rects.Add(leaf.TrueHeight);
            rects.Add(rgb);
            rects.Add(TileFor(leaf));
            if (!legend.ContainsKey(rgb)) legend[rgb] = LabelFor(leaf);
        }
        var entries = legend
            .Select(kv => new LegendEntry(kv.Value, "#" + kv.Key.ToString("x6")))
            .OrderBy(e => e.Label)
            .ToList();
        return (rects.ToArray(), entries);
    }

    private int TileFor(WorldBlock block)
    {
        var terrain = block.Status.Terrain;
        if (!UseTiles || terrain == null || block.Status.WaterStatus == WorldBlockWaterStatus.Water) return -1;
        return (terrain.Climate.ID << 8) | terrain.TerrainType.ID;
    }

    /// <summary>Selected block's rectangle relative to the view origin, or null.</summary>
    public int[] SelectedRect()
    {
        if (Selected == null) return null;
        var b = GetBounds();
        return new[] { Selected.TrueX - b.X, Selected.TrueY - b.Y, Selected.TrueWidth, Selected.TrueHeight };
    }

    private string LabelFor(WorldBlock block)
    {
        var s = block.Status;
        if (EffectiveContext == MapContext.Elevation) return Map.ElevationBand(block) ?? "Unknown";
        if (s.WaterStatus == WorldBlockWaterStatus.Water) return "Water";
        if (EffectiveContext == MapContext.Temperature && s.Temperature.HasValue) return Map.TemperatureBand(block);
        if (EffectiveContext == MapContext.Moisture && s.Moisture.HasValue) return Map.MoistureBand(block);
        switch (EffectiveContext)
        {
            case MapContext.General:
                return s.WaterStatus == WorldBlockWaterStatus.Coastline ? "Coastline" : "Land";
            case MapContext.Continents: return s.Continent?.Name ?? "Unclaimed";
            case MapContext.Regions: return s.Region?.Name ?? "No region";
            case MapContext.Biomes:
                return s.Region?.Biome != null ? Biome.TraitName(s.Region.Biome.Temperature) : "No biome";
            case MapContext.Terrains: return s.Terrain?.Name ?? "No terrain";
        }
        return "";
    }

    /// <summary>Finds the block under a point given as a fraction (0..1) of the view's width and height.</summary>
    public WorldBlock BlockAt(double fx, double fy)
    {
        var b = GetBounds();
        double wx = b.X + fx * b.Width, wy = b.Y + fy * b.Height;
        foreach (var block in Cells(GetGrid()))
        {
            if (!Contains(block, wx, wy)) continue;
            if (HiDef && block.ChildBlocks != null)
            {
                foreach (var child in block.ChildBlocks)
                    if (child != null && Contains(child, wx, wy)) return child;
            }
            return block;
        }
        return null;
    }

    private static bool Contains(WorldBlock b, double x, double y)
        => x >= b.TrueX && x < b.TrueXMax && y >= b.TrueY && y < b.TrueYMax;

    // ---- Block info -------------------------------------------------------------------------

    public List<InfoLine> Describe(WorldBlock block)
    {
        var lines = new List<InfoLine>();
        if (block == null) return lines;
        var s = block.Status;
        lines.Add(new("Position", $"{block.X}, {block.Y}"));
        if (s.Continent != null) lines.Add(new("Continent", s.Continent.Name));
        if (s.Ocean != null) lines.Add(new("Ocean", s.Ocean.Name));
        if (s.Sea != null) lines.Add(new("Sea", s.Sea.Name));
        if (s.Elevation.HasValue) lines.Add(new("Elevation", $"{s.Elevation.Value:0.00} ({Map.ElevationBand(block)})"));
        if (s.Temperature.HasValue) lines.Add(new("Temperature", $"{s.Temperature.Value:0.00} ({Map.TemperatureBand(block)})"));
        if (s.Moisture.HasValue && s.WaterStatus != WorldBlockWaterStatus.Water) lines.Add(new("Moisture", $"{s.Moisture.Value:0.00} ({Map.MoistureBand(block)})"));
        lines.Add(new("Surface", s.WaterStatus switch
        {
            WorldBlockWaterStatus.Water => "Water",
            WorldBlockWaterStatus.Land => "Land",
            WorldBlockWaterStatus.Coastline => "Coastline",
            _ => "Unknown"
        }));
        if (s.Region != null)
        {
            lines.Add(new("Region", s.Region.Name));
            var neighbors = s.Region.NeighboringRegions();
            lines.Add(new("Neighboring regions", neighbors.Count == 0 ? "None" : string.Join(", ", neighbors.Select(r => r.Name))));
            if (s.Region.Biome != null) lines.Add(new("Regional biome", s.Region.Biome.Name));
            if (s.Region.Language != null) lines.Add(new("Regional language", s.Region.Language.LanguageName));
        }
        if (s.Terrain != null)
        {
            lines.Add(new("Terrain", s.Terrain.Name));
            lines.Add(new("Terrain type", s.Terrain.TerrainType.Name));
            lines.Add(new("Climate", s.Terrain.Climate.Name));
            lines.Add(new("Primary climate type", s.Terrain.Climate.PrimaryClimateType.Name));
            if (s.Terrain.Climate.PrimaryClimateType != s.Terrain.Climate.SecondaryClimateType)
                lines.Add(new("Secondary climate type", s.Terrain.Climate.SecondaryClimateType.Name));
        }
        return lines;
    }
}
