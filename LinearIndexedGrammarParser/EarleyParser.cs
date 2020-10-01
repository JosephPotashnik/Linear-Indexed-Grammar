using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LinearIndexedGrammarParser
{
    public class EarleyParser
    {
        private readonly bool _checkForCyclicUnitProductions;
        private int[] _finalColumns;
        public ContextFreeGrammar _grammar;
        public ContextFreeGrammar _oldGrammar;
        private EarleyColumn[] _table;
        private string[] _text;
        private readonly HashSet<EarleyState> statesRemovedInLastReparse = new HashSet<EarleyState>();
        protected Vocabulary Voc;
        public HashSet<string> BracketedRepresentations;

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


        protected void Scan(EarleyColumn startColumn, EarleyColumn nextCol, EarleyState state, DerivedCategory term,
            string token)
        {
            if (!startColumn.Reductors.TryGetValue(term, out var stateList))
            {
                var scannedStateRule = new Rule(term.ToString(), new[] { token });
                var scannedState = new EarleyState(scannedStateRule, 1, startColumn);
                scannedState.EndColumn = nextCol;
                stateList = new HashSet<EarleyState> { scannedState };
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
                reductorState.BracketedRepresentation = sb.ToString();
                col.GammaStates.Add(reductorState);
                col.BracketedRepresentations.Add(reductorState.BracketedRepresentation);
                return;
            }

            var startColumn = reductorState.StartColumn;
            var completedSyntacticCategory = reductorState.Rule.LeftHandSide;
            var predecessorStates = startColumn.Predecessors[completedSyntacticCategory];

            if (!startColumn.Reductors.TryGetValue(reductorState.Rule.LeftHandSide, out var reductorList))
            {
                reductorList = new HashSet<EarleyState>();
                startColumn.Reductors.Add(reductorState.Rule.LeftHandSide, reductorList);
            }

            reductorList.Add(reductorState);

            foreach (var predecessor in predecessorStates)
            {
                if (predecessor.Removed)
                    continue;

                var newState = new EarleyState(predecessor.Rule, predecessor.DotIndex + 1, predecessor.StartColumn);
                predecessor.Parents.Add(newState);
                reductorState.Parents.Add(newState);
                newState.Predecessor = predecessor;
                newState.Reductor = reductorState;

                if (_checkForCyclicUnitProductions)
                    if (IsNewStatePartOfUnitProductionCycle(reductorState, newState, startColumn, predecessor))
                        continue;

                col.AddState(newState, _grammar);
            }
        }

        private static bool IsNewStatePartOfUnitProductionCycle(EarleyState reductorState, EarleyState newState,
            EarleyColumn startColumn, EarleyState predecessor)
        {
            //check if the new state completed and is a parent of past reductor for its LHS,
            //if so - you arrived at a unit production cycle.
            var foundCycle = false;

            if (newState.IsCompleted)
                if (startColumn.Reductors.TryGetValue(newState.Rule.LeftHandSide, out var reductors))
                    foreach (var reductor in reductors)
                        if (newState.Rule.Equals(reductor.Rule) && newState.StartColumn == reductor.StartColumn)
                        {
                            var parents = reductor.GetTransitiveClosureOfParents();
                            foreach (var parent in parents)
                                //found a unit production cycle.
                                if (newState == parent)
                                    foundCycle = true;
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

        public void ReParseSentenceWithRuleAddition(ContextFreeGrammar g, List<Rule> rs)
        {
            _grammar = g;

            foreach (var col in _table)
            {
                for (var i = 0; i < rs.Count; i++)
                {
                    //seed the new rule in the column
                    //think about categories if this would be context sensitive grammar.
                    if (col.Predecessors.TryGetValue(rs[i].LeftHandSide, out var predecessorsWithKey))
                    {
                        foreach (var predecessor in predecessorsWithKey)
                        {
                            if (!predecessor.Removed)
                            {
                                if (!col.ActionableNonTerminalsToPredict.Contains(rs[i].LeftHandSide))
                                {
                                    var newState = new EarleyState(rs[i], 0, col);
                                    col.AddState(newState, _grammar);
                                }

                                break;
                            }
                        }
                    }
                }


                var exhaustedCompletion = false;
                while (!exhaustedCompletion)
                {
                    //1. complete
                    TraverseCompletedStates(col);

                    //2. predict after complete:
                    TraversePredictableStates(col);
                    exhaustedCompletion = col.ActionableCompleteStates.Count == 0;
                }

                //3. scan after predict -- not necessary if the grammar is non lexicalized,
                //i.e if terminals are not mentioned in the grammar rules.
                //we then can prepare all scanned states in advance (PrepareScannedStates)
                //if you uncomment the following line make sure to uncomment the 
                //ActionableNonCompleteStates enqueuing in Column.AddState()
                //TraverseScannableStates(_table, col);

            }
        }

        public void ReParseSentenceWithRuleDeletion(ContextFreeGrammar g, List<Rule> rs,
            Dictionary<DerivedCategory, LeftCornerInfo> predictionSet)
        {
            foreach (var col in _table)
            {
                foreach (var rule in rs)
                    col.Unpredict(rule, _grammar, statesRemovedInLastReparse, predictionSet);


                var exhausted = false;
                while (!exhausted)
                {
                    TraverseStatesToDelete(col, statesRemovedInLastReparse);

                    TraversePredictedStatesToDelete(col, predictionSet, statesRemovedInLastReparse);

                    //unprediction can lead to completed /uncompleted parents in the same column
                    //if there is a nullable production, same as in the regular
                    exhausted = col.ActionableDeletedStates.Count == 0;
                }
            }

            _grammar = g;
        }

        private void TraversePredictedStatesToDelete(EarleyColumn col,
            Dictionary<DerivedCategory, LeftCornerInfo> predictionSet, HashSet<EarleyState> statesRemovedInLastReparse)
        {

            //var visitedDeletedNonterminals = new HashSet<DerivedCategory>();
            while (col.NonTerminalsCandidatesToUnpredict.Count > 0)
            {
                //1. Choose the topmost (root) nonterminal to consider for unprediction
                //based on left corner relations graph.
                //if there is no topmost nonterminal, i.e. a loop, return all nonterminals in the loop.
                var (topmostNonTerminal, nonTerminalsToConsider) = ComputeRootNonTerminalsToUnpredict(col, predictionSet);

                //check the nonterminals to consider if any of them has undeleted predecessor
                bool foundUndeletedPredecessor = FindUndeletedPredecessor(col, predictionSet,
                    statesRemovedInLastReparse, nonTerminalsToConsider);

                var transitiveLeftCornerNonTerminals = predictionSet[topmostNonTerminal].NonTerminals;

                if (foundUndeletedPredecessor)
                {
                    col.NonTerminalsCandidatesToUnpredict.Remove(topmostNonTerminal);
                    col.visitedCategoriesInUnprediction.Add(topmostNonTerminal);

                    //remove from candidate and all its transitive left corner
                    //from non terminals to unprediction candidates
                    foreach (var nt in transitiveLeftCornerNonTerminals)
                    {
                        col.NonTerminalsCandidatesToUnpredict.Remove(nt);
                        col.visitedCategoriesInUnprediction.Add(nt);
                    }
                }
                else
                {
                    foreach (var nt in nonTerminalsToConsider)
                    {
                        //visitedDeletedNonterminals.Add(nt);
                        col.NonTerminalsCandidatesToUnpredict.Remove(nt);
                        col.visitedCategoriesInUnprediction.Add(nt);
                    }

                    //insert all transitive left corner nonterminals to check if they need unprediction too.
                    //avoid examining nonterminals that we already verified whether they should be predicted or not.
                    foreach (var nt in transitiveLeftCornerNonTerminals)
                    {
                        if (!col.visitedCategoriesInUnprediction.Contains(nt))
                        {
                            col.NonTerminalsCandidatesToUnpredict.Add(nt);
                            col.visitedCategoriesInUnprediction.Add(nt);
                        }
                    }

                    foreach (var nt in nonTerminalsToConsider)
                    {
                        //unpredict the relevant nonterminal(s):
                        if (_grammar.StaticRules.TryGetValue(nt, out var ruleList))
                            foreach (var rule in ruleList)
                                col.Unpredict(rule, _grammar, statesRemovedInLastReparse, predictionSet);
                    }

                }
            }

            //clear visited categories since they may be revisited in next loop 
            //from prediction to completion.
            col.visitedCategoriesInUnprediction.Clear();
        }

        private static bool FindUndeletedPredecessor(EarleyColumn col, Dictionary<DerivedCategory, LeftCornerInfo> predictionSet,
            HashSet<EarleyState> statesRemovedInLastReparse, List<DerivedCategory> nonTerminalsToConsider)
        {
            bool foundNonDeletedPredecessor = false;
            foreach (var nonTerminalToConsider in nonTerminalsToConsider)
            {
                if (col.Predecessors.TryGetValue(nonTerminalToConsider, out var predecessors))
                {
                    foreach (var predecessor in predecessors)
                    {
                        if (!predecessor.Removed)
                        {
                            //when have we found a predecessor state which will not be deleted by removing all rules with nonTerminalToConsider as LHS?
                            //either when the predecessor is not predicted itself,
                            //or if it is predicted and also not in the prediction set of rules of the transitive left corner of nonTerminalToConsider.
                            if (predecessor.DotIndex != 0 ||

                                !predictionSet[nonTerminalToConsider].NonTerminals.Contains(predecessor.Rule.LeftHandSide))
                            {
                                foundNonDeletedPredecessor = true;
                                break;
                            }
                        }
                    }

                    if (foundNonDeletedPredecessor)
                        break;
                }
                else
                {
                    throw new Exception("FindUndeletedPredecessor: show me a nonterminal without predecessors.");
                }
            }

            return foundNonDeletedPredecessor;
        }

        private static (DerivedCategory topmost, List<DerivedCategory> nonTerminalsToConsider) ComputeRootNonTerminalsToUnpredict(EarleyColumn col, Dictionary<DerivedCategory, LeftCornerInfo> predictionSet)
        {
            var nonTerminalsToConsider = new List<DerivedCategory>();

            var topmostCandidate = col.NonTerminalsCandidatesToUnpredict.First();
            foreach (var nonterminal in col.NonTerminalsCandidatesToUnpredict)
            {
                if (predictionSet[nonterminal].NonTerminals.Contains(topmostCandidate))
                    topmostCandidate = nonterminal;
            }
            nonTerminalsToConsider.Add(topmostCandidate);

            foreach (var nonterminal in predictionSet[topmostCandidate].NonTerminals)
            {
                //for every non terminal in the left corner that contains the topmost candidate,
                //then it is in a closed loop with the topmost candidate and must be considered.
                if (predictionSet[nonterminal].NonTerminals.Contains(topmostCandidate) && !nonterminal.Equals(topmostCandidate))
                    nonTerminalsToConsider.Add(nonterminal);
            }


            return (topmostCandidate, nonTerminalsToConsider);
        }

        private void TraverseStatesToDelete(EarleyColumn col, HashSet<EarleyState> statesRemovedInLastReparse)
        {
            while (col.ActionableDeletedStates.Count > 0)
            {
                var state = col.ActionableDeletedStates.Pop();
                state.EndColumn.MarkStateDeleted(state, _grammar, statesRemovedInLastReparse);
            }
        }

        public List<EarleyState> GenerateSentence(string[] text, int maxWords = 0)
        {
            _text = text;
            (_table, _finalColumns) = PrepareEarleyTable(text, maxWords);
            PrepareScannedStates();

            //assumption: GenerateAllStaticRulesFromDynamicRules has been called before parsing
            //and added the GammaRule
            var startRule = _grammar.StaticRules[new DerivedCategory(ContextFreeGrammar.GammaRule)][0];

            var startState = new EarleyState(startRule, 0, _table[0]);
            _table[0].AddState(startState, _grammar);
            try
            {
                foreach (var col in _table)
                {
                    var exhaustedCompletion = false;
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

                    //3. scan after predict -- not necessary if the grammar is non lexicalized,
                    //i.e if terminals are not mentioned in the grammar rules.
                    //we then can prepare all scanned states in advance (PrepareScannedStates)
                    //if you uncomment the following line make sure to uncomment the 
                    //ActionableNonCompleteStates enqueuing in Column.AddState()
                    //var anyScanned = TraverseScannableStates(_table, col);

                    //if (!anyCompleted && !anyPredicted /*&& !anyScanned*/) break;
                }
            }
            catch (Exception e)
            {
                var s = e.ToString();
                LogManager.GetCurrentClassLogger().Info(s);
            }

            return GetGammaStates();
        }

        public void ParseSentence(string[] text, int maxWords = 0)
        {
            _text = text;
            (_table, _finalColumns) = PrepareEarleyTable(text, maxWords);
            BracketedRepresentations = _table[_table.Length - 1].BracketedRepresentations;
            PrepareScannedStates();

            //assumption: GenerateAllStaticRulesFromDynamicRules has been called before parsing
            //and added the GammaRule
            var startRule = _grammar.StaticRules[new DerivedCategory(ContextFreeGrammar.GammaRule)][0];

            var startState = new EarleyState(startRule, 0, _table[0]);
            _table[0].AddState(startState, _grammar);
            try
            {
                foreach (var col in _table)
                {
                    var exhaustedCompletion = false;
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

                    //3. scan after predict -- not necessary if the grammar is non lexicalized,
                    //i.e if terminals are not mentioned in the grammar rules.
                    //we then can prepare all scanned states in advance (PrepareScannedStates)
                    //if you uncomment the following line make sure to uncomment the 
                    //ActionableNonCompleteStates enqueuing in Column.AddState()
                    //var anyScanned = TraverseScannableStates(_table, col);

                    //if (!anyCompleted && !anyPredicted /*&& !anyScanned*/) break;
                }
            }
            catch (Exception e)
            {
                var s = e.ToString();
                LogManager.GetCurrentClassLogger().Info(s);
            }

        }

        protected virtual (EarleyColumn[], int[]) PrepareEarleyTable(string[] text, int maxWord)
        {
            var table = new EarleyColumn[text.Length + 1];

            for (var i = 1; i < table.Length; i++)
                table[i] = new EarleyColumn(i, text[i - 1]);

            table[0] = new EarleyColumn(0, "");
            return (table, new[] { table.Length - 1 });
        }

        protected virtual HashSet<string> GetPossibleSyntacticCategoriesForToken(string nextScannableTerm)
        {
            return Voc[nextScannableTerm];
        }

        private void PrepareScannedStates()
        {
            for (int i = 0; i < _table.Length - 1; i++)
            {
                var nextScannableTerm = _table[i + 1].Token;
                var possibleNonTerminals = GetPossibleSyntacticCategoriesForToken(nextScannableTerm);

                foreach (var nonTerminal in possibleNonTerminals)
                {
                    var currentCategory = new DerivedCategory(nonTerminal);

                    var scannedStateRule = new Rule(nonTerminal, new[] { _table[i + 1].Token });
                    var scannedState = new EarleyState(scannedStateRule, 1, _table[i]);
                    scannedState.EndColumn = _table[i + 1];
                    _table[i].Reductors[currentCategory] = new HashSet<EarleyState> { scannedState };
                }
            }
        }

        private bool TraverseScannableStates(EarleyColumn[] table, EarleyColumn col)
        {
            var anyScanned = col.ActionableNonCompleteStates.Count > 0;
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
                        Scan(table[col.Index], table[col.Index + 1], stateToScan, currentCategory, nextScannableTerm);
                }
            }

            return anyScanned;
        }

        private bool TraversePredictableStates(EarleyColumn col)
        {
            var anyPredicted = col.ActionableNonTerminalsToPredict.Count > 0;
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
            var anyCompleted = col.ActionableCompleteStates.Count > 0;
            while (col.ActionableCompleteStates.Count > 0)
            {
                var state = col.ActionableCompleteStates.Dequeue();
                Complete(col, state);
            }

            return anyCompleted;
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
                    var values = new List<string>();
                    foreach (var state in col.Predecessors[key])
                        values.Add(state.ToString());
                    values.Sort();

                    foreach (var value in values)
                        sb.AppendLine(value);
                }

                sb.AppendLine("Predicted:");
                var keys1 = col.Predicted.Keys.Select(x => (x.ToString(), x));
                var ordered = keys1.OrderBy(x => x.Item1);
                foreach (var stringAndRule in ordered)
                    sb.AppendLine($"{stringAndRule.Item1}");
                //List<string> values = new List<string>();
                //values.Add(col.Predicted[key].ToString());
                //values.Sort();

                //foreach (var value in values)
                //    sb.AppendLine(value);
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
                    var values = new List<string>();

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

            if (statesRemovedInLastReparse != null)
            {
                foreach (var state in statesRemovedInLastReparse)
                    state.EndColumn.DeleteState(state, statesRemovedInLastReparse);
            }

            foreach (var col in _table)
                col.OldGammaStates?.Clear();

            statesRemovedInLastReparse?.Clear();

            _oldGrammar = null;
        }

        public void RejectChanges()
        {
            foreach (var state in statesRemovedInLastReparse)
                state.Removed = false;

            foreach (var col in _table)
                col.RejectChanges();

            foreach (var col in _table)
                col.AcceptChanges();

            statesRemovedInLastReparse.Clear();
            _grammar = _oldGrammar;
            _oldGrammar = null;

        }

        //Suggest RHS for a rule that would complete currently unparsed sequence.
        public string[] SuggestRHSForCompletion()
        {
            //1. find completion that is closest to the end of the table
            int furthestCompletedColumn = -1;

            //we go back from penultimate column.
            for (int i = _table.Length - 2; i >= 0; i--)
            {
                if (_table[i].Reductors.Count > 0)
                {
                    foreach (var reductor in _table[i].Reductors)
                    {
                        var isPOS = !_grammar.StaticRules.ContainsKey(reductor.Key);
                        if (isPOS) continue;

                        foreach (var item in reductor.Value)
                        {
                            if (item.EndColumn.Index > furthestCompletedColumn)
                                furthestCompletedColumn = item.EndColumn.Index;
                        }
                    }
                }
            }

            if (furthestCompletedColumn < 0 || furthestCompletedColumn == _table.Length - 1) return null;

            //2. randomly choose a reductor with the same end column
            List<EarleyState> candidates = new List<EarleyState>();

            for (int i = _table.Length - 2; i >= 0; i--)
            {
                if (_table[i].Reductors.Count > 0)
                {
                    foreach (var reductor in _table[i].Reductors)
                    {
                        var isPOS = !_grammar.StaticRules.ContainsKey(reductor.Key);
                        if (isPOS) continue;

                        foreach (var item in reductor.Value)
                        {
                            if (item.EndColumn.Index == furthestCompletedColumn &&
                                item.Rule.LeftHandSide.ToString() != ContextFreeGrammar.StartSymbol)
                                candidates.Add(item);
                        }
                    }
                }
            }
            var furthestUnparsedToken = _table[furthestCompletedColumn + 1].Token;
            var possibleNonTerminals = GetPossibleSyntacticCategoriesForToken(furthestUnparsedToken);

            var r = Pseudorandom.NextInt(candidates.Count);
            return new[] { candidates[r].Rule.LeftHandSide.ToString(), possibleNonTerminals.First() };
        }

        public class CategoryCompare : IComparer<DerivedCategory>
        {
            public int Compare(DerivedCategory x, DerivedCategory y)
            {
                return string.Compare(x.ToString(), y.ToString());
            }
        }
    }
}