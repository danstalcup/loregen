using System;

using LoreGen.Randomizer;

namespace LoreGen.WorldGen.Generators
{
    /// <summary>
    /// Steps shared by world generators.
    /// </summary>
    internal static class WorldGridBuilder
    {
        /// <summary>
        /// Chooses the world's size from the shared world rules and creates its top-level block and grid of child blocks.
        /// </summary>
        public static void InitializeWorldBlock(World world)
        {
            WorldRules rules = world.SimEngine.SimData.WorldRules;
            Rnd rnd = world.SimEngine.Rnd;

            //Determine world length
            int length = rnd.NormalAsInt(rules.WorldLengthMean, rules.WorldLengthStdDev);
            length = Math.Min(length, rules.WorldLengthMax);
            length = Math.Max(length, rules.WorldLengthMin);
            int trueWidth = length;
            trueWidth = (trueWidth / rules.ZoomScaleLarge) * rules.ZoomScaleLarge;
            int trueHeight = (int)(length * rules.WorldHeightLengthRatio);
            trueHeight = (trueHeight / rules.ZoomScaleLarge) * rules.ZoomScaleLarge;

            //Create world block
            world.WorldBlock = new WorldBlock(world, trueWidth, trueHeight);
            world.WorldBlock.InitializeChildBlocks();
        }
    }
}
