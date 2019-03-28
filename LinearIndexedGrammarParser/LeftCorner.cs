using System.Collections.Generic;
using System.Linq;

namespace LinearIndexedGrammarParser
{
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
                if (!leftCorners.TryGetValue(cat, out var lcRules))
                {
                    lcRules = new HashSet<Rule>(new RuleValueEquals());
                    leftCorners.Add(cat, lcRules);
                }

          
                if (grammar.StaticRules.TryGetValue(cat, out var ruleList))
                {
                    foreach (var predicted in ruleList)
                    {
                        if (!lcRules.Contains(predicted))
                            lcRules.Add(predicted);
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
                        if (leftCorners.TryGetValue(reachable.RightHandSide[0], out var reachablesFromReachable))
                        {
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
