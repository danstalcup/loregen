using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LoreGen.Lang
{
    /// <summary>
    /// A specific sound represented by one or more letters
    /// </summary>
    public class LetterSound
    {
        /// <summary>
        /// The internal ID of this letter (used to match sounds to languages)
        /// </summary>
        public int ID;
        /// <summary>
        /// A list of the ways to represent this language, ordered in ascending levels of "exotic" script
        /// </summary>
        public List<string> Written;
        /// <summary>
        /// Is this a vowel?
        /// </summary>
        public bool IsVowel;
        /// <summary>
        /// Is this an S/Z-like consonant? (Important for word generation)
        /// </summary>
        public bool IsSZLike;
        /// <summary>
        /// Is this an R/L-like consonant? (Important for word generation)
        /// </summary>
        public bool IsRLLike;
    }
}
