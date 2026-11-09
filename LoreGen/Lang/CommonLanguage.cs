using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using LoreGen.Randomizer;
using LoreGen.Simulation;
using LoreGen.WorldGen;

namespace LoreGen.Lang
{
    /// <summary>
    /// A language whose names read like those in fantasy and historical fiction: "Valoria", "Aldermere", "the Sea of
    /// Storms", "Greymoor".
    ///
    /// Each language is a blend of one or two flavours (families of naming traditions, see "CommonNameFlavors.csv").
    /// It uses only part of each flavour's pieces and favours some of them, so two languages from the same flavour
    /// still sound distinct while names from one language sound related. Names come from templates
    /// ("CommonNameTemplates.csv") that suit what is being named; each language has its own mix of proper names built
    /// from its sounds and descriptive names in English.
    /// </summary>
    public class CommonLanguage : ILanguage
    {
        private const string FlavorTagPrefix = "flavor:";
        private const int MaxAttempts = 20;

        private readonly SimEngine engine;
        private Dictionary<string, List<Piece>> primaryPalette;
        private Dictionary<string, List<Piece>> secondaryPalette;
        // The palette for the name being built: each name keeps to one flavour, so blends show across names, not within them
        private Dictionary<string, List<Piece>> namePalette;

        private sealed class Piece
        {
            public string Text;
            public double Weight;
        }

        /// <summary>The flavour most of the language's pieces come from.</summary>
        public NameFlavor PrimaryFlavor { get; private set; }

        /// <summary>The flavour blended in, or null if the language has only one.</summary>
        public NameFlavor SecondaryFlavor { get; private set; }

        /// <summary>Share of pieces taken from <see cref="SecondaryFlavor"/>.</summary>
        public double SecondaryWeight { get; private set; }

        /// <summary>Share of names that are descriptive rather than proper, before scaling for what is being named.</summary>
        public double DescriptiveShare { get; private set; }

        /// <inheritdoc/>
        public string LanguageName { get; private set; }

        private CommonNameData Data { get { return engine.SimData.CommonNames; } }
        private CommonLanguageRules Rules { get { return Data.Rules; } }
        private Rnd Rnd { get { return engine.NameRnd; } }

        private CommonLanguage(SimEngine engine)
        {
            this.engine = engine;
        }

        /// <summary>
        /// A new random language. Flavours that already lead languages in the current world are less likely to be
        /// chosen to lead this one, so the continents of a world sound different from each other.
        /// </summary>
        public static CommonLanguage Random(SimEngine engine)
        {
            CommonLanguage lang = new CommonLanguage(engine);
            CommonLanguageRules rules = lang.Rules;
            Rnd rnd = lang.Rnd;
            NameRegistry names = engine.Language.Names;
            IReadOnlyList<NameFlavor> flavors = lang.Data.Flavors;

            lang.PrimaryFlavor = WeightedPick(flavors, f => 1.0 / (1.0 + rules.UsedFlavorPenalty * names.TimesUsed(FlavorTagPrefix + f.Id)), rnd);
            names.Use(FlavorTagPrefix + lang.PrimaryFlavor.Id);
            lang.primaryPalette = lang.BuildPalette(lang.PrimaryFlavor);

            if (flavors.Count > 1 && rnd.R01() < rules.SecondaryFlavorProbability)
            {
                lang.SecondaryFlavor = ListR<NameFlavor>.RandomFromList(flavors.Where(f => f != lang.PrimaryFlavor), rnd);
                lang.SecondaryWeight = rnd.Unweighted(rules.SecondaryWeightMin, rules.SecondaryWeightMax);
                lang.secondaryPalette = lang.BuildPalette(lang.SecondaryFlavor);
            }

            lang.DescriptiveShare = rnd.Unweighted(rules.DescriptiveMin, rules.DescriptiveMax);
            lang.LanguageName = lang.GetName(NameKind.Language);
            return lang;
        }

        /// <summary>
        /// A dialect: the same flavours, with some pieces swapped for others from those flavours and a slightly
        /// different mix of proper and descriptive names.
        /// </summary>
        public ILanguage CreateDialect()
        {
            Rnd rnd = Rnd;
            CommonLanguage dialect = new CommonLanguage(engine);
            dialect.PrimaryFlavor = PrimaryFlavor;
            dialect.SecondaryFlavor = SecondaryFlavor;
            dialect.SecondaryWeight = SecondaryWeight;
            dialect.primaryPalette = ShiftPalette(primaryPalette, PrimaryFlavor);
            if (SecondaryFlavor != null) dialect.secondaryPalette = ShiftPalette(secondaryPalette, SecondaryFlavor);
            double skew = Rules.DialectDescriptiveSkew;
            dialect.DescriptiveShare = Math.Max(0, Math.Min(1, DescriptiveShare + rnd.Unweighted(-skew, skew)));
            dialect.LanguageName = dialect.GetName(NameKind.Language);
            return dialect;
        }

        /// <summary>
        /// A name suited to the context, not given out before in this world where that can be avoided.
        /// </summary>
        public string GetName(NameContext context)
        {
            if (context == null) throw new ArgumentNullException("context");
            return engine.Language.Names.Unique(() => BuildName(context));
        }

        /// <summary>
        /// A single proper word with an ending suited to the context, for example "Valoria" for a continent.
        /// </summary>
        public string GetWord(NameContext context)
        {
            if (context == null) throw new ArgumentNullException("context");
            ChooseNamePalette();
            return ProperWord(EndingSlotFor(context.Kind));
        }

        /// <summary>
        /// The pieces this language favours most, by slot.
        /// </summary>
        public IReadOnlyList<string> FavoritePieces(string slot, int count = 5)
        {
            IEnumerable<Piece> pieces = primaryPalette[slot];
            if (secondaryPalette != null) pieces = pieces.Concat(secondaryPalette[slot].Select(p => new Piece { Text = p.Text, Weight = p.Weight * SecondaryWeight / (1 - SecondaryWeight) }));
            return pieces.OrderByDescending(p => p.Weight).Select(p => p.Text).Distinct().Take(count).ToList();
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("Language Name: ").Append(LanguageName).Append('\n');
            sb.Append("Style: Common\n");
            sb.Append("Primary Flavour: ").Append(PrimaryFlavor.Id).Append('\n');
            if (SecondaryFlavor != null)
                sb.Append("Secondary Flavour: ").Append(SecondaryFlavor.Id).Append(" (").Append(Math.Round(SecondaryWeight * 100)).Append("% of pieces)\n");
            sb.Append("Descriptive Names: ").Append(Math.Round(DescriptiveShare * 100)).Append("% (more for seas and oceans, fewer for continents)\n");
            foreach (string slot in new[] { "Root", "Ending", "LandEnding", "RegionEnding", "WaterEnding", "LangEnding" })
                sb.Append("Favourite ").Append(slot).Append(": ").Append(string.Join(", ", FavoritePieces(slot))).Append('\n');
            return sb.ToString();
        }

        /// <summary>
        /// The vocabulary list a template token draws from, or null if the token is not a vocabulary token.
        /// </summary>
        public static string VocabularyListFor(string token)
        {
            switch (token)
            {
                case "Adj":
                case "AdjC":
                case "LandAdj":
                case "SeaAdj":
                case "SeaNoun":
                case "Feature":
                case "FeatureC":
                    return token;
            }
            return null;
        }

        // ---- Building names -------------------------------------------------------------------

        private string BuildName(NameContext context)
        {
            Rnd rnd = Rnd;
            ChooseNamePalette();
            double share = DescriptiveShare;
            double scale;
            if (Rules.DescriptiveScale.TryGetValue(context.Kind, out scale)) share *= scale;
            share = Math.Max(0, Math.Min(0.95, share));
            NameStyle style = rnd.R01() < share ? NameStyle.Descriptive : NameStyle.Proper;

            List<NameTemplate> candidates = Candidates(context, style);
            if (candidates.Count == 0)
                candidates = Candidates(context, style == NameStyle.Proper ? NameStyle.Descriptive : NameStyle.Proper);

            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                NameTemplate template = WeightedPick(candidates, t => t.Weight, rnd);
                string name = Expand(template, context);
                if (name != null) return name;
            }
            // Every attempt failed the length checks: fall back to a plain proper word, which always succeeds
            return ProperWord(EndingSlotFor(context.Kind));
        }

        private List<NameTemplate> Candidates(NameContext context, NameStyle style)
        {
            return Data.Templates.Where(t => t.Kind == context.Kind && t.Style == style && t.Suits(context)).ToList();
        }

        // Fills in a template, or returns null if the result breaks a rule (a word too long, a piece joined to itself)
        private string Expand(NameTemplate template, NameContext context)
        {
            string name = "";
            string lastValue = null;
            bool join = false;
            foreach (string part in template.Parts)
            {
                if (part == "+") { join = true; continue; }

                string value = part.StartsWith("{") ? TokenValue(part.Substring(1, part.Length - 2), context) : part;
                if (value.Length == 0) continue; // a skipped {Middle}: the join, if any, carries on to the next piece

                if (join)
                {
                    if (lastValue != null && string.Equals(lastValue, value, StringComparison.OrdinalIgnoreCase)) return null;
                    name = Join(name, value);
                }
                else name += value;
                lastValue = value;
                join = false;
            }

            name = Capitalize(name.Trim());
            string[] words = name.Split(' ');
            if (words.Any(w => w.Length > Rules.MaxWordLength)) return null;
            if (name.Length < Rules.MinWordLength) return null;
            // Reject echoes such as "The Frozen Frostlands": two longer words starting with the same three letters
            List<string> stems = words.Where(w => w.Length >= 4).Select(w => w.Substring(0, 3).ToLowerInvariant()).ToList();
            if (stems.Distinct().Count() < stems.Count) return null;
            return name;
        }

        private string TokenValue(string token, NameContext context)
        {
            Rnd rnd = Rnd;
            switch (token)
            {
                case "Middle":
                    return rnd.R01() < Rules.MiddleProbability ? Pick("Middle") : "";
                case "Feature":
                case "FeatureC":
                    return ListR<string>.RandomFromList(Data.Words(token, Data.BiomeCategory(context.Biome)), rnd);
                case "Dir":
                    return context.Direction.Value.ToString();
                case "DirAdj":
                    return context.Direction.Value + "ern";
                case "Parent":
                    return context.ParentName.Trim();
            }
            if (NameFlavor.Slots.Contains(token)) return Pick(token);
            return ListR<string>.RandomFromList(Data.Words(VocabularyListFor(token)), rnd);
        }

        private string ProperWord(string endingSlot)
        {
            if (namePalette == null) ChooseNamePalette();
            string word = null;
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                word = Pick("Root");
                if (Rnd.R01() < Rules.MiddleProbability) word = Join(word, Pick("Middle"));
                word = Capitalize(Join(word, Pick(endingSlot)));
                if (word.Length >= Rules.MinWordLength && word.Length <= Rules.MaxWordLength) return word;
            }
            return word;
        }

        private static string EndingSlotFor(NameKind kind)
        {
            switch (kind)
            {
                case NameKind.Language: return "LangEnding";
                case NameKind.World:
                case NameKind.Continent: return "LandEnding";
                case NameKind.Region: return "RegionEnding";
                case NameKind.Sea:
                case NameKind.Ocean: return "WaterEnding";
            }
            return "Ending";
        }

        private void ChooseNamePalette()
        {
            namePalette = primaryPalette;
            if (secondaryPalette != null && Rnd.R01() < SecondaryWeight) namePalette = secondaryPalette;
        }

        private string Pick(string slot)
        {
            return WeightedPick(namePalette[slot], p => p.Weight, Rnd).Text;
        }

        // ---- Palettes -------------------------------------------------------------------------

        // The part of a flavour this language uses, with a random weight on each piece so that a few recur
        private Dictionary<string, List<Piece>> BuildPalette(NameFlavor flavor)
        {
            Rnd rnd = Rnd;
            double share = rnd.Unweighted(Rules.PaletteShareMin, Rules.PaletteShareMax);
            var palette = new Dictionary<string, List<Piece>>();
            foreach (string slot in NameFlavor.Slots)
            {
                IReadOnlyList<string> all = flavor.Pieces[slot];
                int count = Math.Max(Math.Min(Rules.PaletteMinItems, all.Count), (int)Math.Round(all.Count * share));
                palette[slot] = Shuffled(all, rnd).Take(count).Select(t => NewPiece(t)).ToList();
            }
            return palette;
        }

        private Dictionary<string, List<Piece>> ShiftPalette(Dictionary<string, List<Piece>> palette, NameFlavor flavor)
        {
            Rnd rnd = Rnd;
            var shifted = new Dictionary<string, List<Piece>>();
            foreach (var entry in palette)
            {
                List<Piece> pieces = entry.Value.Select(p => new Piece { Text = p.Text, Weight = p.Weight }).ToList();
                int swaps = (int)Math.Round(pieces.Count * Rules.DialectReplaceShare);
                for (int i = 0; i < swaps; i++)
                {
                    List<string> unused = flavor.Pieces[entry.Key].Where(t => !pieces.Any(p => p.Text == t)).ToList();
                    if (unused.Count == 0) break;
                    pieces.RemoveAt(rnd.Unweighted(pieces.Count));
                    pieces.Add(NewPiece(ListR<string>.RandomFromList(unused, rnd)));
                }
                shifted[entry.Key] = pieces;
            }
            return shifted;
        }

        private Piece NewPiece(string text)
        {
            // Raising a uniform value to a power above 1 leaves most weights small and a few large
            return new Piece { Text = text, Weight = 0.05 + Math.Pow(Rnd.R01(), Rules.FavoriteBias) };
        }

        // ---- Helpers --------------------------------------------------------------------------

        /// <summary>
        /// Joins a piece onto the end of a name, smoothing the seam: a vowel meeting a vowel loses one
        /// ("Valda" + "ia" = "Valdia"), a consonant cluster does not double its last letter ("Ald" + "dale" = "Aldale"),
        /// and no letter appears three times running.
        /// </summary>
        public static string Join(string left, string right)
        {
            if (string.IsNullOrEmpty(right)) return left ?? "";
            if (string.IsNullOrEmpty(left)) return right;

            right = char.ToLowerInvariant(right[0]) + right.Substring(1);
            char x = char.ToLowerInvariant(left[left.Length - 1]);
            char y = right[0];
            int wordLength = left.Length - (left.LastIndexOf(' ') + 1);
            string joined;

            if (IsVowel(x) && IsVowel(y))
            {
                if (x == y) joined = left + right.Substring(1);
                else if (wordLength >= 4) joined = left.Substring(0, left.Length - 1) + right;
                else joined = left + right;
            }
            else if (!IsVowel(x) && x == y && wordLength >= 2 && !IsVowel(left[left.Length - 2]) && !left.EndsWith("ng", StringComparison.OrdinalIgnoreCase))
            {
                joined = left + right.Substring(1);
            }
            else joined = left + right;

            StringBuilder sb = new StringBuilder();
            foreach (char c in joined)
            {
                int n = sb.Length;
                if (n >= 2 && char.ToLowerInvariant(sb[n - 1]) == char.ToLowerInvariant(c) && char.ToLowerInvariant(sb[n - 2]) == char.ToLowerInvariant(c)) continue;
                sb.Append(c);
            }
            return sb.ToString();
        }

        private static bool IsVowel(char c)
        {
            return "aeiouAEIOU".IndexOf(c) >= 0;
        }

        private static string Capitalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return s ?? "";
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }

        private static T WeightedPick<T>(IReadOnlyList<T> items, Func<T, double> weight, Rnd rnd)
        {
            double total = items.Sum(weight);
            double roll = rnd.R01() * total;
            foreach (T item in items)
            {
                roll -= weight(item);
                if (roll < 0) return item;
            }
            return items[items.Count - 1];
        }

        private static List<T> Shuffled<T>(IEnumerable<T> items, Rnd rnd)
        {
            List<T> list = items.ToList();
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rnd.Unweighted(i + 1);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
            return list;
        }
    }
}
