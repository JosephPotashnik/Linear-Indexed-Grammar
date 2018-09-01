using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace LinearIndexedGrammarParser
{
    
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
        public const string StarSymbol = "*";
        public const int maxStackDepth = 3;

        public readonly Dictionary<DerivedCategory, List<Rule>> staticRules = new Dictionary<DerivedCategory, List<Rule>>();
        public readonly Dictionary<SyntacticCategory, List<Rule>> dynamicRules = new Dictionary<SyntacticCategory, List<Rule>>();
        public readonly HashSet<DerivedCategory> staticRulesGeneratedForCategory = new HashSet<DerivedCategory>();
        public readonly HashSet<DerivedCategory> nullableCategories = new HashSet<DerivedCategory>();
        private int ruleCounter = 0;

        public Grammar(Grammar otherGrammar)
        {
            //staticRulesGeneratedForCategory = new HashSet<DerivedCategory>(otherGrammar.staticRulesGeneratedForCategory);
            //staticRules = otherGrammar.staticRules.ToDictionary(x => x.Key, x => x.Value.Select(y => new Rule(y)).ToList());
            //the assumption is that the one who calls Grammar(otherGrammar)
            //calls afterwards to  Grammar.GenerateAllStaticRulesFromDynamicRules()
            //to compute the staticRulesGeneratedForCategory and staticRules fields.

            dynamicRules = otherGrammar.dynamicRules.ToDictionary(x => x.Key, x => x.Value.Select(y => new Rule(y)).ToList());
            nullableCategories = new HashSet<DerivedCategory>(otherGrammar.nullableCategories);
            ruleCounter = 0;
            
            foreach (var rule in Rules)
                rule.Number = ++ruleCounter;
        }
        public IEnumerable<Rule> Rules
        {
            //Note: SelectMany here does not deep-copy, we get the reference to the grammar rules.
            get {  return dynamicRules.Values.SelectMany(x => x); }
        }


        public override string ToString()
        {
            var allRules = Rules;
            return string.Join("\r\n", allRules);
        }

        private Rule UngenerateStaticRuleFromDyamicRule(Rule oldRule, DerivedCategory leftHandSideWithEmptyStack)
        {
            Rule derivedRule = null;
            foreach (var item in staticRules[leftHandSideWithEmptyStack])
            {
                if (item.NumberOfGeneratingRule == oldRule.Number)
                {
                    derivedRule = item;
                    break;
                }
            }
            return derivedRule;
        }

        public void DeleteGrammarRule(Rule oldRule)
        {
            var synCat = new SyntacticCategory(oldRule.LeftHandSide);
     
            var rules = dynamicRules[synCat];
            rules.Remove(oldRule);

            if (rules.Count == 0)
                dynamicRules.Remove(synCat);

            if (oldRule.RightHandSide[0].IsEpsilon())
                nullableCategories.Remove(oldRule.LeftHandSide);

            /*
            var leftHandSideWithEmptyStack = new DerivedCategory(synCat.ToString());
            var derivedRule = UngenerateStaticRuleFromDyamicRule(oldRule, leftHandSideWithEmptyStack);
            DeleteStaticRule(derivedRule);
            */
        }


        public void DeleteStaticRule(Rule r)
        {
            if (r == null) return;

            var LHS = r.LeftHandSide;
            var rulesWithSameLHS = staticRules[LHS];
            rulesWithSameLHS.Remove(r);

            if (rulesWithSameLHS.Count == 0)
            {
                staticRules.Remove(LHS);
                staticRulesGeneratedForCategory.Remove(LHS);
            }
        }

        public bool ContainsSameRHSRule(Rule newRule, SyntacticCategory[] PartsOfSpeechCategories)
        {
            bool bFoundIdentical = false;
            var poses = PartsOfSpeechCategories.Select(x=>x.ToString()).ToHashSet();
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
                        var b = (SyntacticCategory)newRule.RightHandSide[i];
                        string pos = b.ToString();
                        if (poses.Contains(pos))
                        {
                            if (!rule.RightHandSide[i].BaseEquals(newRule.RightHandSide[i]))
                                bFoundIdentical = false;
                        }
                        else
                        {
                            if (!rule.RightHandSide[i].Equals(newRule.RightHandSide[i]))
                                bFoundIdentical = false;
                        }
                    }
                    if (bFoundIdentical) break;
                }
            }

            return bFoundIdentical;
        }
        //private static bool IsDynamicRule(Rule r) =>
        //     r.LeftHandSide.Stack != null && r.LeftHandSide.Stack != string.Empty;

        public void AddGrammarRule(Rule r)
        {
            var newRule = new Rule(r);
            newRule.Number = ++ruleCounter;

            var newSynCat = new SyntacticCategory(newRule.LeftHandSide);
            if (!dynamicRules.ContainsKey(newSynCat))
                dynamicRules[newSynCat] = new List<Rule>();

            dynamicRules[newSynCat].Add(newRule);

            //TODO: calculate the transitive closure of all nullable symbols.
            //at the moment you calculate only the rules that directly lead to epsilon.
            //For instance. C -> D E, D -> epsilon, E-> epsilon. C is not in itself an epsilon rule
            //yet it is a nullable production.
            if (newRule.RightHandSide[0].IsEpsilon())
                nullableCategories.Add(newRule.LeftHandSide);

        }

        public void AddStaticRule(Rule r)
        {
            if (r == null) return;

            var newRule = new Rule(r);
            newRule.Number = ++ruleCounter;

            if (!staticRules.ContainsKey(newRule.LeftHandSide))
                staticRules[newRule.LeftHandSide] = new List<Rule>();

            staticRules[newRule.LeftHandSide].Add(newRule);

        }

        public void PruneUnusedRules(Dictionary<int, int> usagesDic)
        {
            var unusedRules = Rules.Where(x => !usagesDic.ContainsKey(x.Number)).ToArray();

            foreach (var rule in unusedRules)      
                DeleteGrammarRule(rule);
        }

        public Rule GenerateStaticRuleFromDyamicRule(Rule dynamicGrammarRule, DerivedCategory leftHandSide)
        {
            string patternStringLeftHandSide = dynamicGrammarRule.LeftHandSide.Stack;
            var newRule = new Rule(dynamicGrammarRule);
            newRule.NumberOfGeneratingRule = dynamicGrammarRule.Number;

            if (!patternStringLeftHandSide.Contains(Grammar.StarSymbol))
                return (dynamicGrammarRule.LeftHandSide.Equals(leftHandSide)) ? newRule : null;

            //if contains a stack with * symbol (dynamically sized stack)
            //1. make the pattern be your Syntactic Category
            //2. then find the stack contents - anything by "*" (the first group)
            string patternString = patternStringLeftHandSide.Replace(Grammar.StarSymbol, "(.*)");

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
                if (patternRightHandSide != string.Empty)
                {
                    string res = patternRightHandSide.Replace(Grammar.StarSymbol, stackContents);
                    newRule.RightHandSide[i].Stack = res;
                    newRule.RightHandSide[i].StackSymbolsCount += newRule.LeftHandSide.StackSymbolsCount;


                    if (newRule.RightHandSide[i].StackSymbolsCount > Grammar.maxStackDepth)
                        return null;
                }
            }

            return newRule;
        }

        public void RenameVariables()
        {
            var xs = dynamicRules.Keys.Where(x => x.ToString()[0] == 'X').ToList();
            var replacedx = new List<SyntacticCategory>();
            for (int i = 0; i < xs.Count; i++)
                replacedx.Add(new SyntacticCategory($"X{i + 1}"));
            var replaceDic = xs.Zip(replacedx, (x, y) => new { key = x, value = y }).ToDictionary(x => x.key, x => x.value);

            ReplaceVariables(replaceDic);

        }

        


        private void ReplaceVariables(Dictionary<SyntacticCategory, SyntacticCategory> replaceDic)
        {

            foreach (var rule in Rules)
            {
                var v = new SyntacticCategory(rule.LeftHandSide);
                if (replaceDic.ContainsKey(v))
                    rule.LeftHandSide.SetSymbol(replaceDic[v].ToString());

                for (int i = 0; i < rule.RightHandSide.Length; i++)
                {
                    v = new SyntacticCategory(rule.RightHandSide[i]);

                    if (replaceDic.ContainsKey(new SyntacticCategory(v)))
                        rule.RightHandSide[i].SetSymbol(replaceDic[v].ToString());

                }
            }
        }

        public void GenerateAllStaticRulesFromDynamicRules()
        {

            var gammaGrammarRule = new Rule(Grammar.GammaRule, new[] { Grammar.StartRule });
            AddStaticRule(gammaGrammarRule);

            //DO a BFS
            Queue<DerivedCategory> toVisit = new Queue<DerivedCategory>();
            HashSet<DerivedCategory> visited = new HashSet<DerivedCategory>();
            var startCat = new DerivedCategory(StartRule);
            toVisit.Enqueue(startCat);
            visited.Add(startCat);

            while (toVisit.Any())
            {
                var nextTerm = toVisit.Dequeue();

                //if static rules have not been generated for this term yet
                //compute them from dynamaic rules dictionary
                if (!staticRulesGeneratedForCategory.Contains(nextTerm))
                {
                    staticRulesGeneratedForCategory.Add(nextTerm);
                    var baseSyntacticCategory = new SyntacticCategory(nextTerm);

                    if (dynamicRules.ContainsKey(baseSyntacticCategory))
                    {
                        var grammarRuleList = dynamicRules[baseSyntacticCategory];
                        foreach (var item in grammarRuleList)
                        {
                            var derivedRule = GenerateStaticRuleFromDyamicRule(item, nextTerm);
                            AddStaticRule(derivedRule);

                            if (derivedRule != null)
                            {
                                foreach (var rhs in derivedRule.RightHandSide)
                                {
                                    if (!visited.Contains(rhs))
                                    {
                                        visited.Add(rhs);
                                        toVisit.Enqueue(rhs);
                                    }
                                }     
                            }
                        }
                    }
                }
            }
        }
    }
}