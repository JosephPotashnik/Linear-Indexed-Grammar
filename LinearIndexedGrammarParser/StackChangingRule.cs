namespace LinearIndexedGrammarParser
{
    public class StackChangingRule : Rule
    {
        public MoveableOperationsKey OperationKey { get; set; }
        public SyntacticCategory Moveable { get; set; }

        public StackChangingRule(Rule baseRule, MoveableOperationsKey key, SyntacticCategory moveable) : base(baseRule)
        {
            OperationKey = key;
            Moveable = new SyntacticCategory(moveable);
        }

        public StackChangingRule(StackChangingRule otherRule) : base (otherRule)
        {
            OperationKey = otherRule.OperationKey;
            Moveable = new SyntacticCategory(otherRule.Moveable);
        }
        public override Rule Clone()
        {
            return new StackChangingRule(this);
        }

        public override bool AddRuleToGrammar(ContextSensitiveGrammar grammar, bool forceAdd = false)
        {
            return grammar.AddStackChangingRule(Moveable, this, OperationKey, forceAdd);
        }

        public override void DeleteRuleFromGrammar(ContextSensitiveGrammar grammar)
        {
            grammar.DeleteStackChangingRule(Moveable, this, OperationKey);
        }
    }
}