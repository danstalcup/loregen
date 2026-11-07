using System;
using System.Globalization;

namespace LoreGen.WorldGen.Generators
{
    /// <summary>
    /// What kind of control suits an option.
    /// </summary>
    public enum GeneratorOptionKind
    {
        /// <summary>A number in a range (a slider).</summary>
        Number,

        /// <summary>A whole number in a range (a slider that moves in whole steps).</summary>
        Integer,

        /// <summary>On or off, as 1 or 0 (a checkbox).</summary>
        Toggle
    }

    /// <summary>
    /// Describes one setting of a world generator, so that a user interface can offer a suitable control for it without
    /// knowing anything about the generator. The value itself is passed to the generator in
    /// <see cref="Simulation.SimEngineConfiguration.GeneratorSettings"/> under <see cref="Key"/>.
    /// </summary>
    public class GeneratorOption
    {
        /// <summary>Name the setting is stored under.</summary>
        public string Key { get; private set; }

        /// <summary>Short label for people.</summary>
        public string Name { get; private set; }

        /// <summary>One sentence about what the setting does.</summary>
        public string Description { get; private set; }

        /// <summary>Heading the setting is listed under.</summary>
        public string Group { get; private set; }

        public GeneratorOptionKind Kind { get; private set; }

        public double Minimum { get; private set; }

        public double Maximum { get; private set; }

        /// <summary>How far one notch of a slider moves the value.</summary>
        public double Step { get; private set; }

        /// <summary>The value the generator uses when the setting is not given.</summary>
        public double Default { get; private set; }

        /// <summary>
        /// For settings where 0 means "choose for me" (a random land share, say): what to call that, such as "random".
        /// Null for settings where 0 is an ordinary value.
        /// </summary>
        public string AutoLabel { get; private set; }

        private GeneratorOption(string key, string name, string description, string group, GeneratorOptionKind kind,
            double minimum, double maximum, double step, double defaultValue, string autoLabel)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("An option needs a key.", "key");
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("An option needs a name.", "name");
            if (!(minimum < maximum)) throw new ArgumentException("Minimum must be less than maximum for option '" + key + "'.");
            if (!(step > 0)) throw new ArgumentException("Step must be greater than 0 for option '" + key + "'.");
            if (!(defaultValue >= minimum && defaultValue <= maximum))
                throw new ArgumentException("Default " + defaultValue + " is outside " + minimum + " to " + maximum + " for option '" + key + "'.");

            Key = key;
            Name = name;
            Description = description ?? string.Empty;
            Group = string.IsNullOrWhiteSpace(group) ? "General" : group;
            Kind = kind;
            Minimum = minimum;
            Maximum = maximum;
            Step = step;
            Default = defaultValue;
            AutoLabel = autoLabel;
        }

        /// <summary>A number in a range.</summary>
        public static GeneratorOption Number(string key, string name, string description, string group, double minimum, double maximum, double step, double defaultValue, string autoLabel = null)
        {
            return new GeneratorOption(key, name, description, group, GeneratorOptionKind.Number, minimum, maximum, step, defaultValue, autoLabel);
        }

        /// <summary>A whole number in a range.</summary>
        public static GeneratorOption Integer(string key, string name, string description, string group, int minimum, int maximum, int defaultValue, string autoLabel = null)
        {
            return new GeneratorOption(key, name, description, group, GeneratorOptionKind.Integer, minimum, maximum, 1, defaultValue, autoLabel);
        }

        /// <summary>On or off.</summary>
        public static GeneratorOption Toggle(string key, string name, string description, string group, bool defaultValue)
        {
            return new GeneratorOption(key, name, description, group, GeneratorOptionKind.Toggle, 0, 1, 1, defaultValue ? 1 : 0, null);
        }

        /// <summary>
        /// Brings a value into this option's range (and onto a whole number or 0/1 where that applies).
        /// </summary>
        public double Clamp(double value)
        {
            if (double.IsNaN(value)) return Default;
            value = Math.Max(Minimum, Math.Min(Maximum, value));
            if (Kind == GeneratorOptionKind.Integer) return Math.Round(value, MidpointRounding.AwayFromZero);
            if (Kind == GeneratorOptionKind.Toggle) return value >= 0.5 ? 1 : 0;
            return value;
        }

        /// <summary>
        /// A value as people should read it: "On", "random", "12", "0.35".
        /// </summary>
        public string Format(double value)
        {
            if (AutoLabel != null && Math.Abs(value) < 1e-9) return AutoLabel;
            if (Kind == GeneratorOptionKind.Toggle) return value >= 0.5 ? "On" : "Off";
            if (Kind == GeneratorOptionKind.Integer) return Math.Round(value, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
