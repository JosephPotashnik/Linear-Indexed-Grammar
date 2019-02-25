using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LinearIndexedGrammarParser
{
    public class GeneratingRuleComparer : IEqualityComparer<Rule>
    {
        public bool Equals(Rule x, Rule y)
        {
            return (x.NumberOfGeneratingRule == y.NumberOfGeneratingRule);
        }

        public int GetHashCode(Rule obj)
        {
            return obj.NumberOfGeneratingRule;
        }
    }
    public class SubTreeCountsCache
    {
        public Dictionary<DerivedCategory, int[][]> CategoriesCache;
        public Dictionary<Rule, int[][]> RuleCache;

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            if (CategoriesCache != null)
            {
                foreach (var cat in CategoriesCache.Keys)
                {
                    sb.AppendLine($"Category {cat}:");
                    for (int i = 0; i < CategoriesCache[cat].Length; i++)
                    {
                        if (CategoriesCache[cat][i] != null)
                        {
                            sb.AppendLine($"Depth {i}:");
                            for (int j = 0; j < CategoriesCache[cat][i].Length; j++)
                            {
                                sb.Append(CategoriesCache[cat][i][j]);

                            }
                        }

                    }

                }
            }

            return sb.ToString();
        }

        public void Reset(ContextFreeGrammar g, int depth)
        {
            foreach (var cat in CategoriesCache.Keys)
            {
                for (int i = 0; i < CategoriesCache[cat].Length; i++)
                    Array.Clear(CategoriesCache[cat][i], 0, CategoriesCache[cat][i].Length);
            }

            foreach (var rule in RuleCache.Keys)
            {
                for (int i = 0; i < RuleCache[rule].Length; i++)
                    Array.Clear(RuleCache[rule][i], 0, RuleCache[rule][i].Length);
            }

            foreach (var lhs in g.StaticRulesGeneratedForCategory)
            {
                if (!CategoriesCache.ContainsKey(lhs))
                {
                    CategoriesCache[lhs] = new int[depth][];

                    for (int i = 0; i < depth; i++)
                        CategoriesCache[lhs][i] = new int[depth];
                }

            }

            var staticRules = g.StaticRules.Values.SelectMany(x => x);
            foreach (var rule in staticRules)
            {
                if (!RuleCache.ContainsKey(rule))
                {
                    RuleCache[rule] = new int[depth][];
                    for (int i = 0; i < depth; i++)
                        RuleCache[rule][i] = new int[depth];
                }
            }
        }
        public SubTreeCountsCache(ContextFreeGrammar g, int depth)
        {
            CategoriesCache = new Dictionary<DerivedCategory, int[][]>();
            foreach (var lhs in g.StaticRulesGeneratedForCategory)
            {
                CategoriesCache[lhs] = new int[depth][];

                for (int i = 0; i < depth; i++)
                    CategoriesCache[lhs][i] = new int[depth];

            }

            RuleCache = new Dictionary<Rule, int[][]>(new GeneratingRuleComparer());
            var staticRules = g.StaticRules.Values.SelectMany(x => x);
            foreach (var rule in staticRules)
            {
                RuleCache[rule] = new int[depth][];
                for (int i = 0; i < depth; i++)
                    RuleCache[rule][i] = new int[depth];

            }
        }
    }

}