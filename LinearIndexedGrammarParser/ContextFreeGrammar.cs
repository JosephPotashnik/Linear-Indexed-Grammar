using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace LinearIndexedGrammarParser
{
    
    public class WordsTreesCounts
    {
        public int WordsCount { get; set; }
        public int TreesCount { get; set; }
    }
    public class ContextFreeGrammar : IDisposable
    {
        public ContextFreeGrammar() { }
        public const string GammaRule = "Gamma";
        public const string StartRule = "START";
        public const string EpsilonSymbol = "Epsilon";
        public const string StarSymbol = "*";
        public const int maxStackDepth = 3;

        public readonly Dictionary<DerivedCategory, List<Rule>> staticRules = new Dictionary<DerivedCategory, List<Rule>>();
        public readonly HashSet<DerivedCategory> staticRulesGeneratedForCategory = new HashSet<DerivedCategory>();
        public readonly HashSet<DerivedCategory> possibleNullableCategories = new HashSet<DerivedCategory>();
        public readonly HashSet<DerivedCategory> obligatoryNullableCategories = new HashSet<DerivedCategory>();

        private int ruleCounter = 0;

        public override string ToString()
        {
            var allRules = staticRules.Values.SelectMany(x => x); 
            return string.Join("\r\n", allRules);
        }

        public ContextFreeGrammar(ContextSensitiveGrammar cs)
        {
            ruleCounter = 0;
            var stackConstantRules = cs.stackConstantRules.ToDictionary(x => x.Key, x => x.Value.Select(y => new Rule(y)).ToList());
            var rulesDic = new Dictionary<SyntacticCategory, List<Rule>>(stackConstantRules);

            var xy = cs.StackChangingRules;

            foreach (var stackChangingRule in xy)
            {
                var newSynCat = new SyntacticCategory(stackChangingRule.LeftHandSide);
                if (!rulesDic.ContainsKey(newSynCat))
                    rulesDic[newSynCat] = new List<Rule>();

                rulesDic[newSynCat].Add(new Rule(stackChangingRule));
            }

            GenerateAllStaticRulesFromDynamicRules(rulesDic);
            ComputeTransitiveClosureOfNullableCategories();

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

        private bool ContainsCycle(DerivedCategory root, HashSet<DerivedCategory> visited, Dictionary<DerivedCategory, List<DerivedCategory>> dic)
        {
            if (visited.Contains(root)) return true;
            visited.Add(root);

            if (dic.ContainsKey(root))
            { 
                var neighbors = dic[root];
                foreach (var neighbor in neighbors)
                {
                    bool containsCycle = ContainsCycle(neighbor, visited, dic);
                    if (containsCycle) return true;
                }
            }

            return false;

        }
        public bool ContainsCyclicUnitPrdouction()
        {
            var allRules = staticRules.Values.SelectMany(x => x);
            Dictionary<DerivedCategory, List<DerivedCategory>> unitProductions = new Dictionary<DerivedCategory, List<DerivedCategory>>();

            foreach (var r in allRules)
            {
                if (!unitProductions.ContainsKey(r.LeftHandSide))
                    unitProductions[r.LeftHandSide] = new List<DerivedCategory>();

                if (r.RightHandSide.Length == 1)
                    unitProductions[r.LeftHandSide].Add(r.RightHandSide[0]);
                else if (possibleNullableCategories.Contains(r.RightHandSide[0]))
                    unitProductions[r.LeftHandSide].Add(r.RightHandSide[1]);
                else if (possibleNullableCategories.Contains(r.RightHandSide[1]))
                    unitProductions[r.LeftHandSide].Add(r.RightHandSide[0]);
            }

            foreach (var root in unitProductions.Keys)
            {
                HashSet<DerivedCategory> visited = new HashSet<DerivedCategory>();
                if (ContainsCycle(root, visited, unitProductions))
                    return true;
            }

            return false;
        }

        private void ComputeTransitiveClosureOfNullableCategories()
        {
            var allRules = staticRules.Values.SelectMany(x => x);
            var epsilonCat = new DerivedCategory(EpsilonSymbol);
            possibleNullableCategories.Add(epsilonCat);
            bool added = true;

            while (added)
            {
                added = false;
                List<DerivedCategory> toAddToPossibleNullableCategories = new List<DerivedCategory>();

                foreach (var r in allRules)
                {
                    if (!possibleNullableCategories.Contains(r.LeftHandSide))
                    {
                        if (r.RightHandSide.All(x => possibleNullableCategories.Contains(x)))
                        {
                            added = true;
                            toAddToPossibleNullableCategories.Add(new DerivedCategory(r.LeftHandSide));
                        }
                    }
                }

                foreach (var cat in toAddToPossibleNullableCategories)
                    possibleNullableCategories.Add(cat);                
            }

            //the nullable categories here are for left hand side symbols checks,
            //(i.e, left-hand side nullable categories).
            //epsilon symbol appears only right hand; remove it. Epsilon was used internally above 
            //for the transitive closure only.
            possibleNullableCategories.Remove(epsilonCat);

            obligatoryNullableCategories.Add(epsilonCat);
            added = true;

            while (added)
            {
                added = false;
                List<DerivedCategory> toAddToObligatoryNullableCategories = new List<DerivedCategory>();

                foreach (var cat in staticRules.Keys)
                {
                    if (!obligatoryNullableCategories.Contains(cat))
                    {
                        if (IsObligatoryNullableCategory(cat))
                        {
                            added = true;
                            toAddToObligatoryNullableCategories.Add(cat);
                        }
                    }
                }

                foreach (var cat in toAddToObligatoryNullableCategories)
                    obligatoryNullableCategories.Add(cat);
            }

        }

        private bool IsObligatoryNullableCategory(DerivedCategory cat)
        {
            return staticRules[cat].All(r => IsObligatoryNullableRule(r));
        }

        public bool IsObligatoryNullableRule(Rule r)
        {
            return r.RightHandSide.All(x => obligatoryNullableCategories.Contains(x));
        }

        public Rule GenerateStaticRuleFromDyamicRule(Rule dynamicGrammarRule, DerivedCategory leftHandSide)
        {
            string patternStringLeftHandSide = dynamicGrammarRule.LeftHandSide.Stack;
            var newRule = new Rule(dynamicGrammarRule);
            newRule.NumberOfGeneratingRule = dynamicGrammarRule.Number;

            if (!patternStringLeftHandSide.Contains(ContextFreeGrammar.StarSymbol))
                return (dynamicGrammarRule.LeftHandSide.Equals(leftHandSide)) ? newRule : null;

            //if contains a stack with * symbol (dynamically sized stack)
            //1. make the pattern be your Syntactic Category
            //2. then find the stack contents - anything by "*" (the first group)
            string patternString = patternStringLeftHandSide.Replace(ContextFreeGrammar.StarSymbol, "(.*)");

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
                    string res = patternRightHandSide.Replace(ContextFreeGrammar.StarSymbol, stackContents);
                    newRule.RightHandSide[i].Stack = res;
                    newRule.RightHandSide[i].StackSymbolsCount += newRule.LeftHandSide.StackSymbolsCount;

                    if (newRule.RightHandSide[i].StackSymbolsCount > ContextFreeGrammar.maxStackDepth)
                        return null;
                }
            }

            return newRule;
        }

        public void GenerateAllStaticRulesFromDynamicRules(Dictionary<SyntacticCategory, List<Rule>> dynamicRules)
        {

            var gammaGrammarRule = new Rule(ContextFreeGrammar.GammaRule, new[] { ContextFreeGrammar.StartRule });
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

        public void Dispose()
        {
            staticRules.Clear();
            staticRulesGeneratedForCategory.Clear();
            possibleNullableCategories.Clear();
            obligatoryNullableCategories.Clear();
        }
    }
}
