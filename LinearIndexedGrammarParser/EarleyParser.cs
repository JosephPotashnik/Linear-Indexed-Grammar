using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using NLog;

namespace LinearIndexedGrammarParser
{
    public class EarleyParser
    {
        public ContextFreeGrammar _oldGrammar;
        public ContextFreeGrammar _grammar;
        protected Vocabulary Voc;
        private EarleyColumn[] _table;
        private string[] _text;
        int[] _finalColumns;
        private readonly bool _checkForCyclicUnitProductions;

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
                var newState = new EarleyState(rule, 0, col);
                col.AddState(newState, _grammar);
            }
        }


        protected void Scan(EarleyColumn startColumn, EarleyColumn nextCol, EarleyState state, DerivedCategory term, string token)
        {
            if (!startColumn.Reductors.TryGetValue(term, out var stateList))
            {
                var scannedStateRule = new Rule(term.ToString(), new[] { token });
                var scannedState = new EarleyState(scannedStateRule, 1, startColumn);
                scannedState.EndColumn = nextCol;
                stateList = new List<EarleyState>() { scannedState };
                startColumn.Reductors.Add(term, stateList);

            }


            //var reductor = stateList[0];
            var newState = new EarleyState(state.Rule, state.DotIndex + 1, state.StartColumn);
            state.Parents.Add(newState);
            newState.Predecessor = state;
            nextCol.AddState(newState, _grammar);
        }

        private void Complete(EarleyColumn col, EarleyState reductorState)
        {
            if (reductorState.Rule.LeftHandSide.ToString() == ContextFreeGrammar.GammaRule)
            {
                var sb = new StringBuilder();
                reductorState.Reductor.CreateBracketedRepresentation(sb, _grammar);
                reductorState.BracketedTreeRepresentation = sb.ToString();
                col.GammaStates.Add(reductorState);

                return;
            }

            var startColumn = reductorState.StartColumn;
            var completedSyntacticCategory = reductorState.Rule.LeftHandSide;
            var predecessorStates = startColumn.Predecessors[completedSyntacticCategory];

            if (!startColumn.Reductors.TryGetValue(reductorState.Rule.LeftHandSide, out var reductorList))
            {
                reductorList = new List<EarleyState>();
                startColumn.Reductors.Add(reductorState.Rule.LeftHandSide, reductorList);

            }

            reductorList.Add(reductorState);

            foreach (var predecessor in predecessorStates)
            {
                var newState = new EarleyState(predecessor.Rule, predecessor.DotIndex + 1, predecessor.StartColumn);
                predecessor.Parents.Add(newState);
                reductorState.Parents.Add(newState);
                newState.Predecessor = predecessor;
                newState.Reductor = reductorState;

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
                if (startColumn.Reductors.TryGetValue(newState.Rule.LeftHandSide, out var reductors))
                {
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
                reductorState.Parents.Remove(newState);
                predecessor.Parents.Remove(newState);
                return true;
            }

            return false;
        }

        //read the current gamma states from what's stored in the Earley Table.
        public List<EarleyState> GetGammaStates()
        {
            if (_finalColumns.Length == 1)
               return _table[_finalColumns[0]].GammaStates;

            var gammaStates = new List<EarleyState>();
            foreach (var index in _finalColumns)
                gammaStates.AddRange(_table[index].GammaStates);

            return gammaStates;
        }

        public List<EarleyState> ReParseSentenceWithRuleAddition(ContextFreeGrammar g, Rule r)
        {
            var gammaStates = new List<EarleyState>();
            _oldGrammar = _grammar;
            _grammar = g;
            var cat = r.LeftHandSide;

            foreach (var col in _table)
            {
                //seed the new rule in the column
                //think about categories if this would be context sensitive grammar.
                if (col.Predecessors.ContainsKey(cat))
                {
                    //if not already marked to be predicted, predict.
                    if (!col.ActionableNonTerminalsToPredict.Contains(cat))
                    {
                        var newState = new EarleyState(r, 0, col);
                        col.AddState(newState, _grammar);
                    }
                }

                bool exhaustedCompletion = false;
                while (!exhaustedCompletion)
                {
                    //1. complete
                    TraverseCompletedStates(col);

                    //2. predict after complete:
                    TraversePredictableStates(col);
                    exhaustedCompletion = col.ActionableCompleteStates.Count == 0;
                }

                //3. scan after predict.
                TraverseScannableStates(_table, col);
            }

            return GetGammaStates();
        }

        public List<EarleyState>  ReParseSentenceWithRuleDeletion(ContextFreeGrammar g, Rule r, Dictionary<DerivedCategory, HashSet<Rule>> predictionSet)
        {
            var seedNumberToUnpredict = r.NumberOfGeneratingRule;

            foreach (var col in _table)
            {
                //TODO: version 1, re-test with un-commenting parser.ToString()
                //when you have switched to version 2, supporting LIG addition/deletion.
                if (col.Predicted.ContainsKey(seedNumberToUnpredict))
                {
                    var firstTerm = r.RightHandSide[0];
                    if (!col.NonTerminalsToUnpredict.Contains(firstTerm))
                        col.Unpredict(r, _grammar);
                }

                //TODO: version 2, for LIG addition/deletion.
                //if (col.Predicted.ContainsKey(seedNumberToUnpredict))
                //    col.Unpredict(r, _grammar);

                bool exhausted = false;
                while (!exhausted)
                {
                    //1. remove completed states
                    TraverseCompletedStatesToDelete(col);

                    //2. remove uncompleted states
                    while (col.ActionableNonCompleteStates.Count > 0)
                    {
                        var state = col.ActionableNonCompleteStates.Dequeue();
                        state.EndColumn.DeleteState(state, _grammar);
                    }

                    //3. unpredict
                    TraversePredictedStatesToDelete(col, predictionSet);

                    //unprediction can lead to completed /uncompleted parents in the same column
                    //if there is a nullable production, same as in the regular
                    exhausted = (col.ActionableDeletedStates.Count == 0 && col.ActionableNonCompleteStates.Count == 0);

                }
            }

            _oldGrammar = _grammar;
            _grammar = g;
            return GetGammaStates(); 
        }

        private void TraversePredictedStatesToDelete(EarleyColumn col, Dictionary<DerivedCategory, HashSet<Rule>> predictionSet)
        {
            int counter = 0;
            while (col.ActionableNonTerminalsToPredict.Count > 0)
            {
                counter++;
                if (counter > 100)
                {
                    throw new Exception("loop of unpredictions!");
                }
                var nextTerm = col.ActionableNonTerminalsToPredict.Dequeue();

                //you might need to re-check the term following deletions of other predicted states!
                col.NonTerminalsToUnpredict.Remove(nextTerm); 


                bool toUnpredict = col.CheckForUnprediction(nextTerm, predictionSet);
              
                if (toUnpredict)
                {
                    if (_grammar.StaticRules.TryGetValue(nextTerm, out var ruleList))
                    {
                        //delete all predictions
                        foreach (var rule in ruleList)
                            col.Unpredict(rule, _grammar);
                    }
                }
            }
        }

        private void TraverseCompletedStatesToDelete(EarleyColumn col)
        {
            while (col.ActionableDeletedStates.Count > 0)
            {
                var kvp = col.ActionableDeletedStates.First();
                var deletedStatesStackKey = kvp.Key;
                var deletedStatesStack = kvp.Value;

                var state = deletedStatesStack.Pop();

                if (deletedStatesStack.Count == 0)
                    col.ActionableDeletedStates.Remove(deletedStatesStackKey);

                state.EndColumn.DeleteState(state, _grammar);
            }
        }


        public List<EarleyState> ParseSentence(string[] text, CancellationTokenSource cts, int maxWords = 0)
        {
            _text = text;
            (_table, _finalColumns) = PrepareEarleyTable(text, maxWords);

            //assumption: GenerateAllStaticRulesFromDynamicRules has been called before parsing
            //and added the GammaRule
            var startRule = _grammar.StaticRules[new DerivedCategory(ContextFreeGrammar.GammaRule)][0];

            var startState = new EarleyState(startRule, 0, _table[0]);
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
            }
            catch (Exception e)
            {
                var s = e.ToString();
                LogManager.GetCurrentClassLogger().Warn(s);
            }

            return GetGammaStates();
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
            if (col.Index + 1 >= table.Length)
            {
                col.ActionableNonCompleteStates.Clear();
                return false;
            }

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
            bool anyPredicted = col.ActionableNonTerminalsToPredict.Count > 0;
            while (col.ActionableNonTerminalsToPredict.Count > 0)
            {
                var nextTerm = col.ActionableNonTerminalsToPredict.Dequeue();
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

        public class CategoryCompare : IComparer<DerivedCategory>
        {
            public int Compare(DerivedCategory x, DerivedCategory y)
            {
                return string.Compare(x.ToString(), y.ToString());
            }
        }
        public override string ToString()
        {
            var sb = new StringBuilder();
            var cc = new CategoryCompare();

            foreach (var col in _table)
            {
                sb.AppendLine($"col {col.Index}");

                sb.AppendLine("Predecessors:");
                var keys = col.Predecessors.Keys.ToArray();
                Array.Sort(keys, cc);

                foreach (var key in keys)
                {
                    sb.AppendLine($"key {key}");
                    List<string> values = new List<string>();
                    foreach (var state in col.Predecessors[key])
                        values.Add(state.ToString());
                    values.Sort();

                    foreach (var value in values)
                        sb.AppendLine(value);

                }
                sb.AppendLine("Predicted:");
                var keys1 = col.Predicted.Keys.ToArray();
                Array.Sort(keys1);

                foreach (var key in keys1)
                {
           
                    sb.AppendLine($"key {key}");
                    List<string> values = new List<string>();

                    foreach (var state in col.Predicted[key])
                        values.Add(state.ToString());
                    values.Sort();

                    foreach (var value in values)
                        sb.AppendLine(value);
                }
                sb.AppendLine("Reductors:");

                var keys2 = col.Reductors.Keys.ToArray();
                Array.Sort(keys2, cc);
                foreach (var key in keys2)
                {
                    //do not write POS keys into string
                    //reason: for comparison purposes (debugging) between 
                    //from-scratch earley parser and differential earley parser.
                    //the differential parser contains also reductor items for
                    //POS although the column might not be parsed at all.
                    //the from-scratch parser will not contain those reductor items
                    //but this is OK, we don't care about these items.
                    if (!_grammar.StaticRules.ContainsKey(key)) continue;

                    sb.AppendLine($"key {key}");
                    List<string> values = new List<string>();

                    foreach (var state in col.Reductors[key])
                        values.Add(state.ToString());
                    values.Sort();

                    foreach (var value in values)
                        sb.AppendLine(value);
                }
            }

            return sb.ToString();
        }

        public void AcceptChanges()
        {
            foreach (var col in _table)
                col.AcceptChanges();

            _oldGrammar = null;

        }

        public void RejectChanges()
        {
            foreach (var col in _table)
                col.RejectChanges();

            foreach (var col in _table)
                col.AcceptChanges();

            _grammar = _oldGrammar;
            _oldGrammar = null;
        }
    }
}