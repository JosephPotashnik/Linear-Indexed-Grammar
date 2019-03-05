using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NLog;

namespace LinearIndexedGrammarParser
{
    public class EarleyParser
    {
        private ContextFreeGrammar _grammar;
        protected Vocabulary Voc;
        private EarleyColumn[] _table;
        private List<EarleyNode> _nodes;
        private string[] _text;
        int[] _finalColumns;
        private bool _checkForCyclicUnitProductions;

        public EarleyParser(ContextFreeGrammar g, Vocabulary v, bool checkUnitProductionCycles = true)
        {
            Voc = v;
            _grammar = g;
            _checkForCyclicUnitProductions = checkUnitProductionCycles;
        }

        private void Predict(EarleyColumn col, List<Rule> ruleList, DerivedCategory nextTerm)
        {
            foreach (var rule in ruleList)
            {
                var newState = new EarleyState(rule, 0, col, null);
                col.AddState(newState, _grammar);
            }
        }

        protected void Scan(EarleyColumn startColumn, EarleyColumn nextCol, EarleyState state, DerivedCategory term, string token)
        {
            if (!startColumn.Reductors.ContainsKey(term))
            {
                var v = new EarleyNode(term.ToString(), startColumn.Index, nextCol.Index)
                {
                    AssociatedTerminal = token
                };

                //prepared a scanned Earley rule (the scanned rule does not appear in this representations,
                //the terminals are stored separately in a vocabulary under their heading non-terminal, a.k.a their part of speech).
                var scannedStateRule = new Rule(term.ToString(), new[] {token});
                var scannedState = new EarleyState(scannedStateRule, 1, startColumn, v);
                scannedState.EndColumn = nextCol;
                startColumn.Reductors[term] = new List<EarleyState>() {scannedState};
            }

            var reductor = startColumn.Reductors[term][0];
            var y = EarleyState.MakeNode(state, reductor.EndColumn.Index, reductor.Node);
            var newState = new EarleyState(state.Rule, state.DotIndex + 1, state.StartColumn, y);
            state.Parents.Add(newState);
            nextCol.AddState(newState, _grammar);
        }

        private void Complete(EarleyColumn col, EarleyState reductorState)
        {
            if (reductorState.Rule.LeftHandSide.ToString() == ContextFreeGrammar.GammaRule)
            {
                col.GammaStates.Add(reductorState);
                return;
            }

            var startColumn = reductorState.StartColumn;
            var completedSyntacticCategory = reductorState.Rule.LeftHandSide;
            var predecessorStates = startColumn.Predecessors[completedSyntacticCategory];

            if (!startColumn.Reductors.ContainsKey(reductorState.Rule.LeftHandSide))
                startColumn.Reductors[reductorState.Rule.LeftHandSide] = new List<EarleyState>();

            startColumn.Reductors[reductorState.Rule.LeftHandSide].Add(reductorState);

            foreach (var predecessor in predecessorStates)
            {
                var y = EarleyState.MakeNode(predecessor, reductorState.EndColumn.Index, reductorState.Node);
                var newState = new EarleyState(predecessor.Rule, predecessor.DotIndex + 1, predecessor.StartColumn, y);
                predecessor.Parents.Add(newState);
                reductorState.Parents.Add(newState);


                if (_checkForCyclicUnitProductions)
                {
                    //if the state completes a unit production cycle, do not add it.
                    if (IsNewStatePartOfUnitProductionCycle(reductorState, newState, startColumn, predecessor))
                        continue;
                }

                col.AddState(newState, _grammar);
            }
        }

        private static bool IsNewStatePartOfUnitProductionCycle(EarleyState reductorState, EarleyState newState,
            EarleyColumn startColumn, EarleyState predecessor)
        {
            //check if the new state completed and is a parent of past reductor for its LHS,
            //if so - you arrived at a unit production cycle.
            bool foundCycle = false;

            if (newState.IsCompleted)
            {
                if (startColumn.Reductors.ContainsKey(newState.Rule.LeftHandSide))
                {
                    var reductors = startColumn.Reductors[newState.Rule.LeftHandSide];

                    foreach (var reductor in reductors)
                    {
                        if (newState.Rule.Equals(reductor.Rule) && newState.StartColumn == reductor.StartColumn)
                        {
                            var parents = reductor.GetTransitiveClosureOfParents();
                            foreach (var parent in parents)
                            {
                                //found a unit production cycle.
                                if (newState == parent)
                                    foundCycle = true;
                            }
                        }
                    }
                }
            }

            if (foundCycle)
            {
                //undo the parent ties,  and do not insert the new state
                reductorState.Parents.RemoveAt(reductorState.Parents.Count - 1);
                predecessor.Parents.RemoveAt(predecessor.Parents.Count - 1);
                return true;
            }

            return false;
        }

        public List<EarleyNode> ReParseSentenceWithRuleAddition(List<Rule> grammarRules, Rule r)
        {
            _nodes = new List<EarleyNode>();
            _grammar = new ContextFreeGrammar(grammarRules);
            var cat = r.LeftHandSide;

            foreach (var col in _table)
            {
                //seed the new rule in the column
                if (col.Predecessors.ContainsKey(cat))
                {
                    var newState = new EarleyState(r, 0, col, null);
                    col.AddState(newState, _grammar);
                }

                //1. complete
                bool exhaustedCompletion = false;
                while (!exhaustedCompletion)
                {
                    TraverseCompletedStates(col);

                    //2. predict after complete:
                    TraversePredictableStates(col);
                    exhaustedCompletion = col.ActionableCompleteStates.Count == 0;
                }

                //3. scan after predict.
                TraverseScannableStates(_table, col);
            }

            foreach (var index in _finalColumns)
            {
                var n = _table[index].GammaStates.Select(x => x.Node.Children[0]).ToList();
                _nodes.AddRange(n);
            }

            return _nodes;
        }
        public List<EarleyNode> ParseSentence(string[] text, CancellationTokenSource cts, int maxWords = 0)
        {
            _text = text;
            _nodes = new List<EarleyNode>();

            (_table, _finalColumns) = PrepareEarleyTable(text, maxWords);

            //assumption: GenerateAllStaticRulesFromDynamicRules has been called before parsing
            //and added the GammaRule
            var startRule = _grammar.StaticRules[new DerivedCategory(ContextFreeGrammar.GammaRule)][0];

            var startState = new EarleyState(startRule, 0, _table[0], null);
            _table[0].AddState(startState, _grammar);
            try
            {
                foreach (var col in _table)
                {
                    bool exhaustedCompletion = false;
                    var anyCompleted = false;
                    var anyPredicted = false;
                    while (!exhaustedCompletion)
                    {
                        //1. complete
                        anyCompleted = TraverseCompletedStates(col);

                        //2. predict after complete:
                        anyPredicted = TraversePredictableStates(col);

                        //prediction of epsilon transitions can lead to completed states.
                        //hence we might need to complete those states.
                        exhaustedCompletion = col.ActionableCompleteStates.Count == 0;
                    }

                    //3. scan after predict.
                    var anyScanned = TraverseScannableStates(_table, col);

                    if (!anyCompleted && !anyPredicted && !anyScanned) break;
                }

                foreach (var index in _finalColumns)
                {
                    var n = _table[index].GammaStates.Select(x => x.Node.Children[0]).ToList();
                    _nodes.AddRange(n);
                }
            }
            catch (Exception e)
            {
                var s = e.ToString();
                LogManager.GetCurrentClassLogger().Warn(s);
            }

            return _nodes;
        }

        protected virtual (EarleyColumn[], int[]) PrepareEarleyTable(string[] text, int maxWord)
        {
            var table = new EarleyColumn[text.Length + 1];

            for (var i = 1; i < table.Length; i++)
                table[i] = new EarleyColumn(i, text[i - 1]);

            table[0] = new EarleyColumn(0, "");
            return (table, new[] {table.Length - 1});
        }

        protected virtual HashSet<string> GetPossibleSyntacticCategoriesForToken(string nextScannableTerm)
        {
            return Voc[nextScannableTerm];
        }


        private bool TraverseScannableStates(EarleyColumn[] table, EarleyColumn col)
        {
            bool anyScanned = col.ActionableNonCompleteStates.Count > 0;
            if (col.Index + 1 >= table.Length) return false;

            while (col.ActionableNonCompleteStates.Count > 0)
            {
                var stateToScan = col.ActionableNonCompleteStates.Dequeue();

                var nextScannableTerm = table[col.Index + 1].Token;
                var possibleSyntacticCategories = GetPossibleSyntacticCategoriesForToken(nextScannableTerm);

                foreach (var item in possibleSyntacticCategories)
                {
                    var currentCategory = new DerivedCategory(item);
                    if (stateToScan.NextTerm.Equals(currentCategory))
                            Scan(table[col.Index], table[col.Index+1],stateToScan, currentCategory, nextScannableTerm);
                }
            }

            return anyScanned;
        }

        private bool TraversePredictableStates(EarleyColumn col)
        {
            bool anyPredicted = col.CategoriesToPredict.Count > 0;
            while (col.CategoriesToPredict.Count > 0)
            {
                var nextTerm = col.CategoriesToPredict.Dequeue();
                var ruleList = _grammar.StaticRules[nextTerm];
                Predict(col, ruleList, nextTerm);
            }

            return anyPredicted;
        }

        private bool TraverseCompletedStates(EarleyColumn col)
        {
            bool anyCompleted = col.ActionableCompleteStates.Count > 0;
            while (col.ActionableCompleteStates.Count > 0)
            {
                var kvp = col.ActionableCompleteStates.First();
                var completedStatesQueueKey = kvp.Key;
                var completedStatesQueue = kvp.Value;

                var state = completedStatesQueue.Dequeue();

                if (completedStatesQueue.Count == 0)
                    col.ActionableCompleteStates.Remove(completedStatesQueueKey);

                Complete(col, state);
            }

            return anyCompleted;
        }
    }
}