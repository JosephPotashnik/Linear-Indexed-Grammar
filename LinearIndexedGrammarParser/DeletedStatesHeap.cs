using System.Collections.Generic;

namespace LinearIndexedGrammarParser
{
    public class DeletedStatesHeap
    {
        private readonly MaxHeap indicesHeap = new MaxHeap();
        private readonly Dictionary<int, Stack<EarleyState>> items = new Dictionary<int, Stack<EarleyState>>();

        public int Count => indicesHeap.Count;

        public void Push(EarleyState state)
        {
            var index = state.StartColumn.Index;
            if (!items.TryGetValue(index, out var queue))
            {
                indicesHeap.Add(index);
                queue = new Stack<EarleyState>();
                items.Add(index, queue);
            }

            queue.Push(state);
        }

        public EarleyState Pop()
        {
            var index = indicesHeap.Max;
            var queue = items[index];

            var state = queue.Pop();
            if (queue.Count == 0)
            {
                items.Remove(index);
                indicesHeap.PopMax();
            }

            return state;
        }
    }
}