using System;
using System.Collections.Generic;
using System.Linq;

using LoreGen.Randomizer;

namespace LoreGen.WorldGen.Generators
{
    /// <summary>
    /// Grows owned areas (oceans, regions) outward from their starting blocks until every block in a group is claimed.
    /// </summary>
    internal static class BlockGrowth
    {
        /// <summary>
        /// Repeatedly picks a random unclaimed block next to a claimed one and gives it to a random neighbouring owner.
        /// </summary>
        /// <typeparam name="T">The kind of owner (Ocean, Region)</typeparam>
        /// <param name="blocks">The group of blocks to fill. Some should already be owned; the rest are claimed.</param>
        /// <param name="ownerOf">Reads a block's current owner, or null if unclaimed</param>
        /// <param name="assign">Gives a block to an owner</param>
        /// <param name="rnd">Source of randomness</param>
        /// <exception cref="InvalidOperationException">Some blocks can't be reached from any owner</exception>
        public static void Grow<T>(IEnumerable<WorldBlock> blocks, Func<WorldBlock, T> ownerOf, Action<WorldBlock, T> assign, Rnd rnd) where T : class
        {
            List<WorldBlock> all = blocks.ToList();
            HashSet<WorldBlock> unclaimed = new HashSet<WorldBlock>(all.Where(b => ownerOf(b) == null));
            List<WorldBlock> frontier = new List<WorldBlock>();
            HashSet<WorldBlock> inFrontier = new HashSet<WorldBlock>();

            foreach (WorldBlock block in all.Where(b => ownerOf(b) != null))
                AddUnclaimedNeighbours(block, unclaimed, frontier, inFrontier);

            while (frontier.Count > 0)
            {
                int pick = rnd.Unweighted(frontier.Count);
                WorldBlock next = frontier[pick];
                frontier[pick] = frontier[frontier.Count - 1];
                frontier.RemoveAt(frontier.Count - 1);

                List<T> owners = next.SurroundingBlocksAsList().Select(ownerOf).Where(o => o != null).ToList();
                assign(next, owners[rnd.Unweighted(owners.Count)]);
                unclaimed.Remove(next);
                AddUnclaimedNeighbours(next, unclaimed, frontier, inFrontier);
            }

            if (unclaimed.Count > 0)
                throw new InvalidOperationException(unclaimed.Count + " block(s) could not be reached from any starting block.");
        }

        private static void AddUnclaimedNeighbours(WorldBlock block, HashSet<WorldBlock> unclaimed, List<WorldBlock> frontier, HashSet<WorldBlock> inFrontier)
        {
            foreach (WorldBlock neighbour in block.SurroundingBlocksAsList())
                if (unclaimed.Contains(neighbour) && inFrontier.Add(neighbour))
                    frontier.Add(neighbour);
        }
    }
}
