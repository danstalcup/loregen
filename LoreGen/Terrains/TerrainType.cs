using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LoreGen.Terrains
{
    public class TerrainType
    {
        public int ID;
        public string Name;
        public string Description;

        /// <summary>
        /// How high and steep this kind of terrain is, from 0 (flat lowland) to 4 (high peaks). Generators that model
        /// elevation use it to put steeper terrain on higher ground.
        /// </summary>
        public int Relief;

        public const int DefaultRelief = 1;

        /// <summary>
        /// How much standing water this kind of terrain has, from 0 (dry ground) to 1 (marsh). Generators that model water
        /// put wetter terrain where water gathers.
        /// </summary>
        public double Wetness;

        /// <summary>How wooded this kind of terrain is, from 0 (open) to 1 (dense forest).</summary>
        public double Trees;

        /// <summary>
        /// The colour this kind of terrain shows on maps, blended with its climate's, or null to show its climate's colour
        /// alone (as most do).
        /// </summary>
        public Drawing.Color? Color;

        /// <summary>
        /// Feature type Ids this kind of terrain belongs to: if any are given, generators that model terrain only put it on
        /// places one of those features covers (a beach on a Beach, lava on a Volcano). Empty for terrain found anywhere.
        /// </summary>
        public IReadOnlyList<string> RequiresFeature = System.Array.Empty<string>();

        public TerrainType(int ID, string Name, string Description, int Relief = DefaultRelief)
        {
            this.ID = ID;
            this.Name = Name;
            this.Description = Description;
            this.Relief = Relief;
        }
    }
}
