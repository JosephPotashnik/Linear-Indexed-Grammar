using System;
using System.Linq;

namespace LinearIndexedGrammarParser
{
    public class EarleyState
    {
        public static int stateCounter;

        public EarleyState(Rule r, int dotIndex, EarleyColumn c, EarleyNode n)
        {
            Rule = r;
            DotIndex = dotIndex;
            StartColumn = c;
            EndColumn = null;
            Node = n;
            StateNumber = stateCounter;
            stateCounter += 1;
        }

        public Rule Rule { get; set; }
        public EarleyColumn StartColumn { get; set; }
        public EarleyColumn EndColumn { get; set; }
        public int DotIndex { get; set; }
        public EarleyNode Node { get; set; }

        public int StateNumber { get; set; }


        private static string RuleWithDotNotation(Rule rule, int dotIndex)
        {
            var terms = rule.RightHandSide.Select(x => x.ToString()).ToList();
            terms.Insert(dotIndex, "$");
            return string.Format("{0} -> {1}", rule.LeftHandSide, string.Join(" ", terms));
        }

        public bool IsCompleted() => DotIndex >= Rule.RightHandSide.Length;


        public DerivedCategory NextTerm()
        {
            if (IsCompleted())
                return null;
            return Rule.RightHandSide[DotIndex];
        }

        public override string ToString()
        {
            var endColumnIndex = "None";
            if (EndColumn != null)
                endColumnIndex = EndColumn.Index.ToString();
            return string.Format("{0} [{1}-{2}]", RuleWithDotNotation(Rule, DotIndex),
                StartColumn.Index, endColumnIndex);
        }

        public override bool Equals(object obj)
        {
            if (!(obj is EarleyState s))
                return false;

            var val = Rule.Equals(s.Rule) && (DotIndex == s.DotIndex) && (StartColumn.Index == s.StartColumn.Index);

            if (Node == null || s.Node == null)
                return val;
            return val && Node.Equals(s.Node);
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
    }
}