using System;
using System.Collections.Generic;
using System.Linq;

using LoreGen.WorldGen;

namespace LoreGen.Features
{
    /// <summary>
    /// The blocks a geographic feature covers.
    ///
    /// The footprint is a set of blocks at the feature's depth (see <see cref="FeatureScope"/>): its coarse shape. It need
    /// not be in one piece: a mountain range may be broken by passes. Below that depth the feature's
    /// <see cref="IFeatureRefiner"/> decides the finer outline, but only around the edge, so interior blocks are always
    /// in the feature (see <see cref="Contains"/>).
    /// </summary>
    public class FeatureFootprint
    {
        private readonly List<WorldBlock> blocks;
        private readonly HashSet<WorldBlock> members;
        private HashSet<WorldBlock> ancestors;
        private List<List<WorldBlock>> pieces;

        /// <summary>The feature this is the footprint of.</summary>
        public GeographicFeature Feature { get; internal set; }

        /// <summary>The depth of the blocks in the footprint.</summary>
        public int Depth { get; }

        /// <summary>The blocks covered, all at <see cref="Depth"/>, in the order they were given.</summary>
        public IReadOnlyList<WorldBlock> Blocks { get { return blocks; } }

        /// <summary>Number of blocks in the footprint.</summary>
        public int Count { get { return blocks.Count; } }

        /// <param name="blocks">The blocks covered: at least one, all at the given depth, none repeated</param>
        /// <param name="depth">Depth of the blocks</param>
        /// <exception cref="ArgumentException">The blocks are empty, repeated, or at the wrong depth</exception>
        public FeatureFootprint(IEnumerable<WorldBlock> blocks, int depth)
        {
            if (blocks == null) throw new ArgumentNullException("blocks");
            this.blocks = blocks.ToList();
            if (this.blocks.Count == 0) throw new ArgumentException("A feature must cover at least one block.", "blocks");
            if (this.blocks.Any(b => b == null)) throw new ArgumentException("A feature's blocks must not be null.", "blocks");
            WorldBlock wrong = this.blocks.FirstOrDefault(b => b.Depth != depth);
            if (wrong != null)
                throw new ArgumentException("A feature defined at depth " + depth + " was given a block at depth " + wrong.Depth + ".", "blocks");
            members = new HashSet<WorldBlock>(this.blocks);
            if (members.Count != this.blocks.Count) throw new ArgumentException("A feature's blocks must not repeat.", "blocks");
            Depth = depth;
        }

        /// <summary>
        /// Whether a block at the footprint's own depth is one of its blocks (the coarse shape, ignoring any refinement).
        /// </summary>
        public bool IsMember(WorldBlock block)
        {
            return block != null && members.Contains(block);
        }

        /// <summary>
        /// Whether a member block lies on the edge of the footprint: one of its four neighbours is missing or not a member.
        /// </summary>
        public bool IsEdge(WorldBlock block)
        {
            if (!IsMember(block)) return false;
            foreach (WorldBlock neighbour in block.SurroundingBlocks())
                if (neighbour == null || !members.Contains(neighbour)) return true;
            return false;
        }

        /// <summary>
        /// Whether a block at the footprint's depth is not a member but lies directly beside one (shares a side with it).
        /// </summary>
        public bool IsBeside(WorldBlock block)
        {
            if (block == null || block.Depth != Depth || IsMember(block)) return false;
            return block.SurroundingBlocksAsList().Any(members.Contains);
        }

        /// <summary>
        /// The member blocks on the edge of the footprint.
        /// </summary>
        public List<WorldBlock> Edge()
        {
            return blocks.Where(IsEdge).ToList();
        }

        /// <summary>
        /// Whether the feature covers a block, at any depth:
        ///  - coarser than the footprint: whether any part of the feature lies inside it;
        ///  - at the footprint's depth: whether it is one of its blocks;
        ///  - finer than the footprint: inside an interior block, yes; inside an edge block or a block just beside the
        ///    footprint, the feature's refiner decides; anywhere else, no.
        /// </summary>
        public bool Contains(WorldBlock block)
        {
            if (block == null) return false;
            if (block.Depth < Depth) return Overlaps(block);
            if (block.Depth == Depth) return members.Contains(block);

            WorldBlock coarse = block.AncestorAt(Depth);
            bool member = members.Contains(coarse);
            if (member && !IsEdge(coarse)) return true;
            if (!member && !IsBeside(coarse)) return false;
            IFeatureRefiner refiner = Feature?.Refiner ?? InheritRefiner.Instance;
            return refiner.Includes(Feature, block, member);
        }

        /// <summary>
        /// Whether any of the footprint lies inside a block coarser than it (or is the block, at the same depth).
        /// </summary>
        public bool Overlaps(WorldBlock block)
        {
            if (block == null || block.Depth > Depth) return false;
            if (block.Depth == Depth) return members.Contains(block);
            if (ancestors == null)
            {
                ancestors = new HashSet<WorldBlock>();
                foreach (WorldBlock member in blocks)
                    for (WorldBlock a = member.ParentBlock; a != null; a = a.ParentBlock) ancestors.Add(a);
            }
            return ancestors.Contains(block);
        }

        /// <summary>
        /// The separate pieces of the footprint. Blocks touching at a side or a corner are in the same piece. Pieces are
        /// in the order of their first block in <see cref="Blocks"/>, and blocks within a piece keep that order too.
        /// </summary>
        public IReadOnlyList<IReadOnlyList<WorldBlock>> Pieces()
        {
            if (pieces == null)
            {
                int[] piece = Enumerable.Repeat(-1, blocks.Count).ToArray();
                int count = 0;
                for (int i = 0; i < blocks.Count; i++)
                {
                    if (piece[i] >= 0) continue;
                    piece[i] = count;
                    Stack<int> pending = new Stack<int>();
                    pending.Push(i);
                    while (pending.Count > 0)
                    {
                        int j = pending.Pop();
                        for (int k = 0; k < blocks.Count; k++)
                        {
                            if (piece[k] >= 0 || !Touch(blocks[j], blocks[k])) continue;
                            piece[k] = count;
                            pending.Push(k);
                        }
                    }
                    count++;
                }
                pieces = Enumerable.Range(0, count).Select(p => Enumerable.Range(0, blocks.Count).Where(i => piece[i] == p).Select(i => blocks[i]).ToList()).ToList();
            }
            return pieces;
        }

        private static bool Touch(WorldBlock a, WorldBlock b)
        {
            return a.TrueX <= b.TrueXMax && b.TrueX <= a.TrueXMax && a.TrueY <= b.TrueYMax && b.TrueY <= a.TrueYMax;
        }
    }
}
