using LinearIndexedGrammarParser;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LinearIndexedGrammarLearner
{
    public class BestGrammarsQueue
    {
        private List<(BestGrammarsKey, ContextSensitiveGrammar)> list = new List<(BestGrammarsKey, ContextSensitiveGrammar)>();
        private int currentIndex = 0;
        public int Count => list.Count;

        public (BestGrammarsKey, ContextSensitiveGrammar) FindMin()
        {
            var min = list.FirstOrDefault();
            foreach (var item in list)
            {
                if (item.Item1.CompareTo(min.Item1) < 0)
                    min = item;
            }
            return min;
        }

        public (BestGrammarsKey, ContextSensitiveGrammar) FindMax()
        {
            var max = list.FirstOrDefault();
            foreach (var item in list)
            {
                if (item.Item1.CompareTo(max.Item1) > 0)
                    max = item;
            }

            return max;
        }

        public BestGrammarsKey FindMinKey()
        {
            if (list.Count == 0)
                return new BestGrammarsKey(0, false);
            return FindMin().Item1;
        }

        public void RemoveMin()
        {
            if (list.Count > 0)
            {
                var min = FindMin();
                list.Remove(min);
                currentIndex--;

            }
        }

        public (BestGrammarsKey, ContextSensitiveGrammar) RemoveMax()
        {
            if (list.Count > 0)
            {
                var max = FindMax();
                list.Remove(max);
                currentIndex--;
                return max;

            }
            return (new BestGrammarsKey(0, false), null);
        }

        public void Insert((BestGrammarsKey, ContextSensitiveGrammar) item)
        {
            list.Add(item);
        }

        public bool ContainsKey(BestGrammarsKey key)
        {
            bool res = false;
            if (list.Count == 0) return false;
            foreach (var item in list)
            {
                if (item.Item1.CompareTo(key) == 0)
                {
                    res = true;
                    break;
                }
            }
            return res;
        }

        public (BestGrammarsKey, ContextSensitiveGrammar) Next()
        {
            if (list.Count == 0)
                throw new Exception("empty best grammars queue");

            if (currentIndex == list.Count) currentIndex = 0;
            return list[currentIndex++]; 
        }



    }
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
        public override string ToString()
        {
            return $"objective function value {objectiveFunctionValue:0.000} feasible: {feasible}";
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
