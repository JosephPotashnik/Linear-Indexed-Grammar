using System;
using System.Collections.Generic;
using System.Linq;

namespace LinearIndexedGrammarParser
{
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
        internal Dictionary<Rule, EarleyState> Predicted;

        public EarleyColumn(int index, string token)
        {
            Index = index;
            Token = token;

            //completed agenda is ordered in decreasing order of start indices (see Stolcke 1995 about completion priority queue).
            ActionableCompleteStates =
                new SortedDictionary<EarleyState, Queue<EarleyState>>(new CompletedStateComparer());
            ActionableDeletedStates =
                new SortedDictionary<EarleyState, Stack<EarleyState>>(new CompletedStateComparer());

            ActionableNonCompleteStates = new Queue<EarleyState>();
            Predecessors = new Dictionary<DerivedCategory, List<EarleyState>>();
            Reductors = new Dictionary<DerivedCategory, List<EarleyState>>();
            Predicted = new Dictionary<Rule, EarleyState>(new RuleValueEquals());
            GammaStates = new List<EarleyState>();
            OldGammaStates = new List<EarleyState>();
            ActionableNonTerminalsToPredict = new Queue<DerivedCategory>();

        }

        internal HashSet<DerivedCategory> NonTerminalsToUnpredict = new HashSet<DerivedCategory>();
        internal SortedDictionary<EarleyState, Queue<EarleyState>> ActionableCompleteStates { get; set; }
        internal SortedDictionary<EarleyState, Stack<EarleyState>> ActionableDeletedStates { get; set; }

        internal Queue<EarleyState> ActionableNonCompleteStates { get; set; }
        internal Queue<DerivedCategory> ActionableNonTerminalsToPredict;
        internal List<EarleyState> statesAddedInLastReparse = new List<EarleyState>();
        
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

        public void MarkStateDeleted(EarleyState oldState, ContextFreeGrammar grammar, HashSet<EarleyState> statesRemovedInLastReparse)
        {
            if (statesRemovedInLastReparse.Contains(oldState)) return;
            statesRemovedInLastReparse.Add(oldState);

            if (!oldState.IsCompleted)
            {

                var nextTerm = oldState.NextTerm;
                var isPOS = !grammar.StaticRules.ContainsKey(nextTerm);     

                if (!isPOS)
                {
                    if (!NonTerminalsToUnpredict.Contains(nextTerm))
                    {
                        //push to check next terminal to predicted state that might need to be deleted.
                        NonTerminalsToUnpredict.Add(nextTerm);
                        ActionableNonTerminalsToPredict.Enqueue(nextTerm);
                    }
                }
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
                parent.EndColumn.EnqueueToDeletedStack(parent);
                
        }

        public void DeleteState(EarleyState oldState, HashSet<EarleyState> statesRemovedInLastReparse)
        {
            if (oldState.Predecessor != null)
            {
                if (!statesRemovedInLastReparse.Contains(oldState.Predecessor))
                    //need to remove the parent edge between the predecessor to the deleted state
                    oldState.Predecessor.Parents.Remove(oldState);
            }

            if (oldState.Reductor != null)
            {
                if (!statesRemovedInLastReparse.Contains(oldState.Reductor))
                    //need to remove the parent edge between the reductor to the deleted state
                    oldState.Reductor.Parents.Remove(oldState);
            }


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
                    Predicted.Remove(oldState.Rule);
                
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

        //returns true if it finds a predicting state that is not predicted itself
        //i.e, if there exists a state X1 -> Y .Z  such that eventually state S is predicted from it.
        private bool DFSOverPredecessors(EarleyState state, HashSet<EarleyState> visited, DerivedCategory nextTerm, Dictionary<DerivedCategory, HashSet<Rule>> predictionSet, HashSet<EarleyState> statesRemovedInLastReparse)
        {
            visited.Add(state);
            if (state.DotIndex != 0) return true;

            if (!predictionSet[nextTerm].Contains(state.Rule))
                return true;

            var predecessorsUnmarkedForDeletion = PredecessorsUnmarkedForDeletion(state.Rule.LeftHandSide, statesRemovedInLastReparse);
            foreach (var predecessor in predecessorsUnmarkedForDeletion)
            {
                if (!visited.Contains(predecessor))
                {
                    var res = DFSOverPredecessors(predecessor, visited, nextTerm, predictionSet, statesRemovedInLastReparse);
                    if (res) return res;
                }
            }
            return false;

        }
        public bool CheckForUnprediction(DerivedCategory nextTerm, Dictionary<DerivedCategory, HashSet<Rule>> predictionSet, HashSet<EarleyState> statesRemovedInLastReparse)
        {
            var predecessorsUnmarkedForDeletion = PredecessorsUnmarkedForDeletion(nextTerm, statesRemovedInLastReparse);
            if (predecessorsUnmarkedForDeletion.Count == 0) return true;

            foreach (var state in predecessorsUnmarkedForDeletion)
            {
                var visited = new HashSet<EarleyState>();
                bool IsThereANonPredictedPredecessor = DFSOverPredecessors(state, visited, nextTerm, predictionSet, statesRemovedInLastReparse);
                if (IsThereANonPredictedPredecessor)
                    return false;

            }
            return true;
        }

        private List<EarleyState> PredecessorsUnmarkedForDeletion(DerivedCategory nextTerm, HashSet<EarleyState> statesRemovedInLastReparse)
        {
            var predecessorsUnmarkedForDeletion = new List<EarleyState>();
            if (Predecessors.TryGetValue(nextTerm, out var predecessors))
            {
                foreach (var predecessor in predecessors)
                {
                    if (!statesRemovedInLastReparse.Contains(predecessor))
                        predecessorsUnmarkedForDeletion.Add(predecessor);
                }
            }

            return predecessorsUnmarkedForDeletion;
        }

        public void EnqueueToCompletedQueue(EarleyState state)
        {
            if (!ActionableCompleteStates.TryGetValue(state, out var queue))
            {
                queue = new Queue<EarleyState>();
                ActionableCompleteStates.Add(state, queue);
            }

            queue.Enqueue(state);

        }

        public void EnqueueToDeletedStack(EarleyState state)
        {
            if (!ActionableDeletedStates.TryGetValue(state, out var stack))
            {
                stack = new Stack<EarleyState>();
                ActionableDeletedStates.Add(state, stack);
            }

            stack.Push(state);
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
                if (!Predecessors.TryGetValue(term, out var predecessors))
                {
                    predecessors = new List<EarleyState>();
                    Predecessors.Add(term, predecessors);

                    if (!isPOS)
                        ActionableNonTerminalsToPredict.Enqueue(term);
                }

                predecessors.Add(newState);

                if (newState.DotIndex == 0)
                    Predicted[newState.Rule] = newState;


                if (isPOS && !Reductors.ContainsKey(term))
                    ActionableNonCompleteStates.Enqueue(newState);

                if (term.ToString() == ContextFreeGrammar.EpsilonSymbol)
                {
                    if (!Reductors.TryGetValue(term, out var reductors1))
                    {
                        var epsilon = new Rule(term.ToString(), new[] { "" });
                        var epsilonState = new EarleyState(epsilon, 1, this);
                        epsilonState.EndColumn = this;
                        reductors1 = new List<EarleyState>() { epsilonState };
                        Reductors.Add(term, reductors1);
                    }
                }

                if (Reductors.TryGetValue(term, out var reductors))
                {
                    //spontaneous dot shift.
                    foreach (var completedState in reductors)
                        SpontaneousDotShift(newState, completedState, grammar);

                }
            }
            else
            {
                EnqueueToCompletedQueue(newState);
            }
        }

        public void AcceptChanges()
        {

            foreach (var state in statesAddedInLastReparse)
                state.Added = false;
            statesAddedInLastReparse.Clear();

            NonTerminalsToUnpredict.Clear();
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
                        state.EndColumn.Predicted.Remove(state.Rule);

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

            foreach (var state in OldGammaStates.ToList())
            {
                    GammaStates.Add(state);
                    OldGammaStates.Remove(state);
            }

        }

        public void Unpredict(Rule r, ContextFreeGrammar grammar, HashSet<EarleyState> statesRemovedInLastReparse)
        {
            if (Predicted.TryGetValue(r, out var state))
                state.EndColumn.MarkStateDeleted(state, grammar, statesRemovedInLastReparse);
        }
    }
}