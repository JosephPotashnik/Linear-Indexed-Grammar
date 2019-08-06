using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace LinearIndexedGrammarParser
{
    public class CompletedStatesHeap
    {
        private Dictionary<int, Queue<EarleyState>> items = new Dictionary<int, Queue<EarleyState>>();
        private MaxHeap indicesHeap = new MaxHeap();

        public int Count => indicesHeap.Count;
        public void Enqueue(EarleyState state)
        {
            int index = state.StartColumn.Index;
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
            int index = indicesHeap.Max;
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
