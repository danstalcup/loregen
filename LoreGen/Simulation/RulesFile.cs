using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace LoreGen.Simulation
{
    /// <summary>
    /// A keyed rules file: rows of "Key,Value[,Description]" in any order. Blank lines and lines starting with '#' are
    /// ignored, and numbers always use '.' for decimals whatever the machine's culture.
    ///
    /// Get one from <see cref="SimData.LoadRules"/>. Ask for every value the code needs with D/I, then call
    /// <see cref="Finish"/> so typos in the file are reported instead of silently ignored. Errors name the file and key.
    /// </summary>
    public class RulesFile
    {
        private readonly string filename;
        private readonly Dictionary<string, double> values = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Parses the lines of a rules file.
        /// </summary>
        /// <param name="filename">Name used in error messages</param>
        /// <param name="lines">The file's lines</param>
        public RulesFile(string filename, IEnumerable<string> lines)
        {
            this.filename = filename;
            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                string[] parts = line.Split(new char[] { ',' }, 3);
                if (parts.Length < 2)
                    throw new InvalidDataException(filename + ": expected 'Key,Value[,Description]' but found: " + line);
                string key = parts[0].Trim();
                if (values.ContainsKey(key))
                    throw new InvalidDataException(filename + ": duplicate key '" + key + "'");
                double value;
                if (!double.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                    throw new InvalidDataException(filename + ": value for '" + key + "' is not a number: " + parts[1].Trim());
                values[key] = value;
            }
        }

        /// <summary>
        /// True if the file has this key.
        /// </summary>
        public bool Has(string key)
        {
            return values.ContainsKey(key);
        }

        /// <summary>
        /// A required value as a double.
        /// </summary>
        public double D(string key)
        {
            double value;
            if (!values.TryGetValue(key, out value))
                throw new InvalidDataException(filename + ": missing required key '" + key + "'");
            used.Add(key);
            return value;
        }

        /// <summary>
        /// A required value as an int.
        /// </summary>
        public int I(string key)
        {
            return Convert.ToInt32(D(key));
        }

        /// <summary>
        /// Call after reading everything: any key in the file that was never asked for is almost certainly a typo.
        /// </summary>
        public void Finish()
        {
            var unknown = values.Keys.Where(k => !used.Contains(k)).ToList();
            if (unknown.Count > 0)
                throw new InvalidDataException(filename + ": unrecognized key(s): " + string.Join(", ", unknown));
        }
    }
}
