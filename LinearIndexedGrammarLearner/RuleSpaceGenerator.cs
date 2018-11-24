using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LinearIndexedGrammarParser;

namespace LinearIndexedGrammarLearner
{
    public class RuleSpaceGenerator
    {
        private List<string> nonTerminals;

        public List<List<Rule>> RuleSpace { get; set; }
        //public List<Dictionary<string, List<Rule>>> RHSSymbols { get; set; }
        private string[] _partsOfSpeechCategories;

        public RuleSpaceGenerator(string[] partsOfSpeechCategories)
        {
            _partsOfSpeechCategories = partsOfSpeechCategories;
            nonTerminals = new List<string>();
            RuleSpace = new List<List<Rule>>();
            //RHSSymbols = new List<Dictionary<string, List<Rule>>>();

            var rhsStore = _partsOfSpeechCategories.ToList();
            Rule newRule;

            var currentNonTerminalSpace = new List<Rule>();
            string currentLHSNonterminal = "X1";
            newRule = new Rule(ContextFreeGrammar.StartRule, new[] { currentLHSNonterminal });
            currentNonTerminalSpace.Add(newRule);

            foreach (var rhs in rhsStore)
            {
                foreach (var rhs2 in rhsStore)
                {
                    //Xi -> RHS1 RHS2
                    newRule = new Rule(currentLHSNonterminal, new[] { rhs, rhs2 });
                    currentNonTerminalSpace.Add(newRule);
                }

                newRule = new Rule(currentLHSNonterminal, new[] { rhs });
                currentNonTerminalSpace.Add(newRule);

                //Xi -> RHS Xi 
                newRule = new Rule(currentLHSNonterminal, new[] { rhs, currentLHSNonterminal });
                currentNonTerminalSpace.Add(newRule);

                //Xi -> Xi RHS 
                newRule = new Rule(currentLHSNonterminal, new[] { currentLHSNonterminal, rhs });
                currentNonTerminalSpace.Add(newRule);
            }

            nonTerminals.Add(currentLHSNonterminal);
            RuleSpace.Add(currentNonTerminalSpace);

        }

        public void GenerateSpaceUpToNonTerminal(int k)
        {
            //we already have the rule space up to k.
            if (k <= RuleSpace.Count) return;
            Rule newRule;

            var rhsStore = _partsOfSpeechCategories.ToList();
            rhsStore = rhsStore.Concat(nonTerminals).ToList();

            int startIndex = RuleSpace.Count + 1;
            for (int i = startIndex ; i <= k; i++)
            {
                string currentLHSNonterminal = $"X{i}";
                DerivedCategory currentLHSCategory = new DerivedCategory(currentLHSNonterminal);

                var lastNonTerminalSpace = RuleSpace[RuleSpace.Count - 1];
                var currentNonTerminalSpace = lastNonTerminalSpace
                    .Select(x => new Rule(currentLHSCategory, x.RightHandSide)).ToList();

                currentNonTerminalSpace[0] = new Rule(ContextFreeGrammar.StartRule, new[] { currentLHSNonterminal });

                foreach (var rhs in rhsStore)
                {

                    //Xi -> RHS Xi 
                    newRule = new Rule(currentLHSNonterminal, new[] { rhs, currentLHSNonterminal });
                    currentNonTerminalSpace.Add(newRule);

                    //Xi -> Xi RHS 
                    newRule = new Rule(currentLHSNonterminal, new[] {currentLHSNonterminal, rhs});
                    currentNonTerminalSpace.Add(newRule);

                }

                nonTerminals.Add(currentLHSNonterminal);
                rhsStore.Add(currentLHSNonterminal);
                RuleSpace.Add(currentNonTerminalSpace);
            }
        }
    }
}
