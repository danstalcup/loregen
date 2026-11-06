using LoreGen.Simulation;
using Xunit;

namespace LoreGen.Tests.Lang
{
    public class LanguageGenerationTests
    {
        [Fact]
        public void GetRandomLanguage_ProducesNonEmptyOutput()
        {
            SimEngine engine = new SimEngine();

            for (int i = 0; i < 5; i++)
            {
                LoreGen.Lang.Language language = engine.Language.GetRandomLanguage();
                Assert.False(string.IsNullOrWhiteSpace(language.LanguageName));
                Assert.False(string.IsNullOrEmpty(language.GetWord()));
            }
        }
    }
}
