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
