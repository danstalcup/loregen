using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LoreGen.Features;
using LoreGen.Lang;
using LoreGen.Simulation;
using LoreGen.WorldGen;
using Xunit;

namespace LoreGen.Tests.Features
{
    /// <summary>
    /// The geographic feature framework: types, footprints, refinement, lookup.
    /// </summary>
    public class GeographicFeatureTests
    {
        private static readonly FeatureType Big = new FeatureType
        {
            Id = "TestBlob", Name = "Test blob", Category = FeatureCategory.Water, Shape = FeatureShape.Blob,
            Scope = FeatureScope.Continental, NameKind = NameKind.Sea
        };

        private static readonly FeatureType Small = new FeatureType
        {
            Id = "TestPart", Name = "Test part", Category = FeatureCategory.Water, Shape = FeatureShape.Blob,
            Scope = FeatureScope.Regional, ParentType = Big, NameKind = NameKind.Sea
        };

        private static readonly FeatureType Line = new FeatureType
        {
            Id = "TestLine", Name = "Test line", Category = FeatureCategory.Mountain, Shape = FeatureShape.Linear,
            Scope = FeatureScope.Continental, NameKind = NameKind.MountainRange
        };

        private static World NewWorld(int seed = 5)
        {
            var engine = new SimEngine(seed);
            engine.Configuration.GeneratorId = "noise";
            engine.GenerateWorld();
            return engine.World;
        }

        private static WorldBlock Top(World world, int x, int y) { return world.WorldBlock.ChildBlocks[x, y]; }

        private static WorldBlock Child(WorldBlock block, int x = 5, int y = 5)
        {
            if (block.ChildBlocks == null) block.InitializeChildBlocks();
            return block.ChildBlocks[x, y];
        }

        private static List<WorldBlock> Square(World world, int x0, int y0, int size)
        {
            var blocks = new List<WorldBlock>();
            for (int y = y0; y < y0 + size; y++)
                for (int x = x0; x < x0 + size; x++)
                    blocks.Add(Top(world, x, y));
            return blocks;
        }

        private class RecordingRefiner : IFeatureRefiner
        {
            public readonly List<(WorldBlock Block, bool CoarseMember)> Calls = new List<(WorldBlock, bool)>();
            public bool Answer;
            public bool Includes(GeographicFeature feature, WorldBlock block, bool coarseMember)
            {
                Calls.Add((block, coarseMember));
                return Answer;
            }
        }

        // ---- Types ----------------------------------------------------------------------------

        [Fact]
        public void MountainRangeTypeIsLoaded()
        {
            var type = new SimEngine(1).SimData.FeatureType("mountainrange");
            Assert.Equal("Mountain range", type.Name);
            Assert.Equal(FeatureCategory.Mountain, type.Category);
            Assert.Equal(FeatureShape.Linear, type.Shape);
            Assert.Equal(FeatureScope.Continental, type.Scope);
            Assert.Null(type.ParentType);
            Assert.Equal(NameKind.MountainRange, type.NameKind);
            Assert.Contains("passes", type.Description); // descriptions may contain commas
        }

        [Fact]
        public void UnknownFeatureTypeIsAnError()
        {
            Assert.Throws<KeyNotFoundException>(() => new SimEngine(1).SimData.FeatureType("Nope"));
        }

        [Theory]
        [InlineData("A,B,Mountain,Wiggly,Continental,,MountainRange,x")]
        [InlineData("A,B,Mountain,Linear,Galactic,,MountainRange,x")]
        [InlineData("A,B,Mountain,Linear,1,,MountainRange,x")]
        [InlineData("A,B,Mountain,Linear,Continental,MountainRange,x")]
        [InlineData("A,B,Mountain,Linear,Continental|Regional,,MountainRange,x")]
        [InlineData(",B,Mountain,Linear,Continental,,MountainRange,x")]
        [InlineData("A,B,Mountain,Linear,Regional,Nope,MountainRange,x")]
        public void MalformedFeatureTypeRowsAreRejected(string row)
        {
            Assert.Throws<InvalidDataException>(() => FeatureType.Parse(new[] { row }));
        }

        [Fact]
        public void FeatureTypeIdsMustBeUnique()
        {
            Assert.Throws<InvalidDataException>(() => FeatureType.Parse(new[]
            {
                "A,B,Mountain,Linear,Continental,,MountainRange,x", "a,C,Water,Blob,Regional,,Sea,y"
            }));
        }

        [Fact]
        public void ParentTypesAreResolvedAndMustBeLarger()
        {
            var types = FeatureType.Parse(new[] { "# comment", "", "Pond,Pond,Water,Blob,Local,Lake,Sea,A pond, in a lake.", "Lake,Lake,Water,Blob,Regional,,Sea,A lake, inland." });
            Assert.Same(types[1], types[0].ParentType);
            Assert.Null(types[1].ParentType);
            Assert.Equal("A pond, in a lake.", types[0].Description);
            Assert.Throws<InvalidDataException>(() => FeatureType.Parse(new[] { "Pond,Pond,Water,Blob,Regional,Lake,Sea,x", "Lake,Lake,Water,Blob,Regional,,Sea,y" }));
        }

        [Fact]
        public void MountainRangePartsAreRegionalSubrangesAndMassifs()
        {
            var data = new SimEngine(1).SimData;
            var range = data.FeatureType("MountainRange");
            foreach (var part in new[] { data.FeatureType("Subrange"), data.FeatureType("Massif") })
            {
                Assert.Same(range, part.ParentType);
                Assert.Equal(FeatureScope.Regional, part.Scope);
                Assert.Equal(FeatureCategory.Mountain, part.Category);
            }
            Assert.Equal(FeatureShape.Linear, data.FeatureType("Subrange").Shape);
            Assert.Equal(FeatureShape.Blob, data.FeatureType("Massif").Shape);
        }

        [Fact]
        public void ATypeCanBePartOfSeveralTypes()
        {
            var types = FeatureType.Parse(new[]
            {
                "Ocean,Ocean,Water,Blob,Continental,,Ocean,o",
                "Sea,Sea,Water,Blob,Continental,,Sea,s",
                "Bay,Bay,Water,Blob,Regional,Ocean|Sea,Bay,b"
            });
            Assert.Equal(new[] { types[0], types[1] }, types[2].ParentTypes);
            Assert.True(types[2].IsPartOf(types[0]));
            Assert.True(types[2].IsPartOf(types[1]));
            Assert.False(types[0].IsPartOf(types[1]));
            Assert.Throws<InvalidDataException>(() => FeatureType.Parse(new[] { "Ocean,Ocean,Water,Blob,Continental,,Ocean,o", "Bay,Bay,Water,Blob,Regional,Ocean|Lagoon,Bay,b" }));

            var data = new SimEngine(1).SimData;
            Assert.True(data.FeatureType("Bay").IsPartOf(data.FeatureType("Ocean")));
            Assert.True(data.FeatureType("Strait").IsPartOf(data.FeatureType("Sea")));
            Assert.Empty(data.FeatureType("River").ParentTypes);
        }

        [Fact]
        public void PeaksPassesAndDesertPartsAreRegionalParts()
        {
            var data = new SimEngine(1).SimData;
            foreach (var (id, parent, shape) in new[]
            {
                ("Peak", "MountainRange", FeatureShape.Point), ("Pass", "MountainRange", FeatureShape.Point),
                ("Erg", "Desert", FeatureShape.Blob), ("SaltFlat", "Desert", FeatureShape.Blob), ("Oasis", "Desert", FeatureShape.Point)
            })
            {
                FeatureType type = data.FeatureType(id);
                Assert.Same(data.FeatureType(parent), type.ParentType);
                Assert.Equal(FeatureScope.Regional, type.Scope);
                Assert.Equal(shape, type.Shape);
                Assert.Equal(type.ParentType.Category, type.Category);
            }
        }

        [Fact]
        public void ScopesMatchDepths()
        {
            Assert.Equal(1, FeatureScope.Continental.Depth());
            Assert.Equal(2, FeatureScope.Regional.Depth());
            Assert.Equal(FeatureScope.Local, FeatureScopes.AtDepth(3));
            Assert.Throws<ArgumentOutOfRangeException>(() => FeatureScopes.AtDepth(0));
        }

        // ---- Construction ---------------------------------------------------------------------

        [Fact]
        public void FeatureBlocksMustBeAtItsTypesScope()
        {
            World world = NewWorld();
            Assert.Throws<ArgumentException>(() => new GeographicFeature(world, Line, new[] { Child(Top(world, 2, 2)) }, 1));
            Assert.Throws<ArgumentException>(() => new GeographicFeature(world, Small, new[] { Top(world, 2, 2) }, 1));
            Assert.Equal(FeatureScope.Regional, new GeographicFeature(world, Small, new[] { Child(Top(world, 2, 2)) }, 1).Scope);
        }

        [Fact]
        public void FootprintBlocksMustBeAtTheScopesDepthAndDistinct()
        {
            World world = NewWorld();
            Assert.Throws<ArgumentException>(() => new GeographicFeature(world, Big, new WorldBlock[0], 1));
            Assert.Throws<ArgumentException>(() => new GeographicFeature(world, Big, new[] { Child(Top(world, 2, 2)) }, 1));
            Assert.Throws<ArgumentException>(() => new GeographicFeature(world, Big, new[] { Top(world, 2, 2), Top(world, 2, 2) }, 1));
        }

        [Fact]
        public void SpineMustLieInTheFootprint()
        {
            World world = NewWorld();
            var feature = new GeographicFeature(world, Line, Square(world, 3, 3, 2), 1);
            Assert.Throws<ArgumentException>(() => feature.SetSpine(new[] { Top(world, 9, 9) }));
            feature.SetSpine(new[] { Top(world, 3, 3), Top(world, 4, 4) });
            Assert.Equal(2, feature.Spine.Count);
        }

        [Fact]
        public void ChildrenMustBeOfAPartTypeAndHaveOneParent()
        {
            World world = NewWorld();
            var big = new GeographicFeature(world, Big, Square(world, 3, 3, 2), 1);
            var other = new GeographicFeature(world, Big, Square(world, 8, 8, 1), 2);
            var small = new GeographicFeature(world, Small, new[] { Child(Top(world, 3, 3)) }, 3);

            Assert.Throws<ArgumentException>(() => big.AddChild(other));
            var line = new GeographicFeature(world, Line, Square(world, 3, 3, 2), 4);
            Assert.Throws<ArgumentException>(() => line.AddChild(small));   // not a part of that type
            big.AddChild(small);
            Assert.Same(big, small.Parent);
            Assert.Contains(small, big.Children);
            Assert.Throws<ArgumentException>(() => other.AddChild(small));
        }

        [Fact]
        public void WorldAcceptsEachFeatureOnce()
        {
            World world = NewWorld(), otherWorld = NewWorld(6);
            int before = world.Features.Count;
            var feature = new GeographicFeature(world, Big, Square(world, 3, 3, 1), 1);
            world.AddFeature(feature);
            Assert.Equal(before + 1, world.Features.Count);
            Assert.Throws<ArgumentException>(() => world.AddFeature(feature));
            Assert.Throws<ArgumentException>(() => otherWorld.AddFeature(feature));
        }

        [Fact]
        public void SeedDependsOnWhereNotOnOrder()
        {
            World world = NewWorld();
            Assert.Equal(GeographicFeature.SeedFor(7, Line, Top(world, 3, 4)), GeographicFeature.SeedFor(7, Line, Top(world, 3, 4)));
            Assert.NotEqual(GeographicFeature.SeedFor(7, Line, Top(world, 3, 4)), GeographicFeature.SeedFor(7, Line, Top(world, 4, 3)));
            Assert.NotEqual(GeographicFeature.SeedFor(7, Line, Top(world, 3, 4)), GeographicFeature.SeedFor(8, Line, Top(world, 3, 4)));
            Assert.NotEqual(GeographicFeature.SeedFor(7, Line, Top(world, 3, 4)), GeographicFeature.SeedFor(7, Big, Top(world, 3, 4)));
        }

        // ---- Footprints and refinement --------------------------------------------------------

        [Fact]
        public void FinerBlocksInheritMembershipByDefault()
        {
            World world = NewWorld();
            var feature = new GeographicFeature(world, Big, Square(world, 5, 5, 3), 1);

            Assert.True(feature.Contains(Child(Top(world, 6, 6))));   // interior
            Assert.True(feature.Contains(Child(Top(world, 5, 5))));   // edge
            Assert.False(feature.Contains(Child(Top(world, 4, 6))));  // beside
            Assert.False(feature.Contains(Child(Top(world, 1, 1))));  // far away
            Assert.True(feature.Contains(Child(Child(Top(world, 7, 7)))));  // two levels down
        }

        [Fact]
        public void RefinerIsOnlyAskedAboutTheEdge()
        {
            World world = NewWorld();
            var refiner = new RecordingRefiner();
            var feature = new GeographicFeature(world, Big, Square(world, 5, 5, 3), 1) { Refiner = refiner };

            WorldBlock interior = Child(Top(world, 6, 6)), edge = Child(Top(world, 5, 6)), beside = Child(Top(world, 4, 6)), far = Child(Top(world, 1, 1));

            refiner.Answer = false;
            Assert.True(feature.Contains(interior));
            Assert.False(feature.Contains(far));
            Assert.Empty(refiner.Calls);

            Assert.False(feature.Contains(edge));    // the refiner trims the edge...
            refiner.Answer = true;
            Assert.True(feature.Contains(beside));   // ...or extends it just past the coarse footprint
            Assert.Equal(new[] { (edge, true), (beside, false) }, refiner.Calls);

            // At its own depth the coarse footprint is the answer; the refiner is not asked
            Assert.True(feature.Contains(Top(world, 5, 6)));
            Assert.False(feature.Contains(Top(world, 4, 6)));
            Assert.Equal(2, refiner.Calls.Count);
        }

        [Fact]
        public void CoarserBlocksContainAFeatureWhenAnyOfItLiesInside()
        {
            World world = NewWorld();
            WorldBlock parent = Top(world, 3, 3);
            var feature = new GeographicFeature(world, Small, new[] { Child(parent, 2, 2) }, 1);
            Assert.True(feature.Contains(parent));
            Assert.False(feature.Contains(Top(world, 4, 3)));
        }

        [Fact]
        public void FootprintsCanBeInSeveralPieces()
        {
            World world = NewWorld();
            var feature = new GeographicFeature(world, Big,
                new[] { Top(world, 1, 1), Top(world, 2, 2), Top(world, 5, 5), Top(world, 4, 1) }, 1);
            var pieces = feature.Footprint.Pieces();
            Assert.Equal(3, pieces.Count);
            Assert.Equal(new[] { Top(world, 1, 1), Top(world, 2, 2) }, pieces[0]);   // corners touch
            Assert.Equal(new[] { Top(world, 5, 5) }, pieces[1]);
            Assert.Equal(new[] { Top(world, 4, 1) }, pieces[2]);
        }

        [Fact]
        public void EdgeIsTheMemberBlocksWithAnOutsideNeighbour()
        {
            World world = NewWorld();
            var feature = new GeographicFeature(world, Big, Square(world, 5, 5, 3), 1);
            Assert.Equal(8, feature.Footprint.Edge().Count);
            Assert.DoesNotContain(Top(world, 6, 6), feature.Footprint.Edge());
            Assert.True(feature.Footprint.IsBeside(Top(world, 4, 5)));
            Assert.False(feature.Footprint.IsBeside(Top(world, 4, 4)));  // only diagonal
        }

        // ---- Lookup ---------------------------------------------------------------------------

        [Fact]
        public void FeaturesAtFindsOverlappingFeaturesAtEveryDepth()
        {
            World world = NewWorld();
            var existing = world.Features.ToList();
            var a = new GeographicFeature(world, Big, Square(world, 5, 5, 3), 1);
            var b = new GeographicFeature(world, Big, Square(world, 6, 6, 3), 2);
            var c = new GeographicFeature(world, Small, new[] { Child(Top(world, 6, 6), 1, 1) }, 3);
            world.AddFeature(a);
            world.AddFeature(b);
            world.AddFeature(c);

            Func<WorldBlock, List<GeographicFeature>> mine = block => world.FeaturesAt(block).Where(f => !existing.Contains(f)).ToList();

            Assert.Equal(new[] { a, b, c }, mine(Top(world, 6, 6)));          // c lies inside it
            Assert.Equal(new[] { a, b, c }, mine(Child(Top(world, 6, 6), 1, 1)));
            Assert.Equal(new[] { a, b }, mine(Child(Top(world, 6, 6), 2, 2)));
            Assert.Equal(new[] { a }, mine(Top(world, 5, 5)));
            Assert.Equal(new[] { b }, mine(Child(Top(world, 8, 8))));
            Assert.Empty(mine(Top(world, 1, 1)));
        }

        [Fact]
        public void FeaturesAtUsesTheRefinerAcrossTheEdge()
        {
            World world = NewWorld();
            var existing = world.Features.ToList();
            var feature = new GeographicFeature(world, Big, Square(world, 5, 5, 3), 1)
            {
                Refiner = new RecordingRefiner { Answer = true }
            };
            world.AddFeature(feature);
            Assert.Contains(feature, world.FeaturesAt(Child(Top(world, 4, 6))));
            Assert.DoesNotContain(feature, world.FeaturesAt(Child(Top(world, 3, 6))));
        }

        [Fact]
        public void BlocksKnowTheirWorld()
        {
            World world = NewWorld();
            Assert.Same(world, world.WorldBlock.World);
            Assert.Same(world, Child(Child(Top(world, 2, 2))).World);
            Assert.Same(Top(world, 2, 2), Child(Child(Top(world, 2, 2))).AncestorAt(1));
            Assert.Throws<ArgumentOutOfRangeException>(() => Top(world, 2, 2).AncestorAt(2));
        }
    }
}
