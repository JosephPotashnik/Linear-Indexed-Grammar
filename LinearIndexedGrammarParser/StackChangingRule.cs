using System;
using System.Collections.Generic;
using System.Text;

namespace LinearIndexedGrammarParser
{
    public class StackChangingRule : Rule
    {
        public StackChangingRule()
        {

        }

        public StackChangingRule(StackChangingRule otherRule) : base (otherRule)
        {

        }

        public StackChangingRule(DerivedCategory leftHandSide, DerivedCategory[] rightHandSide, int num = -1) : base(leftHandSide, rightHandSide, num)
        {
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override string ToString()
        {
            return base.ToString();
        }
    }
}
