namespace LoreGen.WorldGen
{
    /// <summary>
    /// Fills in the detail inside a block when someone zooms in: its grid of children, with their land and water, height,
    /// climate, continent, region and terrain. A generator sets one on the world it makes (<see cref="World.Detailer"/>),
    /// and <see cref="World.EnsureChildren"/> calls it.
    ///
    /// Implementations must give the same children whatever order blocks are zoomed into: a child may depend only on the
    /// world seed, its position, and blocks at its parent's depth or coarser (which are fixed, or can be worked out the
    /// same way). Then the same seed gives the same map however it is explored, and children of neighbouring blocks meet
    /// without seams.
    /// </summary>
    public interface IBlockDetailer
    {
        /// <summary>
        /// Gives a block that has no children its grid of children (see <see cref="WorldBlock.InitializeChildBlocks"/> for
        /// the grid), each with its status filled in.
        /// </summary>
        void CreateChildren(WorldBlock block);
    }
}
