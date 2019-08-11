using System;
using System.Collections.Generic;

namespace LinearIndexedGrammarParser
{
    public class MaxHeap
    {
        public readonly List<int> elements = new List<int>();

        public int Count => elements.Count;

        public int Max => elements[0];

        public void Add(int item)
        {
            elements.Add(item);
            HeapifyUp(elements.Count - 1);
        }

        public int PopMax()
        {
            if (elements.Count > 0)
            {
                var item = elements[0];
                elements[0] = elements[elements.Count - 1];
                elements.RemoveAt(elements.Count - 1);

                HeapifyDown(0);
                return item;
            }

            throw new InvalidOperationException("no element in heap");
        }

        private void HeapifyUp(int index)
        {
            var parent = index <= 0 ? -1 : (index - 1) / 2;

            if (parent >= 0 && elements[index] > elements[parent])
            {
                var temp = elements[index];
                elements[index] = elements[parent];
                elements[parent] = temp;

                HeapifyUp(parent);
            }
        }

        private void HeapifyDown(int index)
        {
            var largest = index;

            var left = 2 * index + 1;
            var right = 2 * index + 2;

            if (left < Count && elements[left] > elements[index])
                largest = left;

            if (right < Count && elements[right] > elements[largest])
                largest = right;

            if (largest != index)
            {
                var temp = elements[index];
                elements[index] = elements[largest];
                elements[largest] = temp;

                HeapifyDown(largest);
            }
        }
    }
}