using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using LoreGen.Simulation;
using LoreGen.Terrains;

namespace LoreGen.Lang
{
    /// <summary>
    /// A family of naming traditions that a <see cref="CommonLanguage"/> draws its sounds from, read from
    /// "CommonNameFlavors.csv".
    /// </summary>
    public class NameFlavor
    {
        /// <summary>The slots every flavour must fill.</summary>
        public static readonly string[] Slots = { "Root", "Middle", "Ending", "LandEnding", "RegionEnding", "WaterEnding", "LangEnding" };

        /// <summary>Name of the flavour, for example "Latinate".</summary>
        public string Id { get; }

        /// <summary>The pieces in each slot.</summary>
        public IReadOnlyDictionary<string, IReadOnlyList<string>> Pieces { get; }

        internal NameFlavor(string id, IReadOnlyDictionary<string, IReadOnlyList<string>> pieces)
        {
            Id = id;
            Pieces = pieces;
        }

        /// <inheritdoc/>
        public override string ToString() { return Id; }
    }

    /// <summary>
    /// Whether a name template builds a name from a flavour's sounds or describes the place in English.
    /// </summary>
    public enum NameStyle
    {
        /// <summary>A name built from sounds: "Valoria".</summary>
        Proper,
        /// <summary>A description: "The Sea of Storms".</summary>
        Descriptive
    }

    /// <summary>
    /// One way of building a name, read from "CommonNameTemplates.csv".
    /// </summary>
    public class NameTemplate
    {
        private static readonly Regex TokenPattern = new Regex(@"\{(\w+)\}|\+|[^{+]+");

        /// <summary>What the template names.</summary>
        public NameKind Kind { get; }
        /// <summary>Its style.</summary>
        public NameStyle Style { get; }
        /// <summary>How often it is chosen relative to others of the same kind and style.</summary>
        public double Weight { get; }
        /// <summary>The template as written.</summary>
        public string Text { get; }
        /// <summary>The template split into tokens ("{Root}"), joins ("+") and literal text.</summary>
        public IReadOnlyList<string> Parts { get; }

        /// <summary>True if the template uses {Dir} or {DirAdj}.</summary>
        public bool NeedsDirection { get; }
        /// <summary>True if the template uses {Parent}.</summary>
        public bool NeedsParent { get; }

        internal NameTemplate(NameKind kind, NameStyle style, double weight, string text)
        {
            Kind = kind;
            Style = style;
            Weight = weight;
            Text = text;
            Parts = TokenPattern.Matches(text).Cast<Match>().Select(m => m.Value).ToList();
            NeedsDirection = Parts.Contains("{Dir}") || Parts.Contains("{DirAdj}");
            NeedsParent = Parts.Contains("{Parent}");
        }

        /// <summary>
        /// True if the context has the hints this template needs.
        /// </summary>
        public bool Suits(NameContext context)
        {
            if (NeedsDirection && !context.Direction.HasValue) return false;
            if (NeedsParent && (string.IsNullOrWhiteSpace(context.ParentName) || context.ParentName.Trim().Contains(' '))) return false;
            return true;
        }

        /// <inheritdoc/>
        public override string ToString() { return Kind + "/" + Style + ": " + Text; }
    }

    /// <summary>
    /// Settings for <see cref="CommonLanguage"/>, read from "CommonLanguageRules.csv". See that file for what each does.
    /// </summary>
    public struct CommonLanguageRules
    {
        public double DescriptiveMin;
        public double DescriptiveMax;
        public Dictionary<NameKind, double> DescriptiveScale;
        public double SecondaryFlavorProbability;
        public double SecondaryWeightMin;
        public double SecondaryWeightMax;
        public double PaletteShareMin;
        public double PaletteShareMax;
        public int PaletteMinItems;
        public double FavoriteBias;
        public double MiddleProbability;
        public double UsedFlavorPenalty;
        public double DialectReplaceShare;
        public double DialectDescriptiveSkew;
        public int MinWordLength;
        public int MaxWordLength;

        internal static CommonLanguageRules Read(RulesFile r)
        {
            CommonLanguageRules rules = new CommonLanguageRules();
            rules.DescriptiveMin = r.D("DescriptiveMin");
            rules.DescriptiveMax = r.D("DescriptiveMax");
            rules.DescriptiveScale = new Dictionary<NameKind, double>();
            foreach (NameKind kind in Enum.GetValues(typeof(NameKind)))
                rules.DescriptiveScale[kind] = r.D("DescriptiveScale" + kind);
            rules.SecondaryFlavorProbability = r.D("SecondaryFlavorProbability");
            rules.SecondaryWeightMin = r.D("SecondaryWeightMin");
            rules.SecondaryWeightMax = r.D("SecondaryWeightMax");
            rules.PaletteShareMin = r.D("PaletteShareMin");
            rules.PaletteShareMax = r.D("PaletteShareMax");
            rules.PaletteMinItems = r.I("PaletteMinItems");
            rules.FavoriteBias = r.D("FavoriteBias");
            rules.MiddleProbability = r.D("MiddleProbability");
            rules.UsedFlavorPenalty = r.D("UsedFlavorPenalty");
            rules.DialectReplaceShare = r.D("DialectReplaceShare");
            rules.DialectDescriptiveSkew = r.D("DialectDescriptiveSkew");
            rules.MinWordLength = r.I("MinWordLength");
            rules.MaxWordLength = r.I("MaxWordLength");
            r.Finish();
            if (rules.MinWordLength > rules.MaxWordLength)
                throw new InvalidDataException("CommonLanguageRules.csv: MinWordLength is greater than MaxWordLength");
            return rules;
        }
    }

    /// <summary>
    /// Everything <see cref="CommonLanguage"/> needs from the data files: flavours, templates, vocabulary and rules.
    /// </summary>
    public class CommonNameData
    {
        /// <summary>Name of the "no particular biome" key in the vocabulary.</summary>
        public const string AnyKey = "Any";

        /// <summary>The naming flavours.</summary>
        public IReadOnlyList<NameFlavor> Flavors { get; }

        /// <summary>The name templates.</summary>
        public IReadOnlyList<NameTemplate> Templates { get; }

        /// <summary>The settings.</summary>
        public CommonLanguageRules Rules { get; }

        private readonly Dictionary<string, Dictionary<string, IReadOnlyList<string>>> vocabulary;
        private readonly List<KeyValuePair<string, string[]>> biomeKeywords;

        /// <summary>
        /// Parses the data files' lines.
        /// </summary>
        public CommonNameData(IEnumerable<string> flavorLines, IEnumerable<string> templateLines, IEnumerable<string> vocabularyLines, RulesFile rules)
        {
            Flavors = ReadFlavors(flavorLines);
            Templates = ReadTemplates(templateLines);
            vocabulary = ReadVocabulary(vocabularyLines, out biomeKeywords);
            Rules = CommonLanguageRules.Read(rules);
            CheckTemplates();
        }

        /// <summary>
        /// A vocabulary list, for example ("Feature", "Wetland"). Falls back to the "Any" list when the key has none.
        /// </summary>
        public IReadOnlyList<string> Words(string list, string key = AnyKey)
        {
            Dictionary<string, IReadOnlyList<string>> byKey;
            if (!vocabulary.TryGetValue(list, out byKey))
                throw new InvalidDataException("CommonNameVocabulary.csv: no '" + list + "' list");
            IReadOnlyList<string> words;
            if (key != null && byKey.TryGetValue(key, out words)) return words;
            if (byKey.TryGetValue(AnyKey, out words)) return words;
            throw new InvalidDataException("CommonNameVocabulary.csv: no '" + list + "," + AnyKey + "' row");
        }

        /// <summary>
        /// The vocabulary category of a biome ("Wetland" for a swamp), or "Any" if it fits none.
        /// </summary>
        public string BiomeCategory(Biome biome)
        {
            if (biome?.Name == null) return AnyKey;
            foreach (var entry in biomeKeywords)
                if (entry.Value.Any(k => biome.Name.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0))
                    return entry.Key;
            return AnyKey;
        }

        private static IEnumerable<string[]> Rows(IEnumerable<string> lines)
        {
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                yield return line.Split(',').Select(p => p.Trim()).ToArray();
            }
        }

        private static List<NameFlavor> ReadFlavors(IEnumerable<string> lines)
        {
            const string file = "CommonNameFlavors.csv";
            var pieces = new Dictionary<string, Dictionary<string, IReadOnlyList<string>>>(StringComparer.OrdinalIgnoreCase);
            var order = new List<string>();
            foreach (string[] row in Rows(lines))
            {
                if (row.Length < 3) throw new InvalidDataException(file + ": expected 'Flavor,Slot,item,...' but found: " + string.Join(",", row));
                string slot = NameFlavor.Slots.FirstOrDefault(s => s.Equals(row[1], StringComparison.OrdinalIgnoreCase));
                if (slot == null) throw new InvalidDataException(file + ": unknown slot '" + row[1] + "' for flavour " + row[0]);
                if (!pieces.ContainsKey(row[0])) { pieces[row[0]] = new Dictionary<string, IReadOnlyList<string>>(); order.Add(row[0]); }
                if (pieces[row[0]].ContainsKey(slot)) throw new InvalidDataException(file + ": flavour " + row[0] + " has two " + slot + " rows");
                List<string> items = row.Skip(2).Where(i => i.Length > 0).Distinct().ToList();
                if (items.Count == 0) throw new InvalidDataException(file + ": flavour " + row[0] + " has an empty " + slot + " row");
                pieces[row[0]][slot] = items;
            }
            if (order.Count == 0) throw new InvalidDataException(file + ": no flavours");
            foreach (string id in order)
                foreach (string slot in NameFlavor.Slots)
                    if (!pieces[id].ContainsKey(slot))
                        throw new InvalidDataException(file + ": flavour " + id + " has no " + slot + " row");
            return order.Select(id => new NameFlavor(id, pieces[id])).ToList();
        }

        private static List<NameTemplate> ReadTemplates(IEnumerable<string> lines)
        {
            const string file = "CommonNameTemplates.csv";
            var templates = new List<NameTemplate>();
            foreach (string[] row in Rows(lines))
            {
                if (row.Length != 4) throw new InvalidDataException(file + ": expected 'Kind,Style,Weight,Template' but found: " + string.Join(",", row));
                NameKind kind;
                NameStyle style;
                double weight;
                if (!Enum.TryParse(row[0], true, out kind)) throw new InvalidDataException(file + ": unknown kind '" + row[0] + "'");
                if (!Enum.TryParse(row[1], true, out style)) throw new InvalidDataException(file + ": unknown style '" + row[1] + "'");
                if (!double.TryParse(row[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out weight) || weight <= 0)
                    throw new InvalidDataException(file + ": weight must be a positive number: " + row[2]);
                templates.Add(new NameTemplate(kind, style, weight, row[3]));
            }
            return templates;
        }

        private static Dictionary<string, Dictionary<string, IReadOnlyList<string>>> ReadVocabulary(IEnumerable<string> lines, out List<KeyValuePair<string, string[]>> keywords)
        {
            const string file = "CommonNameVocabulary.csv";
            var lists = new Dictionary<string, Dictionary<string, IReadOnlyList<string>>>(StringComparer.OrdinalIgnoreCase);
            keywords = new List<KeyValuePair<string, string[]>>();
            foreach (string[] row in Rows(lines))
            {
                if (row.Length < 3) throw new InvalidDataException(file + ": expected 'List,Key,item,...' but found: " + string.Join(",", row));
                string[] items = row.Skip(2).Where(i => i.Length > 0).ToArray();
                if (row[0].Equals("BiomeKeyword", StringComparison.OrdinalIgnoreCase))
                {
                    keywords.Add(new KeyValuePair<string, string[]>(row[1], items));
                    continue;
                }
                if (!lists.ContainsKey(row[0])) lists[row[0]] = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
                if (lists[row[0]].ContainsKey(row[1])) throw new InvalidDataException(file + ": two '" + row[0] + "," + row[1] + "' rows");
                lists[row[0]][row[1]] = items.Distinct().ToList();
            }
            return lists;
        }

        // Every token must be one we know, every vocabulary list a template uses must have an "Any" row, and every kind
        // must have at least one proper template that needs no hints, so a name can always be made
        private void CheckTemplates()
        {
            const string file = "CommonNameTemplates.csv";
            foreach (NameTemplate t in Templates)
                foreach (string part in t.Parts.Where(p => p.StartsWith("{")))
                {
                    string token = part.Substring(1, part.Length - 2);
                    if (NameFlavor.Slots.Contains(token) || token == "Dir" || token == "DirAdj" || token == "Parent") continue;
                    string list = CommonLanguage.VocabularyListFor(token);
                    if (list == null) throw new InvalidDataException(file + ": unknown token " + part + " in '" + t.Text + "'");
                    Words(list);
                }
            foreach (NameKind kind in Enum.GetValues(typeof(NameKind)))
                if (!Templates.Any(t => t.Kind == kind && t.Style == NameStyle.Proper && !t.NeedsDirection && !t.NeedsParent))
                    throw new InvalidDataException(file + ": " + kind + " needs at least one Proper template without {Dir}, {DirAdj} or {Parent}");
        }
    }
}
