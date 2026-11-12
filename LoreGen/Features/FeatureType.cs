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
    ///
    /// Each type has one designated scope (size), so features of a different size are a different type: the crests of a
    /// Continental mountain range are Regional subranges and massifs, not smaller mountain ranges. A type can be a part of
    /// another (its <see cref="ParentTypes"/>): every feature of it is then a child of a feature of one of those types.
    /// </summary>
    public class FeatureType
    {
        /// <summary>Stable identifier used by code that creates features, for example "MountainRange".</summary>
        public string Id;
        /// <summary>Name shown to people, for example "Mountain range".</summary>
        public string Name;
        public FeatureCategory Category;
        public FeatureShape Shape;
        /// <summary>The size of every feature of this type: the block depth its shape is defined at.</summary>
        public FeatureScope Scope;
        /// <summary>
        /// The types this one can be a part of (a subrange is part of a mountain range; a bay of an ocean or a sea), or empty
        /// if features of this type stand on their own. They are always of a larger scope.
        /// </summary>
        public List<FeatureType> ParentTypes = new List<FeatureType>();

        /// <summary>
        /// The first of <see cref="ParentTypes"/>, or null if there are none. Setting it makes it the only parent type.
        /// </summary>
        public FeatureType ParentType
        {
            get { return ParentTypes.Count > 0 ? ParentTypes[0] : null; }
            set { ParentTypes = value == null ? new List<FeatureType>() : new List<FeatureType> { value }; }
        }

        /// <summary>Whether features of this type can be part of features of the given type.</summary>
        public bool IsPartOf(FeatureType type)
        {
            return type != null && ParentTypes.Contains(type);
        }
        /// <summary>The kind of name features of this type are given.</summary>
        public NameKind NameKind;
        public string Description;

        public const string Filename = "FeatureTypes.csv";

        public override string ToString() { return Name; }

        /// <summary>
        /// Reads feature types from lines of "Id,Name,Category,Shape,Scope,Parent,NameKind,Description", where Parent is
        /// the Id of the type this one is part of, several Ids separated by '|', or empty. Blank lines and lines starting with '#' are ignored.
        /// </summary>
        /// <exception cref="InvalidDataException">A row is malformed, an Id is used twice, or a parent type is unknown
        /// or not of a larger scope</exception>
        public static List<FeatureType> Parse(IEnumerable<string> lines)
        {
            List<FeatureType> types = new List<FeatureType>();
            Dictionary<FeatureType, (string Parent, string Line)> parents = new Dictionary<FeatureType, (string, string)>();
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;

                string[] row = line.Split(new[] { ',' }, 8);
                if (row.Length != 8)
                    throw new InvalidDataException(Filename + ": expected 'Id,Name,Category,Shape,Scope,Parent,NameKind,Description' but found: " + line);

                FeatureType type = new FeatureType
                {
                    Id = row[0].Trim(),
                    Name = row[1].Trim(),
                    Category = ParseEnum<FeatureCategory>(row[2], "category", line),
                    Shape = ParseEnum<FeatureShape>(row[3], "shape", line),
                    Scope = ParseEnum<FeatureScope>(row[4], "scope", line),
                    NameKind = ParseEnum<NameKind>(row[6], "name kind", line),
                    Description = row[7].Trim()
                };
                if (type.Id.Length == 0 || type.Name.Length == 0)
                    throw new InvalidDataException(Filename + ": Id and Name must not be empty: " + line);
                if (types.Any(t => string.Equals(t.Id, type.Id, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidDataException(Filename + ": Id '" + type.Id + "' is used more than once");
                types.Add(type);
                if (row[5].Trim().Length > 0) parents[type] = (row[5].Trim(), line);
            }

            foreach (KeyValuePair<FeatureType, (string Parent, string Line)> entry in parents)
            {
                foreach (string id in entry.Value.Parent.Split('|').Select(p => p.Trim()))
                {
                    FeatureType parent = types.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));
                    if (parent == null)
                        throw new InvalidDataException(Filename + ": unknown parent type '" + id + "' in: " + entry.Value.Line);
                    if (parent.Scope >= entry.Key.Scope)
                        throw new InvalidDataException(Filename + ": a type's parent must be of a larger scope than it: " + entry.Value.Line);
                    if (!entry.Key.ParentTypes.Contains(parent)) entry.Key.ParentTypes.Add(parent);
                }
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
