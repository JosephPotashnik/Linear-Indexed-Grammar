using System.Collections.Generic;
using System.Linq;

namespace LinearIndexedGrammarParser
{
    public class GrammarTreeCountsCalculator
    {
        private readonly SubTreeCountsCache _cache;
        private readonly ContextFreeGrammar _g;
        private readonly HashSet<string> _pos;

        public GrammarTreeCountsCalculator(ContextFreeGrammar g, HashSet<string> pos, SubTreeCountsCache cache)
        {
            _g = g;
            _pos = pos;
            _cache = cache;
        }

        public SubtreeCountsWithNumberOfWords NumberOfParseTreesPerWords(int treeDepth)
        {
            return NumberOfParseTreesPerWords(new DerivedCategory(ContextFreeGrammar.StartSymbol), treeDepth);
        }

        private SubtreeCountsWithNumberOfWords NumberOfParseTreesPerWords(DerivedCategory[] rhs, int treeDepth)
        {
            var res = new SubtreeCountsWithNumberOfWords();

            if (rhs.Length == 2)
            {
                var catCounts1 = NumberOfParseTreesPerWords(rhs[0], treeDepth);
                var catCounts2 = NumberOfParseTreesPerWords(rhs[1], treeDepth);

                var kvpFromCat1 = catCounts1.WordsTreesDic.Values.ToList();
                var kvpFromCat2 = catCounts2.WordsTreesDic.Values.ToList();

                UpdateCounts(res, kvpFromCat1, kvpFromCat2);
            }
            else
            {
                var catCounts1 = NumberOfParseTreesPerWords(rhs[0], treeDepth);
                var kvpFromCat1 = catCounts1.WordsTreesDic.Values.ToList();
                UpdateCounts(res, kvpFromCat1);
            }

            return res;
        }

        private static void UpdateCounts(SubtreeCountsWithNumberOfWords res, List<WordsTreesCounts> kvpFromCat1,
            List<WordsTreesCounts> kvpFromCat2)
        {
            foreach (var wordsTreesDepth1 in kvpFromCat1)
            foreach (var wordsTreesDepth2 in kvpFromCat2)
            {
                var dic = res.WordsTreesDic;

                var wc = wordsTreesDepth1.WordsCount + wordsTreesDepth2.WordsCount;
                var tc = wordsTreesDepth1.TreesCount * wordsTreesDepth2.TreesCount;

                if (!dic.ContainsKey(wc))
                    dic[wc] = new WordsTreesCounts();

                dic[wc].WordsCount = wc;
                dic[wc].TreesCount += tc;
            }
        }


        private SubtreeCountsWithNumberOfWords NumberOfParseTreesPerWords(DerivedCategory cat, int treeDepth)
        {
            var res = new SubtreeCountsWithNumberOfWords {WordsTreesDic = new Dictionary<int, WordsTreesCounts>()};

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
                    while (cachedCatCounts == null && ++indexInCatCacheArr < storedCountsOfCat.Length)
                        cachedCatCounts = storedCountsOfCat[indexInCatCacheArr];

                    if (cachedCatCounts != null)
                        return cachedCatCounts;

                    var ruleList = _g.StaticRules[cat];
                    foreach (var rule in ruleList)
                    {
                        //check if in rules cache.
                        var storedCountsOfRules = _cache.RuleCache[rule];
                        var indexInRuleCacheArr = treeDepth - 1;
                        var cachedRuleCounts = storedCountsOfRules[indexInRuleCacheArr];
                        while (cachedRuleCounts == null && ++indexInRuleCacheArr < storedCountsOfRules.Length)
                            cachedRuleCounts = storedCountsOfRules[indexInRuleCacheArr];

                        SubtreeCountsWithNumberOfWords fromRHS;
                        //use previously computed results if applicable.
                        if (cachedRuleCounts != null)
                            fromRHS = cachedRuleCounts;
                        else
                            fromRHS = NumberOfParseTreesPerWords(rule.RightHandSide, treeDepth - 1);

                        var kvpFromCat1 = fromRHS.WordsTreesDic.Values.ToList();
                        UpdateCounts(res, kvpFromCat1);

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

        private static void CountTerminal(SubtreeCountsWithNumberOfWords res)
        {
            var c = new WordsTreesCounts {TreesCount = 1, WordsCount = 1};
            res.WordsTreesDic[c.WordsCount] = c;
        }

        private static void CountEpsilon(SubtreeCountsWithNumberOfWords res)
        {
            var c = new WordsTreesCounts {TreesCount = 1, WordsCount = 0};
            res.WordsTreesDic[c.WordsCount] = c;
        }

        private static void UpdateCounts(SubtreeCountsWithNumberOfWords res, List<WordsTreesCounts> kvpFromCat1)
        {
            foreach (var wordsTreesDepth1 in kvpFromCat1)
            {
                var dic = res.WordsTreesDic;

                var wc = wordsTreesDepth1.WordsCount;
                var tc = wordsTreesDepth1.TreesCount;

                if (!dic.ContainsKey(wc))
                    dic[wc] = new WordsTreesCounts();

                dic[wc].WordsCount = wc;
                dic[wc].TreesCount += tc;
            }
        }
    }
}