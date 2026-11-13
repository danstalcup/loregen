using System;
using System.Collections.Generic;

namespace LoreGen.Lang
{
    /// <summary>
    /// Keeps the names given out in one world, so that languages can avoid handing out the same name twice. Languages can
    /// also count uses of other things here (tags), such as how many languages in the world lead with each flavour.
    /// </summary>
    public class NameRegistry
    {
        /// <summary>
        /// How many times a language tries for a fresh name before accepting a repeat.
        /// </summary>
        public const int MaxAttempts = 25;

        private readonly HashSet<string> used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> tags = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private int suspended;

        /// <summary>
        /// While the returned object is not disposed, <see cref="Unique"/> neither checks nor records names. For names that
        /// must not depend on what was named before, such as those of places found while exploring.
        /// </summary>
        public IDisposable Suspend()
        {
            suspended++;
            return new Resume(this);
        }

        private sealed class Resume : IDisposable
        {
            private NameRegistry registry;
            public Resume(NameRegistry registry) { this.registry = registry; }
            public void Dispose()
            {
                if (registry != null) registry.suspended--;
                registry = null;
            }
        }

        /// <summary>
        /// How many times a tag has been used in this world.
        /// </summary>
        public int TimesUsed(string tag)
        {
            int count;
            return tags.TryGetValue(tag, out count) ? count : 0;
        }

        /// <summary>
        /// Counts one use of a tag.
        /// </summary>
        public void Use(string tag)
        {
            tags[tag] = TimesUsed(tag) + 1;
        }

        /// <summary>
        /// True if the name has been given out.
        /// </summary>
        public bool Contains(string name)
        {
            return used.Contains(name);
        }

        /// <summary>
        /// Calls <paramref name="make"/> until it returns a name not given out yet (or <see cref="MaxAttempts"/> run
        /// out, in which case the last name is used anyway), records the name and returns it.
        /// </summary>
        public string Unique(Func<string> make)
        {
            if (suspended > 0) return make();
            string name = make();
            for (int i = 1; i < MaxAttempts && used.Contains(name); i++)
                name = make();
            used.Add(name);
            return name;
        }

        /// <summary>
        /// Forgets every name and tag, for a new world.
        /// </summary>
        public void Clear()
        {
            used.Clear();
            tags.Clear();
        }
    }
}
