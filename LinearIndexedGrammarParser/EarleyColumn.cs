using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace LinearIndexedGrammarParser
{
    //internal class CompletedStateEqualityComparer : IEqualityComparer<EarleyState>
    //{

    //    public bool Equals(EarleyState x, EarleyState y)
    //    {
    //        return (x.StartColumn.Index == y.StartColumn.Index && x.EndColumn.Index == y.EndColumn.Index &&
    //                x.Rule.LeftHandSide.Equals(y.Rule.LeftHandSide));
    //    }

    //    public int GetHashCode(EarleyState obj)
    //    {
    //        return obj.Rule.LeftHandSide.GetHashCode();
    //    }
    //}
    internal class EarleyStateReferenceComparer : IEqualityComparer<EarleyState>
    {

        public bool Equals(EarleyState x, EarleyState y)
        {
            return (x.StartColumn.Index == y.StartColumn.Index && x.EndColumn.Index == y.EndColumn.Index &&
                    x.Rule.LeftHandSide.Equals(y.Rule.LeftHandSide));
        }

        public int GetHashCode(EarleyState obj)
        {
            return obj.Rule.LeftHandSide.GetHashCode();
        }
    }


    internal class CompletedStateComparer : IComparer<EarleyState>
    {
        public int Compare(EarleyState x, EarleyState y)
        {
            if (x.StartColumn.Index > y.StartColumn.Index)
                return -1;
            if (x.StartColumn.Index < y.StartColumn.Index)
                return 1;
            return 0;
        }
    }

    public class EarleyColumn
    {
        internal Dictionary<DerivedCategory, List<EarleyState>> Predecessors;
        internal Dictionary<DerivedCategory, List<EarleyState>> Reductors;
        internal Dictionary<int, List<EarleyState>> Predicted;
        private HashSet<int> _statesToUnPredict = new HashSet<int>();
        internal Stack<EarleyState> statesToDelete = new Stack<EarleyState>();

        public EarleyColumn(int index, string token)
        {
            Index = index;
            Token = token;

            //completed agenda is ordered in decreasing order of start indices (see Stolcke 1995 about completion priority queue).
            ActionableCompleteStates =
                new SortedDictionary<EarleyState, Queue<EarleyState>>(new CompletedStateComparer());
            ActionableNonCompleteStates = new Queue<EarleyState>();
            Predecessors = new Dictionary<DerivedCategory, List<EarleyState>>();
            Reductors = new Dictionary<DerivedCategory, List<EarleyState>>();
            Predicted = new Dictionary<int, List<EarleyState>>();
            GammaStates = new List<EarleyState>();
            ActionableNonTerminalsToPredict = new Queue<DerivedCategory>();
        }

        internal SortedDictionary<EarleyState, Queue<EarleyState>> ActionableCompleteStates { get; set; }
        internal Queue<EarleyState> ActionableNonCompleteStates { get; set; }
        internal Queue<DerivedCategory> ActionableNonTerminalsToPredict;
        internal List<EarleyState> statesAddedInLastReparse = new List<EarleyState>();
        internal List<EarleyState> statesRemovedInLastReparse = new List<EarleyState>();

        public List<EarleyState> GammaStates { get; set; }
        public int Index { get; }
        public string Token { get; set; }

        private void SpontaneousDotShift(EarleyState state, EarleyState completedState, ContextFreeGrammar grammar)
        {
            var y = EarleyState.MakeNode(state, completedState.EndColumn.Index, completedState.Node);
            var newState = new EarleyState(state.Rule, state.DotIndex + 1, state.StartColumn, y);
            state.Parents.Add(newState);
            completedState.Parents.Add(newState);
            newState.Predecessor = state;
            newState.Reductor = completedState;
            
            //you need to check for cyclic unit production here.
            //
            //
            //
            //


            completedState.EndColumn.AddState(newState, grammar);
        }

        public void DeleteState(EarleyState oldState, ContextFreeGrammar grammar)
        {
            if (oldState.Removed) return;
            oldState.Removed = true;
            statesRemovedInLastReparse.Add(oldState);

            if (oldState.Predecessor != null)
            {
                if (oldState.Predecessor.Removed == false)
                    //need to remove the parent edge between the predecessor to the deleted state
                    oldState.Predecessor.Parents.Remove(oldState);
            }

            if (oldState.Reductor != null)
            {
                if (oldState.Reductor.Removed == false)          
                    //need to remove the parent edge between the reductor to the deleted state
                    oldState.Reductor.Parents.Remove(oldState);        
            }


            if (!oldState.IsCompleted)
            {
                var nextTerm = oldState.NextTerm;

                var predecessors = Predecessors[nextTerm];
                for (int i = 0; i < predecessors.Count; i++)
                {
                    if (predecessors[i] == oldState)
                    {
                        predecessors.RemoveAt(i);
                        break;
                    }
                }


                if (oldState.DotIndex == 0)
                {
                    Predicted[oldState.Rule.NumberOfGeneratingRule].Remove(oldState);
                    if (Predicted[oldState.Rule.NumberOfGeneratingRule].Count == 0)
                        Predicted.Remove(oldState.Rule.NumberOfGeneratingRule);
                }

                //if this state is the only source of predictions for the next term,
                //we need to recursively delete all earley states predicted from next term.
                if (Predecessors[nextTerm].Count == 0)
                {
                     Predecessors.Remove(nextTerm);

                    if (grammar.StaticRules.ContainsKey(nextTerm))
                    {
                        //delete all predictions
                        var ruleList = grammar.StaticRules[nextTerm];

                        foreach (var rule in ruleList)
                                Unpredict(rule);

                    }
                }


            }
            else
            {
                if (oldState.Rule.LeftHandSide.ToString() == ContextFreeGrammar.GammaRule)
                {
                    var gammaStates = oldState.EndColumn.GammaStates;
                    for (int i = 0; i < gammaStates.Count; i++)
                    {
                        if (gammaStates[i] == oldState)
                        {
                            gammaStates.RemoveAt(i);
                            break;
                        }
                    }

                    return;
                }

                var reductors = oldState.StartColumn.Reductors[oldState.Rule.LeftHandSide];
                for (int i = 0; i < reductors.Count; i++)
                {
                    if (reductors[i] == oldState)
                    {
                        reductors.RemoveAt(i);
                        break;
                    }
                }


                if (reductors.Count == 0)
                    oldState.StartColumn.Reductors.Remove(oldState.Rule.LeftHandSide);

            }

            foreach (var state in oldState.Parents)
                state.EndColumn.statesToDelete.Push(state);
        }

        //The responsibility not to add a state that already exists in the column
        //lays with the caller to AddState(). i.e, either predict, scan or complete,
        //or epsilon complete.
        public void AddState(EarleyState newState, ContextFreeGrammar grammar)
        {
            newState.Added = true;
            newState.EndColumn = this;
            statesAddedInLastReparse.Add(newState);

            if (!newState.IsCompleted)
            {
                var term = newState.NextTerm;
                bool isPOS = !grammar.StaticRules.ContainsKey(term);
                if (!Predecessors.ContainsKey(term))
                {
                    Predecessors[term] = new List<EarleyState>();

                    if (!isPOS)
                        ActionableNonTerminalsToPredict.Enqueue(term);
                }

                Predecessors[term].Add(newState);

                if (newState.DotIndex == 0)
                {
                    //predicted - add to predicted dictionary
                    if (!Predicted.ContainsKey(newState.Rule.NumberOfGeneratingRule))
                        Predicted[newState.Rule.NumberOfGeneratingRule] = new List<EarleyState>();
                    Predicted[newState.Rule.NumberOfGeneratingRule].Add(newState);
                }

                if (isPOS && !Reductors.ContainsKey(term))
                    ActionableNonCompleteStates.Enqueue(newState);

                if (term.ToString() == ContextFreeGrammar.EpsilonSymbol)
                {
                    if (!Reductors.ContainsKey(term))
                    {
                        Reductors[term] = new List<EarleyState>();
                        Reductors[term].Add(newState);
                    }
                }

                if (Reductors.ContainsKey(term))
                {
                    //spontaneous dot shift.
                    foreach (var completedState in Reductors[term])
                        SpontaneousDotShift(newState, completedState, grammar);
                    
                }
            }
            else
            {
                if (!ActionableCompleteStates.ContainsKey(newState))
                    ActionableCompleteStates[newState] = new Queue<EarleyState>();

                ActionableCompleteStates[newState].Enqueue(newState);
            }
        }

        public void AcceptChanges()
        {
            if (statesAddedInLastReparse.Count > 0 && statesRemovedInLastReparse.Count > 0)
                throw new Exception("states should not have been both added and removed");

            foreach (var state in statesAddedInLastReparse)
                state.Added = false;

            statesAddedInLastReparse.Clear();

            foreach (var state in statesRemovedInLastReparse)
                state.Removed = false;

            statesRemovedInLastReparse.Clear();

            _statesToUnPredict.Clear();

        }

        public void RejectChanges()
        {
            if (statesAddedInLastReparse.Count > 0 && statesRemovedInLastReparse.Count > 0)
                throw new Exception("states should not have been both added and removed");

            foreach (var state in statesAddedInLastReparse)
            {
                if (state.IsCompleted)
                {
                    if (state.Rule.LeftHandSide.ToString() == ContextFreeGrammar.GammaRule)
                    {
                        var gammaStates = state.EndColumn.GammaStates;
                        for (int i = 0; i < gammaStates.Count; i++)
                        {
                            if (gammaStates[i] == state)
                            {
                                gammaStates.RemoveAt(i);
                                break;
                            }
                        }
                    }
                    else
                    {
                        state.StartColumn.Reductors[state.Rule.LeftHandSide].Remove(state);
                        if (state.StartColumn.Reductors[state.Rule.LeftHandSide].Count == 0)
                            state.StartColumn.Reductors.Remove(state.Rule.LeftHandSide);

                    }


                }
                else
                {
                    var nextTerm = state.NextTerm;
                    state.EndColumn.Predecessors[nextTerm].Remove(state);
                    if (state.EndColumn.Predecessors[nextTerm].Count == 0)
                        state.EndColumn.Predecessors.Remove(nextTerm);

                    if (state.DotIndex == 0)
                    {
                        state.EndColumn.Predicted[state.Rule.NumberOfGeneratingRule].Remove(state);
                        if (state.EndColumn.Predicted[state.Rule.NumberOfGeneratingRule].Count == 0)
                            state.EndColumn.Predicted.Remove(state.Rule.NumberOfGeneratingRule);
                    }
                }

                if (state.Predecessor != null)
                {
                    if (state.Predecessor.Added == false)
                        //need to remove the parent edge between the predecessor to the deleted state
                        state.Predecessor.Parents.Remove(state);
                }

                if (state.Reductor != null)
                {
                    if (state.Reductor.Added == false)
                        //need to remove the parent edge between the reductor to the deleted state
                        state.Reductor.Parents.Remove(state);
                }



            }

            foreach (var state in statesRemovedInLastReparse)
            {
                if (state.IsCompleted)
                {
                    if (state.Rule.LeftHandSide.ToString() == ContextFreeGrammar.GammaRule)
                    {
                        var gammaStates = state.EndColumn.GammaStates;
                        gammaStates.Add(state);
                    }
                    else
                    {
                        if (!state.StartColumn.Reductors.ContainsKey(state.Rule.LeftHandSide))
                            state.StartColumn.Reductors[state.Rule.LeftHandSide] = new List<EarleyState>();
                        state.StartColumn.Reductors[state.Rule.LeftHandSide].Add(state);
                       

                    }
                }
                else
                {
                    var nextTerm = state.NextTerm;
                    if (!state.EndColumn.Predecessors.ContainsKey(nextTerm))
                        state.EndColumn.Predecessors[nextTerm] = new List<EarleyState>();
                    state.EndColumn.Predecessors[nextTerm].Add(state);


                    if (state.DotIndex == 0)
                    {
                        if (!state.EndColumn.Predicted.ContainsKey(state.Rule.NumberOfGeneratingRule))
                            state.EndColumn.Predicted[state.Rule.NumberOfGeneratingRule] = new List<EarleyState>();

                        state.EndColumn.Predicted[state.Rule.NumberOfGeneratingRule].Add(state);

                    }
                }

                if (state.Predecessor != null)
                {
                    if (state.Predecessor.Removed == false)
                        //need to re-add the parent edge between the predecessor to the deleted state
                        state.Predecessor.Parents.Add(state);
                }

                if (state.Reductor != null)
                {
                    if (state.Reductor.Removed == false)
                        //need to re-add the parent edge between the reductor to the deleted state
                        state.Reductor.Parents.Add(state);
                }

            }

            AcceptChanges();
        }

        public void Unpredict(Rule r)
        {
            if (!_statesToUnPredict.Contains(r.NumberOfGeneratingRule))
            {
                _statesToUnPredict.Add(r.NumberOfGeneratingRule);

                var states = Predicted[r.NumberOfGeneratingRule];
                foreach (var state in states)
                    statesToDelete.Push(state);
            }
        }
    }
}