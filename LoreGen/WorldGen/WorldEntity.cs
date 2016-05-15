using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LoreGen.WorldGen
{
    /// <summary>
    /// Any entity that takes up space on a map. (e.g. continent, region, nation, etc.)
    /// </summary>
    public class WorldEntity
    {
        /// <summary>
        /// The type of entity
        /// </summary>
        public WorldEntityType WorldEntityType;
        /// <summary>
        /// Name of this entity.
        /// </summary>
        public string Name;

        public WorldDisplay.WorldEntityDisplayInfo DisplayInfo;

        public World World;
    }

    /// <summary>
    /// A type identifier for the World Entity class (i.e. is the entity a nation, continent, etc.)
    /// </summary>
    public enum WorldEntityType
    {
        /// <summary>
        /// The whole world
        /// </summary>
        World,
        
        /// <summary>
        /// A continent
        /// </summary>
        Continent,

        /// <summary>
        /// An ocean
        /// </summary>
        Ocean,

        /// <summary>
        /// A region
        /// </summary>
        Region,

        /// <summary>
        /// A sea
        /// </summary>
        Sea
    }
}
