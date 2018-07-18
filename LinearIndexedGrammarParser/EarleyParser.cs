using System;
using System.Collections.Generic;
using System.Linq;

namespace LinearIndexedGrammarParser
{
    public class LogException : Exception
    {
        public LogException(string str) : base(str) { }
    }

    public class GenerateException : Exception { }
    
    public class EarleyParser
    {
        protected Vocabulary voc;
        private Grammar grammar;

        public EarleyParser(Grammar g, Vocabulary v = null)
        {
            voc  = v ?? Vocabulary.ReadVocabularyFromFile(@"Vocabulary.json");

            //deep copy to a private variable, because the rules of the grammar
            //are dynamically changeable, do not alter the input grammar.
            grammar = new Grammar(g);
        }
        
        private void Predict(EarleyColumn col, List<Rule> ruleList)
        {
            foreach (var rule in ruleList)
            {
                var newState = new EarleyState(rule, 0, col, null);
                col.AddState(newState, grammar);
            }
        }

        protected void Scan(EarleyColumn col, EarleyState state, SyntacticCategory term, string token)
        {
            var v = new EarleyNode(term.ToString(), col.Index - 1, col.Index)
            {
                AssociatedTerminal = token
            };
            var y = EarleyState.MakeNode(state, col.Index, v);
            var newState = new EarleyState(state.Rule, state.DotIndex + 1, state.StartColumn, y);

            col.AddState(newState, grammar);
        }

        private void Complete(EarleyColumn col, EarleyState state)
        {
            if (state.Rule.LeftHandSide.ToString() == Grammar.GammaRule)
            {
                col.GammaStates.Add(state);
                return;
            }

            var startColumn = state.StartColumn;
            var completedSyntacticCategory = state.Rule.LeftHandSide;
            var predecessorStates = startColumn.StatesWithNextSyntacticCategory[completedSyntacticCategory];

            foreach (var st in predecessorStates)
            {
                var y = EarleyState.MakeNode(st, state.EndColumn.Index, state.Node);
                var newState = new EarleyState(st.Rule, st.DotIndex + 1, st.StartColumn, y);
                col.AddState(newState, grammar);       
            }
        }

        //inactive. use for debugging when you suspect program stuck.
        //at the moment, the promiscuous grammar could generate +100,000 earley states
        //so a very large amount of earley states is not necessarily a a bug.
        private void TestForTooManyStatesInColumn(EarleyColumn col, int count)
        {
            //if (count > 100000)
            //{
            //    Console.WriteLine("More than 10000 states in a single column. Suspicious. Grammar is : {0}",
            //        grammar);
            //    throw new Exception("Grammar with infinite parse. abort this grammar..");
            //}
        }
        
        public List<EarleyNode> ParseSentence(string text, int maxWords = 0)
        {
            (EarleyColumn[] table, int[] finalColumns) = PrepareEarleyTable(text, maxWords);
            EarleyState.stateCounter = 0;
            List<EarleyNode> nodes = new List<EarleyNode>();

            var startGrammarRule = new Rule(Grammar.GammaRule, new[] { Grammar.StartRule });
            var startRule = new Rule(startGrammarRule);
            grammar.AddStaticRule(startGrammarRule);

            var startState = new EarleyState(startRule, 0, table[0], null);
            table[0].AddState(startState, grammar);
            try
            {
                foreach (var col in table)
                {
                    var count = 0;

                    //1. complete
                    count = TraverseCompletedStates(col, count);

                    //2. predict after complete:
                    count = TraversePredictableStates(col, count);

                    //3. scan after predict.
                    TraverseScannableStates(table, col);
                }

                int numOfColumnsToLookForGamma = finalColumns.Length;
                foreach (var index in finalColumns)
                {
                    var n = table[index].GammaStates.Select(x => x.Node.Children[0]).ToList();
                    nodes.AddRange(n);
                }
                
            }
            catch (LogException e)
            {
                var s = e.ToString();
                Console.WriteLine(s);
                Console.WriteLine(string.Format("sentence: {0}, grammar: {1}", text, grammar));
            }

            //if the parse is unsuccessful - nodes will contain an empty list with 0 trees.
            //return nodes;

            catch (Exception e)
            {
                var s = e.ToString();
                Console.WriteLine(s);
            }
            if (nodes.Count > 0)
                return nodes;

            throw new Exception("Parsing Failed!");
        }

        virtual protected (EarleyColumn[], int[]) PrepareEarleyTable(string text, int maxWord)
        {
            string[] arr = text.Split();
            //check below that the text appears in the vocabulary
            if (arr.Any(str => !voc.ContainsWord(str)))
                throw new Exception("word in text does not appear in the vocabulary.");

            var table = new EarleyColumn[arr.Length + 1];

            for (var i = 1; i < table.Length; i++)
                table[i] = new EarleyColumn(i, arr[i - 1]);

            table[0] = new EarleyColumn(0, "");
            return (table, new[] { table.Length - 1 });
        }

        protected virtual HashSet<string> GetPossibleSyntacticCategoriesForToken(string nextScannableTerm)
        {
            return voc[nextScannableTerm];
        }


        private void TraverseScannableStates(EarleyColumn[] table, EarleyColumn col)
        {
            if (col.Index + 1 >= table.Length) return;

            var nextScannableTerm = table[col.Index + 1].Token;
            var possibleSyntacticCategories = GetPossibleSyntacticCategoriesForToken(nextScannableTerm);

            foreach (var item in possibleSyntacticCategories)
            {
                var currentCategory = new DerivedCategory(item);

                if (col.StatesWithNextSyntacticCategory.ContainsKey(currentCategory))
                {
                    foreach (var state in col.StatesWithNextSyntacticCategory[currentCategory])
                        Scan(table[col.Index + 1], state, currentCategory, nextScannableTerm);
                }
            }
                
        }

        private int TraversePredictableStates(EarleyColumn col, int count)
        {
            while (col.CategoriesToPredict.Any())
            {
                var nextTerm = col.CategoriesToPredict.Dequeue();

                if (col.ActionableCompleteStates.Any())
                    throw new Exception(
                        "completed states queue should always be empty while processing predicted states.");
                count++;
                //TestForTooManyStatesInColumn(col, count);

                //if static rules have not been generated for this term yet
                //compute them from dynamaic rules dictionary
                if (!grammar.staticRulesGeneratedForCategory.Contains(nextTerm))
                {
                    grammar.staticRulesGeneratedForCategory.Add(nextTerm);
                    var baseSyntacticCategory = new SyntacticCategory(nextTerm);

                    if (grammar.dynamicRules.ContainsKey(baseSyntacticCategory))
                    {
                        var grammarRuleList = grammar.dynamicRules[baseSyntacticCategory];
                        foreach (var item in grammarRuleList)
                        {
                            var derivedRule = grammar.GenerateStaticRuleFromDyamicRule(item, nextTerm);
                            grammar.AddStaticRule(derivedRule);
                        }
                    }
                }

                if (!grammar.staticRules.ContainsKey(nextTerm)) continue;

                var ruleList = grammar.staticRules[nextTerm];
                Predict(col, ruleList);
            }

            return count;
        }

        private int TraverseCompletedStates(EarleyColumn col, int count)
        {
            while (col.ActionableCompleteStates.Any())
            {
                count++;
                //TestForTooManyStatesInColumn(col, count);

                var completedStatesQueueKey = col.ActionableCompleteStates.First().Key;
                var completedStatesQueue = col.ActionableCompleteStates.First().Value;

                var state = completedStatesQueue.Dequeue();

                if (!completedStatesQueue.Any())
                    col.ActionableCompleteStates.Remove(completedStatesQueueKey);

                Complete(col, state);
            }

            return count;
        }
    }
}