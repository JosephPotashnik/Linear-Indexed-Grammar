using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace LinearIndexedGrammarParser
{

    public class SubtreeCountsWithNumberOfWords
    {
        public Dictionary<int ,WordsTreesCounts> WordsTreesDic = new Dictionary<int, WordsTreesCounts>();
    }
    public class WordsTreesCounts
    {
        public int WordsCount { get; set; }
        public int TreesCount { get; set; }
    }
    public class Grammar
    {
        public Grammar() { }
        public const string GammaRule = "Gamma";
        public const string StartRule = "START";
        public const string EpsislonSymbol = "Epsilon";

        public readonly Dictionary<DerivedCategory, List<Rule>> staticRules = new Dictionary<DerivedCategory, List<Rule>>();
        public readonly Dictionary<SyntacticCategory, List<Rule>> dynamicRules = new Dictionary<SyntacticCategory, List<Rule>>();
        public readonly HashSet<DerivedCategory> staticRulesGeneratedForCategory = new HashSet<DerivedCategory>();
        public readonly HashSet<DerivedCategory> nullableCategories = new HashSet<DerivedCategory>();
        internal static int ruleCounter = 0;
        private int ruleCount = 0;
        public int RuleCount => ruleCount;

        public Grammar(Grammar otherGrammar)
        {
            staticRules = otherGrammar.staticRules.ToDictionary(x => x.Key, x => x.Value.Select(y => new Rule(y)).ToList());
            dynamicRules = otherGrammar.dynamicRules.ToDictionary(x => x.Key, x => x.Value.Select(y => new Rule(y)).ToList());
            staticRulesGeneratedForCategory = new HashSet<DerivedCategory>(otherGrammar.staticRulesGeneratedForCategory);
            nullableCategories = new HashSet<DerivedCategory>(otherGrammar.nullableCategories);
            ruleCount = otherGrammar.ruleCount;
        }
        public IEnumerable<Rule> Rules
        {
            //Note: SelectMany here does not deep-copy, we get the reference to the grammar rules.
            get {  return staticRules.Values.SelectMany(x => x); }
        }
        public override string ToString()
        {
            var allRules = Rules;
            return string.Join("\r\n", allRules);
        }

        //takes care properly of staticRules, staticRulesGeneratedForCategory fields of Grammar class.
        //TODO: dynamicRules, nullableCategories are not properly handled yet!
        public void DeleteGrammarRule(Rule r)
        {
            var LHS = r.LeftHandSide;
            var rulesWithSameLHS = staticRules[LHS];
            rulesWithSameLHS.Remove(r);
            ruleCount--;

            if (rulesWithSameLHS.Count == 0)
            {
                staticRules.Remove(LHS);
                staticRulesGeneratedForCategory.Remove(LHS);

            }

        }

        public bool ContainsSameRHSRule(Rule newRule)
        {
            bool bFoundIdentical = false;
            //assuming compositionality.
            // if found rule with the same right hand side, do not re-add it.
            //the nonterminal of the left hand side does not matter.

            foreach (var rule in Rules)
            {
                if (!rule.RightHandSide[0].IsEpsilon() && rule.RightHandSide.Length == newRule.RightHandSide.Length)
                {
                    bFoundIdentical = true;
                    for (int i = 0; i < rule.RightHandSide.Length; i++)
                    {
                        if (!rule.RightHandSide[i].Equals(newRule.RightHandSide[i]))
                            bFoundIdentical = false;
                    }
                    if (bFoundIdentical) break;
                }
            }

            return bFoundIdentical;
        }

        //takes care properly of staticRules, staticRulesGeneratedForCategory fields of Grammar class.
        //TODO: dynamicRules, nullableCategories are not properly handled yet!
        public void AddGrammarRule(Rule r)
        {
            var newRule = new Rule(r);

            //if non-empty stack
            if (newRule.LeftHandSide.Stack != null)
            {
                var stackContents = newRule.LeftHandSide.Stack;
                //and if the left hand side allows manipulating the stack (has the wildcard)
                //insert into the stackManipulationRules dictionary.
                if (stackContents.Contains("*"))
                {

                    var newSynCat = new SyntacticCategory(newRule.LeftHandSide);
                    if (!dynamicRules.ContainsKey(newSynCat))
                        dynamicRules[newSynCat] = new List<Rule>();

                    dynamicRules[newSynCat].Add(newRule);

                    var emptyStackRule = new DerivedCategory(newSynCat.ToString());
                    //generate base form of the rule with the empty stack
                    //as a starting point of the grammar (= equal to context free case)
                    staticRulesGeneratedForCategory.Add(emptyStackRule);
                    var derivedRule = GenerateStaticRuleFromDyamicRule(newRule, emptyStackRule);
                    if (derivedRule != null)
                        AddStaticRule(derivedRule);


                }
                else
                {
                    staticRulesGeneratedForCategory.Add(newRule.LeftHandSide);
                    AddStaticRule(newRule);
                }
            }
            else
            {
                staticRulesGeneratedForCategory.Add(newRule.LeftHandSide);
                AddStaticRule(newRule);
            }
        }

        public void AddStaticRule(Rule r)
        {
            if (r == null) return;

            Grammar.ruleCounter++;
            var newRule = new Rule(r);
            newRule.Number = Grammar.ruleCounter;

            if (!staticRules.ContainsKey(newRule.LeftHandSide))
                staticRules[newRule.LeftHandSide] = new List<Rule>();

            staticRules[newRule.LeftHandSide].Add(newRule);
            ruleCount++;

            //TODO: calculate the transitive closure of all nullable symbols.
            //at the moment you calculate only the rules that directly lead to epsilon.
            //For instance. C -> D E, D -> epsilon, E-> epsilon. C is not in itself an epsilon rule
            //yet it is a nullable production.
            if (newRule.RightHandSide[0].IsEpsilon())
                nullableCategories.Add(newRule.LeftHandSide);
        }

        public void PruneUnusedRules(Dictionary<int, int> usagesDic)
        {
            var unusedRules = Rules.Where(x => !usagesDic.ContainsKey(x.Number)).ToArray();

            foreach (var rule in unusedRules)      
                DeleteGrammarRule(rule);
        }

        public Rule GenerateStaticRuleFromDyamicRule(Rule dynamicGrammarRule, DerivedCategory leftHandSide)
        {
            if (dynamicGrammarRule.LeftHandSide.Stack == null || dynamicGrammarRule.LeftHandSide.Stack == string.Empty)
                return null;

            string patternStringLeftHandSide = dynamicGrammarRule.LeftHandSide.Stack;

            //1. make the pattern be your Syntactic Category
            //2. then find the stack contents - anything by "*" (the first group)
            var newRule = new Rule(dynamicGrammarRule);
            string patternString = patternStringLeftHandSide.Replace("*", "(.*)");

            Regex pattern = new Regex(patternString);

            string textToMatch = leftHandSide.Stack;
            Match match = pattern.Match(textToMatch);
            if (!match.Success) return null;

            var stackContents = match.Groups[1].Value;
            newRule.LeftHandSide = leftHandSide;

            //3. replace the contents of the stack * in the right hand side productions.
            for (int i = 0; i < newRule.RightHandSide.Length; i++)
            {
                string patternRightHandSide = newRule.RightHandSide[i].Stack;
                string res = patternRightHandSide.Replace("*", stackContents);
                newRule.RightHandSide[i].Stack = res;
            }

            return newRule;
        }
        public SubtreeCountsWithNumberOfWords NumberOfParseTreesPerWords(DerivedCategory[] RHS, int treeDepth, HashSet<string> POS)
        {
            SubtreeCountsWithNumberOfWords res = new SubtreeCountsWithNumberOfWords();

            if (RHS.Length == 2)
            {
                var catCounts1 = NumberOfParseTreesPerWords(RHS[0], treeDepth, POS);
                var catCounts2 = NumberOfParseTreesPerWords(RHS[1], treeDepth, POS);

                var kvpFromCat1 = catCounts1.WordsTreesDic.Values.ToList();
                var kvpFromCat2 = catCounts2.WordsTreesDic.Values.ToList();

                UpdateCounts(res, kvpFromCat1, kvpFromCat2);
            }
            else
            {
                var catCounts1 = NumberOfParseTreesPerWords(RHS[0], treeDepth, POS);
                var kvpFromCat1 = catCounts1.WordsTreesDic.Values.ToList();
                UpdateCounts(res, kvpFromCat1);

            }

            return res;
        }

        private static void UpdateCounts(SubtreeCountsWithNumberOfWords res, List<WordsTreesCounts> kvpFromCat1, List<WordsTreesCounts> kvpFromCat2)
        {
            foreach (var wordsTreesDepth1 in kvpFromCat1)
            {
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
        }

        //private static List<KeyValuePair<int, WordsTreesCounts>> GetListOfTreesCountsWithDepths(SubtreeCountsWithNumberOfWords catCounts1)
        //{
        //    //var xy = catCounts1.WordTreesCounts.Select(x => x.WordsTreesDic.Values).ToArray();
        //    var xy = catCounts1.WordsTreesDic.Values;
        //    List<KeyValuePair<int, WordsTreesCounts>> listTreesCountWithDepths = new List<KeyValuePair<int, WordsTreesCounts>>();

        //    for (int i = 0; i < xy.Length; i++)
        //    {
        //        foreach (var item in xy[i])
        //            listTreesCountWithDepths.Add(new KeyValuePair<int, WordsTreesCounts>(i, item));
        //    }

        //    return listTreesCountWithDepths;
        //}

        public SubtreeCountsWithNumberOfWords NumberOfParseTreesPerWords(DerivedCategory cat, int treeDepth, HashSet<string> POS)
        {
            var res = new SubtreeCountsWithNumberOfWords();
            res.WordsTreesDic = new Dictionary<int, WordsTreesCounts>();

            if (staticRules.ContainsKey(cat))
            {

                //POS can sometimes be a left-side nonterminal, for instance NP -> D N
                if (POS.Contains(cat.ToString()))
                    CountTerminal(res);

                if (treeDepth > 0)
                {
                    var ruleList = staticRules[cat];
                    foreach (var rule in ruleList)
                    {
                        var fromRHS = NumberOfParseTreesPerWords(rule.RightHandSide, treeDepth - 1, POS);
                        var kvpFromCat1 = fromRHS.WordsTreesDic.Values.ToList();
                        UpdateCounts(res, kvpFromCat1);
                    }
                }
            }
            else if (POS.Contains(cat.ToString()))
                CountTerminal(res);

            return res;

        }

        private static void CountTerminal(SubtreeCountsWithNumberOfWords res)
        {
            WordsTreesCounts c = new WordsTreesCounts();
            c.TreesCount = 1;
            c.WordsCount = 1;
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

        /*
        public int NumberOfParseTrees(DerivedCategory[] RHS, int treeDepth, HashSet<string> POS)
        {
            int res = 1;
            foreach (var cat in RHS)
                res *= NumberOfParseTrees(cat, treeDepth, POS);

            return res;
        }

        public int NumberOfParseTrees(DerivedCategory cat, int treeDepth, HashSet<string> POS)
        {
            if (staticRules.ContainsKey(cat))
            {
                int res = 0;
                //POS can sometimes be a left-side nonterminal, for instance NP -> D N
                if (POS.Contains(cat.ToString()))
                    res += 1;

                if (treeDepth > 0)
                {
                    var ruleList = staticRules[cat];
                    foreach (var rule in ruleList)
                        res += NumberOfParseTrees(rule.RightHandSide, treeDepth - 1, POS);
                }

                return res;

            }
            else if (POS.Contains(cat.ToString()))
                return 1;
            else
                return 0;
        }
        */
    }
}