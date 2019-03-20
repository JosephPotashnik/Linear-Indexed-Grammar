using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LinearIndexedGrammarParser
{
    public class RuleLCComparer : IEqualityComparer<Rule>
    {
        public bool Equals(Rule x, Rule y)
        {
            return x.NumberOfGeneratingRule == y.NumberOfGeneratingRule;
        }

        public int GetHashCode(Rule obj)
        {
            return obj.NumberOfGeneratingRule;
        }
    }
    public class LeftCorner
    {
        public Dictionary<DerivedCategory, HashSet<Rule>> ComputeLeftCorner(ContextFreeGrammar grammar)
        {
            var rules = grammar.Rules;
            //key - nonterminal, value - set of the numbers of reachable rules by transitive left corner.
            var leftCorners = new Dictionary<DerivedCategory, HashSet<Rule>>();

            foreach (var item in rules)
            {
                var cat = item.LeftHandSide;
                if (!leftCorners.ContainsKey(cat))
                    leftCorners[cat] = new HashSet<Rule>(new RuleLCComparer());

                if (grammar.StaticRules.ContainsKey(cat))
                {
                    List<Rule> ruleList = grammar.StaticRules[cat];
                    foreach (var predicted in ruleList)
                    {
                        if (!leftCorners[cat].Contains(predicted))
                            leftCorners[cat].Add(predicted);
                    }
                }
            }

            var changed = true;
            while (changed)
            {
                changed = false;
                foreach (var item in leftCorners)
                {
                    var cat = item.Key;
                    var reachableRules = item.Value;
                    foreach (var reachable in reachableRules.ToArray())
                    {
                        if (leftCorners.ContainsKey(reachable.RightHandSide[0]))
                        {
                            var reachablesFromReachable = leftCorners[reachable.RightHandSide[0]];

                            foreach (var reachreach in reachablesFromReachable)
                            {
                                if (!leftCorners[cat].Contains(reachreach))
                                {
                                    leftCorners[cat].Add(reachreach);
                                    changed = true;
                                }
                            }
                        }
                    }
                }
            }
            return leftCorners;
        }
    }
}
