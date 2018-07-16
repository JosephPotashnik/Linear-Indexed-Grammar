using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace LinearIndexedGrammarParser
{
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

        public Grammar(Grammar otherGrammar)
        {
            staticRules = otherGrammar.staticRules.ToDictionary(x => x.Key, x => x.Value.Select(y => new Rule(y)).ToList());
            dynamicRules = otherGrammar.dynamicRules.ToDictionary(x => x.Key, x => x.Value.Select(y => new Rule(y)).ToList());
            staticRulesGeneratedForCategory = new HashSet<DerivedCategory>(otherGrammar.staticRulesGeneratedForCategory);
            nullableCategories = new HashSet<DerivedCategory>(otherGrammar.nullableCategories);

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

            if (rulesWithSameLHS.Count == 0)
            {
                staticRules.Remove(LHS);
                staticRulesGeneratedForCategory.Remove(LHS);

            }

        }

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
                    var derivedRule = GenerateRule(newRule, emptyStackRule);
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

            //TODO: calculate the transitive closure of all nullable symbols.
            //at the moment you calculate only the rules that directly lead to epsilon.
            //For instance. C -> D E, D -> epsilon, E-> epsilon. C is not in itself an epsilon rule
            //yet it is a nullable production.
            if (newRule.RightHandSide[0].IsEpsilon())
                nullableCategories.Add(newRule.LeftHandSide);
        }

        public Rule GenerateRule(Rule grammarRule, DerivedCategory leftHandSide)
        {
            if (grammarRule.LeftHandSide.Stack == null || grammarRule.LeftHandSide.Stack == string.Empty)
                return null;

            string patternStringLeftHandSide = grammarRule.LeftHandSide.Stack;

            //1. make the pattern be your Syntactic Category
            //2. then find the stack contents - anything by "*" (the first group)
            var newRule = new Rule(grammarRule);
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
    }
}