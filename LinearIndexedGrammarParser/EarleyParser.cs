using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NLog;

namespace LinearIndexedGrammarParser
{
    public class InfiniteParseException : Exception
    {
        public InfiniteParseException(string str) : base(str)
        {
        }
    }

    public class GrammarOverlyRecursiveException : Exception
    {
        public GrammarOverlyRecursiveException(string str) : base(str)
        {
        }
    }

    public class EarleyParser
    {
        private readonly ContextFreeGrammar _grammar;
        protected Vocabulary Voc;

        public EarleyParser(ContextFreeGrammar g, Vocabulary v)
        {
            Voc = v;
            _grammar = g;
        }

        private void Predict(EarleyColumn col, List<Rule> ruleList, DerivedCategory nextTerm)
        {
            var isPossibleNullable = _grammar.PossibleNullableCategories.Contains(nextTerm);

            foreach (var rule in ruleList)
            {
                //if the rule obligatorily expands to the nullable production,
                //do not predict it. you have already performed a spontaneous dot-shift
                //in EarleyColumn.AddState().
                if (isPossibleNullable && _grammar.IsObligatoryNullableRule(rule)) continue;

                var newState = new EarleyState(rule, 0, col, null);
                col.AddState(newState, _grammar);
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

            col.AddState(newState, _grammar);
        }

        private void Complete(EarleyColumn col, EarleyState state)
        {
            if (state.Rule.LeftHandSide.ToString() == ContextFreeGrammar.GammaRule)
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
                col.AddState(newState, _grammar);
            }
        }

        //inactive. use for debugging when you suspect program stuck.
        //at the moment, the promiscuous grammar could generate +100,000 earley states
        //so a very large amount of earley states is not necessarily a a bug.
        // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
        //private static void TestForTooManyStatesInColumn(int count)
        //{
        //    if (count > 100000) throw new InfiniteParseException("Grammar with infinite parse. abort this grammar..");
        //}

        public List<EarleyNode> ParseSentence(string text, CancellationTokenSource cts, int maxWords = 0)
        {
            var (table, finalColumns) = PrepareEarleyTable(text, maxWords);
            var nodes = new List<EarleyNode>();

            //assumption: GenerateAllStaticRulesFromDynamicRules has been called before parsing
            //and added the GammaRule
            var startRule = _grammar.StaticRules[new DerivedCategory(ContextFreeGrammar.GammaRule)][0];

            var startState = new EarleyState(startRule, 0, table[0], null);
            table[0].AddState(startState, _grammar);
            try
            {
                foreach (var col in table)
                {
                    var count = 0;

                    //1. complete
                    count = TraverseCompletedStates(col, count);

                    //2. predict after complete:
                    // ReSharper disable once RedundantAssignment
                    count = TraversePredictableStates(col, count);

                    //3. scan after predict.
                    TraverseScannableStates(table, col);
                }

                foreach (var index in finalColumns)
                {
                    var n = table[index].GammaStates.Select(x => x.Node.Children[0]).ToList();
                    nodes.AddRange(n);
                }
            }
            catch (InfiniteParseException)
            {
                throw;
            }

            catch (Exception e)
            {
                var s = e.ToString();
                LogManager.GetCurrentClassLogger().Warn(s);
            }

            //if (nodes.Count == 0)
            //    cts.Cancel();

            return nodes;
        }

        protected virtual (EarleyColumn[], int[]) PrepareEarleyTable(string text, int maxWord)
        {
            var arr = text.Split();
            //check below that the text appears in the vocabulary
            if (arr.Any(str => !Voc.ContainsWord(str)))
                throw new Exception("word in text does not appear in the vocabulary.");

            var table = new EarleyColumn[arr.Length + 1];

            for (var i = 1; i < table.Length; i++)
                table[i] = new EarleyColumn(i, arr[i - 1]);

            table[0] = new EarleyColumn(0, "");
            return (table, new[] {table.Length - 1});
        }

        protected virtual HashSet<string> GetPossibleSyntacticCategoriesForToken(string nextScannableTerm)
        {
            return Voc[nextScannableTerm];
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
                    foreach (var state in col.StatesWithNextSyntacticCategory[currentCategory])
                        Scan(table[col.Index + 1], state, currentCategory, nextScannableTerm);
            }
        }

        private int TraversePredictableStates(EarleyColumn col, int count)
        {
            while (col.CategoriesToPredict.Count > 0)
            {
                var nextTerm = col.CategoriesToPredict.Dequeue();

                if (col.ActionableCompleteStates.Count > 0) col.ActionableCompleteStates.Clear();

                //count++;
                //TestForTooManyStatesInColumn(count);

                if (!_grammar.StaticRules.ContainsKey(nextTerm)) continue;

                var ruleList = _grammar.StaticRules[nextTerm];
                Predict(col, ruleList, nextTerm);
            }

            return count;
        }

        private int TraverseCompletedStates(EarleyColumn col, int count)
        {
            while (col.ActionableCompleteStates.Count > 0)
            {
                //count++;
                //TestForTooManyStatesInColumn(count);

                var completedStatesQueueKey = col.ActionableCompleteStates.First().Key;
                var completedStatesQueue = col.ActionableCompleteStates.First().Value;

                var state = completedStatesQueue.Dequeue();

                if (completedStatesQueue.Count == 0)
                    col.ActionableCompleteStates.Remove(completedStatesQueueKey);

                Complete(col, state);
            }

            return count;
        }
    }
}