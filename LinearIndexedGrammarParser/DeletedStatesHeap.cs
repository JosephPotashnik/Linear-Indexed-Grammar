using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace LinearIndexedGrammarParser
{
    public class DeletedStatesHeap
    {
        private Dictionary<int, Stack<EarleyState>> items = new Dictionary<int, Stack<EarleyState>>();
        private MaxHeap indicesHeap = new MaxHeap();

        public int Count => indicesHeap.Count;
        public void Push(EarleyState state)
        {
            int index = state.StartColumn.Index;
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
            int index = indicesHeap.Max;
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
