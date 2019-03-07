using System;
using System.Linq;
using Newtonsoft.Json;

namespace LinearIndexedGrammarParser
{
    [JsonObject(MemberSerialization.OptIn)]
    public class Rule : IEquatable<Rule>
    {
        public Rule()
        {
        }

        public Rule(DerivedCategory leftHandSide, DerivedCategory[] rightHandSide, int num = -1)
        {
            LeftHandSide = new DerivedCategory(leftHandSide);
            if (rightHandSide != null)
            {
                int length = rightHandSide.Length;
                RightHandSide = new DerivedCategory[length];
                for (int i = 0; i < length; i++)
                    RightHandSide[i] = new DerivedCategory(rightHandSide[i]);
            }

            Number = num;
        }

        public Rule(string leftHandSide, string[] rightHandSide, int num = -1)
        {
            LeftHandSide = new DerivedCategory(leftHandSide);
            if (rightHandSide != null)
            {
                int length = rightHandSide.Length;
                RightHandSide = new DerivedCategory[length];
                for (int i = 0; i < length; i++)
                    RightHandSide[i] = new DerivedCategory(rightHandSide[i]);
            }
            Number = num;
        }

        public Rule(Rule otherRule)
        {
            LeftHandSide = new DerivedCategory(otherRule.LeftHandSide);
            if (otherRule.RightHandSide != null)
            {
                int length = otherRule.RightHandSide.Length;
                RightHandSide = new DerivedCategory[length];
                for (int i = 0; i < length; i++)
                    RightHandSide[i] = new DerivedCategory(otherRule.RightHandSide[i]);
            }
            Number = otherRule.Number;
            NumberOfGeneratingRule = otherRule.NumberOfGeneratingRule;
        }

        [JsonProperty] public DerivedCategory LeftHandSide { get; set; }

        [JsonProperty] public DerivedCategory[] RightHandSide { get; set; }

        public int Number { get; set; }
        public int NumberOfGeneratingRule { get; set; }

        public override string ToString() 
        {
            var p = RightHandSide.Select(x => x.ToString()).ToArray();
            return $"{Number}. {LeftHandSide} -> {string.Join(" ", p)}";
        }

        public override int GetHashCode()
        {
            return Number;
        }

        public bool Equals(Rule other)
        {
            return Number == other.Number;
        }
    }
}