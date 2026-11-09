using System;
using System.Collections.Generic;

namespace LoreGen.WorldGen
{
    /// <summary>
    /// One of the plates of a world's crust, for generators that model plate tectonics (see
    /// <see cref="Generators.TectonicWorldGenerator"/>). A plate covers a set of top-level blocks and drifts in one
    /// direction; where plates meet, their motion builds mountains, trenches, island arcs and rifts.
    /// </summary>
    public class TectonicPlate : WorldEntity
    {
        private readonly Dictionary<WorldBlock, PlateBoundaryKind> boundaries = new Dictionary<WorldBlock, PlateBoundaryKind>();

        /// <summary>Number of the plate, from 1, in the order plates were made.</summary>
        public int Number { get; }

        /// <summary>Whether the plate carries a continent (continental crust), rather than only ocean floor.</summary>
        public bool Continental { get; }

        /// <summary>Drift east (negative is west), in blocks per step of the model.</summary>
        public double VelocityX { get; }

        /// <summary>Drift south (negative is north), in blocks per step of the model.</summary>
        public double VelocityY { get; }

        /// <summary>The top-level blocks the plate covers.</summary>
        public IReadOnlyList<WorldBlock> Blocks { get; }

        public TectonicPlate(World world, int number, bool continental, double velocityX, double velocityY, IReadOnlyList<WorldBlock> blocks)
        {
            World = world;
            WorldEntityType = WorldEntityType.TectonicPlate;
            Number = number;
            Continental = continental;
            VelocityX = velocityX;
            VelocityY = velocityY;
            Blocks = blocks;
            Name = "Plate " + number;
        }

        /// <summary>
        /// The compass direction the plate drifts in, such as "north-east".
        /// </summary>
        public string Heading
        {
            get
            {
                string[] names = { "east", "south-east", "south", "south-west", "west", "north-west", "north", "north-east" };
                double angle = Math.Atan2(VelocityY, VelocityX); // y grows southwards
                int sector = (int)Math.Round(angle / (Math.PI / 4));
                return names[((sector % 8) + 8) % 8];
            }
        }

        /// <summary>
        /// What kind of plate boundary a block lies on (its top-level ancestor, for finer blocks), or None if it lies inside
        /// the plate or on another plate.
        /// </summary>
        public PlateBoundaryKind BoundaryAt(WorldBlock block)
        {
            if (block == null || block.Depth < 1) return PlateBoundaryKind.None;
            PlateBoundaryKind kind;
            return boundaries.TryGetValue(block.AncestorAt(1), out kind) ? kind : PlateBoundaryKind.None;
        }

        internal void SetBoundary(WorldBlock block, PlateBoundaryKind kind)
        {
            boundaries[block] = kind;
        }
    }

    /// <summary>
    /// How two plates meet.
    /// </summary>
    public enum PlateBoundaryKind
    {
        /// <summary>Not on a boundary.</summary>
        None,
        /// <summary>The plates slide past each other.</summary>
        Transform,
        /// <summary>The plates pull apart: rifts on land, ridges under the sea.</summary>
        Divergent,
        /// <summary>The plates push together: mountains, trenches and island arcs.</summary>
        Convergent
    }
}
