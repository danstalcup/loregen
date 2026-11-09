using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

using LoreGen.Lang;

namespace LoreGen.Features
{
    /// <summary>
    /// A kind of geographic feature, such as "Mountain range", read from FeatureTypes.csv.
    /// </summary>
    public class FeatureType
    {
        /// <summary>Stable identifier used by code that creates features, for example "MountainRange".</summary>
        public string Id;
        /// <summary>Name shown to people, for example "Mountain range".</summary>
        public string Name;
        public FeatureCategory Category;
        public FeatureShape Shape;
        /// <summary>The scopes a feature of this type may have (each feature has exactly one of them).</summary>
        public IReadOnlyList<FeatureScope> Scopes;
        /// <summary>The kind of name features of this type are given.</summary>
        public NameKind NameKind;
        public string Description;

        public const string Filename = "FeatureTypes.csv";

        public override string ToString() { return Name; }

        /// <summary>
        /// Reads feature types from lines of "Id,Name,Category,Shape,Scopes,NameKind,Description", where Scopes is one or
        /// more scope names separated by '|'. Blank lines and lines starting with '#' are ignored.
        /// </summary>
        /// <exception cref="InvalidDataException">A row is malformed, or an Id is used twice</exception>
        public static List<FeatureType> Parse(IEnumerable<string> lines)
        {
            List<FeatureType> types = new List<FeatureType>();
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;

                string[] row = line.Split(new[] { ',' }, 7);
                if (row.Length != 7)
                    throw new InvalidDataException(Filename + ": expected 'Id,Name,Category,Shape,Scopes,NameKind,Description' but found: " + line);

                FeatureType type = new FeatureType
                {
                    Id = row[0].Trim(),
                    Name = row[1].Trim(),
                    Category = ParseEnum<FeatureCategory>(row[2], "category", line),
                    Shape = ParseEnum<FeatureShape>(row[3], "shape", line),
                    Scopes = row[4].Split('|').Select(s => ParseEnum<FeatureScope>(s, "scope", line)).Distinct().ToList(),
                    NameKind = ParseEnum<NameKind>(row[5], "name kind", line),
                    Description = row[6].Trim()
                };
                if (type.Id.Length == 0 || type.Name.Length == 0)
                    throw new InvalidDataException(Filename + ": Id and Name must not be empty: " + line);
                if (types.Any(t => string.Equals(t.Id, type.Id, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidDataException(Filename + ": Id '" + type.Id + "' is used more than once");
                types.Add(type);
            }
            return types;
        }

        private static T ParseEnum<T>(string text, string what, string line) where T : struct
        {
            T value;
            string trimmed = text.Trim();
            // Reject numbers: "3" would otherwise parse as whatever the third value is
            if (trimmed.Length == 0 || char.IsDigit(trimmed[0]) || !Enum.TryParse(trimmed, true, out value))
                throw new InvalidDataException(Filename + ": unknown " + what + " '" + trimmed + "' in: " + line);
            return value;
        }
    }
}
