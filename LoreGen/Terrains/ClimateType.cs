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
        public string Description;

        /// <summary>
        /// The temperature (0 coldest to 1 hottest) this kind of climate is most typical of. Generators that model climate
        /// use it, with <see cref="IdealMoisture"/>, to give each place the climate that fits it best.
        /// </summary>
        public double IdealTemperature = 0.5;

        /// <summary>The moisture (0 driest to 1 wettest) this kind of climate is most typical of.</summary>
        public double IdealMoisture = 0.5;

        public ClimateType(string Name, int ID, string ColorName, string Description)
        {
            this.ID = ID;            
            this.Name = Name;
            DisplayInfo = new ClimateTypeDisplayInfo(this,ColorName);
            this.Description = Description;
        }
    }
}
