using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LoreGen.Randomizer
{
    public class ListR<T>
    {
        Rnd Rnd;
        IEnumerable<T> List;

        public ListR(IEnumerable<T> List, Rnd Rnd)
        {
            this.List = List;
            this.Rnd = Rnd;
        }

        public T RandomFromList()
        {
            return List.ElementAt(Rnd.Unweighted(List.Count()));
        }

        public static T RandomFromList(IEnumerable<T> List, Rnd Rnd)
        {
            return new ListR<T>(List, Rnd).RandomFromList();
        }
    }
}
