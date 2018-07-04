using System;
using System.Collections.Generic;
using System.Text;

namespace LinearIndexedGrammarLearner
{
    public class Energy
    {
        public int DataEnergy { get; set; }

        public static bool operator >(Energy c1, Energy c2)
        {
            return c1.DataEnergy > c2.DataEnergy;
        }

        public static bool operator <(Energy c1, Energy c2)
        {
            return c1.DataEnergy < c2.DataEnergy;
        }

        public static int operator -(Energy c1, Energy c2)
        {
            return c1.DataEnergy - c2.DataEnergy;
        }

        public override string ToString()
        {
            return string.Format("Data Energy:{0} ",  DataEnergy);
        }
    }
}

