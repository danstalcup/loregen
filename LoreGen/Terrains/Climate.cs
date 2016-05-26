using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LoreGen.Terrains
{
    public class Climate
    {
        public int ID;
        public ClimateType PrimaryClimateType;
        public ClimateType SecondaryClimateType;        
        public string Name;
        public string Description;
        public Climate(int ID, ClimateType PrimaryClimateType, ClimateType SecondaryClimateType, string Name, string Description)
        {
            this.ID = ID;
            this.PrimaryClimateType = PrimaryClimateType;
            this.SecondaryClimateType = SecondaryClimateType;
            this.Name = Name;
            this.Description = Description;
        }
    }
}
