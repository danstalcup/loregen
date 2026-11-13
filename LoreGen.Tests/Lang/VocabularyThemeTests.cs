using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LoreGen.Lang;
using LoreGen.Simulation;
using LoreGen.WorldGen;
using LoreGen.WorldGen.Generators;
using Xunit;

namespace LoreGen.Tests.Lang
{
    /// <summary>
    /// Word themes (CommonNameWordTags.csv), the themes each kind of name avoids and prefers (CommonNameKindThemes.csv), and
    /// names that suit the climate of the place they name.
    /// </summary>
    public class VocabularyThemeTests
    {
        private static readonly SimEngine Engine = MakeEngine();

        private static SimEngine MakeEngine()
        {
            var engine = new SimEngine(3);
            engine.Configuration.LanguageStyle = LanguageStyle.Common;
            engine.GenerateWorld();
            return engine;
        }

        private static CommonNameData Data => Engine.SimData.CommonNames;

        // Many names of a kind, each from its own seed
        private static List<string> Names(NameKind kind, double? temperature = null, double? moisture = null, int count = 400)
        {
            var language = Engine.World.Continents[0].RootLanguage;
            return Enumerable.Range(0, count).Select(i => Engine.Language.NameFromSeed(language,
                new NameContext(kind) { ParentName = "Valoria", Temperature = temperature, Moisture = moisture }, i)).ToList();
        }

        // Whether a name has a separate word with one of the tags (joined words such as "Emberfire" aren't split apart)
        private static bool Has(string name, params string[] tags) =>
            name.Split(' ', '-').Any(w => Data.Tags(w).Any(t => tags.Contains(t, StringComparer.OrdinalIgnoreCase)));

        [Fact]
        public void ThemesAreLoaded()
        {
            Assert.Contains("hot", Data.Tags("Burning"));
            Assert.Contains("cold", Data.Tags("Frozen"));
            Assert.Empty(Data.Tags("Whispering"));
            Assert.Contains("hot", Data.Themes(NameKind.Glacier).Avoid);
            Assert.Contains("fire", Data.Themes(NameKind.Volcano).Prefer);
            Assert.Empty(Data.Themes(NameKind.Region).Avoid);
        }

        [Theory]
        [InlineData(NameKind.Glacier, "hot", "fire")]
        [InlineData(NameKind.IceSheet, "hot", "fire", "lush")]
        [InlineData(NameKind.Volcano, "cold", "ice", "wet")]
        [InlineData(NameKind.Desert, "cold", "ice", "wet", "lush")]
        [InlineData(NameKind.Forest, "fire", "dry")]
        [InlineData(NameKind.Lake, "fire", "dry")]
        public void KindsNeverGetWordsTheyAvoid(NameKind kind, params string[] tags)
        {
            Assert.DoesNotContain(Names(kind), n => Has(n, tags));
        }

        [Fact]
        public void NamesSuitTheClimateOfThePlace()
        {
            Assert.DoesNotContain(Names(NameKind.Region, temperature: 0.05), n => Has(n, "hot", "fire"));
            Assert.DoesNotContain(Names(NameKind.Region, temperature: 0.95), n => Has(n, "cold", "ice"));
            Assert.DoesNotContain(Names(NameKind.MountainRange, moisture: 0.05), n => Has(n, "wet", "lush"));
            Assert.DoesNotContain(Names(NameKind.Subregion, moisture: 0.95), n => Has(n, "dry"));
        }

        [Fact]
        public void KindsFavourTheirThemes()
        {
            // A volcano's names use the words it favours (fire, hot and dark) far more often than a mountain range's do
            double Share(NameKind kind) => Names(kind, count: 1500).Count(n => Has(n, "fire", "hot", "dark")) / 1500.0;
            Assert.True(Share(NameKind.Volcano) > 1.5 * Share(NameKind.MountainRange) + 0.005, $"volcano {Share(NameKind.Volcano):P1}, range {Share(NameKind.MountainRange):P1}");
        }

        [Fact]
        public void ThemeFilesCanBeGivenDirectly()
        {
            string[] flavours = NameFlavor.Slots.Select(s => "Test," + s + ",ab").ToArray();
            string[] templates = Enum.GetValues<NameKind>().Select(k => k + ",Proper,1,{Root}+{Ending}")
                .Concat(new[] { "Glacier,Descriptive,100,The {Adj} Glacier" }).ToArray();
            var data = new CommonNameData(flavours, templates, new[] { "Adj,Any,Burning,Sunlit" }, new RulesFile("CommonLanguageRules.csv", RulesLines()),
                new[] { "Burning,hot", "Sunlit,hot" }, new[] { "Glacier,hot," });
            Assert.Contains("hot", data.Tags("Sunlit"));
            Assert.Contains("hot", data.Themes(NameKind.Glacier).Avoid);
        }

        [Fact]
        public void TagsMustBeForWordsInTheVocabulary()
        {
            var ex = Assert.Throws<InvalidDataException>(() => Build(new[] { "Smouldering,hot" }, new string[0]));
            Assert.Contains("Smouldering", ex.Message);
        }

        [Fact]
        public void ThemesMustBeForKnownKinds()
        {
            var ex = Assert.Throws<InvalidDataException>(() => Build(new string[0], new[] { "Volcanoes,cold," }));
            Assert.Contains("Volcanoes", ex.Message);
        }

        [Fact]
        public void WorldsNameTheirPlacesForTheirClimate()
        {
            // No feature or region in a generated world clashes with its own kind's avoided themes
            var engine = new SimEngine(11);
            engine.Configuration.GeneratorId = TectonicWorldGenerator.GeneratorId;
            engine.GenerateWorld();
            var data = engine.SimData.CommonNames;
            foreach (var feature in engine.World.Features)
            {
                var avoid = data.Themes(feature.Type.NameKind).Avoid;
                Assert.DoesNotContain(feature.Name.Split(' ', '-'), w => data.Tags(w).Any(avoid.Contains));
            }
        }

        private static CommonNameData Build(string[] tags, string[] themes)
        {
            string[] flavours = NameFlavor.Slots.Select(s => "Test," + s + ",ab").ToArray();
            string[] templates = Enum.GetValues<NameKind>().Select(k => k + ",Proper,1,{Root}").ToArray();
            return new CommonNameData(flavours, templates, new[] { "Adj,Any,Grey,Burning" }, new RulesFile("CommonLanguageRules.csv", RulesLines()), tags, themes);
        }

        private static string[] RulesLines()
        {
            using var stream = typeof(SimData).Assembly.GetManifestResourceStream("LoreGen.Resources.CommonLanguageRules.csv");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd().Split('\n');
        }
    }
}
