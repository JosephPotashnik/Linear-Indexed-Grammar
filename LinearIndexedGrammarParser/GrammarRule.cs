using System;
using System.Linq;
using Newtonsoft.Json;

namespace LinearIndexedGrammarParser
{
    [JsonObject(MemberSerialization.OptIn)]
    public class GrammarRule
    {
        [JsonProperty]
        public SyntacticCategory LeftHandSide { get; set; }

        [JsonProperty]
        public SyntacticCategory[] RightHandSide { get; set; }

        [JsonProperty]
        public int HeadPosition { get; set; }

        [JsonProperty]
        public int ComplementPosition { get; set; }

        public int Number { get; set; }

        public string HeadTerm => RightHandSide[HeadPosition].Symbol;
        public string NonHeadTerm => RightHandSide[(HeadPosition + 1) % RightHandSide.Length].Symbol;
        public string ComplementTerm => RightHandSide[ComplementPosition].Symbol;
        public string NonComplementTerm => RightHandSide[(ComplementPosition + 1) % RightHandSide.Length].Symbol;

        public GrammarRule() {}

        public GrammarRule(string leftHandSide, string[] rightHandSide, int headPos = 0, int compPos = 1, int num = -1)
        {
            LeftHandSide = new SyntacticCategory(leftHandSide);
            RightHandSide = rightHandSide?.Select(cat => new SyntacticCategory(cat)).ToArray();
            HeadPosition = headPos;
            ComplementPosition = compPos;
            Number = num;
        }

        public GrammarRule(GrammarRule otherRule)
        {
            LeftHandSide = new SyntacticCategory(otherRule.LeftHandSide);
            RightHandSide = otherRule.RightHandSide.Select(cat => new SyntacticCategory(cat)).ToArray();
            HeadPosition = otherRule.HeadPosition;
            ComplementPosition = otherRule.ComplementPosition;
            Number = otherRule.Number;
        }

        public override string ToString()
        {
            var p = RightHandSide.Select(x => x.ToString()).ToArray();
            return $"{Number}.{LeftHandSide}->{string.Join(" ", p)} {HeadPosition}{ComplementPosition}";
        }

        public override bool Equals(object obj)
        {
            if (!(obj is GrammarRule p))
                return false;

            return Number == p.Number;

        }

        public override int GetHashCode()
        {
            // ReSharper disable once NonReadonlyMemberInGetHashCode
            return Number;
        }

        public bool IsEpsilonRule() => RightHandSide[0].IsEpsilon();

    }

}
