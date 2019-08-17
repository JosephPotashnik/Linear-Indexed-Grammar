using System.Collections.Generic;

namespace LinearIndexedGrammarParser
{
    //This class is used to store the data required for the dictionary of left corner.
    public class LeftCornerInfo
    {
        //the rules contained in the transitive closure of left corner of the key.
        public HashSet<Rule> LeftCornerRules { get; set; }

        //the set of nonterminals contained in the transitive left corner of the key.
        public HashSet<DerivedCategory> NonTerminals { get; set; }
    }

    public class LeftCorner
    {
        private static HashSet<DerivedCategory> pos = new HashSet<DerivedCategory>();

        public Dictionary<DerivedCategory, LeftCornerInfo> LeftCorners { get; set; }


        public static void SetPOS(HashSet<string> _pos)
        {
            foreach (var s in _pos)
                pos.Add(new DerivedCategory(s));
        }

        private void DFS(DerivedCategory root, DerivedCategory cat, HashSet<DerivedCategory> visited,
            Dictionary<DerivedCategory, List<Rule>> rules)
        {
            visited.Add(cat);
            LeftCorners[root].NonTerminals.Add(cat);

            if (rules.TryGetValue(cat, out var rulesOfCat))
            {
                foreach (var r in rulesOfCat)
                    LeftCorners[root].LeftCornerRules.Add(r);

                foreach (var r in rulesOfCat)
                {
                    var leftCorner = r.RightHandSide[0];
                    if (rules.ContainsKey(leftCorner))
                    {
                        if (!visited.Contains(leftCorner))
                            DFS(root, leftCorner, visited, rules);
                    }
                    else
                        LeftCorners[root].NonTerminals.Add(leftCorner);
                }
            }
        }

        public LeftCorner(Dictionary<DerivedCategory, List<Rule>> rules)
        {
            //key - nonterminal, value - see above
            LeftCorners = new Dictionary<DerivedCategory, LeftCornerInfo>();

            var nonTerminals = rules.Keys;

            foreach (var nt in nonTerminals)
            {
                LeftCorners[nt] = new LeftCornerInfo();
                LeftCorners[nt].LeftCornerRules = new HashSet<Rule>(new RuleValueEquals());
                LeftCorners[nt].NonTerminals = new HashSet<DerivedCategory>();
                var visited = new HashSet<DerivedCategory>();
                foreach (var r in rules[nt])
                {
                    LeftCorners[nt].LeftCornerRules.Add(r);

                    var leftCorner = r.RightHandSide[0];
                    if (rules.ContainsKey(leftCorner))
                    {
                        if (!visited.Contains(leftCorner))
                            DFS(nt, leftCorner, visited, rules);
                    }
                    else
                        LeftCorners[nt].NonTerminals.Add(leftCorner);

                }
            }

            //parts of speech are not really in the left corner of themselves,
            //since the parser works with non-lexicalized grammars, we do not save 
            //states such as [D -> 'the' $]. So, encountering a rule such as NP -> D N,
            //Predict() will search for D in the left corner of D.
            //allow this convention only for completed parts of speech. See EarleyParser.Predict().
            foreach (var cat in pos)
            {
                LeftCorners[cat] = new LeftCornerInfo();
                LeftCorners[cat].LeftCornerRules = new HashSet<Rule>(new RuleValueEquals());
                LeftCorners[cat].NonTerminals = new HashSet<DerivedCategory>();
                LeftCorners[cat].NonTerminals.Add(cat);
            }
        }
    }
}