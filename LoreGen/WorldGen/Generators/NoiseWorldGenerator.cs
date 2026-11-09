using System;
using System.Collections.Generic;
using System.Linq;

using LoreGen.Features;
using LoreGen.Noise;
using LoreGen.Randomizer;
using LoreGen.Simulation;
using LoreGen.Terrains;

namespace LoreGen.WorldGen.Generators
{
    /// <summary>
    /// Builds the land from a height field of fractal noise instead of carving it out of a partitioned grid.
    ///
    /// An elevation value is computed for every block (fractal noise, bent by domain warping, pulled up around a few
    /// randomly placed continent centres and down toward the map edges). Sea level is then chosen so that a target
    /// share of the map is land. Continents, islands, inland seas and oceans are discovered from the result rather than
    /// decided beforehand, so the number of continents is a target, not a promise.
    ///
    /// Everything after the elevation (climate, features, regions, fine detail) is shared with the other elevation-based
    /// generators: see <see cref="ElevationWorldBuilder"/>.
    ///
    /// Settings are in NoiseGeneratorRules.csv; world size and the number of continents and regions come from the
    /// shared WorldRules.csv.
    /// </summary>
    public class NoiseWorldGenerator : IWorldGenerator
    {
        /// <summary>
        /// Id of this generator.
        /// </summary>
        public const string GeneratorId = "noise";

        public string Id { get { return GeneratorId; } }

        public string DisplayName { get { return "Noise"; } }

        public string Description
        {
            get { return "Land is shaped by an elevation map of fractal noise, so continents have natural coastlines, peninsulas, islands and inland seas."; }
        }

        public string Version { get { return "7"; } }

        public IReadOnlyList<GeneratorOption> GetOptions(SimData data)
        {
            // Defaults come from the rules file, so they follow any customised data
            RulesFile file = data.LoadRules(NoiseGeneratorRules.Filename);
            Func<string, double> usual = key => file.D(key);

            List<GeneratorOption> options = new List<GeneratorOption>
            {
                GeneratorOption.Number("LandFraction", "Land share", "How much of the map is land. Random varies it from world to world.", "Land", 0, 0.7, 0.01, usual("LandFraction"), "random"),
                GeneratorOption.Integer("ContinentCentres", "Land centres", "How many places land gathers around. More centres spread the land over more of the map. How many continents result varies, since land can merge or break apart.", "Land", 0, 12, (int)usual("ContinentCentres"), "random"),
                GeneratorOption.Number("MaskStrength", "Continent blobbiness", "Low: land is shaped by noise alone and is scattered. High: land gathers into smooth blobs around the centres.", "Land", 0, 1, 0.05, usual("MaskStrength")),
                GeneratorOption.Number("Frequency", "Fragmentation", "Higher gives smaller, more numerous features.", "Land", 1, 8, 0.25, usual("Frequency")),
                GeneratorOption.Number("WarpStrength", "Coast winding", "How much coastlines twist and wind.", "Land", 0, 1.5, 0.05, usual("WarpStrength")),
            };
            options.AddRange(ElevationWorldBuilder.SharedOptions(data));
            return options;
        }

        public World Generate(SimEngine engine)
        {
            NoiseGeneratorRules rules = NoiseGeneratorRules.Load(engine.SimData, engine.Configuration.GeneratorSettings);
            Rnd rnd = engine.Rnd;

            World world = new World(engine);
            WorldGridBuilder.InitializeWorldBlock(world);
            int width = world.WorldBlock.Width, height = world.WorldBlock.Height;

            // The elevation field; everything after that is shared with the other elevation-based generators
            double landFraction = ElevationWorldBuilder.ChooseLandFraction(rules, rnd);
            Heightmap field = BuildField(engine, rules, width, height);
            return ElevationWorldBuilder.Build(engine, world, rules, field, landFraction);
        }

        // ---- Elevation ------------------------------------------------------------------------

        private static Heightmap BuildField(SimEngine engine, NoiseGeneratorRules rules, int width, int height)
        {
            Rnd rnd = engine.Rnd;
            WorldRules worldRules = engine.SimData.WorldRules;
            FractalNoise noise = new FractalNoise(new SimplexNoise(rnd.Unweighted(int.MaxValue)),
                rules.Octaves, rules.Frequency, rules.Lacunarity, rules.Persistence);

            int centreCount = worldRules.ContinentsMax > worldRules.ContinentsMin
                ? rnd.Unweighted(worldRules.ContinentsMin, worldRules.ContinentsMax)
                : worldRules.ContinentsMin;
            if (rules.ContinentCentres > 0) centreCount = rules.ContinentCentres;
            centreCount = Math.Max(1, centreCount);
            double[] centreU = new double[centreCount], centreV = new double[centreCount], radius = new double[centreCount];
            for (int i = 0; i < centreCount; i++)
            {
                centreU[i] = rnd.Unweighted(0.12, 0.88);
                centreV[i] = rnd.Unweighted(0.15, 0.85);
                radius[i] = rnd.Unweighted(rules.MaskRadiusMin, rules.MaskRadiusMax);
            }

            // Work in units where the map is `aspect` wide and 1 tall, so features aren't stretched
            double aspect = (double)width / height;
            return Heightmap.Sample(width, height, (u, v) =>
            {
                double n = (noise.Warped(u * aspect, v, rules.WarpStrength) + 1.0) * 0.5;

                double mask = 0.0;
                for (int i = 0; i < centreCount; i++)
                {
                    double dx = (u - centreU[i]) * aspect, dy = v - centreV[i];
                    double d = Math.Sqrt(dx * dx + dy * dy) / radius[i];
                    if (d < 1.0) mask = Math.Max(mask, 1.0 - Smoothstep(0.0, 1.0, d));
                }

                double edge = Math.Min(Math.Min(u, 1.0 - u), Math.Min(v, 1.0 - v));
                double edgeFactor = Smoothstep(0.0, rules.EdgeBorder, edge);

                return ((1.0 - rules.MaskStrength) * n + rules.MaskStrength * mask) * edgeFactor;
            });
        }

        private static double Smoothstep(double from, double to, double x)
        {
            double t = Math.Min(1.0, Math.Max(0.0, (x - from) / (to - from)));
            return t * t * (3.0 - 2.0 * t);
        }

    }
}
