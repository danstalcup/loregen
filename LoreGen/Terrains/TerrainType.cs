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

        public TerrainType(int ID, string Name, string Description, int Relief = DefaultRelief)
        {
            this.ID = ID;
            this.Name = Name;
            this.Description = Description;
            this.Relief = Relief;
        }
    }
}
