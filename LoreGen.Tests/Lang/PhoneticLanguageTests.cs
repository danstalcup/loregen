using System;
using LoreGen.Lang;
using LoreGen.Simulation;
using Xunit;

namespace LoreGen.Tests.Lang
{
    public class PhoneticLanguageTests
    {
        [Fact]
        public void GetRandomPhoneticLanguage_ProducesNonEmptyOutput()
        {
            SimEngine engine = new SimEngine(3);

            for (int i = 0; i < 5; i++)
            {
                PhoneticLanguage language = engine.Language.GetRandomPhoneticLanguage();
                Assert.False(string.IsNullOrWhiteSpace(language.LanguageName));
                foreach (NameKind kind in Enum.GetValues<NameKind>())
                {
                    Assert.False(string.IsNullOrWhiteSpace(language.GetName(kind)));
                    Assert.False(string.IsNullOrEmpty(language.GetWord(kind)));
                }
            }
        }

        [Fact]
        public void ContinentAndLanguageNamesAreOneWord()
        {
            SimEngine engine = new SimEngine(4);
            for (int i = 0; i < 20; i++)
            {
                PhoneticLanguage language = engine.Language.GetRandomPhoneticLanguage();
                Assert.DoesNotContain(' ', language.LanguageName);
                Assert.DoesNotContain(' ', language.GetName(NameKind.Continent));
            }
        }

        [Fact]
        public void DialectIsANewPhoneticLanguage()
        {
            SimEngine engine = new SimEngine(5);
            PhoneticLanguage language = engine.Language.GetRandomPhoneticLanguage();
            ILanguage dialect = language.CreateDialect();
            Assert.IsType<PhoneticLanguage>(dialect);
            Assert.NotSame(language, dialect);
        }
    }
}
