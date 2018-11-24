using System;
using System.Collections.Generic;
using System.Linq;

namespace LinearIndexedGrammarLearner
{
    //adapted from: https://blogs.msdn.microsoft.com/ericlippert/2007/10/08/path-finding-using-a-in-c-3-0-part-three/
    //(added Values property,  Last property, PeekFirstKey operation)
    public class PriorityQueue<P, V>
    {
        private SortedDictionary<P, Queue<V>> list = new SortedDictionary<P, Queue<V>>();
        public void Enqueue(P priority, V value)
        {
            if (!list.TryGetValue(priority, out var q))
            {
                q = new Queue<V>();
                list.Add(priority, q);
            }
            q.Enqueue(value);
        }
        public V Dequeue()
        {
            // will throw if there isn’t any first element!
            var pair = list.First();
            var v = pair.Value.Dequeue();
            if (pair.Value.Count == 0) // nothing left of the top priority.
                list.Remove(pair.Key);
            return v;
        }
        public bool IsEmpty
        {
            get { return !list.Any(); }
        }

        public KeyValuePair<P, Queue<V>> Last() => list.Last();



        public P PeekFirstKey()
        {
            // will throw if there isn’t any first element!
            var pair = list.First();
            return pair.Key;
        }

        public IEnumerable<(P, V)> KeyValuePairs
        {
            get
            {

                var q = from i in list
                    from j in i.Value
                    select (i.Key, j);

                return q;
               
            }

        }


        public IEnumerable<V> Values
        {
            get
            {
                var allQueues = list.Values;
                return allQueues.SelectMany(x => x);
            }
        }
    }
}
