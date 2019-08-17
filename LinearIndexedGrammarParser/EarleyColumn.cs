using System;
using System.Collections.Generic;
using System.Linq;

namespace LinearIndexedGrammarParser
{
    public class EarleyColumn
    {
        internal Queue<DerivedCategory> ActionableNonTerminalsToPredict;
        internal Dictionary<DerivedCategory, HashSet<EarleyState>> Predecessors;
        internal Dictionary<Rule, List<EarleyState>> Predicted;
        internal Dictionary<DerivedCategory, HashSet<EarleyState>> Reductors;
        internal List<EarleyState> statesAddedInLastReparse = new List<EarleyState>();

        internal HashSet<DerivedCategory> visitedCategoriesInUnprediction = new HashSet<DerivedCategory>();
        internal HashSet<DerivedCategory> NonTerminalsCandidatesToUnpredict = new HashSet<DerivedCategory>();

        public EarleyColumn(int index, string token)
        {
            Index = index;
            Token = token;

            //completed agenda is ordered in decreasing order of start indices (see Stolcke 1995 about completion priority queue).

            //ActionableCompleteStates =
            //    new SortedDictionary<EarleyState, Queue<EarleyState>>(new CompletedStateComparer());
            ActionableCompleteStates = new CompletedStatesHeap();

            ActionableDeletedStates = new DeletedStatesHeap();

            //ActionableNonCompleteStates = new Queue<EarleyState>();
            Predecessors = new Dictionary<DerivedCategory, HashSet<EarleyState>>();
            Reductors = new Dictionary<DerivedCategory, HashSet<EarleyState>>();
            Predicted = new Dictionary<Rule, List<EarleyState>>(new RuleValueEquals());
            GammaStates = new List<EarleyState>();
            OldGammaStates = new List<EarleyState>();
            ActionableNonTerminalsToPredict = new Queue<DerivedCategory>();
        }

        internal CompletedStatesHeap ActionableCompleteStates { get; set; }
        internal DeletedStatesHeap ActionableDeletedStates { get; set; }

        internal Queue<EarleyState> ActionableNonCompleteStates { get; set; }


        public List<EarleyState> GammaStates { get; set; }
        public List<EarleyState> OldGammaStates { get; set; }

        public int Index { get; }
        public string Token { get; set; }

        private void SpontaneousDotShift(EarleyState state, EarleyState completedState, ContextFreeGrammar grammar)
        {
            var newState = new EarleyState(state.Rule, state.DotIndex + 1, state.StartColumn);
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

        public void MarkStateDeleted(EarleyState oldState, ContextFreeGrammar grammar,
            HashSet<EarleyState> statesRemovedInLastReparse)
        {
            if (oldState.Removed) return;
            oldState.Removed = true;
            statesRemovedInLastReparse.Add(oldState);

            if (!oldState.IsCompleted)
            {
                var nextTerm = oldState.NextTerm;
                var isPOS = !grammar.StaticRules.ContainsKey(nextTerm);

                if (!isPOS && !visitedCategoriesInUnprediction.Contains(nextTerm))
                    NonTerminalsCandidatesToUnpredict.Add(nextTerm);
            }
            else
            {
                if (oldState.Rule.LeftHandSide.ToString() == ContextFreeGrammar.GammaRule)
                {
                    var gammaStates = oldState.EndColumn.GammaStates;
                    gammaStates.Remove(oldState);
                    OldGammaStates.Add(oldState);
                    return;
                }
            }

            foreach (var parent in oldState.Parents)
                parent.EndColumn.ActionableDeletedStates.Push(parent);
        }

        public void DeleteState(EarleyState oldState, HashSet<EarleyState> statesRemovedInLastReparse)
        {
            if (oldState.Predecessor != null)
                if (!statesRemovedInLastReparse.Contains(oldState.Predecessor))
                    //need to remove the parent edge between the predecessor to the deleted state
                    oldState.Predecessor.Parents.Remove(oldState);

            if (oldState.Reductor != null)
                if (!statesRemovedInLastReparse.Contains(oldState.Reductor))
                    //need to remove the parent edge between the reductor to the deleted state
                    oldState.Reductor.Parents.Remove(oldState);


            if (!oldState.IsCompleted)
            {
                var nextTerm = oldState.NextTerm;
                if (Predecessors.TryGetValue(nextTerm, out var predecessors))
                {
                    predecessors.Remove(oldState);
                    if (predecessors.Count == 0)
                        Predecessors.Remove(nextTerm);
                }

                if (oldState.DotIndex == 0)
                {
                    Predicted[oldState.Rule].Remove(oldState);
                    if (Predicted[oldState.Rule].Count == 0)
                        Predicted.Remove(oldState.Rule);

                }
            }
            else
            {
                if (oldState.Rule.LeftHandSide.ToString() == ContextFreeGrammar.GammaRule)
                {
                    var oldgammaStates = oldState.EndColumn.OldGammaStates;
                    oldgammaStates.Remove(oldState);
                    return;
                }

                var reductors = oldState.StartColumn.Reductors[oldState.Rule.LeftHandSide];
                reductors.Remove(oldState);
                if (reductors.Count == 0)
                    oldState.StartColumn.Reductors.Remove(oldState.Rule.LeftHandSide);
            }
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
                var isPOS = !grammar.StaticRules.ContainsKey(term);
                bool addTermToPredict = !isPOS;

                if (!Predecessors.TryGetValue(term, out var predecessors))
                {
                    predecessors = new HashSet<EarleyState>();
                    Predecessors.Add(term, predecessors);
                }
                else
                {
                    if (addTermToPredict)
                    {
                        foreach (var predecessor in predecessors)
                        {
                            if (!predecessor.Removed)
                            {
                                addTermToPredict = false;
                                break;
                            }
                        }
                    }
                }
                if (addTermToPredict)
                    ActionableNonTerminalsToPredict.Enqueue(term);

                predecessors.Add(newState);

                if (newState.DotIndex == 0)
                {
                    if (!Predicted.TryGetValue(newState.Rule, out var list))
                    {
                        list = new List<EarleyState>();
                        Predicted[newState.Rule] = list;
                    }
                    list.Add(newState);
                }


                //if grammar is non-lexicalized, we prepare all scannable states in advance.
                //if (isPOS && !Reductors.ContainsKey(term))
                //ActionableNonCompleteStates.Enqueue(newState);

                if (term.ToString() == ContextFreeGrammar.EpsilonSymbol)
                    if (!Reductors.TryGetValue(term, out var reductors1))
                    {
                        var epsilon = new Rule(term.ToString(), new[] {""});
                        var epsilonState = new EarleyState(epsilon, 1, this);
                        epsilonState.EndColumn = this;
                        reductors1 = new HashSet<EarleyState> {epsilonState};
                        Reductors.Add(term, reductors1);
                    }

                if (Reductors.TryGetValue(term, out var reductors))
                    foreach (var completedState in reductors)
                    {
                        if (!completedState.Removed)
                            SpontaneousDotShift(newState, completedState, grammar);
                    }
            }
            else
            {
                ActionableCompleteStates.Enqueue(newState);
            }
        }

        public void AcceptChanges()
        {
            if (statesAddedInLastReparse != null)
            {
                foreach (var state in statesAddedInLastReparse)
                    state.Added = false;
                statesAddedInLastReparse.Clear();
            }

            NonTerminalsCandidatesToUnpredict?.Clear();
            visitedCategoriesInUnprediction?.Clear();
        }

        public void RejectChanges()
        {
            foreach (var state in statesAddedInLastReparse)
            {

                if (state.IsCompleted)
                {
                    if (state.Rule.LeftHandSide.ToString() == ContextFreeGrammar.GammaRule)
                    {
                        var gammaStates = state.EndColumn.GammaStates;
                        gammaStates.Remove(state);
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
                        state.EndColumn.Predicted[state.Rule].Remove(state);
                        if (state.EndColumn.Predicted[state.Rule].Count == 0)
                            state.EndColumn.Predicted.Remove(state.Rule); 
                    }

                }

                if (state.Predecessor != null)
                    if (state.Predecessor.Added == false)
                        //need to remove the parent edge between the predecessor to the deleted state
                        state.Predecessor.Parents.Remove(state);

                if (state.Reductor != null)
                    if (state.Reductor.Added == false)
                        //need to remove the parent edge between the reductor to the deleted state
                        state.Reductor.Parents.Remove(state);
            }

            foreach (var state in OldGammaStates.ToList())
            {
                GammaStates.Add(state);
                OldGammaStates.Remove(state);
            }

            visitedCategoriesInUnprediction?.Clear();
        }

        public void Unpredict(Rule r, ContextFreeGrammar grammar, HashSet<EarleyState> statesRemovedInLastReparse, Dictionary<DerivedCategory, LeftCornerInfo> predictionSet)
        {
            if (Predicted.TryGetValue(r, out var list))
            {
                if (list.Count > 1)
                    throw new Exception("list of predicted should be at this stage 1 item only.");

                foreach (var state in list)
                    state.EndColumn.MarkStateDeleted(state, grammar, statesRemovedInLastReparse);
            }
            
        }


    }
}