using System;
using System.Collections.Generic;
using System.Text;

namespace LinearIndexedGrammarParser
{
    public class SubTreeCountsCache
    {
        public Dictionary<DerivedCategory, SubtreeCountsWithNumberOfWords[]> CategoriesCache;
        public Dictionary<Rule, SubtreeCountsWithNumberOfWords[]> RuleCache;

        public SubTreeCountsCache(Grammar g, int depth)
        {
            CategoriesCache = new Dictionary<DerivedCategory, SubtreeCountsWithNumberOfWords[]>();
            foreach (var lhs in g.staticRulesGeneratedForCategory)
                CategoriesCache[lhs] = new SubtreeCountsWithNumberOfWords[depth];

            RuleCache = new Dictionary<Rule, SubtreeCountsWithNumberOfWords[]>();
            foreach (var rule in g.Rules)
                RuleCache[rule] = new SubtreeCountsWithNumberOfWords[depth];
        }

    }

    public class SubtreeCountsWithNumberOfWords
    {
        public Dictionary<int, WordsTreesCounts> WordsTreesDic = new Dictionary<int, WordsTreesCounts>();
    }
}
