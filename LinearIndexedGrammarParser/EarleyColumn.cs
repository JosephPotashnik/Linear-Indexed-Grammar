using System.Collections.Generic;

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
        internal Queue<DerivedCategory> CategoriesToPredict;
        internal Dictionary<DerivedCategory, List<EarleyState>> StatesWithNextSyntacticCategory;

        public EarleyColumn(int index, string token)
        {
            Index = index;
            Token = token;

            //completed agenda is ordered in decreasing order of start indices (see Stolcke 1995 about completion priority queue).
            ActionableCompleteStates =
                new SortedDictionary<EarleyState, Queue<EarleyState>>(new CompletedStateComparer());
            StatesWithNextSyntacticCategory = new Dictionary<DerivedCategory, List<EarleyState>>();
            GammaStates = new List<EarleyState>();
            CategoriesToPredict = new Queue<DerivedCategory>();
        }

        internal SortedDictionary<EarleyState, Queue<EarleyState>> ActionableCompleteStates { get; set; }
        public List<EarleyState> GammaStates { get; set; }
        public int Index { get; }
        public string Token { get; set; }

        private void EpsilonComplete(EarleyState state, ContextFreeGrammar grammar)
        {
            var v = new EarleyNode("trace", Index, Index);
            var y = EarleyState.MakeNode(state, Index, v);
            var newState = new EarleyState(state.Rule, state.DotIndex + 1, state.StartColumn, y);
            AddState(newState, grammar);
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

                if (!StatesWithNextSyntacticCategory.ContainsKey(term))
                {
                    StatesWithNextSyntacticCategory[term] = new List<EarleyState>();

                    if (!grammar.ObligatoryNullableCategories.Contains(term))
                        CategoriesToPredict.Enqueue(term);
                }

                StatesWithNextSyntacticCategory[term].Add(newState);

                //check if the next nonterminal leads to an expansion of null production, if yes,
                //then perform a spontaneous dot shift.
                if (grammar.PossibleNullableCategories.Contains(term))
                    EpsilonComplete(newState, grammar);
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