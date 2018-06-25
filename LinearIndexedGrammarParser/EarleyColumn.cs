
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
        public EarleyColumn(int index, string token)
        {
            Index = index;
            Token = token;

            //completed agenda is ordered in decreasing order of start indices (see Stolcke 1995 about completion priority queue).
            ActionableCompleteStates = new SortedDictionary<EarleyState, Queue<EarleyState>>(new CompletedStateComparer());
            StatesWithNextSyntacticCategory = new Dictionary<DerivedCategory, List<EarleyState>>();
            GammaStates = new List<EarleyState>();
            CategoriesToPredict = new Queue<DerivedCategory>();

        }

        public int Index { get; set; }
        public string Token { get; set; }

        internal SortedDictionary<EarleyState, Queue<EarleyState>> ActionableCompleteStates { get; set; }
        internal Dictionary<DerivedCategory, List<EarleyState>> StatesWithNextSyntacticCategory;
        internal Queue<DerivedCategory> CategoriesToPredict;
        public List<EarleyState> GammaStates { get; set; }

        private void EpsilonComplete(EarleyState state, Grammar grammar)
        {
            //TODO:
            //make sure epsilon complete is called only once per rule in that column.

            var v = new EarleyNode("trace", Index, Index);
            var y = EarleyState.MakeNode(state, Index, v);
            var newState = new EarleyState(state.Rule, state.DotIndex + 1, state.StartColumn, y);
            AddState(newState, grammar);
        }

        //The responsibility not to add a state that already exists in the columnn
        //lays with the caller to AddState(). i.e, either predict, scan or complete,
        //or epsilon complete.
        public void AddState(EarleyState newState, Grammar grammar)
        {
            newState.EndColumn = this;

            if (!newState.IsCompleted())
            {
                var term = newState.NextTerm();

                if (!StatesWithNextSyntacticCategory.ContainsKey(term))
                {
                    //TODO: consider if is necessary to enqueue a category that is part of speech
                    //is there any derivation rule with part of speech on the left hand side?
                    CategoriesToPredict.Enqueue(term);
                    StatesWithNextSyntacticCategory[term] = new List<EarleyState>();
                }

                StatesWithNextSyntacticCategory[term].Add(newState);

                //check if the next nonterminal leads to an expansion of null production, if yes, insert it to the 
                //completed rules.
                if (grammar.nullableCategories.Contains(term))
                {
                    //spontaneous dot shift.
                    EpsilonComplete(newState, grammar);
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