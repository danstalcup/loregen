namespace LoreGen.Lang
{
    /// <summary>
    /// Something that can name things in a world. Implementations differ in how names are made (see
    /// <see cref="PhoneticLanguage"/> and <see cref="CommonLanguage"/>), but callers only ask for names and words for a
    /// given <see cref="NameContext"/>.
    /// </summary>
    public interface ILanguage
    {
        /// <summary>
        /// Name of the language itself, for example "Valdish".
        /// </summary>
        string LanguageName { get; }

        /// <summary>
        /// A full name suited to the context, for example "The Sea of Mists" for an ocean or "Aldermere" for a region.
        /// </summary>
        /// <param name="context">What is being named, plus optional hints</param>
        string GetName(NameContext context);

        /// <summary>
        /// A single word in the language suited to the context: no articles, no descriptive phrases.
        /// </summary>
        /// <param name="context">What the word is for, plus optional hints</param>
        string GetWord(NameContext context);

        /// <summary>
        /// A related language that sounds like this one but is noticeably different, used for the regions of a continent.
        /// </summary>
        ILanguage CreateDialect();
    }
}
