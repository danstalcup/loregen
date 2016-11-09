using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LoreGen.WorldGen;

namespace LoreGen.Landforms
{
    public class Landform : WorldEntity
    {
        public LandformType Type;

        public Landform(World World)
        {
            this.WorldEntityType = WorldGen.WorldEntityType.Landform;
            this.World = World;
            this.Name = string.Empty;
            this.DisplayInfo = WorldDisplay.WorldEntityDisplayInfo.GenerateWithRandomColor(this);
        }

    }
}
