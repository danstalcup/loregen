using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LoreGen.WorldGen
{
    /// <summary>
    /// A general pattern describes the shape of a pattern of the edge between two areas.
    /// </summary>
    public class EdgePattern
    {
        /// <summary>
        /// Is the north an edge line?
        /// </summary>
        public bool NorthEdge;
        /// <summary>
        /// Is the south an edge line?
        /// </summary>
        public bool SouthEdge;
        /// <summary>
        /// Is the east an edge line?
        /// </summary>
        public bool EastEdge;
        /// <summary>
        /// Is the west an edge line?
        /// </summary>
        public bool WestEdge;
        /// <summary>
        /// A square grid representing the pattern
        /// </summary>
        public EdgeStatus[,] Pattern;
        public bool PreservesContiguity;
        /// <summary>
        /// The length of the side of the grid.
        /// </summary>        
        public int Length;
        /// <summary>
        /// Generates a blank pattern with a grid of length "Length"
        /// </summary>
        /// <param name="Length">Length of the pattern grid</param>        
        public EdgePattern(int Length, bool PreservesContiguity)
        {
            this.Length = Length;
            this.PreservesContiguity = PreservesContiguity;
            Pattern = new EdgeStatus[Length,Length];
            NorthEdge = false;
            SouthEdge = false;
            EastEdge = false;
            WestEdge = false;
        }
        /// <summary>
        /// Generates and returns an EdgePattern with each square of the grid assigned to Status.
        /// </summary>
        /// <param name="Length">Length of each side of the grid</param>
        /// <param name="Status">Status to set each value in the grid to</param>
        /// <returns>The generated pattern</returns>
        public static EdgePattern GeneratePatternUniformStatus(int Length, EdgeStatus Status)
        {
            EdgePattern pattern = new EdgePattern(Length,true);
            for(int i=0;i<Length;i++)
            {
                for(int j=0;j<Length;j++)
                {
                    pattern.Pattern[i,j]=Status;
                }
            }
            return pattern;
        }
        /// <summary>
        /// Returns a copy of this pattern scaled to fit the provided length
        /// </summary>
        /// <param name="NewLength">Length of the new pattern grid</param>
        /// <returns>The scaled copy of the pattern</returns>
        public EdgePattern FitToLength(int NewLength)
        {

            EdgePattern output = new EdgePattern(NewLength,this.PreservesContiguity);
            output.NorthEdge = NorthEdge;
            output.SouthEdge = SouthEdge;
            output.EastEdge = EastEdge;
            output.WestEdge = WestEdge;

            if (NewLength == Length)
            {
                for (int i = 0; i < NewLength; i++)
                {
                    for (int j = 0; j < NewLength; j++)
                    {
                        output.Pattern[i, j] = Pattern[i, j];
                    }
                }
            }

            else
            {
                double scalar = (((double)Length) / ((double)NewLength));

                for (int i = 0; i < NewLength; i++)
                {
                    for (int j = 0; j < NewLength; j++)
                    {                        
                        int oldI = (int)(scalar * i);
                        int oldJ = (int)(scalar * j);
                      
                        if (oldI > Length - 1) oldI = Length - 1;
                        if (oldJ > Length - 1) oldJ = Length - 1;
                        //if (oldI < 0) oldI = 0;
                        //if (oldJ < 0) oldJ = 0;
                        output.Pattern[i, j] = Pattern[oldI, oldJ];
                    }
                }
            }

            return output;
        }

        public static List<EdgePattern> GetValidEdgePatterns(List<EdgePattern> EdgePatterns, bool N, bool S, bool W, bool E, bool PreservesContiguityOnly=false)
        {
            return EdgePatterns.Where(ep => ep.NorthEdge == N && ep.SouthEdge == S & ep.WestEdge == W && ep.EastEdge == E && (!PreservesContiguityOnly || ep.PreservesContiguity)).ToList();
        }
    }
    /// <summary>
    /// Statuses for the edge pattern
    /// </summary>
    public enum EdgeStatus
    {
        /// <summary>
        /// This region is "on" the edge -- but not on the very edge
        /// </summary>
        On,
        /// <summary>
        /// This region is "off" the edge
        /// </summary>
        Off,
        /// <summary>
        /// This region is on the very edge, but still "on"
        /// </summary>
        Edge
    }
}
