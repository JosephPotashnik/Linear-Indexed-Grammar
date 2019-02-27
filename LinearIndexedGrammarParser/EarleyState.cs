using System;
using System.Linq;

namespace LinearIndexedGrammarParser
{
    public class EarleyState : IEquatable<EarleyState>
    {
        public EarleyState(Rule r, int dotIndex, EarleyColumn c, EarleyNode n)
        {
            Rule = r;
            DotIndex = dotIndex;
            StartColumn = c;
            EndColumn = null;
            Node = n;
        }

        public Rule Rule { get; }
        public EarleyColumn StartColumn { get; }
        public EarleyColumn EndColumn { get; set; }
        public int DotIndex { get; }
        public EarleyNode Node { get; set; }

        public bool IsCompleted => DotIndex >= Rule.RightHandSide.Length;

        public DerivedCategory NextTerm => IsCompleted ? null : Rule.RightHandSide[DotIndex];

        private static string RuleWithDotNotation(Rule rule, int dotIndex)
        {
            //this function is called very heavily, so the following code
            //is optimized to return exactly the possible 5 cases.
            if (rule.RightHandSide.Length == 1)
            {
                if (dotIndex == 0)
                    return $"{rule.LeftHandSide} -> $ {rule.RightHandSide[0]}";

                return $"{rule.LeftHandSide} -> {rule.RightHandSide[0]} $";

            }
            //length  = 2
            if (dotIndex == 0)
                return $"{rule.LeftHandSide} -> $ {rule.RightHandSide[0]} {rule.RightHandSide[1]}";
            if (dotIndex == 1)
                return $"{rule.LeftHandSide} -> {rule.RightHandSide[0]} $ {rule.RightHandSide[1]}";

            return $"{rule.LeftHandSide} -> {rule.RightHandSide[0]} {rule.RightHandSide[1]} $";  
        }

        public override string ToString()
        {
            var endColumnIndex = "None";
            if (EndColumn != null)
                endColumnIndex = EndColumn.Index.ToString();
            return string.Format("{0} [{1}-{2}]", RuleWithDotNotation(Rule, DotIndex),
                StartColumn.Index, endColumnIndex);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 23 + Rule.GetHashCode();
                hash = hash * 23 + DotIndex;
                hash = hash * 23 + StartColumn.Index;
                return hash;
            }
        }


        public static EarleyNode MakeNode(EarleyState predecessorState, int endIndex, EarleyNode reductor)
        {
            EarleyNode y;
            var nextDotIndex = predecessorState.DotIndex + 1;
            var nodeName = RuleWithDotNotation(predecessorState.Rule, nextDotIndex);

            if (nextDotIndex == 1 && predecessorState.Rule.RightHandSide.Length > 1)
            {
                y = reductor;
            }
            else
            {
                y = new EarleyNode(nodeName, predecessorState.StartColumn.Index, endIndex);
                if (!y.HasChildren())
                    y.AddChildren(reductor, predecessorState.Node);

                y.RuleNumber = predecessorState.Rule.NumberOfGeneratingRule;
            }

            return y;
        }

        public bool Equals(EarleyState other)
        {
            var val = Rule.Equals(other.Rule) && DotIndex == other.DotIndex && StartColumn.Index == other.StartColumn.Index;
            if (Node == null || other.Node == null)
                return val;
            return val && Node.Equals(other.Node);
        }
    }
}