using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using LoreGen.WorldGen;
using LoreGen.WorldGen.Generators;

namespace LoreGen.Simulation
{
    /// <summary>
    /// A world being generated one step at a time, so a caller can show progress or let a user interface redraw between
    /// steps. Start one with <see cref="SimEngine.BeginWorldGeneration"/> and call <see cref="RunNextStep"/> until
    /// <see cref="IsComplete"/>; the engine's World is set when the last step finishes. To cancel, stop calling it: the
    /// engine keeps whatever world it had before. If a step throws, the exception propagates and the generation cannot
    /// continue.
    /// </summary>
    public sealed class WorldGeneration
    {
        private readonly WorldBuildPlan plan;
        private readonly Action<World> finish;
        private readonly TimeSpan[] durations;
        private int next;
        private bool failed;

        internal WorldGeneration(WorldBuildPlan plan, Action<World> finish)
        {
            this.plan = plan;
            this.finish = finish;
            durations = new TimeSpan[plan.Steps.Count];
        }

        /// <summary>Names of all the steps, in the order they run.</summary>
        public IReadOnlyList<string> StepNames { get { return plan.Steps.Select(s => s.Name).ToList(); } }

        /// <summary>How many steps there are.</summary>
        public int StepCount { get { return plan.Steps.Count; } }

        /// <summary>How many steps have finished.</summary>
        public int CompletedSteps { get { return next; } }

        /// <summary>Name of the step that runs next, or null once every step has run.</summary>
        public string NextStep { get { return next < plan.Steps.Count ? plan.Steps[next].Name : null; } }

        /// <summary>How long each step took. Steps that have not run yet are zero.</summary>
        public IReadOnlyList<TimeSpan> Durations { get { return durations; } }

        /// <summary>True once every step has run and the world has been handed to the engine.</summary>
        public bool IsComplete { get; private set; }

        /// <summary>
        /// Roughly how much of the work is done, from 0 to 1: the finished steps' share of all the steps, each counted by
        /// its <see cref="GenerationStep.Weight"/>.
        /// </summary>
        public double Fraction
        {
            get
            {
                if (IsComplete || plan.Steps.Count == 0) return 1;
                double total = plan.Steps.Sum(s => s.Weight);
                return plan.Steps.Take(next).Sum(s => s.Weight) / total;
            }
        }

        /// <summary>
        /// Runs the next step. After the last step the finished world is handed to the engine.
        /// </summary>
        /// <returns>True if steps remain, false once generation is complete</returns>
        /// <exception cref="InvalidOperationException">An earlier step threw</exception>
        public bool RunNextStep()
        {
            if (failed) throw new InvalidOperationException("A step of this world generation failed, so it cannot continue.");
            if (IsComplete) return false;
            if (next < plan.Steps.Count)
            {
                Stopwatch watch = Stopwatch.StartNew();
                try { plan.Steps[next].Run(); }
                catch { failed = true; throw; }
                durations[next] = watch.Elapsed;
                next++;
            }
            if (next < plan.Steps.Count) return true;
            try { finish(plan.Result != null ? plan.Result() : null); }
            catch { failed = true; throw; }
            IsComplete = true;
            return false;
        }

        /// <summary>Runs every remaining step.</summary>
        public void RunToEnd()
        {
            while (RunNextStep()) { }
        }
    }
}
