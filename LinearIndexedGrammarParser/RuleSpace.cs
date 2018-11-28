using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;


namespace LinearIndexedGrammarParser
{
    public static class ThreadSafeRandom
    {
        [ThreadStatic] private static Random _local;

        public static Random ThisThreadsRandom => _local ?? (_local =
                                                      new Random(unchecked(Environment.TickCount * 31 +
                                                                           Thread.CurrentThread.ManagedThreadId)));
    }

    public class RuleSpace
    {
        public Rule[][] _ruleSpace { get; }
        private List<int> _allowedRHSIndices;
        private HashSet<string> _partsOfSpeechCategories { get;  }
        readonly Dictionary<string, int> nonTerminalsRHS = new Dictionary<string, int>();
        readonly Dictionary<string, int> nonTerminalLHS = new Dictionary<string, int>();

        public RuleSpace(string[] partsOfSpeechCategories, int maxNonTerminals)
        {
            _allowedRHSIndices = new List<int>();
            List<string> rhsStore = new List<string>();
            List<string> nonTerminals = new List<string>();
            _partsOfSpeechCategories = new HashSet<string>(partsOfSpeechCategories);
            _ruleSpace = new Rule[maxNonTerminals][];
            for (int i = 1; i <= maxNonTerminals; i++)
            {
                nonTerminals.Add($"X{i}");
                nonTerminalLHS[$"X{i}"] = i-1;
            }

            rhsStore.Add("EMPTY");
            rhsStore = rhsStore.Concat(partsOfSpeechCategories).ToList();
            rhsStore = rhsStore.Concat(nonTerminals).ToList();

            for (int i = 0; i < rhsStore.Count; i++)
                nonTerminalsRHS[rhsStore[i]] = i;
            

            var currentCategory = new DerivedCategory("X1", ContextFreeGrammar.StarSymbol);
            var startCategory = new DerivedCategory(ContextFreeGrammar.StartRule, ContextFreeGrammar.StarSymbol);
            int length = rhsStore.Count;
            int numberOfPossibleRHS = (length * length);

            _ruleSpace[0] = new Rule[numberOfPossibleRHS - length + 1];
            _ruleSpace[0][0] = new Rule(startCategory, new[] { currentCategory });
            _allowedRHSIndices.Add(0);

            for (int i = length; i < numberOfPossibleRHS; i++)
            {
                var rhs1 = rhsStore[i / length];
                if (i % length == 0)
                {
                    var rhs1cat = new DerivedCategory(rhs1);
                    _ruleSpace[0][i - length + 1] = new Rule(currentCategory, new[] { rhs1cat });

                    //add to excluded rhs indices list: unit production such as x1 -> x2
                    //note:  S -> Xi is allowed (in index 0).
                    if (rhs1cat.ToString()[0] != 'X')
                        _allowedRHSIndices.Add(i - length + 1);

                }
                else
                {

                    var rhs2 = rhsStore[i % length];
                    var rhs2cat = new DerivedCategory(rhs2);
                    var rhs1cat = new DerivedCategory(rhs1, ContextFreeGrammar.StarSymbol);
                    _ruleSpace[0][i - length + 1] = new Rule(currentCategory, new[] {rhs1cat, rhs2cat});

                    //Assumption: we allow only different RHS sides for non-terminals.
                    //to be relaxed later.
                    //note: in future, make sure not to relax it include the form Xi -> Xi Xi
                    //(perhaps allow Xj -> Xi Xi)
                    if (rhs1 != rhs2 || _partsOfSpeechCategories.Contains(rhs1))
                        _allowedRHSIndices.Add(i - length + 1);
                }
            }

            for (int i = 1; i < maxNonTerminals; i++)
            {
                var currentLHSNonterminal = $"X{i+1}";
                currentCategory = new DerivedCategory(currentLHSNonterminal, ContextFreeGrammar.StarSymbol);

                _ruleSpace[i] = _ruleSpace[0]
                    .Select(x => new Rule(currentCategory, x.RightHandSide)).ToArray();
                _ruleSpace[i][0] = new Rule(startCategory, new[] { currentCategory });
            }

            for (int j = 0; j < _ruleSpace.Length; j++)
            {
                for (int i = 0; i < _ruleSpace[j].Length; i++)
                    _ruleSpace[j][i].Number = (j * _ruleSpace[0].Length) + i + 1;
            }
        }

        public int GetRandomRHSIndex()
        {
            var rand = ThreadSafeRandom.ThisThreadsRandom;
            return _allowedRHSIndices[rand.Next(_allowedRHSIndices.Count)];
        }

        public int GetRandomLHSIndex()
        {
            var rand = ThreadSafeRandom.ThisThreadsRandom;
            return rand.Next(_ruleSpace.Length);
        }

        public Rule this[RuleCoordinates rc] => _ruleSpace[rc.LHSIndex][rc.RHSIndex];

        public RuleCoordinates FindRule(Rule r)
        {            
            var rc = new RuleCoordinates();
            var LHS = new SyntacticCategory(r.LeftHandSide);
            if (LHS.ToString() == ContextFreeGrammar.StartRule)
            {

                //each start rule is of the form S -> Xi and is the first element of the xi column
                rc.LHSIndex = FindLHSIndex(new SyntacticCategory(r.RightHandSide[0]).ToString());
                rc.RHSIndex = 0;
            }
            else
            {
                rc.LHSIndex = FindLHSIndex(new SyntacticCategory(LHS).ToString());
                rc.RHSIndex = FindRHSIndex(r.RightHandSide.Select(x => new SyntacticCategory(x).ToString()).ToArray());
            }
            return rc;
        }
        private int FindLHSIndex(string LHS)
        {
            return nonTerminalLHS[LHS];
        }

        private int FindRHSIndex(string[] RHS)
        {
            int length = nonTerminalsRHS.Count;
            var rhsIndexinStore1 = nonTerminalsRHS[RHS[0]];

            if (RHS.Length == 1)
                return (rhsIndexinStore1-1) * (length) + 1;

            var rhsIndexinStore2 = nonTerminalsRHS[RHS[1]];
            return ( ((rhsIndexinStore1-1) * length) + rhsIndexinStore2) + 1;
        }
    }
}
