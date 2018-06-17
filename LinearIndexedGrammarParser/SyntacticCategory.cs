using System;
using System.Collections.Generic;
using System.Text;

namespace LinearIndexedGrammarParser
{

    public class SyntacticCategory
    {
        public const string Epsilon = "epsilon";

        private readonly string Symbol;
        public SyntacticCategory() { }
        public SyntacticCategory(string symbol) => Symbol = symbol;
        public SyntacticCategory(SyntacticCategory otherCategory) => Symbol = otherCategory.Symbol;

        public override bool Equals(object obj)
        {
            if (!(obj is SyntacticCategory p))
                return false;

            return Symbol == p.Symbol;
        }

        public override int GetHashCode() => Symbol.GetHashCode();
        public override string ToString() => Symbol;
        internal bool IsEpsilon() =>  Symbol == Epsilon;

    }

    public class DerivedCategory : SyntacticCategory
    {
        public string Stack { get; set; }

        public DerivedCategory(string baseCategorySymbol, string _stack = "") : base(baseCategorySymbol)
        {
            Stack = _stack;
        }


        public DerivedCategory(DerivedCategory other) : base (other)
        {
            Stack = other.Stack;
        }
        public override bool Equals(object obj)
        {
            if (!(obj is DerivedCategory p))
                return false;

            return base.Equals(p) &&  Stack == p.Stack;
        }

        private string contents() { return base.ToString() + Stack; }
        public override int GetHashCode() => contents().GetHashCode();
        public override string ToString() => contents();
    }
}
