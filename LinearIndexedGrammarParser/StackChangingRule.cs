namespace LinearIndexedGrammarParser
{
    public class StackChangingRule : Rule
    {
        public StackChangingRule()
        {
        }

        public StackChangingRule(StackChangingRule otherRule) : base(otherRule)
        {
        }

        public StackChangingRule(DerivedCategory leftHandSide, DerivedCategory[] rightHandSide, int num = -1) : base(
            leftHandSide, rightHandSide, num)
        {
        }
    }
}