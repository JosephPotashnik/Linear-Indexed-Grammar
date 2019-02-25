using System.Collections.Generic;
using System.Linq;

namespace LinearIndexedGrammarParser
{
    public class GrammarTreeCountsCalculator
    {
        private SubTreeCountsCache _cache;
        public ContextFreeGrammar _g;
        private readonly HashSet<string> _pos;

        //private ContextFreeGrammar _originalG;
        private readonly int _maxWordsInSentence;
        private readonly int _minWordsInSentence;
        private int _treeDepth;

        public GrammarTreeCountsCalculator(HashSet<string> pos, int min, int max)
        {
            _pos = pos;
            _minWordsInSentence = min;
            _maxWordsInSentence = max;
            _treeDepth = max + 3;
        }

        public int[] NumberOfParseTreesPerWords()
        {
            return NumberOfParseTreesPerWords(new DerivedCategory(ContextFreeGrammar.StartSymbol), _treeDepth-1);
        }

        //in a given depth treedepth, we have array of arr[wc] = tc;
        private int[] NumberOfParseTreesPerWords(Rule r, int treeDepth)
        {
            var res = _cache.RuleCache[r][treeDepth];
            var rhs = r.RightHandSide;

            if (rhs.Length == 2)
            {
                var catCounts1 = NumberOfParseTreesPerWords(rhs[0], treeDepth);
                var catCounts2 = NumberOfParseTreesPerWords(rhs[1], treeDepth);

                UpdateCounts(res, catCounts1, catCounts2);
            }
            else
            {
                var catCounts1 = NumberOfParseTreesPerWords(rhs[0], treeDepth);
                UpdateCounts(res, catCounts1);
            }

            return res;
        }

        private static void UpdateCounts(int[] res, int[] fromCat1, int[] fromCat2)
        {
            for (int i = 0; i < fromCat1.Length; i++)
            {
                for (int j = 0; j < fromCat2.Length; j++)
                {
                    if (fromCat1[i] > 0 && fromCat2[j] > 0)
                    {
                        var wc = i + j;
                        var tc = fromCat1[i] * fromCat2[j];

                        if (wc < res.Length)
                            res[wc] += tc;
                    }
                }

            }

        }

        private static bool IsEmpty(int[] wordLengthAndTrees)
        {
            for (int i = 0; i < wordLengthAndTrees.Length; i++)
                if (wordLengthAndTrees[i] != 0)
                    return false;

            return true;
        }

        private int[] NumberOfParseTreesPerWords(DerivedCategory cat, int treeDepth)
        {
            var res = _cache.CategoriesCache[cat][treeDepth];

            if (_g.StaticRules.ContainsKey(cat))
            {
                //if not in cache, compute counts of subtrees
                //POS can sometimes be a left-side nonterminal, for instance NP -> D N
                if (_pos.Contains(cat.ToString()))
                    CountTerminal(res);

                if (treeDepth > 0)
                {
                    //check if in categories cache.
                    var storedCountsOfCat = _cache.CategoriesCache[cat];
                    var indexInCatCacheArr = treeDepth - 1;
                    var cachedCatCounts = storedCountsOfCat[indexInCatCacheArr];

                    while (IsEmpty(cachedCatCounts) && ++indexInCatCacheArr < storedCountsOfCat.Length)
                        cachedCatCounts = storedCountsOfCat[indexInCatCacheArr];

                    if (!IsEmpty(cachedCatCounts))
                        return cachedCatCounts;

                    var ruleList = _g.StaticRules[cat];
                    foreach (var rule in ruleList)
                    {
                        //check if in rules cache.
                        var storedCountsOfRules = _cache.RuleCache[rule];
                        var indexInRuleCacheArr = treeDepth - 1;
                        var cachedRuleCounts = storedCountsOfRules[indexInRuleCacheArr];
                        while (IsEmpty(cachedRuleCounts) && ++indexInRuleCacheArr < storedCountsOfRules.Length)
                            cachedRuleCounts = storedCountsOfRules[indexInRuleCacheArr];

                        int[] fromRHS;
                        //use previously computed results if applicable.
                        if (!IsEmpty(cachedRuleCounts))
                            fromRHS = cachedRuleCounts;
                        else
                            fromRHS = NumberOfParseTreesPerWords(rule, treeDepth - 1);

                        UpdateCounts(res, fromRHS);

                        //store in rules cache.
                        storedCountsOfRules[treeDepth - 1] = fromRHS;
                    }

                    //store in categories cache.
                    storedCountsOfCat[treeDepth - 1] = res;
                }
            }
            else if (_pos.Contains(cat.ToString()))
            {
                CountTerminal(res);
            }
            else if (cat.ToString() == ContextFreeGrammar.EpsilonSymbol)
            {
                CountEpsilon(res);
            }

            return res;
        }

        private static void CountTerminal(int[] res)
        {
            res[1] = 1;
        }

        private static void CountEpsilon(int[] res)
        {
            res[0] = 1;
        }

        private static void UpdateCounts(int[] res, int[] fromRHS)
        {
            for (int i = 0; i < fromRHS.Length; i++)
                res[i] += fromRHS[i];
        }

        public Dictionary<int, int> Recalculate(ContextFreeGrammar grammar)
        {
            //if (_cache == null)
                _cache = new SubTreeCountsCache(grammar, _treeDepth);
            //else
             //   _cache.Reset(grammar, _treeDepth);

            _g = grammar;

            var t = NumberOfParseTreesPerWords();
            var grammarTreesPerLength = new Dictionary<int, int>();
            for (int i = 0; i < t.Length; i++)
            {
                if (i <= _maxWordsInSentence && i >= _minWordsInSentence && t[i] > 0)
                    grammarTreesPerLength[i] = t[i];
            }

            return grammarTreesPerLength;

        }
    }



}