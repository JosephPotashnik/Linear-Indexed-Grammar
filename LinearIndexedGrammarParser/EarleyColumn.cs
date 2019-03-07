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

        public List<EarleyState> GammaStates { get; set; }
        public int Index { get; }
        public string Token { get; set; }

        private void SpontaneousDotShift(EarleyState state, EarleyState completedState, ContextFreeGrammar grammar)
        {
            var y = EarleyState.MakeNode(state, completedState.EndColumn.Index, completedState.Node);
            var newState = new EarleyState(state.Rule, state.DotIndex + 1, state.StartColumn, y);
            state.Parents.Add(newState);
            completedState.Parents.Add(newState);

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
                        {
                            //you need to change it to generating rule number when dealing with CSG
                            var statesToDelete = Predicted[rule.Number];
                            foreach (var state in statesToDelete)
                                DeleteState(state, grammar);

                            Predicted[rule.Number].Clear();
                            Predicted.Remove(rule.Number); 
                        }
                    }
                }

                foreach (var state in oldState.Parents)
                    state.EndColumn.DeleteState(state, grammar);
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


                if (oldState.StartColumn.Reductors[oldState.Rule.LeftHandSide].Count == 0)
                    oldState.StartColumn.Reductors.Remove(oldState.Rule.LeftHandSide);

                //remove parents of reductor.
                foreach (var state in oldState.Parents)
                    state.EndColumn.DeleteState(state, grammar);

            }
        }

        //The responsibility not to add a state that already exists in the column
        //lays with the caller to AddState(). i.e, either predict, scan or complete,
        //or epsilon complete.
        public void AddState(EarleyState newState, ContextFreeGrammar grammar)
        {
            newState.EndColumn = this;

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
                    if (!Predicted.ContainsKey(newState.Rule.Number))
                        Predicted[newState.Rule.Number] = new List<EarleyState>();
                    Predicted[newState.Rule.Number].Add(newState);
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
    }
}