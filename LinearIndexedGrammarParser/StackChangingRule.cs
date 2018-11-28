namespace LinearIndexedGrammarParser
{
    public class StackChangingRule : Rule
    {
        public StackChangingRule(Rule baseRule, MoveableOperationsKey key, SyntacticCategory moveable) : base(baseRule)
        {
            OperationKey = key;
            Moveable = new SyntacticCategory(moveable);
        }

        public StackChangingRule(StackChangingRule otherRule) : base(otherRule)
        {
            OperationKey = otherRule.OperationKey;
            Moveable = new SyntacticCategory(otherRule.Moveable);
        }

        public MoveableOperationsKey OperationKey { get; set; }
        public SyntacticCategory Moveable { get; set; }
    }
}