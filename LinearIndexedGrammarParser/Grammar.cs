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
            if (IsDynamicRule(oldRule))
            {
                var synCat = new SyntacticCategory(oldRule.LeftHandSide);
                if (!dynamicRules.ContainsKey(synCat))
                    throw new KeyNotFoundException($"syntactic category {synCat} was not found in dynamic rules dictionary");

                var rules = dynamicRules[synCat];
                if (!rules.Contains(oldRule))
                    throw new KeyNotFoundException($"rule {oldRule} was not found in dynamic rules dictionary");

                rules.Remove(oldRule);
                if (rules.Count == 0)
                    dynamicRules.Remove(synCat);

                var leftHandSideWithEmptyStack = new DerivedCategory(synCat.ToString());
                var derivedRule = UngenerateStaticRuleFromDyamicRule(oldRule, leftHandSideWithEmptyStack);
                DeleteStaticRule(derivedRule);
            }
            else
                DeleteStaticRule(oldRule);
        }


        public void DeleteStaticRule(Rule r)
        {
            if (r == null) return;

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
        private static bool IsDynamicRule(Rule r) =>
             r.LeftHandSide.Stack != null && r.LeftHandSide.Stack.Contains("*");

        public void AddGrammarRule(Rule r)
        {
            var newRule = new Rule(r);

            if (IsDynamicRule(newRule))
            {
                //if the left hand side allows manipulating the stack (has the wildcard)
                //insert into the stackManipulationRules dictionary.

                var newSynCat = new SyntacticCategory(newRule.LeftHandSide);
                if (!dynamicRules.ContainsKey(newSynCat))
                    dynamicRules[newSynCat] = new List<Rule>();

                dynamicRules[newSynCat].Add(newRule);

                var leftHandSideWithEmptyStack = new DerivedCategory(newSynCat.ToString());
                //generate base form of the rule with the empty stack
                //as a starting point of the grammar (= equal to context free case)
                var derivedRule = GenerateStaticRuleFromDyamicRule(newRule, leftHandSideWithEmptyStack);
                AddStaticRule(derivedRule);
            }
            else
                AddStaticRule(newRule);
        }

        public void AddStaticRule(Rule r)
        {
            if (r == null) return;

            staticRulesGeneratedForCategory.Add(r.LeftHandSide);

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

            newRule.NumberOfGeneratingRule = dynamicGrammarRule.Number;
            return newRule;
        }

        public void RenameStartVariable()
        {
            var startVariable = new DerivedCategory(StartRule);
            var newStartVariable = new DerivedCategory(StartRule + "TAG");
            var replaceDic = new Dictionary<DerivedCategory, DerivedCategory>();
            replaceDic[startVariable] = newStartVariable;
            ReplaceVariables(replaceDic);
            var newStartRule = new Rule(startVariable, new[]  { newStartVariable });
            AddGrammarRule(newStartRule);
        }


        public void RenameVariables()
        {
            var xs = staticRulesGeneratedForCategory.Where(x => x.ToString()[0] == 'X').Select(x => x).ToList();
            var replacedx = new List<DerivedCategory>();
            for (int i = 0; i < xs.Count; i++)
                replacedx.Add(new DerivedCategory($"X{i + 1}"));
            var replaceDic = xs.Zip(replacedx, (x, y) => new { key = x, value = y }).ToDictionary(x => x.key, x => x.value);

            //replace all StartTAG symbols with START 
            //(initially, we changed in the promiscuous grammar all occurrences of START with STARTTAG for simplicity)
            var originalStartVariable = new DerivedCategory(StartRule);
            var startVariable = new DerivedCategory(StartRule + "TAG");
            replaceDic[startVariable] = originalStartVariable;
            ReplaceVariables(replaceDic);

            DismissStartToStartTagRule(); //remove START -> START rule  (initially, added START -> STARTTAG to the promiscuous grammar for simplicity)
        }

        private void DismissStartToStartTagRule()
        {
            DerivedCategory originalStartVariable = new DerivedCategory(StartRule);
            var startRulesLHS = staticRules[originalStartVariable];
            bool foundStartToStartTagRule = false;
            Rule dismissedStartRule = null;

            foreach (var rule in startRulesLHS)
            {
                if (rule.RightHandSide[0].Equals(originalStartVariable))
                {
                    foundStartToStartTagRule = true;
                    dismissedStartRule = rule;
                    break;
                }
            }
            if (foundStartToStartTagRule)
                DeleteGrammarRule(dismissedStartRule);
        }


        private void ReplaceVariables(Dictionary<DerivedCategory, DerivedCategory> replaceDic)
        {
            List<Rule> newRules = new List<Rule>();
            List<Rule> deletedRules = new List<Rule>();
            foreach (var rule in Rules)
            {
                var newRule = new Rule(rule);
                bool created = false;
                if (replaceDic.ContainsKey(rule.LeftHandSide))
                {
                    newRule.LeftHandSide = replaceDic[rule.LeftHandSide];
                    created = true;
                }
                for (int i = 0; i < rule.RightHandSide.Length; i++)
                {
                    if (replaceDic.ContainsKey(rule.RightHandSide[i]))
                        newRule.RightHandSide[i] = replaceDic[rule.RightHandSide[i]];
                    created = true;
                }

                if (created)
                {
                    deletedRules.Add(rule);
                    newRules.Add(newRule);
                }
            }

            foreach (var rule in deletedRules)
                DeleteGrammarRule(rule);

            foreach (var rule in newRules)
                AddGrammarRule(rule);
        }
    }
}