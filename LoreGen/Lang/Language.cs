using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LoreGen.Simulation;
using LoreGen.Randomizer;

namespace LoreGen.Lang
{
    /// <summary>
    /// A simulated language based off of a real phonetic vocabulary, but with unique behavior patterns for word generation.
    /// </summary>
    public class Language
    {

        SimEngine Engine;

        PhoneticInventory PhoneticInventory;        

        bool CanHaveLongRime;
        double LongRimeProb;

        bool CanHaveComplexPreOnset;
        double ComplexPreOnsetProb;

        bool CanHaveComplexPostOnset;
        double ComplexPostOnsetProb;

        bool CanHaveCoda;     
        double CodaProb;

        bool CanHaveComplexCoda;
        double ComplexCodaProb;

        bool UsesHyphens;
        double HyphenProb;

        bool UsesApostrophes;

        double SyllablesMean;
        double SyllablesStdDev;

        double WordsMean;
        double WordsStdDev;

        double ExoticnessMean;
        double ExoticnessStdDev;

        double ProbFirstConsonantMissing;

        LanguageRules Rules
        {
            get
            {
                return Engine.SimData.LanguageRules;
            }
        }

        /// <summary>
        /// Name of the simulated language
        /// </summary>
        public string LanguageName;

        /// <summary>
        /// Returns a string showing all of the characteristics of the language
        /// </summary>
        /// <returns>String of the characteristics of the language</returns>
        public override string ToString()
        {
            string ret = "Language Name: " + LanguageName + "\n" +
                "Model Language: " + PhoneticInventory.ModelLanguage + "\n" +
                "Can Have Diphthong: " + CanHaveLongRime + "\n" +
                "Diphthong Probability: " + LongRimeProb + "\n" +
                "Can Have Complex Pre-Onset: " + CanHaveComplexPreOnset + "\n" +
                "Complex Pre-Onset Probability: " + ComplexPreOnsetProb + "\n" +
                "Can Have Complex Post-Onset: " + CanHaveComplexPostOnset + "\n" +
                "Complex Post-Onset Probability: " + ComplexPostOnsetProb + "\n" +
                "Can Have Coda: " + CanHaveCoda + "\n" +
                "Coda Probability: " + CodaProb + "\n" +
                "Can Have Complex Coda: " + CanHaveComplexCoda + "\n" +
                "Complex Coda Probability: " + ComplexCodaProb + "\n" +
                "Probability of starting words with vowels: " + ProbFirstConsonantMissing + "\n" +
                "Uses Hyphens Between Words: " + UsesHyphens + "\n" +
                "Hyphen Probability: " + HyphenProb + "\n" +
                "Uses Apostrophes To Denote Glottal Stops: " + UsesApostrophes + "\n" +
                "Syllables Per Word - Mean: " + SyllablesMean + "\n" +
                "Syllables Per Word - StdDev: " + SyllablesStdDev + "\n" +
                "Words Per Title - Mean: " + WordsMean + "\n" +
                "Words Per Title - StdDev: " + WordsStdDev + "\n" +
                "Exoticness of script - Mean: " + ExoticnessMean + "\n" +
                "Exoticness of script - StdDev: " + ExoticnessStdDev + "\n";
            
            return ret;


        }
        
        /// <summary>
        /// Returns a language with the same phonetic vocabulary but different behavior
        /// </summary>
        /// <returns></returns>
        public Language RandomCopy()
        {
            return RandomLanguageWithPhoneticInventory(this.PhoneticInventory, this.Engine);
        }

        /// <summary>
        /// Returns an exact copy of the language
        /// </summary>
        /// <returns>A copy of the language</returns>
        public Language Copy()
        {
            Language newLang = new Language();
            newLang.PhoneticInventory = PhoneticInventory;

            newLang.CanHaveLongRime = CanHaveLongRime;
            newLang.LongRimeProb = LongRimeProb;

            newLang.CanHaveComplexPreOnset=CanHaveComplexPreOnset;
            newLang.ComplexPreOnsetProb = ComplexPreOnsetProb;

            newLang.CanHaveComplexPostOnset = CanHaveComplexPostOnset;
            newLang.ComplexPostOnsetProb = ComplexPostOnsetProb;

            newLang.CanHaveCoda = CanHaveCoda;
            newLang.CodaProb = CodaProb;

            newLang.CanHaveComplexCoda = CanHaveComplexCoda;
            newLang.ComplexCodaProb = ComplexCodaProb;

            newLang.UsesHyphens = UsesHyphens;
            newLang.HyphenProb = HyphenProb;

            newLang.UsesApostrophes = UsesApostrophes;

            newLang.SyllablesMean = SyllablesMean;
            newLang.SyllablesStdDev = SyllablesStdDev;

            newLang.WordsMean = WordsMean;
            newLang.WordsStdDev = WordsStdDev;

            newLang.ExoticnessMean = ExoticnessMean;
            newLang.ExoticnessStdDev = ExoticnessStdDev;

            newLang.ProbFirstConsonantMissing = ProbFirstConsonantMissing;

            newLang.LanguageName = GetName(1);

            newLang.Engine = Engine;

            return newLang;

        }

        /// <summary>
        /// Returns a language that is the same, except some of the behaviors changed
        /// </summary>
        /// <param name="numElementsToSkew">The number of behaviors to change; -1 = a random number of behaviors</param>
        /// <returns>The resulting language</returns>
        public Language Skew(int numElementsToSkew=-1)
        {

            Rnd rnd = Engine.Rnd;

            if (numElementsToSkew == -1)
                numElementsToSkew = rnd.Unweighted(1, Rules.MaxSkewedElements);

            for (int i = 0; i < numElementsToSkew; i++)
            {                
                int sk = rnd.Unweighted(20);
                switch (sk)
                {
                    case 0:
                        CanHaveLongRime = !CanHaveLongRime;
                        break;
                    case 1:
                        LongRimeProb += rnd.Unweighted(-Rules.LongRimeSkew, Rules.LongRimeSkew);
                        if (LongRimeProb < 0) LongRimeProb = 0;
                        if (LongRimeProb > 1) LongRimeProb = 1;
                        break;
                    case 2:
                        CanHaveComplexPreOnset = !CanHaveComplexPreOnset;
                        break;
                    case 3:
                        ComplexPreOnsetProb += rnd.Unweighted(-Rules.ComplexPreOnsetSkew, Rules.ComplexPreOnsetSkew);
                        if (ComplexPreOnsetProb < 0) ComplexPreOnsetProb = 0;
                        if (ComplexPreOnsetProb > 1) ComplexPreOnsetProb = 1;
                        break;
                    case 4:
                        CanHaveComplexPostOnset = !CanHaveComplexPostOnset;
                        break;
                    case 5:
                        ComplexPostOnsetProb += rnd.Unweighted(-Rules.ComplexPostOnsetSkew, Rules.ComplexPostOnsetSkew);
                        if (ComplexPostOnsetProb < 0) ComplexPostOnsetProb = 0;
                        if (ComplexPostOnsetProb > 1) ComplexPostOnsetProb = 1;
                        break;
                    case 6:
                        CanHaveCoda = !CanHaveCoda;
                        break;
                    case 7:
                        CodaProb += rnd.Unweighted(-Rules.CodaSkew, Rules.CodaSkew);
                        if (CodaProb < 0) CodaProb = 0;
                        if (CodaProb > 1) CodaProb = 1;
                        break;
                    case 8:
                        CanHaveComplexCoda = !CanHaveComplexCoda;
                        break;
                    case 9:
                        ComplexCodaProb += rnd.Unweighted(-Rules.ComplexCodaSkew, Rules.ComplexCodaSkew);
                        if (ComplexCodaProb < 0) ComplexCodaProb = 0;
                        if (ComplexCodaProb > 1) ComplexCodaProb = 1;
                        break;
                    case 10:
                        UsesHyphens = !UsesHyphens;
                        break;
                    case 11:
                        HyphenProb += rnd.Unweighted(-Rules.HyphenSkew, Rules.HyphenSkew);
                        if (HyphenProb < 0) HyphenProb = 0;
                        if (HyphenProb > 1) HyphenProb = 1;
                        break;
                    case 12:
                        UsesApostrophes = !UsesApostrophes;
                        break;
                    case 13:
                        SyllablesMean += rnd.Unweighted(-Rules.SyllablesMeanSkew, Rules.SyllablesMeanSkew);
                        break;
                    case 14:
                        SyllablesStdDev += rnd.Unweighted(-Rules.SyllablesStdDevSkew, Rules.SyllablesStdDevSkew);
                        if (SyllablesStdDev < 0) SyllablesStdDev = .1;
                        break;
                    case 15:
                        WordsMean += rnd.Unweighted(-Rules.WordsMeanSkew, Rules.WordsMeanSkew);
                        break;
                    case 16:
                        WordsStdDev += rnd.Unweighted(-Rules.WordsStdDevSkew, Rules.WordsStdDevSkew);
                        if (WordsStdDev < 0) WordsStdDev = .1;
                        break;
                    case 17:
                        ExoticnessMean += rnd.Unweighted(-Rules.ExoticnessMeanSkew, Rules.ExoticnessMeanSkew);
                        break;
                    case 18:
                        ExoticnessStdDev += rnd.Unweighted(-Rules.ExoticnessStdDevSkew, Rules.ExoticnessStdDevSkew);
                        if (ExoticnessStdDev < 0) ExoticnessStdDev = .1;
                        break;
                    case 19:
                        ProbFirstConsonantMissing += rnd.Unweighted(-Rules.FirstConsonantSkippedSkew, Rules.FirstConsonantSkippedSkew);
                        if (ProbFirstConsonantMissing < 0) ProbFirstConsonantMissing = 0;
                        if (ProbFirstConsonantMissing > 1) ProbFirstConsonantMissing = 1;
                        break;
                }
            }

            return this;
        }

        /// <summary>
        /// Returns a language with the given phonetic vocabulary
        /// </summary>
        /// <param name="phoneticInventory">The phonetic vocabulary to use</param>
        /// <param name="engine">Simulation engine</param>
        /// <returns>A language with the same phonetic vocabulary</returns>
        public static Language RandomLanguageWithPhoneticInventory(PhoneticInventory phoneticInventory, SimEngine engine)
        {
            Language newLang = new Language();            
            newLang.Engine = engine;
            newLang.PhoneticInventory = phoneticInventory;

            Rnd rnd = newLang.Engine.Rnd;

            newLang.CanHaveLongRime = false;
            if (rnd.R01() < newLang.Rules.LongRimeProbability)
                newLang.CanHaveLongRime = true;

            newLang.LongRimeProb = rnd.Unweighted(newLang.Rules.LongRimeMin, newLang.Rules.LongRimeMax);

            newLang.CanHaveComplexPreOnset = false;
            if (rnd.R01() < newLang.Rules.ComplexPreOnsetProbability)
                newLang.CanHaveComplexPreOnset = true;

            newLang.ComplexPreOnsetProb = rnd.Unweighted(newLang.Rules.ComplexPreOnsetMin, newLang.Rules.ComplexPreOnsetMax);

            newLang.CanHaveComplexPostOnset = false;
            if (rnd.R01() < newLang.Rules.ComplexPostOnsetProbability)
                newLang.CanHaveComplexPostOnset = true;

            newLang.ComplexPostOnsetProb = rnd.Unweighted(newLang.Rules.ComplexPostOnsetMin, newLang.Rules.ComplexPostOnsetMax);

            newLang.CanHaveCoda = false;
            if (rnd.R01() < newLang.Rules.CodaProbability)
                newLang.CanHaveCoda = true;

            newLang.CodaProb = rnd.Unweighted(newLang.Rules.CodaMin, newLang.Rules.CodaMax);

            newLang.CanHaveComplexCoda = false;
            if (rnd.R01() < newLang.Rules.ComplexCodaProbability && newLang.CanHaveCoda)
                newLang.CanHaveComplexCoda = true;

            newLang.ComplexCodaProb = rnd.Unweighted(newLang.Rules.ComplexCodaMin, newLang.Rules.ComplexCodaMax);

            newLang.UsesHyphens = false;
            if (rnd.R01() < newLang.Rules.HyphenProbability)
                newLang.UsesHyphens = true;

            newLang.HyphenProb = rnd.Unweighted(newLang.Rules.HyphenMin, newLang.Rules.HyphenMax);

            newLang.UsesApostrophes = false;
            if (rnd.R01() < newLang.Rules.ApostropheProbability)
                newLang.UsesApostrophes = true;

            newLang.ProbFirstConsonantMissing = rnd.Unweighted(newLang.Rules.FirstConsonantSkippedMin, newLang.Rules.FirstConsonantSkippedMax);

            newLang.SyllablesMean = rnd.Unweighted(newLang.Rules.SyllablesMeanMin, newLang.Rules.SyllablesMeanMax);
            newLang.SyllablesStdDev = rnd.Unweighted(newLang.Rules.SyllablesStdDevMin, newLang.Rules.SyllablesStdDevMax);

            newLang.WordsMean = rnd.Unweighted(newLang.Rules.WordsMeanMin, newLang.Rules.WordsMeanMax);
            newLang.WordsStdDev = rnd.Unweighted(newLang.Rules.WordsStdDevMin, newLang.Rules.WordsStdDevMax);

            newLang.ExoticnessMean = rnd.Unweighted(newLang.Rules.ExoticnessMeanMin, newLang.Rules.ExoticnessMeanMax);
            newLang.ExoticnessStdDev = rnd.Unweighted(newLang.Rules.ExoticnessStdDevMin, newLang.Rules.ExoticnessStdDevMax);            

            newLang.LanguageName = newLang.GetName(1);

            return newLang;

        }

        /// <summary>
        /// Generates a name using the rules of the language
        /// </summary>
        /// <param name="numWords">Number of words in the name (by default it randomly chooses)</param>
        /// <returns>A name in the language</returns>
        public string GetName(int numWords=0)
        {
            Rnd rnd = Engine.Rnd;
            string name = "";

            if (numWords == 0)
            {
                numWords = rnd.NormalAsInt(WordsMean, WordsStdDev);
            }
            if (numWords < 1) numWords = 1;

            List<string> words = new List<string>();

            for (int i = 0; i < numWords; i++)
            {
                words.Add(GetWord());
            }            

            bool firstWord = true;
            foreach (string word in words)
            {
                string thisWord = word;
                if (firstWord)
                {
                    thisWord = UppercaseFirst(word);
                    name += thisWord;
                }
                else if (UsesApostrophes && IsVowel(name[name.Length - 1]) && IsVowel(thisWord[0]))
                {
                    name += "\'";
                    name += thisWord;
                }
                else if (UsesHyphens && rnd.R01() < HyphenProb)
                {
                    thisWord = UppercaseFirst(word);
                    name += "-" + thisWord;
                }
                else
                {
                    thisWord = UppercaseFirst(word);
                    name += " " + thisWord;
                }

                firstWord = false;
            }

            
            return name;
        }

        private bool IsVowel(char c)
        {
            switch (c)
            {
                case 'a':
                    return true;
                case 'e':
                    return true;
                case 'i':
                    return true;
                case 'o':
                    return true;
                case 'u':
                    return true;
                case 'A':
                    return true;
                case 'E':
                    return true;
                case 'I':
                    return true;
                case 'O':
                    return true;
                case 'U':
                    return true;
            }
            return false;
        }

        private static string UppercaseFirst(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }

            return char.ToUpper(s[0]) + s.Substring(1);
        }

        /// <summary>
        /// Returns a world using the rules of the language
        /// </summary>
        /// <param name="debug">Set to true for testing purposes only</param>
        /// <returns>A word in the given language</returns>
        public string GetWord(bool debug=false)
        {
            Rnd rnd = Engine.Rnd;
            String word = "";
            String wordDebug = "";

            int numsyll = rnd.NormalAsInt(SyllablesMean, SyllablesStdDev);

            if (numsyll < 1) numsyll = 1;

            for (int i = 0; i < numsyll; i++)
            {
                String syl = GetSyllable(i == 0);
                word += syl;
                wordDebug += syl + " ";
            }
            if (debug)
            {
                word += "\nNumber of Syllables: " + numsyll + "\n";
                word += wordDebug + "\n";
            }

            return word;
        }

        /// <summary>
        /// Returns one syllable using the rules of the language
        /// </summary>
        /// <param name="firstSyllable">Is this the first syllable in the world?</param>
        /// <returns>A syllable in the language</returns>
        public string GetSyllable(bool firstSyllable=false)
        {
            Rnd rnd = Engine.Rnd;
            List<LetterSound> syll = new List<LetterSound>();
            bool skipOnset = false;

            if (firstSyllable && rnd.R01() < ProbFirstConsonantMissing)
            {
                skipOnset = true;
            }

            if (!skipOnset)
            {

                if (CanHaveComplexPreOnset)
                {
                    if (rnd.R01() < ComplexPreOnsetProb)
                        syll.Add(ListR<LetterSound>.RandomFromList(PhoneticInventory.ConsonantsSZ,rnd));
                }

                syll.Add(ListR<LetterSound>.RandomFromList(PhoneticInventory.Consonants,rnd));

                if (CanHaveComplexPostOnset)
                {
                    if (rnd.R01() < ComplexPostOnsetProb)
                        syll.Add(ListR<LetterSound>.RandomFromList(PhoneticInventory.ConsonantsRL,rnd));
                }

            }

            syll.Add(ListR<LetterSound>.RandomFromList(PhoneticInventory.Vowels,rnd));

            if (CanHaveLongRime)
            {
                if (rnd.R01() < LongRimeProb)
                    syll.Add(ListR<LetterSound>.RandomFromList(PhoneticInventory.Vowels,rnd));
            }

            if (CanHaveCoda)
            {
                if (rnd.R01() < CodaProb)
                {                    
                    if (CanHaveComplexCoda)
                    {
                        if (rnd.R01() < ComplexCodaProb)
                            syll.Add(ListR<LetterSound>.RandomFromList(PhoneticInventory.ConsonantsSZRL,rnd));                        
                    }
                    syll.Add(ListR<LetterSound>.RandomFromList(PhoneticInventory.Consonants,rnd));
                }
            }

            String sy = "";

            foreach (LetterSound ls in syll)
            {
                double exoticnessRange = (double)(ls.Written.Count());
                double soundExoticness = rnd.Normal(ExoticnessMean, ExoticnessStdDev);
                if (soundExoticness > 6.0) soundExoticness = 6.0;

                soundExoticness *= (exoticnessRange / 6.0);
                soundExoticness -= 1;

                int exot = (int)(Math.Round(soundExoticness, MidpointRounding.AwayFromZero));

                if (exot < 0) exot = 0;                

                sy += ls.Written.ElementAt(exot);
            }

            return sy;
        }              

    }

    /// <summary>
    /// Collects values used when generating random words and languages.
    /// </summary>
    public struct LanguageRules
    {
        /// <summary>
        /// Probability a language uses long rimes/diphthongs.
        /// </summary>
        public double LongRimeProbability;
        /// <summary>
        /// Minimum syllabic probability of long rimes/diphthongs.
        /// </summary>
        public double LongRimeMin;
        /// <summary>
        /// Maximum syllabic probability of long rimes/diphthongs.
        /// </summary>
        public double LongRimeMax;
        /// <summary>
        /// Amount long rime probability can skew
        /// </summary>
        public double LongRimeSkew;
        /// <summary>
        /// Probability a language uses complex pre-onsets.
        /// </summary>
        public double ComplexPreOnsetProbability;
        /// <summary>
        /// Minimum syllabic probability of complex pre-onsets
        /// </summary>
        public double ComplexPreOnsetMin;
        /// <summary>
        /// Maximum syllabic probability of complex pre-onsets
        /// </summary>
        public double ComplexPreOnsetMax;
        /// <summary>
        /// Amount complex pre-onset probability can skew
        /// </summary>
        public double ComplexPreOnsetSkew;
        /// <summary>
        /// Probability a language uses complex post-onsets
        /// </summary>
        public double ComplexPostOnsetProbability;
        /// <summary>
        /// Minimum syllabic probability of complex post-onsets
        /// </summary>
        public double ComplexPostOnsetMin;
        /// <summary>
        /// Maximum syllabic prbability of complex post-onsets
        /// </summary>
        public double ComplexPostOnsetMax;
        /// <summary>
        /// Amount complex post-onset probability can skew
        /// </summary>
        public double ComplexPostOnsetSkew;
        /// <summary>
        /// Probability a language uses codas
        /// </summary>
        public double CodaProbability;
        /// <summary>
        /// Minimum syllabic probability of codas
        /// </summary>
        public double CodaMin;
        /// <summary>
        /// Maximum syllabic probability of codas
        /// </summary>
        public double CodaMax;
        /// <summary>
        /// Amount coda probability can skew
        /// </summary>
        public double CodaSkew;
        /// <summary>
        /// Probability a language uses complex codas (assuming they also use codas)
        /// </summary>
        public double ComplexCodaProbability;
        /// <summary>
        /// Minimum syllabic probability of complex codas
        /// </summary>
        public double ComplexCodaMin;
        /// <summary>
        /// Maximum syllabic probability of complex codas
        /// </summary>
        public double ComplexCodaMax;
        /// <summary>
        /// Amount complex coda probability can skew
        /// </summary>
        public double ComplexCodaSkew;
        /// <summary>
        /// Probability a language uses hyphens between words
        /// </summary>
        public double HyphenProbability;
        /// <summary>
        /// Minimum probability a word transition uses hyphens
        /// </summary>
        public double HyphenMin;
        /// <summary>
        /// Maximum probability a word transition uses hyphens
        /// </summary>
        public double HyphenMax;
        /// <summary>
        /// Amount hyphen probability can skew
        /// </summary>
        public double HyphenSkew;
        /// <summary>
        /// Probability a language uses apostrophes to denote glottal stops between words
        /// </summary>
        public double ApostropheProbability;
        /// <summary>
        /// Minimum probability words skip their first consonant (start with a vowel)
        /// </summary>
        public double FirstConsonantSkippedMin;
        /// <summary>
        /// Maximum probability words skip their first consonant (start with a vowel)
        /// </summary>
        public double FirstConsonantSkippedMax;
        /// <summary>
        /// Amount skipped first consonant probability can skew
        /// </summary>
        public double FirstConsonantSkippedSkew;
        /// <summary>
        /// Minimum mean syllables per word
        /// </summary>
        public double SyllablesMeanMin;
        /// <summary>
        /// Maximum mean syllables per word
        /// </summary>
        public double SyllablesMeanMax;
        /// <summary>
        /// Amount mean syllables per word can skew
        /// </summary>
        public double SyllablesMeanSkew;
        /// <summary>
        /// Minimum standard deviation syllables per word
        /// </summary>
        public double SyllablesStdDevMin;
        /// <summary>
        /// Maximum standard deviation syllables per word
        /// </summary>
        public double SyllablesStdDevMax;
        /// <summary>
        /// Amount standard deviation syllbles per word can skew
        /// </summary>
        public double SyllablesStdDevSkew;
        /// <summary>
        /// Minimum mean words per name
        /// </summary>
        public double WordsMeanMin;
        /// <summary>
        /// Maximum mean words per name
        /// </summary>
        public double WordsMeanMax;
        /// <summary>
        /// Amount mean words per name can skew
        /// </summary>
        public double WordsMeanSkew;
        /// <summary>
        /// Maximum standard deviation words per name
        /// </summary>
        public double WordsStdDevMin;
        /// <summary>
        /// Minimum standard deviation words per name
        /// </summary>
        public double WordsStdDevMax;
        /// <summary>
        /// Amount standard deviation words per name can skew
        /// </summary>
        public double WordsStdDevSkew;
        /// <summary>
        /// Minimum mean script exoticness factor
        /// </summary>
        public double ExoticnessMeanMin;
        /// <summary>
        /// Maximum mean script exoticness factor
        /// </summary>
        public double ExoticnessMeanMax;
        /// <summary>
        /// Amount script exoticness factor mean can skew
        /// </summary>
        public double ExoticnessMeanSkew;
        /// <summary>
        /// Minimum standard deviation of script exoticness factor
        /// </summary>
        public double ExoticnessStdDevMin;
        /// <summary>
        /// Maximum standard deviation of script exoticness factor
        /// </summary>
        public double ExoticnessStdDevMax;
        /// <summary>
        /// Amount standard deviation of script exoticness can skew
        /// </summary>
        public double ExoticnessStdDevSkew;
        /// <summary>
        /// Maximum number of skewed elements when generating new languages
        /// </summary>
        public int MaxSkewedElements;

    }
}
