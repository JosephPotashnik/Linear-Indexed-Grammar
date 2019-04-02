﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace LinearIndexedGrammarParser
{
    public class ContextFreeGrammar
    {
        public const string GammaRule = "Gamma";
        public const string StartSymbol = "START";
        public const string EpsilonSymbol = "Epsilon";
        public const string StarSymbol = "*";
        public const int MaxStackDepth = 3;
        public static HashSet<SyntacticCategory> PartsOfSpeech;

        public readonly Dictionary<DerivedCategory, List<Rule>> StaticRules =
            new Dictionary<DerivedCategory, List<Rule>>();

        public readonly HashSet<DerivedCategory> StaticRulesGeneratedForCategory = new HashSet<DerivedCategory>();

        public ContextFreeGrammar(List<Rule> ruleList)
        {
            ConstructCFG(ruleList);
        }

        public ContextFreeGrammar(ContextSensitiveGrammar cs)
        {
            var rules = ExtractRules(cs);
            ConstructCFG(rules);
        }

        public List<Rule> Rules
        {
            get { return StaticRules.Values.SelectMany(x => x).ToList(); }
        }

        private void ConstructCFG(IEnumerable<Rule> ruleList)
        {
            var rulesDic = CreateRulesDictionary(ruleList);
            GenerateAllStaticRulesFromDynamicRules(rulesDic);
        }

        public static IEnumerable<Rule> ExtractRules(ContextSensitiveGrammar cs)
        {
            IEnumerable<Rule> rules;
            var stackConstantRules = cs.StackConstantRules.Select(x => ContextSensitiveGrammar.RuleSpace[x]);
            if (cs.StackPush1Rules.Count > 0)
            {
                var stackChangingRules = cs.StackPush1Rules.Select(x => ContextSensitiveGrammar.RuleSpace[x]).ToList();
                foreach (var moveableKvp in cs.MoveableReferences)
                    if (moveableKvp.Value > 0) //if number of references to this moveable is positive
                    {
                        var rc = new RuleCoordinates //find moveable in pop rules table.
                        {
                            LHSIndex = moveableKvp.Key,
                            RHSIndex = 0,
                            RuleType = RuleType.PopRules
                        };

                        stackChangingRules.Add(ContextSensitiveGrammar.RuleSpace[rc]);
                    }

                rules = stackConstantRules.Concat(stackChangingRules);
            }
            else
            {
                rules = stackConstantRules;
            }

            return rules;
        }

        private static Dictionary<SyntacticCategory, List<Rule>> CreateRulesDictionary(IEnumerable<Rule> xy)
        {
            var rulesDic = new Dictionary<SyntacticCategory, List<Rule>>();

            foreach (var rule in xy)
            {
                var newSynCat = new SyntacticCategory(rule.LeftHandSide);
                if (!rulesDic.TryGetValue(newSynCat, out var rules))
                {
                    rules = new List<Rule>();
                    rulesDic.Add(newSynCat, rules);
                }

                rules.Add(new Rule(rule));
            }

            return rulesDic;
        }


        public override string ToString()
        {
            var allRules = StaticRules.Values.SelectMany(x => x);
            return string.Join("\r\n", allRules);
        }

        public void AddStaticRule(Rule r)
        {
            if (r == null) return;

            var newRule = new Rule(r);
            if (!StaticRules.TryGetValue(newRule.LeftHandSide, out var rules))
            {
                rules = new List<Rule>();
                StaticRules.Add(newRule.LeftHandSide, rules);
            }

            rules.Add(newRule);
        }


        public static Rule GenerateStaticRuleFromDynamicRule(Rule dynamicGrammarRule, DerivedCategory leftHandSide)
        {
            var patternStringLeftHandSide = dynamicGrammarRule.LeftHandSide.Stack;
            var newRule = new Rule(dynamicGrammarRule);

            if (!patternStringLeftHandSide.Contains(StarSymbol))
                return dynamicGrammarRule.LeftHandSide.Equals(leftHandSide) ? newRule : null;

            //if contains a stack with * symbol (dynamically sized stack)
            //1. make the pattern be your Syntactic Category
            //2. then find the stack contents - anything by "*" (the first group)
            var patternString = patternStringLeftHandSide.Replace(StarSymbol, "(.*)");

            var pattern = new Regex(patternString);

            var textToMatch = leftHandSide.Stack;
            var match = pattern.Match(textToMatch);
            if (!match.Success) return null;

            var stackContents = match.Groups[1].Value;
            newRule.LeftHandSide = leftHandSide;
            var posInRhsCount = 0;

            //3. replace the contents of the stack * in the right hand side productions.
            for (var i = 0; i < newRule.RightHandSide.Length; i++)
            {
                var patternRightHandSide = newRule.RightHandSide[i].Stack;
                if (patternRightHandSide != string.Empty)
                {
                    var res = patternRightHandSide.Replace(StarSymbol, stackContents);
                    newRule.RightHandSide[i].Stack = res;
                    newRule.RightHandSide[i].StackSymbolsCount += newRule.LeftHandSide.StackSymbolsCount;

                    if (newRule.RightHandSide[i].StackSymbolsCount > MaxStackDepth)
                        return null;

                    if (newRule.RightHandSide[i].StackSymbolsCount > 0
                        && PartsOfSpeech.Contains(newRule.RightHandSide[i]))
                        return null;
                }
                else
                {
                    posInRhsCount++;
                }
            }

            if (stackContents != string.Empty && posInRhsCount == newRule.RightHandSide.Length)
                return null;


            if (newRule.LeftHandSide.Stack.Length > 0)
            {
                var found = false;
                for (var i = 0; i < newRule.RightHandSide.Length; i++)
                    if (newRule.RightHandSide[i].Stack.Length > 0)
                        found = true;

                if (found == false) throw new Exception("illegal GenerateStaticRuleFromDynamicRule");
            }

            return newRule;
        }

        public void GenerateAllStaticRulesFromDynamicRules(Dictionary<SyntacticCategory, List<Rule>> dynamicRules)
        {
            var gammaGrammarRule = new Rule(GammaRule, new[] {StartSymbol});
            AddStaticRule(gammaGrammarRule);

            //DO a BFS
            var toVisit = new Queue<DerivedCategory>();
            var visited = new HashSet<DerivedCategory>();
            var startCat = new DerivedCategory(StartSymbol);
            toVisit.Enqueue(startCat);
            visited.Add(startCat);

            while (toVisit.Count > 0)
            {
                var nextTerm = toVisit.Dequeue();

                //if static rules have not been generated for this term yet
                //compute them from dynamic rules dictionary
                if (!StaticRulesGeneratedForCategory.Contains(nextTerm))
                {
                    StaticRulesGeneratedForCategory.Add(nextTerm);
                    var baseSyntacticCategory = new SyntacticCategory(nextTerm);

                    if (dynamicRules.TryGetValue(baseSyntacticCategory, out var grammarRuleList))
                        foreach (var item in grammarRuleList)
                        {
                            var derivedRule = GenerateStaticRuleFromDynamicRule(item, nextTerm);
                            AddStaticRule(derivedRule);

                            if (derivedRule != null)
                                foreach (var rhs in derivedRule.RightHandSide)
                                    if (!visited.Contains(rhs))
                                    {
                                        visited.Add(rhs);
                                        toVisit.Enqueue(rhs);
                                    }
                        }
                }
            }
        }


        public static void RenameVariables(List<Rule> rules, HashSet<string> partOfSpeechCategories)
        {
            var originalVariables = rules.Select(x => new SyntacticCategory(x.LeftHandSide).ToString()).ToList();
            originalVariables = originalVariables.Distinct().ToList();

            var replaceVariables = new List<string>();
            for (var i = 0; i < originalVariables.Count; i++)
                replaceVariables.Add($"X{i + 1}");

            foreach (var originalVariable in originalVariables)
                if (replaceVariables.Contains(originalVariable))
                    throw new Exception("renaming variables failed. Please do not use X1,X2,X3 nonterminals");
            var replaceDic = originalVariables.Zip(replaceVariables, (x, y) => new {key = x, value = y})
                .ToDictionary(x => x.key, x => x.value);

            var startRenamedVariable = replaceDic[StartSymbol];
            replaceDic.Remove(StartSymbol);
            ReplaceVariables(replaceDic, rules);

            var startCategory = new DerivedCategory(StartSymbol);
            var startRulesToReplace = new List<Rule>();
            foreach (var rule in rules)
            {
                if (rule.RightHandSide.Length == 2)
                    if (rule.LeftHandSide.BaseEquals(startCategory) ||
                        rule.RightHandSide[0].BaseEquals(startCategory) ||
                        rule.RightHandSide[1].BaseEquals(startCategory))
                        startRulesToReplace.Add(rule);

                if (rule.RightHandSide.Length == 1)
                {
                    var baseCat = new SyntacticCategory(rule.RightHandSide[0]);
                    if (partOfSpeechCategories.Contains(baseCat.ToString()))
                        startRulesToReplace.Add(rule);
                }
            }

            if (startRulesToReplace.Count > 0)
            {
                replaceDic[StartSymbol] = startRenamedVariable;
                ReplaceVariables(replaceDic, startRulesToReplace);
                var newStartRule = new Rule(StartSymbol, new[] {startRenamedVariable});
                rules.Add(newStartRule);
            }
        }


        private static void ReplaceVariables(Dictionary<string, string> replaceDic, IEnumerable<Rule> rules)
        {
            foreach (var rule in rules)
            {
                rule.LeftHandSide.Replace(replaceDic);

                for (var i = 0; i < rule.RightHandSide.Length; i++)
                    rule.RightHandSide[i].Replace(replaceDic);
            }
        }

        public static HashSet<(string rhs1, string rhs2)> GetBigramsOfData(string[][] data,
            Vocabulary universalVocabulary)
        {
            var bigrams = new HashSet<(string rhs1, string rhs2)>();

            foreach (var words in data)
                for (var i = 0; i < words.Length - 1; i++)
                {
                    var rhs1 = words[i];
                    var rhs2 = words[i + 1];

                    var possiblePOSforrhs1 = universalVocabulary.WordWithPossiblePOS[rhs1].ToArray();
                    var possiblePOSforrhs2 = universalVocabulary.WordWithPossiblePOS[rhs2].ToArray();

                    foreach (var pos1 in possiblePOSforrhs1)
                    foreach (var pos2 in possiblePOSforrhs2)
                        bigrams.Add((pos1, pos2));
                }

            return bigrams;
        }


        private bool ContainsCycle(DerivedCategory root, HashSet<DerivedCategory> visited,
            Dictionary<DerivedCategory, List<DerivedCategory>> dic)
        {
            if (visited.Contains(root)) return true;
            visited.Add(root);

            if (dic.TryGetValue(root, out var neighbors))
                foreach (var neighbor in neighbors)
                {
                    var containsCycle = ContainsCycle(neighbor, visited, dic);
                    if (containsCycle) return true;
                }

            return false;
        }

        public bool ContainsCyclicUnitProduction()
        {
            var possibleNullableCategories = ComputeTransitiveClosureOfNullableCategories();
            var allRules = StaticRules.Values.SelectMany(x => x);
            var unitProductions = new Dictionary<DerivedCategory, List<DerivedCategory>>();

            foreach (var r in allRules)
            {
                if (!unitProductions.TryGetValue(r.LeftHandSide, out var categories))
                {
                    categories = new List<DerivedCategory>();
                    unitProductions.Add(r.LeftHandSide, categories);
                }

                if (r.RightHandSide.Length == 1)
                    categories.Add(r.RightHandSide[0]);
                else if (possibleNullableCategories.Contains(r.RightHandSide[0]))
                    categories.Add(r.RightHandSide[1]);
                else if (possibleNullableCategories.Contains(r.RightHandSide[1]))
                    categories.Add(r.RightHandSide[0]);
            }

            foreach (var root in unitProductions.Keys)
            {
                var visited = new HashSet<DerivedCategory>();
                if (ContainsCycle(root, visited, unitProductions))
                    return true;
            }

            return false;
        }

        private HashSet<DerivedCategory> ComputeTransitiveClosureOfNullableCategories()
        {
            var allRules = StaticRules.Values.SelectMany(x => x).ToArray();
            var epsilonCat = new DerivedCategory(EpsilonSymbol);
            var possibleNullableCategories = new HashSet<DerivedCategory>();
            possibleNullableCategories.Add(epsilonCat);
            var added = true;

            while (added)
            {
                added = false;
                var toAddToPossibleNullableCategories = new List<DerivedCategory>();

                foreach (var r in allRules)
                    if (!possibleNullableCategories.Contains(r.LeftHandSide))
                        if (r.RightHandSide.All(x => possibleNullableCategories.Contains(x)))
                        {
                            added = true;
                            toAddToPossibleNullableCategories.Add(new DerivedCategory(r.LeftHandSide));
                        }

                foreach (var cat in toAddToPossibleNullableCategories)
                    possibleNullableCategories.Add(cat);
            }

            //the nullable categories here are for left hand side symbols checks,
            //(i.e, left-hand side nullable categories).
            //epsilon symbol appears only right hand; remove it. Epsilon was used internally above 
            //for the transitive closure only.
            possibleNullableCategories.Remove(epsilonCat);

            return possibleNullableCategories;
        }
    }
}