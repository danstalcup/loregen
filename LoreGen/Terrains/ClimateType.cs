using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LoreGen.WorldDisplay;

namespace LoreGen.Terrains
{
    public class ClimateType
    {
        public int ID;
        public string Name;
        public ClimateTypeDisplayInfo DisplayInfo;

        public ClimateType(string Name, int ID, string ColorName)
        {
            this.ID = ID;            
            this.Name = Name;
            DisplayInfo = new ClimateTypeDisplayInfo(this,ColorName);
        }
    }
}
