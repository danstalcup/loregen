using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LoreGen.Lang;
using LoreGen.Simulation;
using LoreGen.WorldGen;
using Xunit;

namespace LoreGen.Tests.Lang
{
    public class CommonLanguageTests
    {
        [Fact]
        public void EveryKindGetsNamesAndWords()
        {
            SimEngine engine = new SimEngine(11);
            for (int i = 0; i < 30; i++)
            {
                CommonLanguage language = engine.Language.GetRandomCommonLanguage();
                Assert.False(string.IsNullOrWhiteSpace(language.LanguageName));
                foreach (NameKind kind in Enum.GetValues<NameKind>())
                {
                    string name = language.GetName(kind);
                    Assert.False(string.IsNullOrWhiteSpace(name), kind + " name");
                    Assert.True(char.IsUpper(name[0]), "'" + name + "' should start with a capital");
                    string word = language.GetWord(kind);
                    Assert.DoesNotContain(' ', word);
                    Assert.InRange(word.Length, engine.SimData.CommonNames.Rules.MinWordLength, engine.SimData.CommonNames.Rules.MaxWordLength);
                }
            }
        }

        [Fact]
        public void SameSeedGivesTheSameNames()
        {
            Assert.Equal(Sample(21), Sample(21));
            Assert.NotEqual(Sample(21), Sample(22));
        }

        private static List<string> Sample(int seed)
        {
            SimEngine engine = new SimEngine(seed);
            List<string> names = new List<string>();
            for (int i = 0; i < 5; i++)
            {
                ILanguage language = engine.Language.GetRandomCommonLanguage();
                ILanguage dialect = language.CreateDialect();
                names.Add(language.LanguageName);
                names.Add(language.GetName(NameKind.Continent));
                names.Add(dialect.GetName(NameKind.Region));
                names.Add(language.GetName(NameKind.Ocean));
            }
            return names;
        }

        [Fact]
        public void DialectKeepsItsParentsFlavours()
        {
            SimEngine engine = new SimEngine(31);
            for (int i = 0; i < 10; i++)
            {
                CommonLanguage language = engine.Language.GetRandomCommonLanguage();
                CommonLanguage dialect = Assert.IsType<CommonLanguage>(language.CreateDialect());
                Assert.Same(language.PrimaryFlavor, dialect.PrimaryFlavor);
                Assert.Same(language.SecondaryFlavor, dialect.SecondaryFlavor);
            }
        }

        [Fact]
        public void LanguagesInAWorldPreferDifferentLeadingFlavours()
        {
            SimEngine engine = new SimEngine(41);
            int flavours = engine.SimData.CommonNames.Flavors.Count;
            var leads = Enumerable.Range(0, flavours).Select(_ => engine.Language.GetRandomCommonLanguage().PrimaryFlavor).ToList();
            Assert.True(leads.Distinct().Count() >= flavours / 2, "only " + leads.Distinct().Count() + " different flavours led " + flavours + " languages");
        }

        [Fact]
        public void DirectionTemplatesNeedADirection()
        {
            SimEngine engine = new SimEngine(51);
            string[] directional = { "North", "South", "East", "West" };
            for (int i = 0; i < 20; i++)
            {
                ILanguage language = engine.Language.GetRandomCommonLanguage();
                for (int j = 0; j < 30; j++)
                {
                    string region = language.GetName(new NameContext(NameKind.Region) { ParentName = "Valoria" });
                    string ocean = language.GetName(NameKind.Ocean);
                    Assert.DoesNotContain(region.Split(' '), w => directional.Any(d => w.StartsWith(d)));
                    Assert.DoesNotContain(ocean.Split(' '), w => directional.Any(d => w.StartsWith(d)));
                }
            }
        }

        [Fact]
        public void DirectionHintIsUsed()
        {
            SimEngine engine = new SimEngine(52);
            ILanguage language = engine.Language.GetRandomCommonLanguage();
            var context = new NameContext(NameKind.Ocean) { Direction = Directions.West };
            var names = Enumerable.Range(0, 300).Select(_ => language.GetName(context)).ToList();
            Assert.Contains("The Western Ocean", names);
        }

        [Theory]
        [InlineData("Swamp", "Wetland")]
        [InlineData("Glacier", "Frozen")]
        [InlineData("Glacial Mountain", "Frozen")]
        [InlineData("Mountain Forest", "Highland")]
        [InlineData("Broadleaf Forest", "Forest")]
        [InlineData("Arid Desert", "Desert")]
        [InlineData("Savannah", "Grassland")]
        public void BiomesMapToVocabularyCategories(string biomeName, string category)
        {
            SimEngine engine = new SimEngine(1);
            var biome = engine.SimData.Biomes.Single(b => b.Name == biomeName);
            Assert.Equal(category, engine.SimData.CommonNames.BiomeCategory(biome));
        }

        [Fact]
        public void EveryBiomeHasACategory()
        {
            SimEngine engine = new SimEngine(1);
            Assert.All(engine.SimData.Biomes, b => Assert.NotEqual(CommonNameData.AnyKey, engine.SimData.CommonNames.BiomeCategory(b)));
        }

        [Theory]
        [InlineData("Valda", "ia", "Valdia")]
        [InlineData("Ald", "dale", "Aldale")]
        [InlineData("Valor", "ia", "Valoria")]
        [InlineData("Lan", "ning", "Lanning")]
        [InlineData("Dzong", "guo", "Dzongguo")]
        [InlineData("Xi", "an", "Xian")]
        [InlineData("Grey", "moor", "Greymoor")]
        [InlineData("Hell", "lake", "Hellake")]
        public void JoinSmoothsTheSeam(string left, string right, string expected)
        {
            Assert.Equal(expected, CommonLanguage.Join(left, right));
        }

        [Fact]
        public void WorldNamesAreUniqueAndUseCommonStyleByDefault()
        {
            for (int seed = 1; seed <= 8; seed++)
            {
                SimEngine engine = new SimEngine(seed);
                engine.GenerateWorld();
                World world = engine.World;
                Assert.Equal(LanguageStyle.Common, world.LanguageStyle);
                Assert.IsType<CommonLanguage>(world.Language);

                var names = new List<string> { world.Name };
                names.AddRange(world.Oceans.Select(o => o.Name));
                foreach (Continent c in world.Continents)
                {
                    Assert.IsType<CommonLanguage>(c.RootLanguage);
                    names.Add(c.Name);
                    names.AddRange(c.Regions.Select(r => r.Name));
                    names.AddRange(c.Seas.Select(s => s.Name));
                }
                var repeats = names.GroupBy(n => n).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
                Assert.True(repeats.Count == 0, "seed " + seed + " repeats: " + string.Join(", ", repeats));
            }
        }

        [Theory]
        [InlineData("legacy")]
        [InlineData("noise")]
        public void LanguageStyleDoesNotChangeTheLand(string generatorId)
        {
            string common = Land(7, generatorId, LanguageStyle.Common);
            string phonetic = Land(7, generatorId, LanguageStyle.Phonetic);
            Assert.Equal(common, phonetic);
        }

        private static string Land(int seed, string generatorId, LanguageStyle style)
        {
            SimEngine engine = new SimEngine(seed);
            engine.Configuration.GeneratorId = generatorId;
            engine.Configuration.LanguageStyle = style;
            engine.GenerateWorld();
            return string.Join(",", engine.World.WorldBlock.ChildBlocksAsList().Select(b => (int)b.Status.WaterStatus + ":" + b.Status.Terrain?.Name));
        }

        [Fact]
        public void PhoneticStyleUsesPhoneticLanguages()
        {
            SimEngine engine = new SimEngine(9);
            engine.Configuration.LanguageStyle = LanguageStyle.Phonetic;
            engine.GenerateWorld();
            Assert.Equal(LanguageStyle.Phonetic, engine.World.LanguageStyle);
            Assert.All(engine.World.Continents, c => Assert.IsType<PhoneticLanguage>(c.RootLanguage));
        }

        [Fact]
        public void MissingFlavourSlotIsReported()
        {
            var ex = Assert.Throws<InvalidDataException>(() => new CommonNameData(
                new[] { "Test,Root,Ab" },
                new[] { "Region,Proper,1,{Root}" },
                new[] { "Adj,Any,Grey" },
                new RulesFile("CommonLanguageRules.csv", new string[0])));
            Assert.Contains("Middle", ex.Message);
        }

        [Fact]
        public void UnknownTemplateTokenIsReported()
        {
            SimEngine engine = new SimEngine(1);
            string[] flavours = NameFlavor.Slots.Select(s => "Test," + s + ",ab").ToArray();
            string[] templates = Enum.GetValues<NameKind>().Select(k => k + ",Proper,1,{Root}").Concat(new[] { "Region,Descriptive,1,{Nonsense}" }).ToArray();
            string[] rules = RulesLines();
            var ex = Assert.Throws<InvalidDataException>(() => new CommonNameData(flavours, templates, new[] { "Adj,Any,Grey" }, new RulesFile("CommonLanguageRules.csv", rules)));
            Assert.Contains("{Nonsense}", ex.Message);
        }

        private static string[] RulesLines()
        {
            using var stream = typeof(SimData).Assembly.GetManifestResourceStream("LoreGen.Resources.CommonLanguageRules.csv");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd().Split('\n');
        }
    }
}
