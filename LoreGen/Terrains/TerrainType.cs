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

        public TerrainType(int ID, string Name)
        {
            this.ID = ID;
            this.Name = Name;
        }
    }
}
