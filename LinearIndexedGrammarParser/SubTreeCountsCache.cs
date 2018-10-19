﻿using System.Collections.Generic;
using System.Linq;

namespace LinearIndexedGrammarParser
{
    public class SubTreeCountsCache
    {
        public Dictionary<DerivedCategory, SubtreeCountsWithNumberOfWords[]> CategoriesCache;
        public Dictionary<Rule, SubtreeCountsWithNumberOfWords[]> RuleCache;

        public SubTreeCountsCache(ContextFreeGrammar g, int depth)
        {
            CategoriesCache = new Dictionary<DerivedCategory, SubtreeCountsWithNumberOfWords[]>();
            foreach (var lhs in g.StaticRulesGeneratedForCategory)
                CategoriesCache[lhs] = new SubtreeCountsWithNumberOfWords[depth];

            RuleCache = new Dictionary<Rule, SubtreeCountsWithNumberOfWords[]>();
            var staticRules = g.StaticRules.Values.SelectMany(x => x);
            foreach (var rule in staticRules)
                RuleCache[rule] = new SubtreeCountsWithNumberOfWords[depth];
        }
    }

    public class SubtreeCountsWithNumberOfWords
    {
        public Dictionary<int, WordsTreesCounts> WordsTreesDic = new Dictionary<int, WordsTreesCounts>();
    }
}