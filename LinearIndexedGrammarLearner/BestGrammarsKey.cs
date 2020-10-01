using System;

namespace LinearIndexedGrammarLearner
{
    public class BestGrammarsKey : IComparable<BestGrammarsKey>, IEquatable<BestGrammarsKey>
    {
        public readonly double objectiveFunctionValue;
        public readonly bool feasible;
        public double Key
        {
            get
            {

                var k = feasible ? objectiveFunctionValue + 1.0 : objectiveFunctionValue;
                return k;
            }
        }

        public BestGrammarsKey(double objectiveVal, bool f)
        {
            objectiveFunctionValue = objectiveVal;
            feasible = f;
        }

        public BestGrammarsKey(BestGrammarsKey other)
        {
            objectiveFunctionValue = other.objectiveFunctionValue;
            feasible = other.feasible;
        }

        public int CompareTo(BestGrammarsKey other)
        {
            return Key.CompareTo(other.Key);
        }

        public bool Equals(BestGrammarsKey other)
        {
            return Key == other.Key;
        }
    }
}
