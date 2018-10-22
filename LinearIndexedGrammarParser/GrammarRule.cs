﻿using System.Linq;
using Newtonsoft.Json;

namespace LinearIndexedGrammarParser
{
    [JsonObject(MemberSerialization.OptIn)]
    public class Rule
    {
        public Rule()
        {
        }

        public Rule(DerivedCategory leftHandSide, DerivedCategory[] rightHandSide, int num = -1)
        {
            LeftHandSide = new DerivedCategory(leftHandSide);
            RightHandSide = rightHandSide?.Select(cat => new DerivedCategory(cat)).ToArray();
            Number = num;
        }

        public Rule(string leftHandSide, string[] rightHandSide, int num = -1)
        {
            LeftHandSide = new DerivedCategory(leftHandSide);
            RightHandSide = rightHandSide?.Select(cat => new DerivedCategory(cat)).ToArray();
            Number = num;
        }

        public Rule(Rule otherRule)
        {
            LeftHandSide = new DerivedCategory(otherRule.LeftHandSide);
            RightHandSide = otherRule.RightHandSide.Select(cat => new DerivedCategory(cat)).ToArray();
            Number = otherRule.Number;
            NumberOfGeneratingRule = otherRule.NumberOfGeneratingRule;
        }

        public virtual Rule Clone()
        {
            return new Rule(this);
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

        public override bool Equals(object obj)
        {
            if (!(obj is Rule p))
                return false;

            return Number == p.Number;
        }

        public override int GetHashCode()
        {
            // ReSharper disable once NonReadonlyMemberInGetHashCode
            return Number;
        }

        public bool IsEpsilonRule() => RightHandSide[0].IsEpsilon();

        public virtual bool AddRuleToGrammar(ContextSensitiveGrammar grammar, bool forceAdd = false)
        {
            return grammar.AddStackConstantRule(this, forceAdd);
        }

        public virtual void DeleteRuleFromGrammar(ContextSensitiveGrammar grammar)
        {
            grammar.DeleteStackConstantRule(this);
        }
    }
}