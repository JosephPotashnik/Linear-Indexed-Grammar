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

    public class ContextFreeGrammar
    {
        public const string GammaRule = "Gamma";
        public const string StartRule = "START";
        public const string EpsilonSymbol = "Epsilon";
        public const string StarSymbol = "*";
        public const int MaxStackDepth = 3;
        public readonly HashSet<DerivedCategory> ObligatoryNullableCategories = new HashSet<DerivedCategory>();
        public readonly HashSet<DerivedCategory> PossibleNullableCategories = new HashSet<DerivedCategory>();

        public readonly Dictionary<DerivedCategory, List<Rule>> StaticRules =
            new Dictionary<DerivedCategory, List<Rule>>();

        public readonly HashSet<DerivedCategory> StaticRulesGeneratedForCategory = new HashSet<DerivedCategory>();

        private int _ruleCounter;

        public ContextFreeGrammar()
        {
        }

        public ContextFreeGrammar(Rule[] ruleList)
        {
            var rulesDic = CreateRulesDictionary(ruleList);
            GenerateAllStaticRulesFromDynamicRules(rulesDic);
            ComputeTransitiveClosureOfNullableCategories();
        }

        public ContextFreeGrammar(ContextSensitiveGrammar cs)
        {
            var xy1 = cs.StackConstantRules.Select(x => ContextSensitiveGrammar.RuleSpace[x]);
            var xy2 = cs.StackChangingRules.Select(x => ContextSensitiveGrammar.RuleSpace[x]);
            var rulesArr = xy1.Concat(xy2);

            var rulesDic = CreateRulesDictionary(rulesArr);
            GenerateAllStaticRulesFromDynamicRules(rulesDic);
            ComputeTransitiveClosureOfNullableCategories();
        }

        private static Dictionary<SyntacticCategory, List<Rule>> CreateRulesDictionary(IEnumerable<Rule> xy)
        {
            var rulesDic = new Dictionary<SyntacticCategory, List<Rule>>();

            foreach (var rule in xy)
            {
                var newSynCat = new SyntacticCategory(rule.LeftHandSide);
                if (!rulesDic.ContainsKey(newSynCat))
                    rulesDic[newSynCat] = new List<Rule>();

                rulesDic[newSynCat].Add(new Rule(rule));
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

            var newRule = new Rule(r) {Number = ++_ruleCounter};

            if (!StaticRules.ContainsKey(newRule.LeftHandSide))
                StaticRules[newRule.LeftHandSide] = new List<Rule>();

            StaticRules[newRule.LeftHandSide].Add(newRule);
        }

        private bool ContainsCycle(DerivedCategory root, HashSet<DerivedCategory> visited,
            Dictionary<DerivedCategory, List<DerivedCategory>> dic)
        {
            if (visited.Contains(root)) return true;
            visited.Add(root);

            if (dic.ContainsKey(root))
            {
                var neighbors = dic[root];
                foreach (var neighbor in neighbors)
                {
                    var containsCycle = ContainsCycle(neighbor, visited, dic);
                    if (containsCycle) return true;
                }
            }

            return false;
        }

        public bool ContainsCyclicUnitProduction()
        {
            var allRules = StaticRules.Values.SelectMany(x => x);
            var unitProductions = new Dictionary<DerivedCategory, List<DerivedCategory>>();

            foreach (var r in allRules)
            {
                if (!unitProductions.ContainsKey(r.LeftHandSide))
                    unitProductions[r.LeftHandSide] = new List<DerivedCategory>();

                if (r.RightHandSide.Length == 1)
                    unitProductions[r.LeftHandSide].Add(r.RightHandSide[0]);
                else if (PossibleNullableCategories.Contains(r.RightHandSide[0]))
                    unitProductions[r.LeftHandSide].Add(r.RightHandSide[1]);
                else if (PossibleNullableCategories.Contains(r.RightHandSide[1]))
                    unitProductions[r.LeftHandSide].Add(r.RightHandSide[0]);
            }

            foreach (var root in unitProductions.Keys)
            {
                var visited = new HashSet<DerivedCategory>();
                if (ContainsCycle(root, visited, unitProductions))
                    return true;
            }

            return false;
        }

        private void ComputeTransitiveClosureOfNullableCategories()
        {
            var allRules = StaticRules.Values.SelectMany(x => x).ToArray();
            var epsilonCat = new DerivedCategory(EpsilonSymbol);
            PossibleNullableCategories.Add(epsilonCat);
            var added = true;

            while (added)
            {
                added = false;
                var toAddToPossibleNullableCategories = new List<DerivedCategory>();

                foreach (var r in allRules)
                    if (!PossibleNullableCategories.Contains(r.LeftHandSide))
                        if (r.RightHandSide.All(x => PossibleNullableCategories.Contains(x)))
                        {
                            added = true;
                            toAddToPossibleNullableCategories.Add(new DerivedCategory(r.LeftHandSide));
                        }

                foreach (var cat in toAddToPossibleNullableCategories)
                    PossibleNullableCategories.Add(cat);
            }

            //the nullable categories here are for left hand side symbols checks,
            //(i.e, left-hand side nullable categories).
            //epsilon symbol appears only right hand; remove it. Epsilon was used internally above 
            //for the transitive closure only.
            PossibleNullableCategories.Remove(epsilonCat);

            ObligatoryNullableCategories.Add(epsilonCat);
            added = true;

            while (added)
            {
                added = false;
                var toAddToObligatoryNullableCategories = new List<DerivedCategory>();

                foreach (var cat in StaticRules.Keys)
                    if (!ObligatoryNullableCategories.Contains(cat))
                        if (IsObligatoryNullableCategory(cat))
                        {
                            added = true;
                            toAddToObligatoryNullableCategories.Add(cat);
                        }

                foreach (var cat in toAddToObligatoryNullableCategories)
                    ObligatoryNullableCategories.Add(cat);
            }
        }

        private bool IsObligatoryNullableCategory(DerivedCategory cat)
        {
            return StaticRules[cat].All(r => IsObligatoryNullableRule(r));
        }

        public bool IsObligatoryNullableRule(Rule r)
        {
            return r.RightHandSide.All(x => ObligatoryNullableCategories.Contains(x));
        }

        public Rule GenerateStaticRuleFromDyamicRule(Rule dynamicGrammarRule, DerivedCategory leftHandSide)
        {
            var patternStringLeftHandSide = dynamicGrammarRule.LeftHandSide.Stack;
            var newRule = new Rule(dynamicGrammarRule) {NumberOfGeneratingRule = dynamicGrammarRule.Number};

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
                }
            }

            return newRule;
        }

        public void GenerateAllStaticRulesFromDynamicRules(Dictionary<SyntacticCategory, List<Rule>> dynamicRules)
        {
            var gammaGrammarRule = new Rule(GammaRule, new[] {StartRule});
            AddStaticRule(gammaGrammarRule);

            //DO a BFS
            var toVisit = new Queue<DerivedCategory>();
            var visited = new HashSet<DerivedCategory>();
            var startCat = new DerivedCategory(StartRule);
            toVisit.Enqueue(startCat);
            visited.Add(startCat);

            while (toVisit.Any())
            {
                var nextTerm = toVisit.Dequeue();

                //if static rules have not been generated for this term yet
                //compute them from dynamaic rules dictionary
                if (!StaticRulesGeneratedForCategory.Contains(nextTerm))
                {
                    StaticRulesGeneratedForCategory.Add(nextTerm);
                    var baseSyntacticCategory = new SyntacticCategory(nextTerm);

                    if (dynamicRules.ContainsKey(baseSyntacticCategory))
                    {
                        var grammarRuleList = dynamicRules[baseSyntacticCategory];
                        foreach (var item in grammarRuleList)
                        {
                            var derivedRule = GenerateStaticRuleFromDyamicRule(item, nextTerm);
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
        }
    }
}