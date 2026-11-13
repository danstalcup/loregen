using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

using LoreGen.Features;
using LoreGen.Noise;
using LoreGen.Simulation;
using LoreGen.Terrains;

namespace LoreGen.WorldGen.Generators
{
    /// <summary>
    /// What is known about a place when its terrain is chosen. Targets are worked out from the place's height, climate and
    /// water, then the features and plate boundary it lies on push them further (see TerrainInfluences.csv).
    /// </summary>
    internal struct TerrainSite
    {
        /// <summary>Height above sea level (0 to 1).</summary>
        public double Elevation;
        /// <summary>Temperature (0 coldest to 1 hottest).</summary>
        public double Temperature;
        /// <summary>Moisture from rain (0 driest to 1 wettest).</summary>
        public double Moisture;
        /// <summary>How much water the ground holds: its moisture plus what rivers and lakes bring (0 to 1).</summary>
        public double Hydration;
        /// <summary>The features covering the place, largest scope first.</summary>
        public IReadOnlyList<GeographicFeature> Features;
        /// <summary>The kind of plate boundary the place is on, if any.</summary>
        public PlateBoundaryKind Boundary;
        /// <summary>
        /// How strongly the plate boundary acts here, from 0 to 1, or null for fully (see
        /// <see cref="TerrainModel.BoundaryBlend"/>, which fades it out across the edge of a boundary belt).
        /// </summary>
        public double? BoundaryStrength;
    }

    /// <summary>
    /// How a feature (or a kind of plate boundary) changes the terrain on it: a row of TerrainInfluences.csv.
    /// Relief is in steps of <see cref="TerrainType.Relief"/> (0 to 4); wetness and trees are 0 to 1 as in TerrainTypes.csv.
    /// </summary>
    internal sealed class TerrainInfluence
    {
        public const string Filename = "TerrainInfluences.csv";
        public const string PlatePrefix = "Plate:";

        public string Id;
        public double ReliefAdd;
        public double ReliefMin = double.NegativeInfinity, ReliefMax = double.PositiveInfinity;
        public double WetMin = double.NegativeInfinity, WetMax = double.PositiveInfinity;
        public double TreesMin = double.NegativeInfinity, TreesMax = double.PositiveInfinity;
        public double Moisture;

        public static Dictionary<string, TerrainInfluence> Parse(IEnumerable<string> lines, SimData data)
        {
            Dictionary<string, TerrainInfluence> influences = new Dictionary<string, TerrainInfluence>(StringComparer.OrdinalIgnoreCase);
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                string[] parts = line.Split(new[] { ',' }, 10);
                if (parts.Length < 9) throw new InvalidDataException(Filename + ": expected 'Influence,ReliefAdd,ReliefMin,ReliefMax,WetMin,WetMax,TreesMin,TreesMax,Moisture[,Description]' but found: " + line);
                string id = parts[0].Trim();
                if (influences.ContainsKey(id)) throw new InvalidDataException(Filename + ": duplicate influence '" + id + "'");
                if (id.StartsWith(PlatePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    PlateBoundaryKind kind;
                    if (!Enum.TryParse(id.Substring(PlatePrefix.Length), true, out kind) || kind == PlateBoundaryKind.None)
                        throw new InvalidDataException(Filename + ": unknown plate boundary '" + id + "' (use Plate:Convergent, Plate:Divergent or Plate:Transform)");
                }
                else if (data != null && !data.FeatureTypes.Any(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidDataException(Filename + ": unknown feature type '" + id + "'");
                }

                TerrainInfluence influence = new TerrainInfluence { Id = id };
                influence.ReliefAdd = Number(parts[1], 0, id);
                influence.ReliefMin = Number(parts[2], double.NegativeInfinity, id);
                influence.ReliefMax = Number(parts[3], double.PositiveInfinity, id);
                influence.WetMin = Number(parts[4], double.NegativeInfinity, id);
                influence.WetMax = Number(parts[5], double.PositiveInfinity, id);
                influence.TreesMin = Number(parts[6], double.NegativeInfinity, id);
                influence.TreesMax = Number(parts[7], double.PositiveInfinity, id);
                influence.Moisture = Number(parts[8], 0, id);
                influences[id] = influence;
            }
            return influences;
        }

        private static double Number(string text, double blank, string id)
        {
            text = text.Trim();
            if (text.Length == 0) return blank;
            double value;
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                throw new InvalidDataException(Filename + ": '" + text + "' for '" + id + "' is not a number");
            return value;
        }
    }

    /// <summary>
    /// Chooses each place's terrain from everything known about it, rather than from its region's usual mix.
    ///
    /// Every terrain is a climate (such as Grassy or Sandy-Savannah) and a terrain type (such as Hills or Marsh), and each
    /// is scored on how well it fits the place:
    ///  - the climate, by how near the place's temperature and moisture are to the climate's ideal (ClimateTypes.csv);
    ///  - the terrain type's relief, against a target from the place's height (higher is steeper);
    ///  - its wetness, against a target from how much water the place holds (its moisture, plus the water rivers and
    ///    lakes bring), on flat ground only;
    ///  - its tree cover, against a target from how wet and warm the place is, thinning out on high, steep ground.
    /// The features on the place (a mountain range, a desert's erg, an oasis, a river) and the plate boundary it lies on push
    /// these targets as TerrainInfluences.csv says, smaller features last so they have the final say.
    ///
    /// The place takes the landscape (broad climate, ruggedness and tree cover) whose terrains together score best, then one
    /// of that landscape's terrains is drawn at random in proportion to the scores. The random number comes from smooth noise over the
    /// world (patches about TerrainPatchSize top-level blocks across), so neighbouring places with similar scores draw alike
    /// and terrain comes in patches rather than speckles; and since the noise is fixed by the world's seed and the place's
    /// position, the choice does not depend on the order places are visited in.
    /// </summary>
    internal sealed class TerrainModel
    {
        private readonly NoiseGeneratorRules rules;
        private readonly int worldSeed;
        private readonly Dictionary<string, TerrainInfluence> influences;
        private readonly List<Climate> climates;
        private readonly List<TerrainType> types;
        private readonly (double T, double M)[] climatePoint;
        private readonly Terrain[,] terrainOf; // [climate, type], null where there is no such terrain
        // Every terrain, alike ones together: its climate and type, its landscape, and the terrain itself
        private readonly int[] drawClimate, drawType, drawLandscape;
        private readonly Terrain[] drawTerrain;
        private readonly int landscapeSlots;
        private readonly FractalNoise patches;
        private readonly double patchSpread;
        // Patch noise for zoomed-in blocks, measured in blocks of their own size: one sample for which terrain, another for
        // whether the block keeps its parent's
        private readonly FractalNoise zoomPatches;
        // Looked up once: the influence of each kind of plate boundary, and each terrain's place in the draw order
        private readonly Dictionary<PlateBoundaryKind, TerrainInfluence> plateInfluence = new Dictionary<PlateBoundaryKind, TerrainInfluence>();
        private Dictionary<Terrain, int> drawIndex;
        // Raggedness of the edges of plate boundary belts below the top level
        private readonly FractalNoise boundaryNoise;
        // Nudges where the top-level climate is read for sub-blocks (see ClimateAt)
        private readonly FractalNoise climateWarp;

        /// <summary>How far (in top-level blocks) a sub-block's climate may be read from its own position.</summary>
        private const double ClimateWarp = 0.45;
        private readonly double zoomSpread;
        private double blockSize = 1; // true units per top-level block

        public TerrainModel(SimEngine engine, NoiseGeneratorRules rules)
        {
            this.rules = rules;
            worldSeed = engine.Rnd.Seed;
            influences = TerrainInfluence.Parse(engine.SimData.LoadLines(TerrainInfluence.Filename), engine.SimData);

            climates = engine.SimData.Climates.ToList();
            types = engine.SimData.TerrainTypes.ToList();
            foreach (TerrainType type in types)
                foreach (string id in type.RequiresFeature)
                    if (!engine.SimData.FeatureTypes.Any(f => f.Id == id))
                        throw new InvalidDataException("TerrainTypes.csv: " + type.Name + " belongs to an unknown feature type '" + id + "'");
            climatePoint = climates.Select(ClimatePoint).ToArray();
            if (rules.TerrainPatchSize > 0)
            {
                patches = new FractalNoise(new SimplexNoise(unchecked(worldSeed * 7919 + 0x7e77a1)), 6, 1.0 / rules.TerrainPatchSize, 2.0, 0.5);
                // The noise's spread, measured once, turns its values into evenly spread random numbers
                double sum = 0;
                for (int i = 0; i < 4096; i++) { double v = patches.Fbm(i * 0.6180339887 * 37.0, i * 0.4142135623 * 41.0); sum += v * v; }
                patchSpread = Math.Max(1e-6, Math.Sqrt(sum / 4096));
            }
            zoomPatches = new FractalNoise(new SimplexNoise(unchecked(worldSeed * 6271 + 0x200a)), 2, 1.0 / ZoomPatchBlocks, 2.0, 0.5);
            boundaryNoise = new FractalNoise(new SimplexNoise(unchecked(worldSeed * 92821 + 0x200b)), 4, 1.0, 2.0, 0.55);
            climateWarp = new FractalNoise(new SimplexNoise(unchecked(worldSeed * 15485863 + 0x200c)), 5, 1.2, 2.0, 0.55);
            {
                double sum = 0;
                for (int i = 0; i < 4096; i++) { double v = zoomPatches.Fbm(i * 0.6180339887 * 37.0, i * 0.4142135623 * 41.0); sum += v * v; }
                zoomSpread = Math.Max(1e-6, Math.Sqrt(sum / 4096));
            }
            terrainOf = new Terrain[climates.Count, types.Count];
            foreach (Terrain terrain in engine.SimData.Terrains)
            {
                int c = climates.IndexOf(terrain.Climate), k = types.IndexOf(terrain.TerrainType);
                if (c >= 0 && k >= 0 && terrainOf[c, k] == null) terrainOf[c, k] = terrain;
            }

            // Terrains are drawn in order of landscape, so a slightly different random number (the next place along) mostly
            // gives a terrain of the same landscape even when it gives a different terrain
            List<(int C, int K)> order = new List<(int, int)>();
            for (int c = 0; c < climates.Count; c++)
                for (int k = 0; k < types.Count; k++)
                    if (terrainOf[c, k] != null) order.Add((c, k));
            if (order.Count == 0) throw new InvalidDataException("There are no terrains to choose from.");
            order = order.OrderBy(p => RegionPartitioner.Landscape(terrainOf[p.C, p.K])).ThenBy(p => types[p.K].Relief)
                .ThenBy(p => types[p.K].Trees).ThenBy(p => p.C).ThenBy(p => p.K).ToList();
            drawClimate = order.Select(p => p.C).ToArray();
            drawType = order.Select(p => p.K).ToArray();
            drawTerrain = order.Select(p => terrainOf[p.C, p.K]).ToArray();
            drawLandscape = drawTerrain.Select(RegionPartitioner.Landscape).ToArray();
            landscapeSlots = drawLandscape.Max() + 1;
        }

        /// <summary>
        /// The temperature and moisture a climate is most typical of: its primary type's ideal, pulled a third of the way
        /// toward its secondary type's.
        /// </summary>
        public static (double T, double M) ClimatePoint(Climate climate)
        {
            ClimateType a = climate.PrimaryClimateType, b = climate.SecondaryClimateType;
            if (b == null || ReferenceEquals(a, b)) return (a.IdealTemperature, a.IdealMoisture);
            return ((2 * a.IdealTemperature + b.IdealTemperature) / 3, (2 * a.IdealMoisture + b.IdealMoisture) / 3);
        }

        /// <summary>The influence a feature type has on terrain, or null if it has none.</summary>
        public TerrainInfluence InfluenceOf(string id)
        {
            TerrainInfluence influence;
            return influences.TryGetValue(id, out influence) ? influence : null;
        }

        /// <summary>
        /// The relief (0 to 4), wetness and tree cover (0 to 1) a place's terrain should have, and the moisture its climate
        /// should be judged by.
        /// </summary>
        public (double Relief, double Wet, double Trees, double Moisture) Targets(TerrainSite site)
        {
            double relief = Clamp(site.Elevation / rules.ElevationPerRelief, 0, 4);
            double moisture = site.Moisture;
            List<TerrainInfluence> applied = new List<TerrainInfluence>(4);
            double strength = Clamp(site.BoundaryStrength ?? 1, 0, 1);
            if (site.Boundary != PlateBoundaryKind.None && strength > 0)
            {
                // A plate boundary pushes in proportion to its strength, and only sets limits where it is the stronger
                TerrainInfluence plate;
                if (!plateInfluence.TryGetValue(site.Boundary, out plate))
                    plateInfluence[site.Boundary] = plate = InfluenceOf(TerrainInfluence.PlatePrefix + site.Boundary);
                if (plate != null)
                {
                    relief = Clamp(relief + plate.ReliefAdd * strength, 0, 4);
                    moisture += plate.Moisture * strength;
                    if (strength >= 0.5) applied.Add(new TerrainInfluence
                    {
                        Id = plate.Id, ReliefMin = plate.ReliefMin, ReliefMax = plate.ReliefMax, WetMin = plate.WetMin,
                        WetMax = plate.WetMax, TreesMin = plate.TreesMin, TreesMax = plate.TreesMax
                    });
                }
            }
            if (site.Features != null)
                for (int scope = (int)FeatureScope.Continental; scope <= (int)FeatureScope.Minor; scope++)
                    foreach (GeographicFeature feature in site.Features)
                    {
                        if ((int)feature.Scope != scope) continue;
                        TerrainInfluence influence = InfluenceOf(feature.Type.Id);
                        if (influence != null) applied.Add(influence);
                    }

            foreach (TerrainInfluence influence in applied)
            {
                relief = Clamp(Math.Min(influence.ReliefMax, Math.Max(influence.ReliefMin, relief + influence.ReliefAdd)), 0, 4);
                moisture += influence.Moisture;
            }

            // Standing water gathers on flat ground that holds a lot of water
            double flat = Clamp(1 - relief / 2, 0, 1);
            double wet = Clamp((site.Hydration - rules.WetlandFrom) / Math.Max(1e-9, 1 - rules.WetlandFrom), 0, 1) * flat;

            // Trees need water and warmth, and thin out on high, steep ground
            double water = Clamp((site.Hydration - rules.TreeMoistureFrom) / Math.Max(1e-9, rules.TreeMoistureFull - rules.TreeMoistureFrom), 0, 1);
            double warmth = Clamp((site.Temperature - rules.TreeTemperatureFrom) / 0.15, 0, 1);
            double treeLine = Clamp(rules.TreeLineRelief - relief, 0, 1);
            double trees = water * warmth * treeLine;

            foreach (TerrainInfluence influence in applied)
            {
                wet = Clamp(Math.Min(influence.WetMax, Math.Max(influence.WetMin, wet)), 0, 1);
                trees = Clamp(Math.Min(influence.TreesMax, Math.Max(influence.TreesMin, trees)), 0, 1);
            }
            return (relief, wet, trees, Clamp(moisture, 0, 1));
        }

        /// <summary>
        /// How well every terrain fits a place, as (terrain, weight) pairs with positive weights.
        /// </summary>
        public List<(Terrain Terrain, double Weight)> Weights(TerrainSite site)
        {
            double[] scores = Scores(site);
            List<(Terrain, double)> weights = new List<(Terrain, double)>();
            for (int i = 0; i < scores.Length; i++)
                if (scores[i] > 0) weights.Add((drawTerrain[i], scores[i]));
            return weights;
        }

        // How well each terrain (in draw order) fits a place
        private double[] Scores(TerrainSite site)
        {
            var targets = Targets(site);
            double spread = Math.Max(1e-6, rules.TerrainClimateSpread);
            double[] climateFit = new double[climates.Count];
            double reach = 2 * spread * spread;
            for (int c = 0; c < climates.Count; c++)
            {
                double dt = site.Temperature - climatePoint[c].T, dm = targets.Moisture - climatePoint[c].M;
                double d = (dt * dt + dm * dm) / reach;
                climateFit[c] = d > 40 ? 0 : Math.Exp(-d); // too far to matter (below e^-40)
            }
            double[] typeFit = new double[types.Count];
            for (int k = 0; k < types.Count; k++)
            {
                // Terrain that belongs to features (a beach, lava) only goes on them, where it is favoured
                double boost = 1;
                if (types[k].RequiresFeature.Count > 0)
                {
                    if (!Covers(site.Features, types[k].RequiresFeature)) { typeFit[k] = 0; continue; }
                    boost = FeatureTerrainBoost;
                }
                double relief = types[k].Relief - targets.Relief, wet = types[k].Wetness - targets.Wet, trees = types[k].Trees - targets.Trees;
                typeFit[k] = boost * Math.Exp(-rules.TerrainReliefStrength * relief * relief - rules.TerrainWetStrength * wet * wet - rules.TerrainTreeStrength * trees * trees);
            }

            double[] scores = new double[drawTerrain.Length];
            for (int i = 0; i < scores.Length; i++)
            {
                double fit = climateFit[drawClimate[i]];
                scores[i] = fit == 0 ? 0 : fit * typeFit[drawType[i]];
            }
            return scores;
        }

        /// <summary>How much more likely terrain that belongs to a feature is on that feature than other terrain that fits as well.</summary>
        private const double FeatureTerrainBoost = 6;

        private static bool Covers(IReadOnlyList<GeographicFeature> features, IReadOnlyList<string> typeIds)
        {
            if (features == null) return false;
            foreach (GeographicFeature feature in features)
                foreach (string id in typeIds)
                    if (feature.Type.Id == id) return true;
            return false;
        }

        /// <summary>
        /// The landscape (see <see cref="RegionPartitioner.Landscape"/>) that fits a place best: the one whose terrains
        /// together fit it best. It depends only on the place's height, climate, water and features, which change smoothly,
        /// so neighbouring places mostly share it.
        /// </summary>
        public int LandscapeAt(TerrainSite site)
        {
            return BestLandscape(LandscapeFits(Scores(site)));
        }

        // How well each landscape's terrains together fit, by landscape
        private double[] LandscapeFits(double[] scores)
        {
            double[] totals = new double[landscapeSlots];
            for (int i = 0; i < scores.Length; i++) totals[drawLandscape[i]] += scores[i];
            return totals;
        }

        // The best-fitting landscape (the lowest-numbered, if tied); -1 if nothing fits at all
        private static int BestLandscape(double[] fits)
        {
            int best = -1;
            for (int l = 0; l < fits.Length; l++)
                if (fits[l] > 0 && (best < 0 || fits[l] > fits[best])) best = l;
            return best;
        }

        /// <summary>
        /// The terrain for a land block: first the landscape that fits it best (<see cref="LandscapeAt"/>), then one of that
        /// landscape's terrains drawn in proportion to how well each fits, with a random number fixed by the world's seed and
        /// the block's position.
        /// </summary>
        public Terrain Pick(TerrainSite site, WorldBlock block)
        {
            double[] scores = Scores(site);
            return Pick(scores, block, BestLandscape(LandscapeFits(scores)));
        }

        // Draws a terrain of the given landscape
        private Terrain Pick(double[] scores, WorldBlock block, int landscape)
        {
            // Nothing fits at all (an extreme setting): take the first terrain
            if (landscape < 0) return drawTerrain[0];

            double total = 0;
            int last = -1;
            for (int i = 0; i < scores.Length; i++)
                if (drawLandscape[i] == landscape && scores[i] > 0) { total += scores[i]; last = i; }
            if (last < 0) return drawTerrain[0];
            double roll = Unit(block) * total;
            for (int i = 0; i < scores.Length; i++)
            {
                if (drawLandscape[i] != landscape || scores[i] <= 0) continue;
                roll -= scores[i];
                if (roll < 0) return drawTerrain[i];
            }
            return drawTerrain[last];
        }

        /// <summary>
        /// The scale of the patch noise for zoomed-in blocks, in blocks of their own size. Several terrains usually share a
        /// landscape, so a draw crosses from one to the next well within this distance: patches come out a few blocks across.
        /// </summary>
        private const double ZoomPatchBlocks = 16;

        /// <summary>Terrains fitting less than this share as well as the best of their landscape are not drawn when zooming in.</summary>
        private const double ZoomMinShare = 0.1;

        /// <summary>
        /// The terrain for a land block made when zooming in (below the detail made with the world). The block keeps the
        /// landscape of the block it lies in wherever that fits at least (1 - TerrainSmoothing) as well as the best one.
        /// Where the parent's terrain belongs to the landscape chosen, patches covering about keepShare of the
        /// ground keep that terrain, so zooming in mostly shows more of what was there; elsewhere a terrain of that
        /// landscape is drawn from patch noise measured in blocks of its own size, so each level has patches of its own.
        /// </summary>
        public Terrain PickZoomed(TerrainSite site, WorldBlock block, Terrain parentTerrain, double keepShare)
        {
            double[] scores = Scores(site);
            double[] fits = LandscapeFits(scores);
            int landscape = BestLandscape(fits);
            if (landscape < 0) return drawTerrain[0];
            if (drawIndex == null)
            {
                drawIndex = new Dictionary<Terrain, int>();
                for (int i = 0; i < drawTerrain.Length; i++) drawIndex[drawTerrain[i]] = i;
            }
            int parentIndex = parentTerrain != null && drawIndex.TryGetValue(parentTerrain, out int found) ? found : -1;
            if (parentIndex >= 0)
            {
                // The parent's landscape carries on wherever it still fits nearly as well (as the top level evens out)
                int parentLandscape = drawLandscape[parentIndex];
                if (fits[parentLandscape] > 0 && fits[parentLandscape] >= (1 - rules.TerrainSmoothing) * fits[landscape]) landscape = parentLandscape;
            }
            double x = (block.TrueX + block.TrueWidth / 2.0) / block.TrueWidth, y = (block.TrueY + block.TrueHeight / 2.0) / block.TrueHeight;
            // Different levels sample different stretches of the noise, so their patches are unrelated
            double shift = 1000.0 * block.Depth;
            if (parentIndex >= 0 && keepShare > 0)
            {
                int index = parentIndex;
                if (drawLandscape[index] == landscape && scores[index] > 0
                    && ZoomUnit(x + shift + 517.3, y - shift + 291.1) < keepShare)
                    return parentTerrain;
            }
            // Terrains that hardly fit are left out: their thin slices of the draw would only show as stray specks and
            // lines between the patches of the others
            double top = 0;
            for (int i = 0; i < scores.Length; i++)
                if (drawLandscape[i] == landscape) top = Math.Max(top, scores[i]);
            double floor = Math.Max(1e-300, top * ZoomMinShare);
            double total = 0;
            int last = -1;
            for (int i = 0; i < scores.Length; i++)
                if (drawLandscape[i] == landscape && scores[i] >= floor) { total += scores[i]; last = i; }
            if (last < 0) return drawTerrain[0];
            double roll = ZoomUnit(x + shift, y + shift) * total;
            for (int i = 0; i < scores.Length; i++)
            {
                if (drawLandscape[i] != landscape || scores[i] < floor) continue;
                roll -= scores[i];
                if (roll < 0) return drawTerrain[i];
            }
            return drawTerrain[last];
        }

        // A number in [0, 1) from the zoom patch noise at a position in blocks
        private double ZoomUnit(double x, double y)
        {
            return Math.Max(0, Math.Min(1 - 1e-12, NormalCdf(zoomPatches.Fbm(x, y) / zoomSpread)));
        }

        // A number in [0, 1) fixed by the world seed and the block's position: from the smooth patch noise, or (without
        // patches) a hash
        private double Unit(WorldBlock block)
        {
            if (patches != null)
            {
                double x = (block.TrueX + block.TrueWidth / 2.0) / blockSize, y = (block.TrueY + block.TrueHeight / 2.0) / blockSize;
                double u = NormalCdf(patches.Fbm(x, y) / patchSpread);
                return Math.Max(0, Math.Min(1 - 1e-12, u));
            }
            ulong h = 1469598103934665603UL;
            foreach (long part in new long[] { worldSeed, block.TrueX, block.TrueY, block.Depth, 0x7e77a1 })
            {
                h ^= unchecked((ulong)part);
                h *= 1099511628211UL;
            }
            // SplitMix64 finish so neighbouring positions are unrelated
            h ^= h >> 30; h *= 0xbf58476d1ce4e5b9UL;
            h ^= h >> 27; h *= 0x94d049bb133111ebUL;
            h ^= h >> 31;
            return (h >> 11) * (1.0 / (1UL << 53));
        }

        // The standard normal distribution function (Abramowitz and Stegun 7.1.26, accurate to about 1e-7)
        private static double NormalCdf(double z)
        {
            double x = Math.Abs(z) / Math.Sqrt(2);
            double t = 1 / (1 + 0.3275911 * x);
            double erf = 1 - ((((1.061405429 * t - 1.453152027) * t + 1.421413741) * t - 0.284496736) * t + 0.254829592) * t * Math.Exp(-x * x);
            return z >= 0 ? (1 + erf) / 2 : (1 - erf) / 2;
        }

        private static double Clamp(double value, double low, double high)
        {
            return Math.Max(low, Math.Min(high, value));
        }

        // ---- Applying it to a world -----------------------------------------------------------

        /// <summary>
        /// How much water each top-level block holds: its moisture, plus up to RiverHydration where a lot of water flows
        /// through it and LakeHydration in the hollows that hold lakes.
        /// </summary>
        public double[,] CoarseHydration(bool[,] land, double[,] moisture, HydrologyModel hydrology)
        {
            int width = land.GetLength(0), height = land.GetLength(1);
            double[,] hydration = new double[width, height];
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    double river = hydrology != null ? Clamp(hydrology.Flow[x, y] / rules.RiverFlowFrom, 0, 1) : 0;
                    hydration[x, y] = moisture[x, y] + rules.RiverHydration * river;
                }
            if (hydrology != null)
                foreach (LakeBasin basin in hydrology.Basins)
                    if (basin.HasLake && basin.Open)
                        foreach ((int X, int Y) b in basin.Blocks) hydration[b.X, b.Y] += rules.LakeHydration;
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    hydration[x, y] = Clamp(hydration[x, y], 0, 1);
            return hydration;
        }

        /// <summary>
        /// Chooses the terrain of every top-level land block, from its height, climate, water, the continent-sized features
        /// on it (mountain ranges, deserts) and its plate boundary.
        ///
        /// At this scale (a thousand kilometres a block) a lone block of a different landscape among its neighbours is
        /// noise, so the landscapes are evened out first: a block takes the landscape most of the eight blocks around it
        /// (under the same features) have, if at least three of them have it, more than have its own, and it fits the block
        /// at least (1 - TerrainSmoothing) as well as the block's own. Each block's terrain is then drawn from its
        /// landscape.
        /// </summary>
        public void ApplyCoarse(World world, bool[,] land, double[,] hydration)
        {
            WorldBlock[,] grid = world.WorldBlock.ChildBlocks;
            blockSize = grid[0, 0].TrueWidth;
            int width = grid.GetLength(0), height = grid.GetLength(1);

            var scores = new double[width, height][];
            var fits = new double[width, height][];
            int[,] landscape = new int[width, height];
            string[,] features = new string[width, height];
            Dictionary<GeographicFeature, int> number = new Dictionary<GeographicFeature, int>();
            for (int i = 0; i < world.Features.Count; i++) number[world.Features[i]] = i;
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    grid[x, y].Status.Hydration = hydration[x, y];
                    if (!land[x, y]) continue;
                    WorldBlock block = grid[x, y];
                    WorldBlockStatus status = block.Status;
                    IReadOnlyList<GeographicFeature> on = world.FeaturesAt(block);
                    features[x, y] = string.Join("|", on.Select(f => number[f]));
                    scores[x, y] = Scores(new TerrainSite
                    {
                        Elevation = status.Elevation.Value,
                        Temperature = status.Temperature.Value,
                        Moisture = status.Moisture.Value,
                        Hydration = hydration[x, y],
                        Features = on,
                        Boundary = BoundaryAt(block)
                    });
                    fits[x, y] = LandscapeFits(scores[x, y]);
                    landscape[x, y] = BestLandscape(fits[x, y]);
                }

            for (int pass = 0; pass < 2 && rules.TerrainSmoothing > 0; pass++)
            {
                int[,] next = (int[,])landscape.Clone();
                for (int x = 0; x < width; x++)
                    for (int y = 0; y < height; y++)
                    {
                        if (!land[x, y]) continue;
                        Dictionary<int, int> votes = new Dictionary<int, int>();
                        for (int dx = -1; dx <= 1; dx++)
                            for (int dy = -1; dy <= 1; dy++)
                            {
                                int nx = x + dx, ny = y + dy;
                                if ((dx == 0 && dy == 0) || nx < 0 || ny < 0 || nx >= width || ny >= height || !land[nx, ny]) continue;
                                if (features[nx, ny] != features[x, y]) continue;
                                votes[landscape[nx, ny]] = (votes.ContainsKey(landscape[nx, ny]) ? votes[landscape[nx, ny]] : 0) + 1;
                            }
                        int own = landscape[x, y];
                        if (own < 0) continue;
                        int ownVotes = votes.ContainsKey(own) ? votes[own] : 0;
                        double ownFit = fits[x, y][own];
                        int best = own, bestVotes = ownVotes;
                        foreach (KeyValuePair<int, int> vote in votes.OrderBy(v => v.Key))
                        {
                            double fit = vote.Key >= 0 ? fits[x, y][vote.Key] : 0;
                            if (vote.Value >= 3 && vote.Value > bestVotes && fit >= (1 - rules.TerrainSmoothing) * ownFit) { best = vote.Key; bestVotes = vote.Value; }
                        }
                        next[x, y] = best;
                    }
                landscape = next;
            }

            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    if (land[x, y]) grid[x, y].Status.Terrain = Pick(scores[x, y], grid[x, y], landscape[x, y]);
        }

        /// <summary>
        /// Chooses the terrain of every land sub-block, once all features (down to peaks, oases and rivers) are known. Its
        /// climate and water are the top-level values smoothly interpolated to where it lies, plus LakeHydration beside a
        /// lake's shore.
        /// </summary>
        public void ApplyFine(World world, double[,] temperature, double[,] moisture, double[,] hydration)
        {
            ApplyFine(world, temperature, moisture, hydration, 0, world.WorldBlock.Width);
        }

        /// <summary>
        /// Fine terrain for the sub-blocks of the top-level columns from fromColumn up to (not including) toColumn. Each
        /// column is independent of the others, so the work can be split up and run in any order.
        /// </summary>
        public void ApplyFine(World world, double[,] temperature, double[,] moisture, double[,] hydration, int fromColumn, int toColumn)
        {
            WorldBlock[,] grid = world.WorldBlock.ChildBlocks;
            blockSize = grid[0, 0].TrueWidth;
            int width = grid.GetLength(0), height = grid.GetLength(1);
            for (int bx = Math.Max(0, fromColumn); bx < Math.Min(width, toColumn); bx++)
                for (int by = 0; by < height; by++)
                {
                    WorldBlock parent = grid[bx, by];
                    if (parent.ChildBlocks == null) continue;
                    int n = parent.ChildBlocks.GetLength(0);
                    for (int i = 0; i < n; i++)
                        for (int j = 0; j < parent.ChildBlocks.GetLength(1); j++)
                        {
                            WorldBlock child = parent.ChildBlocks[i, j];
                            WorldBlockStatus status = child.Status;
                            // Every sub-block records its own climate and water, smoothly blended from the top level, so
                            // finer detail made later can carry on from them
                            // Read at a nudged position, so the lines where the blended climate crosses a threshold
                            // (trees, marsh, a kind of climate) wander instead of running straight along the top-level grid
                            double gx = bx + (i + 0.5) / n, gy = by + (j + 0.5) / n;
                            (gx, gy) = (gx + ClimateWarp * climateWarp.Fbm(gx, gy), gy + ClimateWarp * climateWarp.Fbm(gx + 41.7, gy - 23.3));
                            status.Temperature = Clamp(Interpolate(temperature, gx, gy), 0, 1);
                            status.Moisture = Clamp(Interpolate(moisture, gx, gy), 0, 1);
                            status.Hydration = Clamp(Interpolate(hydration, gx, gy), 0, 1);
                            if (status.WaterStatus == WorldBlockWaterStatus.Water || status.Region == null) continue;
                            double shore = BesideLake(grid, bx * n + i, by * n + j) ? rules.LakeHydration : 0;
                            status.Hydration = Clamp(status.Hydration.Value + shore, 0, 1);
                            var boundary = BoundaryBlend(child);
                            status.Terrain = Pick(new TerrainSite
                            {
                                Elevation = status.Elevation.Value,
                                Temperature = status.Temperature.Value,
                                Moisture = status.Moisture.Value,
                                Hydration = status.Hydration.Value,
                                Features = world.FeaturesAt(child),
                                Boundary = boundary.Kind,
                                BoundaryStrength = boundary.Strength
                            }, child);
                        }
                }
        }

        /// <summary>
        /// The plate boundary at a block and how strongly it acts there. Boundaries are recorded on top-level blocks, so
        /// for anything finer they would change all at once along a top-level block's edge. Instead the four top-level
        /// blocks whose centres surround the block are blended by distance: the strongest kind among them, by its share,
        /// nudged by noise and eased so it stays full inside a boundary belt and fades out raggedly across its edge (over
        /// about a top-level block). Depends only on position. Top-level blocks get their own boundary at full strength.
        /// </summary>
        internal (PlateBoundaryKind Kind, double Strength) BoundaryBlend(WorldBlock block)
        {
            if (block.Depth <= 1) return (BoundaryAt(block), 1.0);
            WorldBlock[,] top = block.World?.WorldBlock?.ChildBlocks;
            if (top == null) return (BoundaryAt(block), 1.0);
            int width = top.GetLength(0), height = top.GetLength(1);
            double size = top[0, 0].TrueWidth;
            double gx = (block.TrueX + block.TrueWidth / 2.0) / size - 0.5, gy = (block.TrueY + block.TrueHeight / 2.0) / size - 0.5;
            int x0 = (int)Math.Floor(gx), y0 = (int)Math.Floor(gy);
            double tx = gx - x0, ty = gy - y0;
            PlateBoundaryKind best = PlateBoundaryKind.None;
            double bestShare = 0;
            var shares = new Dictionary<PlateBoundaryKind, double>();
            for (int k = 0; k < 4; k++)
            {
                int cx = Math.Max(0, Math.Min(width - 1, x0 + (k & 1))), cy = Math.Max(0, Math.Min(height - 1, y0 + (k >> 1)));
                PlateBoundaryKind kind = BoundaryAt(top[cx, cy]);
                if (kind == PlateBoundaryKind.None) continue;
                double share = ((k & 1) == 1 ? tx : 1 - tx) * ((k >> 1) == 1 ? ty : 1 - ty);
                shares[kind] = (shares.TryGetValue(kind, out double sum) ? sum : 0) + share;
                if (shares[kind] > bestShare) { bestShare = shares[kind]; best = kind; }
            }
            if (best == PlateBoundaryKind.None) return (best, 0);
            double nudged = bestShare + 0.3 * boundaryNoise.Fbm(gx * 2.5 + 57.3, gy * 2.5 - 21.9);
            double t = Clamp((nudged - 0.25) / 0.5, 0, 1);
            return (best, t * t * (3 - 2 * t));
        }

        internal static PlateBoundaryKind BoundaryAt(WorldBlock block)
        {
            for (WorldBlock b = block; b != null; b = b.ParentBlock)
                if (b.Status.Plate != null) return b.Status.Plate.BoundaryAt(b);
            return PlateBoundaryKind.None;
        }

        // Whether open-lake water is within two sub-blocks
        private static bool BesideLake(WorldBlock[,] grid, int gx, int gy)
        {
            for (int dx = -2; dx <= 2; dx++)
                for (int dy = -2; dy <= 2; dy++)
                {
                    WorldBlock cell = WaterFeatureDetector.FineCell(grid, gx + dx, gy + dy);
                    if (cell?.Status.Lake != null && cell.Status.Lake.Type.Id == WaterFeatureDetector.LakeTypeId) return true;
                }
            return false;
        }

        /// <summary>
        /// A top-level value at a position in top-level blocks (block (x, y) spans x to x + 1), blended between the four
        /// nearest block centres.
        /// </summary>
        private static double Interpolate(double[,] field, double gx, double gy)
        {
            int width = field.GetLength(0), height = field.GetLength(1);
            double fx = gx - 0.5, fy = gy - 0.5;
            int x0 = (int)Math.Floor(fx), y0 = (int)Math.Floor(fy);
            double tx = fx - x0, ty = fy - y0;
            Func<int, int, double> at = (x, y) => field[Math.Max(0, Math.Min(width - 1, x)), Math.Max(0, Math.Min(height - 1, y))];
            return (1 - tx) * (1 - ty) * at(x0, y0) + tx * (1 - ty) * at(x0 + 1, y0) + (1 - tx) * ty * at(x0, y0 + 1) + tx * ty * at(x0 + 1, y0 + 1);
        }
    }
}
