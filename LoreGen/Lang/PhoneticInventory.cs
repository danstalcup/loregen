using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LoreGen.Lang
{
    /// <summary>
    /// A collection of "letters" representing the possible sounds for use in a language
    /// </summary>
    public class PhoneticInventory
    {
        /// <summary>
        /// All "letters"
        /// </summary>
        public List<LetterSound> AllLetters;
        /// <summary>
        /// Vowel letters
        /// </summary>
        public List<LetterSound> Vowels;
        /// <summary>
        /// Consonant letters
        /// </summary>
        public List<LetterSound> Consonants;
        /// <summary>
        /// Consonant letters that have an SZ sound (important for word generation)
        /// </summary>        
        public List<LetterSound> ConsonantsSZ;
        /// <summary>
        /// Consonant letters that have an RL sound (important for word generation)
        /// </summary>
        public List<LetterSound> ConsonantsRL;
        /// <summary>
        /// Consonant letters that have either an RL or an SZ sound (important for word generation)
        /// </summary>
        public List<LetterSound> ConsonantsSZRL;
        /// <summary>
        /// The name of the language this phonetic vocabulary is derived from
        /// </summary>
        public string ModelLanguage;

        /// <summary>
        /// Builds a phonetic inventory from the letters given
        /// </summary>
        /// <param name="letters">All letters in the inventory</param>
        /// <param name="modelLanguage">Name of the language this is modeled after</param>
        public PhoneticInventory(List<LetterSound> letters, string modelLanguage)
        {
            ModelLanguage = modelLanguage;
            AllLetters = letters;
            Vowels = letters.Where(l => l.IsVowel).ToList();
            Consonants = letters.Where(l => !l.IsVowel).ToList();
            ConsonantsSZ = letters.Where(l => !l.IsVowel && l.IsSZLike).ToList();
            ConsonantsRL = letters.Where(l => !l.IsVowel && l.IsRLLike).ToList();
            ConsonantsSZRL = letters.Where(l => !l.IsVowel && (l.IsSZLike || l.IsRLLike)).ToList();
        }
    }
}
