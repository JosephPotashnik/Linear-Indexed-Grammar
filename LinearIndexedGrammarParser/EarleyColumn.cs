using System;
using System.Collections.Generic;

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
        internal Queue<DerivedCategory> CategoriesToPredict;
        internal Dictionary<DerivedCategory, List<EarleyState>> Predecessors;
        internal Dictionary<DerivedCategory, List<EarleyState>> Reductors;

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
            GammaStates = new List<EarleyState>();
            CategoriesToPredict = new Queue<DerivedCategory>();
        }

        internal SortedDictionary<EarleyState, Queue<EarleyState>> ActionableCompleteStates { get; set; }
        internal Queue<EarleyState> ActionableNonCompleteStates { get; set; }

        public List<EarleyState> GammaStates { get; set; }
        public int Index { get; }
        public string Token { get; set; }

        private void SpontaneousDotShift(EarleyState state, EarleyState completedState, ContextFreeGrammar grammar)
        {
            var y = EarleyState.MakeNode(state, completedState.EndColumn.Index, completedState.Node);
            var newState = new EarleyState(state.Rule, state.DotIndex + 1, state.StartColumn, y);
            state.Parents.Add(newState);
            completedState.EndColumn.AddState(newState, grammar);
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
                        CategoriesToPredict.Enqueue(term);
                }

                Predecessors[term].Add(newState);

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