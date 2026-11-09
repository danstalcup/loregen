using System;
using System.Collections.Generic;
using System.Linq;

namespace LoreGen.WorldGen.Generators
{
    /// <summary>
    /// The set of world generators an engine can choose from, looked up by <see cref="IWorldGenerator.Id"/>.
    /// Registration is explicit (no scanning), and each engine has its own registry, so registering a custom
    /// generator on one engine never affects another.
    /// </summary>
    public class WorldGeneratorRegistry
    {
        /// <summary>
        /// Id of the generator used when none is configured.
        /// </summary>
        public const string DefaultId = LegacyWorldGenerator.GeneratorId;

        private readonly Dictionary<string, IWorldGenerator> generators = new Dictionary<string, IWorldGenerator>(StringComparer.OrdinalIgnoreCase);
        private readonly List<IWorldGenerator> inOrder = new List<IWorldGenerator>();

        /// <summary>
        /// Creates an empty registry.
        /// </summary>
        public WorldGeneratorRegistry()
        {
        }

        /// <summary>
        /// Creates a registry holding all the generators that ship with LoreGen.
        /// </summary>
        public static WorldGeneratorRegistry CreateDefault()
        {
            WorldGeneratorRegistry registry = new WorldGeneratorRegistry();
            registry.Register(new LegacyWorldGenerator());
            registry.Register(new NoiseWorldGenerator());
            registry.Register(new TectonicWorldGenerator());
            return registry;
        }

        /// <summary>
        /// Adds a generator. Its Id must be unique within this registry.
        /// </summary>
        public void Register(IWorldGenerator generator)
        {
            if (generator == null) throw new ArgumentNullException("generator");
            string id = generator.Id;
            if (string.IsNullOrEmpty(id) || !id.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'))
                throw new ArgumentException("A generator Id must be non-empty and contain only letters, digits, '-' and '_' (was '" + id + "').", "generator");
            if (generators.ContainsKey(id))
                throw new InvalidOperationException("A world generator with Id '" + id + "' is already registered.");
            generators.Add(id, generator);
            inOrder.Add(generator);
        }

        /// <summary>
        /// All registered generators, in registration order.
        /// </summary>
        public IReadOnlyList<IWorldGenerator> All
        {
            get { return inOrder; }
        }

        /// <summary>
        /// Ids of all registered generators, in registration order.
        /// </summary>
        public IReadOnlyList<string> Ids
        {
            get { return inOrder.Select(g => g.Id).ToList(); }
        }

        /// <summary>
        /// True if a generator with this Id (case-insensitive) is registered.
        /// </summary>
        public bool Contains(string id)
        {
            return id != null && generators.ContainsKey(id);
        }

        /// <summary>
        /// Looks up a generator by Id (case-insensitive).
        /// </summary>
        /// <exception cref="ArgumentException">No generator has that Id. The message lists the ones that exist.</exception>
        public IWorldGenerator Get(string id)
        {
            IWorldGenerator generator;
            if (id != null && generators.TryGetValue(id, out generator)) return generator;
            throw new ArgumentException("Unknown world generator '" + id + "'. Available: " + string.Join(", ", Ids) + ".", "id");
        }
    }
}
