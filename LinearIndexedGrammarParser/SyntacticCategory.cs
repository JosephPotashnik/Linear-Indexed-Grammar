﻿using System.Collections.Generic;

namespace LinearIndexedGrammarParser
{
    public class SyntacticCategory
    {
        protected string Symbol;

        public SyntacticCategory()
        {
        }

        public SyntacticCategory(string symbol)
        {
            Symbol = symbol;
        }

        public SyntacticCategory(SyntacticCategory otherCategory)
        {
            Symbol = otherCategory.Symbol;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is SyntacticCategory p))
                return false;

            return Symbol == p.Symbol;
        }

        public override int GetHashCode()
        {
            // ReSharper disable once NonReadonlyMemberInGetHashCode
            return Symbol.GetHashCode();
        }

        public override string ToString()
        {
            return Symbol;
        }

        internal bool IsEpsilon()
        {
            return Symbol == ContextFreeGrammar.EpsilonSymbol;
        }
    }

    public class DerivedCategory : SyntacticCategory
    {
        public DerivedCategory(string baseCategorySymbol, string stack = "") : base(baseCategorySymbol)
        {
            Stack = stack;
        }


        public DerivedCategory(DerivedCategory other) : base(other)
        {
            Stack = other.Stack;
            StackSymbolsCount = other.StackSymbolsCount;
        }

        public string Stack { get; set; }
        public int StackSymbolsCount { get; set; }

        private string Contents => Symbol + Stack;

        public override bool Equals(object obj)
        {
            if (!(obj is DerivedCategory p))
                return false;

            return base.Equals(p) && Stack.Equals(p.Stack);
        }

        public override int GetHashCode()
        {
            return Contents.GetHashCode();
        }

        public override string ToString()
        {
            return Contents;
        }

        public bool BaseEquals(DerivedCategory other)
        {
            return Symbol == other.Symbol;
        }

        public void SetBase(DerivedCategory other)
        {
            Symbol = other.Symbol;
        }

        public void Replace(Dictionary<string, string> replaceDic)
        {
            if (replaceDic.ContainsKey(Symbol))
                Symbol = replaceDic[Symbol];

            string moveable = null;
            if (Stack.Length > 0)
            {
                if (Stack.Contains(ContextFreeGrammar.StarSymbol))
                    moveable = Stack.Substring(1);
                else
                    moveable = Stack;
            }

            if (moveable != null)
                if (replaceDic.ContainsKey(moveable))
                    Stack = Stack.Replace(moveable, replaceDic[moveable]);
        }
    }
}