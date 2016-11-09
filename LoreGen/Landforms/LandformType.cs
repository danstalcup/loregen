using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LoreGen.Landforms
{
    public class LandformType
    {
        public int ID;
        public string Name;
        public string Description;
        public LandformShape Shape;
        public int Magnitude;
    }

    public enum LandformShape
    {
        Point, Segment, Area
    }
}
