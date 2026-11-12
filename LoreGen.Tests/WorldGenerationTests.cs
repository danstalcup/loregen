using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LoreGen.Simulation;
using LoreGen.WorldGen;
using LoreGen.WorldGen.Generators;
using Xunit;

namespace LoreGen.Tests
{
    /// <summary>
    /// Generating a world one step at a time (SimEngine.BeginWorldGeneration), as the web app does to show progress.
    /// </summary>
    public class WorldGenerationTests
    {
        [Fact]
        public void ElevationGeneratorsReportTheirStages()
        {
            var engine = new SimEngine(5);
            engine.Configuration.GeneratorId = TectonicWorldGenerator.GeneratorId;
            var names = engine.BeginWorldGeneration().StepNames;
            Assert.Equal("Tectonic plates", names.First());
            Assert.Equal("Naming plates", names.Last());
            Assert.Contains("Climate", names);
            Assert.Contains("Regions", names);
            Assert.Equal(4, names.Count(n => n.StartsWith("Fine terrain (")));
            Assert.Equal(names.Count, names.Distinct().Count());

            engine.Configuration.GeneratorId = NoiseWorldGenerator.GeneratorId;
            Assert.Equal("Elevation", engine.BeginWorldGeneration().StepNames.First());
        }

        [Fact]
        public void GeneratorsWithoutStepsRunAsOne()
        {
            var engine = new SimEngine(5);
            engine.Configuration.GeneratorId = LegacyWorldGenerator.GeneratorId;
            WorldGeneration generation = engine.BeginWorldGeneration();
            Assert.Equal(1, generation.StepCount);
            Assert.False(generation.RunNextStep());
            Assert.True(generation.IsComplete);
            Assert.NotNull(engine.World);
        }

        [Fact]
        public void StepsAreTimed()
        {
            var engine = new SimEngine(5);
            engine.Configuration.GeneratorId = TectonicWorldGenerator.GeneratorId;
            WorldGeneration generation = engine.BeginWorldGeneration();
            Assert.All(generation.Durations, d => Assert.Equal(TimeSpan.Zero, d));
            generation.RunToEnd();
            Assert.True(generation.Durations.Sum(d => d.TotalMilliseconds) > 0);
        }

        [Fact]
        public void SkippedFineBordersAreNotAStep()
        {
            var engine = new SimEngine(5);
            engine.Configuration.GeneratorId = TectonicWorldGenerator.GeneratorId;
            Assert.Contains("Fine region borders", engine.BeginWorldGeneration().StepNames);
            engine.Configuration.GeneratorSettings["RegionBorderDetail"] = 0;
            Assert.DoesNotContain("Fine region borders", engine.BeginWorldGeneration().StepNames);
        }

        [Fact]
        public void BadSettingsAreRejectedBeforeAnyStepRuns()
        {
            var engine = new SimEngine(5);
            engine.Configuration.GeneratorId = TectonicWorldGenerator.GeneratorId;
            engine.Configuration.GeneratorSettings["NoSuchSetting"] = 1;
            Assert.ThrowsAny<Exception>(() => engine.BeginWorldGeneration());
        }

        [Fact]
        public void AFailedStepCannotBeContinued()
        {
            var engine = new SimEngine(5);
            engine.WorldGenerator = new FailingGenerator();
            WorldGeneration generation = engine.BeginWorldGeneration();
            Assert.True(generation.RunNextStep());
            Assert.Throws<InvalidDataException>(() => generation.RunNextStep());
            Assert.Throws<InvalidOperationException>(() => generation.RunNextStep());
            Assert.False(generation.IsComplete);
            Assert.Null(engine.World);
        }

        [Fact]
        public void ProgressFollowsTheStepWeights()
        {
            var engine = new SimEngine(5);
            engine.WorldGenerator = new WeightedGenerator();
            WorldGeneration generation = engine.BeginWorldGeneration();
            Assert.Equal(0.0, generation.Fraction);
            generation.RunNextStep();
            Assert.Equal(0.25, generation.Fraction, 6);
            generation.RunNextStep();
            Assert.Equal(1.0, generation.Fraction);
            Assert.True(generation.IsComplete);
        }

        [Fact]
        public void StepsNeedAPositiveWeight()
        {
            var plan = new WorldBuildPlan();
            Assert.Throws<ArgumentOutOfRangeException>(() => plan.Add("Zero", () => { }, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => plan.Add("Not a number", () => { }, double.NaN));
        }

        private sealed class WeightedGenerator : IWorldGenerator
        {
            public string Id => "weighted";
            public string DisplayName => "Weighted";
            public string Description => "A cheap step, then a long one.";
            public string Version => "1";
            public World Generate(SimEngine engine) => Plan(engine).RunAll();
            public WorldBuildPlan Plan(SimEngine engine)
            {
                var legacy = new LegacyWorldGenerator();
                World world = null;
                var plan = new WorldBuildPlan();
                plan.Add("Cheap", () => { }, 1);
                plan.Add("Long", () => world = legacy.Generate(engine), 3);
                plan.Result = () => world;
                return plan;
            }
        }

        private sealed class FailingGenerator : IWorldGenerator
        {
            public string Id => "failing";
            public string DisplayName => "Failing";
            public string Description => "Fails in its second step.";
            public string Version => "1";
            public World Generate(SimEngine engine) => Plan(engine).RunAll();
            public WorldBuildPlan Plan(SimEngine engine)
            {
                var plan = new WorldBuildPlan();
                plan.Add("Fine", () => { });
                plan.Add("Breaks", () => throw new InvalidDataException("broken"));
                plan.Add("Never runs", () => { });
                return plan;
            }
        }
    }
}
