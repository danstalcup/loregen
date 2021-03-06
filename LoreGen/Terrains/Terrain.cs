﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LoreGen.WorldDisplay;

namespace LoreGen.Terrains
{
    public class Terrain
    {
        
        public TerrainType TerrainType;
        public Climate Climate;
        public string Name;
        public int ID;
        public TerrainDisplayInfo DisplayInfo;
        public string Description;

        public Terrain(int ID, Climate Climate, TerrainType TerrainType, string Name, string Description)
        {
            this.ID = ID;
            this.Climate = Climate;
            this.TerrainType = TerrainType;
            this.Name = Name;
            this.Description = Description;
            this.DisplayInfo = new TerrainDisplayInfo(this);            
        }

    }
    public struct TerrainRules
    {
        public double CombinationWeightReducer;
    }
}
