using System;
using System.Collections.Generic;
using System.Text;

namespace LinearIndexedGrammarParser
{

    public class SyntacticCategory
    {
        public const string Epsilon = "epsilon";

        public string Symbol { get; set; }
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
}
