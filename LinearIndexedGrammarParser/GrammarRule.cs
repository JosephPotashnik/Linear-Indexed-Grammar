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

        public Rule(DerivedCategory leftHandSide, DerivedCategory[] rightHandSide)
        {
            LeftHandSide = new DerivedCategory(leftHandSide);
            if (rightHandSide != null)
            {
                int length = rightHandSide.Length;
                RightHandSide = new DerivedCategory[length];
                for (int i = 0; i < length; i++)
                    RightHandSide[i] = new DerivedCategory(rightHandSide[i]);
            }

        }

        public Rule(string leftHandSide, string[] rightHandSide)
        {
            LeftHandSide = new DerivedCategory(leftHandSide);
            if (rightHandSide != null)
            {
                int length = rightHandSide.Length;
                RightHandSide = new DerivedCategory[length];
                for (int i = 0; i < length; i++)
                    RightHandSide[i] = new DerivedCategory(rightHandSide[i]);
            }
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
            NumberOfGeneratingRule = otherRule.NumberOfGeneratingRule;
        }

        [JsonProperty] public DerivedCategory LeftHandSide { get; set; }

        [JsonProperty] public DerivedCategory[] RightHandSide { get; set; }

        public int NumberOfGeneratingRule { get; set; }

        public override string ToString() 
        {
            var p = RightHandSide.Select(x => x.ToString()).ToArray();
            return $"{NumberOfGeneratingRule}. {LeftHandSide} -> {string.Join(" ", p)}";
        }

        public override int GetHashCode()
        {
            return NumberOfGeneratingRule;
        }

        public bool Equals(Rule other)
        {
            return NumberOfGeneratingRule == other.NumberOfGeneratingRule;
        }
    }
}