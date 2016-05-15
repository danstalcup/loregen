using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LoreGen.Randomizer
{
    /// <summary>
    /// Used to randomly select items.
    /// </summary>
    /// <typeparam name="T">The type of item randomly selected from.</typeparam>
    public class WeightedRandomContainer<T>
    {        
        private List<T> Elements;        
        private List<double> Weights;
        private double TotalWeight;
        private Rnd rnd;

        /// <summary>
        /// Use the provided Rnd to generate random numbers
        /// </summary>
        /// <param name="r">Random generator</param>
        public WeightedRandomContainer(Rnd r)
        {
            rnd = r;
            Elements = new List<T>();
            Weights = new List<double>();
            TotalWeight = 0.0;
        }

        /// <summary>
        /// Get a random item
        /// </summary>
        /// <param name="wght">Optional -- provide the weight to use to retrieve an item</param>
        /// <returns>An item from the collection</returns>
        public T Get(double wght = -1.0)
        {
            if (wght < 0)
                wght = rnd.Unweighted(TotalWeight);

            int ind = 0;
            foreach (double d in Weights)
            {
                if (d > wght)
                    break;

                wght -= d;
                ind++;
            }

            return Elements.ElementAt(ind);
        }
        /// <summary>
        /// Add an item to the collection
        /// </summary>
        /// <param name="element">Element to add to the collection</param>
        /// <param name="weight">The element's weight</param>
        public void Add(T element, double weight)
        {
            Elements.Add(element);
            Weights.Add(weight);
            TotalWeight += weight;
        }
        
        //TODO: Make these more secure?
        public List<T> GetAllElements()
        {
            return this.Elements;
        }

        public List<double> GetAllWeights()
        {
            return this.Weights;
        }

    }
}
