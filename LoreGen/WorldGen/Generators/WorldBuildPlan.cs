using System;
using System.Collections.Generic;

namespace LoreGen.WorldGen.Generators
{
    /// <summary>
    /// One named stage of building a world, such as "Climate" or "Rivers and lakes".
    /// </summary>
    public sealed class GenerationStep
    {
        /// <summary>Name shown to people while the step runs.</summary>
        public string Name { get; }

        /// <summary>
        /// Roughly how long the step takes compared with the plan's other steps, so progress can be shown in proportion to
        /// the work rather than the number of steps. Only the ratios matter.
        /// </summary>
        public double Weight { get; }

        internal Action Run { get; }

        internal GenerationStep(string name, Action run, double weight)
        {
            Name = name;
            Run = run;
            Weight = weight;
        }
    }

    /// <summary>
    /// The steps that build one world, to be run in order. A generator returns one from
    /// <see cref="IWorldGenerator.Plan"/> so that callers can run the steps one at a time and report progress between
    /// them (see <see cref="Simulation.WorldGeneration"/>). The steps share their working state through the closures that
    /// make them, so a plan can only be run once.
    /// </summary>
    public sealed class WorldBuildPlan
    {
        private readonly List<GenerationStep> steps = new List<GenerationStep>();

        /// <summary>The steps, in the order they run.</summary>
        public IReadOnlyList<GenerationStep> Steps { get { return steps; } }

        /// <summary>Gives the finished world once every step has run.</summary>
        public Func<World> Result { get; set; }

        /// <summary>Adds a step to the end of the plan.</summary>
        /// <param name="name">Name shown to people while the step runs</param>
        /// <param name="run">The work</param>
        /// <param name="weight">Roughly how long the step takes compared with the others (see <see cref="GenerationStep.Weight"/>)</param>
        public void Add(string name, Action run, double weight = 1)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("A step needs a name.", "name");
            if (run == null) throw new ArgumentNullException("run");
            if (!(weight > 0) || double.IsInfinity(weight)) throw new ArgumentOutOfRangeException("weight", "A step's weight must be a positive number.");
            steps.Add(new GenerationStep(name, run, weight));
        }

        /// <summary>A plan of one step that runs the whole of a generator's <see cref="IWorldGenerator.Generate"/>.</summary>
        public static WorldBuildPlan Single(string name, Func<World> generate)
        {
            WorldBuildPlan plan = new WorldBuildPlan();
            World world = null;
            plan.Add(name, () => world = generate());
            plan.Result = () => world;
            return plan;
        }

        /// <summary>Runs every step and returns the finished world.</summary>
        public World RunAll()
        {
            foreach (GenerationStep step in steps) step.Run();
            return Result != null ? Result() : null;
        }
    }
}
