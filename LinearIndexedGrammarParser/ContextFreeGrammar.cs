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
        public const string EpsislonSymbol = "Epsilon";
        public const string StarSymbol = "*";
        public const int maxStackDepth = 3;

        public readonly Dictionary<DerivedCategory, List<Rule>> staticRules = new Dictionary<DerivedCategory, List<Rule>>();
        public readonly HashSet<DerivedCategory> staticRulesGeneratedForCategory = new HashSet<DerivedCategory>();
        public readonly HashSet<DerivedCategory> nullableCategories = new HashSet<DerivedCategory>();
        private int ruleCounter = 0;

        public override string ToString()
        {
            var allRules = staticRules.Values.SelectMany(x => x); 
            return string.Join("\r\n", allRules);
        }

        public ContextFreeGrammar(ContextSensitiveGrammar cs)
        {
            GenerateAllStaticRulesFromDynamicRules(cs.stackConstantRules);
        }
        public ContextFreeGrammar(ContextFreeGrammar otherGrammar)
        {
            nullableCategories = new HashSet<DerivedCategory>(otherGrammar.nullableCategories);
            ruleCounter = 0;

        }

        public void AddStaticRule(Rule r)
        {
            if (r == null) return;

            var newRule = new Rule(r);
            newRule.Number = ++ruleCounter;

            if (!staticRules.ContainsKey(newRule.LeftHandSide))
                staticRules[newRule.LeftHandSide] = new List<Rule>();

            staticRules[newRule.LeftHandSide].Add(newRule);

            //TODO: calculate the transitive closure of all nullable symbols.
            //at the moment you calculate only the rules that directly lead to epsilon.
            //For instance. C -> D E, D -> epsilon, E-> epsilon. C is not in itself an epsilon rule
            //yet it is a nullable production.
            if (newRule.RightHandSide[0].IsEpsilon())
                nullableCategories.Add(newRule.LeftHandSide);
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
            nullableCategories.Clear();
        }
    }
}
