using System.Collections.Generic;

namespace LinearIndexedGrammarParser
{
    public class CompletedStatesHeap
    {
        private readonly MaxHeap indicesHeap = new MaxHeap();
        private readonly Dictionary<int, Queue<EarleyState>> items = new Dictionary<int, Queue<EarleyState>>();

        public int Count => indicesHeap.Count;

        public void Enqueue(EarleyState state)
        {
            var index = state.StartColumn.Index;
            if (!items.TryGetValue(index, out var queue))
            {
                indicesHeap.Add(index);
                queue = new Queue<EarleyState>();
                items.Add(index, queue);
            }

            queue.Enqueue(state);
        }

        public EarleyState Dequeue()
        {
            var index = indicesHeap.Max;
            var queue = items[index];

            var state = queue.Dequeue();
            if (queue.Count == 0)
            {
                items.Remove(index);
                indicesHeap.PopMax();
            }

            return state;
        }
    }
}