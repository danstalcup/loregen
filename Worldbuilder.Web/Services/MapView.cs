using LoreGen.Drawing;
using LoreGen.Simulation;
using LoreGen.WorldDisplay;
using LoreGen.WorldGen;
using LoreGen.Terrains;

namespace Worldbuilder.Web.Services;

public enum MapZoom { World, Continent, Region, Block }

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
    /// <summary>In block view, the block whose children are shown.</summary>
    public WorldBlock CurrentBlock { get; private set; }
    public WorldBlock Selected { get; private set; }

    // The views zoomed out of, to go back to
    private readonly Stack<(MapZoom Zoom, Continent Continent, LoreGen.WorldGen.Region Region, WorldBlock Block)> back = new();

    private static readonly MapContext[] BaseContexts =
    {
        MapContext.General, MapContext.Continents, MapContext.Regions, MapContext.Biomes, MapContext.Terrains, MapContext.Features
    };

    /// <summary>The colour modes to offer. Elevation appears only if the world's generator produced elevation data.</summary>
    public IReadOnlyList<MapContext> AvailableContexts
    {
        get
        {
            var contexts = BaseContexts.ToList();
            if (HasElevation) contexts.Add(MapContext.Elevation);
            if (HasClimate) contexts.AddRange(new[] { MapContext.Temperature, MapContext.Moisture });
            if (session.Engine.World.Plates.Count > 0) contexts.Add(MapContext.Plates);
            if (session.Engine.World.HasSubregions) contexts.Insert(contexts.IndexOf(MapContext.Regions) + 1, MapContext.Subregions);
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
        MapZoom.Block => $"{CurrentBlock.ScaleName} block, {Place(CurrentBlock)}",
        _ => "World"
    };

    /// <summary>The views from the world down to the current one, for a breadcrumb trail.</summary>
    public IReadOnlyList<string> Trail
    {
        get
        {
            var trail = new List<string> { "World" };
            var views = back.Reverse().Append((Zoom: Zoom, Continent: CurrentContinent, Region: CurrentRegion, Block: CurrentBlock));
            foreach (var view in views.Where(v => v.Zoom != MapZoom.World))
            {
                trail.Add(view.Zoom switch
                {
                    MapZoom.Continent => view.Continent?.Name ?? "Continent",
                    MapZoom.Region => view.Region?.Name ?? "Region",
                    MapZoom.Block => view.Block.ScaleName,
                    _ => "World"
                });
            }
            return trail;
        }
    }

    /// <summary>The size of the squares drawn in the current view, such as "10 km".</summary>
    public string CellSize => Leaves(GetGrid()).FirstOrDefault()?.ScaleName ?? "";

    private static string Place(WorldBlock block)
    {
        var s = block.Status;
        return s.Region?.Name ?? s.Lake?.Name ?? s.Sea?.Name ?? s.Ocean?.Name ?? s.Continent?.Name ?? "open water";
    }

    // ---- Navigation -------------------------------------------------------------------------

    public bool CanZoomOut => Zoom != MapZoom.World;

    public void ZoomOut()
    {
        Selected = null;
        if (back.Count == 0) { Zoom = MapZoom.World; CurrentContinent = null; CurrentRegion = null; CurrentBlock = null; return; }
        (Zoom, CurrentContinent, CurrentRegion, CurrentBlock) = back.Pop();
    }

    private void Push() => back.Push((Zoom, CurrentContinent, CurrentRegion, CurrentBlock));

    /// <summary>
    /// Whether double-clicking this block zooms into it: a continent from the world, a region from a continent, and from
    /// a region or block view any block that can be split further (down to 100 m).
    /// </summary>
    public bool CanZoomInto(WorldBlock block)
    {
        block = ZoomTarget(block);
        var status = block?.Status;
        if (status == null) return false;
        return Zoom switch
        {
            MapZoom.World => status.Continent != null,
            MapZoom.Continent => status.Region != null,
            _ => block.CanSubdivide
        };
    }

    /// <summary>Whether zooming into this block has to make its detail first (which can take a moment).</summary>
    public bool NeedsDetail(WorldBlock block)
    {
        block = ZoomTarget(block);
        return (Zoom == MapZoom.Region || Zoom == MapZoom.Block) && block != null && block.ChildBlocks == null && block.CanSubdivide;
    }

    /// <summary>
    /// The block a double-click zooms into: in region and block views, the square of the view's own grid that holds the
    /// clicked block, so each double-click goes exactly one level deeper even where finer detail is drawn (High detail).
    /// </summary>
    private WorldBlock ZoomTarget(WorldBlock block)
    {
        if (block == null || (Zoom != MapZoom.Region && Zoom != MapZoom.Block)) return block;
        foreach (var cell in Cells(GetGrid()))
            if (block.Depth >= cell.Depth && block.AncestorAt(cell.Depth) == cell) return cell;
        return block;
    }

    public void ZoomInto(WorldBlock block)
    {
        if (!CanZoomInto(block)) return;
        block = ZoomTarget(block);
        var status = block.Status;
        Push();
        Selected = null;
        if (Zoom == MapZoom.World)
        {
            CurrentContinent = status.Continent;
            CurrentRegion = null;
            Zoom = MapZoom.Continent;
        }
        else if (Zoom == MapZoom.Continent)
        {
            CurrentRegion = status.Region;
            Zoom = MapZoom.Region;
        }
        else
        {
            session.Engine.World.EnsureChildren(block);
            CurrentBlock = block;
            Zoom = MapZoom.Block;
        }
    }

    /// <summary>
    /// In block view, the squares of the grid whose own detail has not been made yet (and can be), for "Generate details".
    /// </summary>
    public IReadOnlyList<WorldBlock> SquaresWithoutDetail() =>
        Zoom == MapZoom.Block && CurrentBlock?.ChildBlocks != null
            ? CurrentBlock.ChildBlocks.Cast<WorldBlock>().Where(c => c.ChildBlocks == null && c.CanSubdivide).ToList()
            : new List<WorldBlock>();

    /// <summary>Whether the squares of the current block view can have finer detail at all (not already 100 m).</summary>
    public bool SquaresCanHaveDetail =>
        Zoom == MapZoom.Block && CurrentBlock?.ChildBlocks != null && CurrentBlock.ChildBlocks[0, 0].CanSubdivide;

    /// <summary>Makes the detail inside one square of the grid (see <see cref="SquaresWithoutDetail"/>).</summary>
    public void MakeDetail(WorldBlock square) => session.Engine.World.EnsureChildren(square);

    /// <summary>In block view, whether there is a neighbouring block of the same size in a direction.</summary>
    public bool CanPan(int dx, int dy) => Zoom == MapZoom.Block && Neighbour(CurrentBlock, dx, dy, false) != null;

    /// <summary>In block view, moves to the neighbouring block of the same size, making its detail if need be.</summary>
    public void Pan(int dx, int dy)
    {
        if (Zoom != MapZoom.Block) return;
        var next = Neighbour(CurrentBlock, dx, dy, true);
        if (next == null) return;
        session.Engine.World.EnsureChildren(next);
        CurrentBlock = next;
        Selected = null;
    }

    /// <summary>Whether moving in a direction has to make detail first.</summary>
    public bool PanNeedsDetail(int dx, int dy)
    {
        var next = Zoom == MapZoom.Block ? Neighbour(CurrentBlock, dx, dy, false) : null;
        return next == null || next.ChildBlocks == null;
    }

    // The block of the same depth beside this one (making the blocks on the way if asked), or null off the map. Without
    // making blocks, a neighbour that has not been made yet gives the nearest ancestor of it that has.
    private WorldBlock Neighbour(WorldBlock block, int dx, int dy, bool create)
    {
        if (block == null || block.Depth < 1) return null;
        var world = session.Engine.World;
        int size = block.TrueWidth;
        long tx = block.TrueX + (long)dx * size, ty = block.TrueY + (long)dy * size;
        var root = world.WorldBlock;
        if (tx < 0 || ty < 0 || tx >= root.TrueWidth || ty >= root.TrueHeight) return null;
        WorldBlock at = root;
        while (at.Depth < block.Depth)
        {
            if (at.ChildBlocks == null)
            {
                // Not made yet: when only asking whether it exists, its nearest made ancestor stands in for it
                if (!create) return at.Depth == 0 ? null : at;
                if (!world.EnsureChildren(at)) return null;
            }
            at = at.ChildBlocks.Cast<WorldBlock>().FirstOrDefault(c => tx >= c.TrueX && tx < c.TrueXMax && ty >= c.TrueY && ty < c.TrueYMax);
            if (at == null) return null;
        }
        return at;
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
        else if (Zoom == MapZoom.Block && CurrentBlock?.ChildBlocks != null)
            grid = CurrentBlock.ChildBlocks;

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

    /// <summary>
    /// Whether High detail draws the squares of this grid one level finer. Every view is drawn at one level throughout,
    /// never a patchwork of fine and coarse squares (which shows as hard-edged blocks): a block view goes finer only once
    /// every square in it has its detail ("Generate details"); the wider views have the detail of all their land made
    /// first (see <see cref="EnsureLandDetail"/>), and open water looks the same either way.
    /// </summary>
    private bool DrawFiner(WorldBlock[,] grid) =>
        HiDef && (Zoom != MapZoom.Block || Cells(grid).All(c => c.ChildBlocks != null));

    /// <summary>
    /// In the world, continent and region views, makes the 100 km detail of any land block that doesn't have it yet, so
    /// High detail shows all the land at the same level. Almost all land gets it when the world is made, so this is quick.
    /// </summary>
    private void EnsureLandDetail(WorldBlock[,] grid)
    {
        if (!HiDef || Zoom == MapZoom.Block) return;
        var world = session.Engine.World;
        foreach (var block in Cells(grid).Where(b => b.ChildBlocks == null && b.Status.WaterStatus != WorldBlockWaterStatus.Water).ToList())
            world.EnsureChildren(block);
    }

    private IEnumerable<WorldBlock> Leaves(WorldBlock[,] grid)
    {
        bool finer = DrawFiner(grid);
        foreach (var block in Cells(grid))
        {
            if (finer && block.ChildBlocks != null)
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
        // Subregions are worked out for the regions in view, from a region view down (a whole continent would take a while)
        if (EffectiveContext == MapContext.Subregions && session.Engine.World.HasSubregions)
        {
            var world = session.Engine.World;
            if (Zoom == MapZoom.Region && CurrentRegion != null) world.SubregionsOf(CurrentRegion);
            else if (Zoom == MapZoom.Block)
                foreach (var region in Leaves(GetGrid()).Select(b => b.Status.Region).Where(r => r != null).Distinct().ToList())
                    world.SubregionsOf(region);
        }
        var grid = GetGrid();
        EnsureLandDetail(grid);
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

    /// <summary>Terrain type IDs 1 to this have tiles in tiles1.png.</summary>
    private const int TilesetTypes = 10;

    private int TileFor(WorldBlock block)
    {
        var terrain = block.Status.Terrain;
        if (!UseTiles || terrain == null || block.Status.WaterStatus == WorldBlockWaterStatus.Water) return -1;
        // The tileset image has tiles for the original terrain types only; newer ones show their colour
        if (terrain.TerrainType.ID > TilesetTypes) return -1;
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
        if (EffectiveContext == MapContext.Plates) return Map.PlateLabel(block);
        if (s.WaterStatus == WorldBlockWaterStatus.Water) return "Water";
        if (EffectiveContext == MapContext.Temperature && s.Temperature.HasValue) return Map.TemperatureBand(block);
        if (EffectiveContext == MapContext.Moisture && s.Moisture.HasValue) return Map.MoistureBand(block);
        switch (EffectiveContext)
        {
            case MapContext.General:
                return s.WaterStatus == WorldBlockWaterStatus.Coastline ? "Coastline" : "Land";
            case MapContext.Continents: return s.Continent?.Name ?? "Unclaimed";
            case MapContext.Regions: return s.Region?.Name ?? "No region";
            case MapContext.Subregions:
                return s.Subregion != null ? $"{s.Subregion.Name} ({s.Subregion.Region.Name})"
                    : s.Region != null ? $"{s.Region.Name} (zoom into the region for its subregions)" : "No region";
            case MapContext.Features: return Map.FeatureToShow(block)?.Name ?? "No feature";
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
        var grid = GetGrid();
        bool finer = DrawFiner(grid);
        foreach (var block in Cells(grid))
        {
            if (!Contains(block, wx, wy)) continue;
            if (finer && block.ChildBlocks != null)
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
        lines.Add(new("Size", $"{block.ScaleName} across"));
        lines.Add(new("Position", $"{block.X}, {block.Y}"));
        if (s.Continent != null) lines.Add(new("Continent", s.Continent.Name));
        if (s.Ocean != null) lines.Add(new("Ocean", s.Ocean.Name));
        if (s.Sea != null) lines.Add(new("Sea", s.Sea.Name));
        if (s.Plate != null)
        {
            lines.Add(new("Plate", $"{s.Plate.Name} ({(s.Plate.Continental ? "continental" : "oceanic")}, drifting {s.Plate.Heading})"));
            var boundary = s.Plate.BoundaryAt(block);
            if (boundary != PlateBoundaryKind.None) lines.Add(new("Plate boundary", boundary.ToString()));
        }
        var blockFeatures = session.Engine.World.FeaturesAt(block);
        if (blockFeatures.Count > 0)
            lines.Add(new("Features", string.Join(", ", blockFeatures.Select(f => $"{f.Name} ({f.Type.Name.ToLowerInvariant()})"))));
        if (s.Elevation.HasValue) lines.Add(new("Elevation", $"{s.Elevation.Value:0.00} ({Map.ElevationBand(block)})"));
        if (s.Temperature.HasValue) lines.Add(new("Temperature", $"{s.Temperature.Value:0.00} ({Map.TemperatureBand(block)})"));
        if (s.Moisture.HasValue && s.WaterStatus != WorldBlockWaterStatus.Water) lines.Add(new("Moisture", $"{s.Moisture.Value:0.00} ({Map.MoistureBand(block)})"));
        if (s.Hydration.HasValue && s.WaterStatus != WorldBlockWaterStatus.Water) lines.Add(new("Ground water", $"{s.Hydration.Value:0.00}"));
        if (s.Lake != null) lines.Add(new("Lake", s.Lake.Name));
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
            var subregion = session.Engine.World.SubregionAt(block);
            if (subregion != null) lines.Add(new("Subregion", $"{subregion.Name} ({subregion.Kind.ToString().ToLowerInvariant()})"));
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
