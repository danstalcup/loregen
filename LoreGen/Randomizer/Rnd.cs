using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


using LoreGen.Lang;
using LoreGen.WorldGen;
using LoreGen.Terrains;

namespace LoreGen.Randomizer
{
    /// <summary>
    /// Random Number Generator - Tweaked for this sim
    /// </summary>
    public class Rnd
    {

        private Random rnd;

        /// <summary>
        /// Seed used in this random number generator
        /// </summary>
        public int Seed;
        public int[] Keys;

        /// <summary>
        /// Uses clock ticks as seed
        /// </summary>
        public Rnd()
        {
            Seed = unchecked((int)(DateTime.Now.Ticks));
            Initialize();
        }

        /// <summary>
        /// Uses given seed
        /// </summary>
        /// <param name="Seed"></param>
        public Rnd(int Seed)
        {
            this.Seed = Seed;
            Initialize();
        }

        private void Initialize()
        {
            rnd = new Random(Seed);
            SimpleRNG.SetSeed(AnyUint(), AnyUint());
            byte[] mykey = new byte[64];
            rnd.NextBytes(mykey);            
            Keys = new int[1000];
            for(int i=0;i<1000;i++)
            {
                Keys[i] = rnd.Next(10000);
            }
        }

        /// <summary>
        /// Returns nonnegative int less than n. See Random.Next(int a)
        /// </summary>
        /// <param name="n">Exclusive upper bound</param>
        /// <returns>Nonnegative int less than n (unweighted distr)</returns>
        public int Unweighted(int n)
        {
            return rnd.Next(n);
        }

        /// <summary>
        /// Returns int greater than or equal to l, less than u
        /// </summary>
        /// <param name="l">Inclusive lower bound</param>
        /// <param name="u">Exclusive upper bound</param>
        /// <returns>Int between l and u (unweighted distr)</returns>
        public int Unweighted(int l, int u)
        {
            return rnd.Next(l, u);
        }

        /// <summary>
        /// Returns nonnegative double less than n. See Random.Next(double a)
        /// </summary>
        /// <param name="u">Exclusive upper bound</param>
        /// <returns>Nonnegative double less than n (unweighted distr)</returns>
        public double Unweighted(double u)
        {
            return Unweighted(0.0, u);
        }

        /// <summary>
        /// Returns double greater than or equal to l, less than u
        /// </summary>
        /// <param name="l">Inclusive lower bound</param>
        /// <param name="u">Exclusive upper bound</param>
        /// <returns>Double between l and u (unweighted distr)</returns>
        public double Unweighted(double l, double u)
        {
            return (R01() * (u - l)) + l;
        }

        /// <summary>
        /// Returns double between 0 (incl.) and 1 (excl.)
        /// </summary>
        /// <returns>0.ABCDE...</returns>
        public double R01()
        {
            return rnd.NextDouble();
        }

        /*
        /// <summary>
        /// Returns a random element from the provided list
        /// </summary>
        /// <param name="list">List of items</param>
        /// <returns>Random element from the list</returns>
        public string RandomFromList(IList<string> list)
        {
            return list.ElementAt(Unweighted(list.Count()));
        }

        /// <summary>
        /// Returns a random element from the provided list
        /// </summary>
        /// <param name="list">List of items</param>
        /// <returns>Random element from the list</returns>
        public int RandomFromList(IList<int> list)
        {
            return list.ElementAt(Unweighted(list.Count()));
        }
        /// <summary>
        /// Returns a random element from the provided list
        /// </summary>
        /// <param name="list">List of items</param>
        /// <returns>Random element from the list</returns>
        public LetterSound RandomFromList(IList<LetterSound> list)
        {
            return list.ElementAt(Unweighted(list.Count()));
        }

        /// <summary>
        /// Returns a random element from the provided list
        /// </summary>
        /// <param name="list">List of items</param>
        /// <returns>Random element from the list</returns>
        public PhoneticInventory RandomFromList(IList<PhoneticInventory> list)
        {
            return list.ElementAt(Unweighted(list.Count()));
        }

        /// <summary>
        /// Returns a random element from the provided list
        /// </summary>
        /// <param name="list">List of items</param>
        /// <returns>Random element from the list</returns>
        public WorldBlock RandomFromList(IList<WorldBlock> list)
        {
            return list.ElementAt(Unweighted(list.Count()));
        }

        /// <summary>
        /// Returns a random element from the provided list
        /// </summary>
        /// <param name="list">List of items</param>
        /// <returns>Random element from the list</returns>
        public EdgePattern RandomFromList(IList<EdgePattern> list)
        {
            return list.ElementAt(Unweighted(list.Count()));
        }

        /// <summary>
        /// Returns a random element from the provided list
        /// </summary>
        /// <param name="list">List of items</param>
        /// <returns>Random element from the list</returns>
        public Biome RandomFromList(IList<Biome> list)
        {
            return list.ElementAt(Unweighted(list.Count()));
        }
        public Biome RandomFromList(IList<Biome> list)
        {
            return list.ElementAt(Unweighted(list.Count()));
        }
        public Block RandomFromList(IList<Block> list)
        {
            return list.ElementAt(Unweighted(list.Count()));
        }
        
        public Language RandomFromList(IList<Language> list)
        {
            return list.ElementAt(Unweighted(list.Count()));
        }
        public Town RandomFromList(IList<Town> list)
        {
            return list.ElementAt(Unweighted(list.Count()));
        }
        public Continent RandomFromList(IList<Continent> list)
        {
            return list.ElementAt(Unweighted(list.Count()));
        }*/
        
        /// <summary>
        /// Returns a random unsigned int
        /// </summary>
        /// <returns>An unsigned int between 0 and 4294967295</returns>
        public uint AnyUint()
        {
            return (uint)(rnd.Next());
        }

        /// <summary>
        /// Randomly selects a double on a normal distribution with the given mean and standard deviation
        /// </summary>
        /// <param name="mean">mean</param>
        /// <param name="stddev">standard deviation</param>
        /// <returns>Randomly selected double (normal distr)</returns>
        public double Normal(double mean, double stddev)
        {
            //AnyUint());
            
            return SimpleRNG.GetNormal(mean, stddev);
        }

        /// <summary>
        /// Randomly selects a double on a normal distribution with the given mean and standard deviation, then truncates it to an int
        /// </summary>
        /// <param name="mean">mean</param>
        /// <param name="stddev">standard deviation</param>
        /// <returns>Randomly selected double, truncated to an int (normal distr)</returns>
        public int NormalAsInt(double mean, double stddev)
        {            
            return (int)(Math.Round(Normal(mean, stddev), MidpointRounding.AwayFromZero));
        }


        // BEGIN DAN EXPERIMENTS 5-26

        public double Next(int[] Params)
        {                       
            long value = Seed;            
            int ind = Seed;            
            long cap = 1000000000000000000;
            for(int j =0; j<Params.Length;j++)
            {
                int i = Params[j];
                int nextInd = Keys[Math.Abs(i+j+ind)%1000];
                string newValue = value.ToString() + nextInd.ToString();
                if (newValue.Length > 18) newValue = newValue.Substring(0, 18);
                value = Convert.ToInt64(newValue);
                ind = nextInd;

            }
            value = value % cap;
            if (value < 0)
            {
                value = Math.Abs(value);                
            }            
            string valueString = value.ToString();
            if(valueString.Length < 3)
            {
                valueString += "1234321";
            }
            string outString = "0.";
            for (int i = 0; i < valueString.Length / 3;i++ )
            {
                outString += Keys[Convert.ToInt32(valueString.Substring(valueString.Length - (i+1) * 3, 3))].ToString("0000");
            }
            return Convert.ToDouble(outString);
        }                

        public int Next(int min, int max, int[] Params)
        {
            return (int)(Next(Params) * (max + min)) - min;
        }
    }
}
