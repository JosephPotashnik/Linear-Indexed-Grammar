using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LinearIndexedGrammarParser
{
    public class ContextSensitiveGrammar : IDisposable
    {
        public readonly Dictionary<SyntacticCategory, List<Rule>> dynamicRules = new Dictionary<SyntacticCategory, List<Rule>>();
        private int ruleCounter = 0;

        public ContextSensitiveGrammar() { }

        public ContextSensitiveGrammar(ContextSensitiveGrammar otherGrammar)
        {
            dynamicRules = otherGrammar.dynamicRules.ToDictionary(x => x.Key, x => x.Value.Select(y => new Rule(y)).ToList());
            ruleCounter = 0;

            foreach (var rule in Rules)
                rule.Number = ++ruleCounter;
        }

        public IEnumerable<Rule> Rules
        {
            //Note: SelectMany here does not deep-copy, we get the reference to the grammar rules.
            get { return dynamicRules.Values.SelectMany(x => x); }
        }

        public override string ToString()
        {
            var allRules = Rules;
            return string.Join("\r\n", allRules);
        }

        public void DeleteGrammarRule(Rule oldRule)
        {
            var synCat = new SyntacticCategory(oldRule.LeftHandSide);

            var rules = dynamicRules[synCat];
            rules.Remove(oldRule);

            if (rules.Count == 0)
                dynamicRules.Remove(synCat);
        }

        public bool OnlyStartSymbolsRHS(Rule newRule)
        {
            bool onlyStartSymbols = true;
            DerivedCategory startCategory = new DerivedCategory(ContextFreeGrammar.StartRule);

            for (int i = 0; i < newRule.RightHandSide.Length; i++)
            {
                if (!newRule.RightHandSide[i].BaseEquals(startCategory))
                {
                    onlyStartSymbols = false;
                    break;
                }
            }

            return onlyStartSymbols;
        }

        public bool ContainsSameEpsilonRule(Rule newRule)
        {
            foreach (var rule in Rules)
            {
                if (rule.RightHandSide[0].IsEpsilon() && rule.LeftHandSide.BaseEquals(newRule.LeftHandSide))
                    return true;
            }
            return false;
        }
        public bool ContainsSameRHSRule(Rule newRule, SyntacticCategory[] PartsOfSpeechCategories)
        {
            bool bFoundIdentical = false;
            var poses = PartsOfSpeechCategories.Select(x => x.ToString()).ToHashSet();
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
                        if (!rule.RightHandSide[i].BaseEquals(newRule.RightHandSide[i]))
                            bFoundIdentical = false;

                    }
                    if (bFoundIdentical) break;
                }
            }

            return bFoundIdentical;
        }

        public void AddGrammarRule(Rule r)
        {
            var newRule = new Rule(r);
            newRule.Number = ++ruleCounter;

            var newSynCat = new SyntacticCategory(newRule.LeftHandSide);
            if (!dynamicRules.ContainsKey(newSynCat))
                dynamicRules[newSynCat] = new List<Rule>();

            dynamicRules[newSynCat].Add(newRule);

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

        public void PruneUnusedRules(Dictionary<int, int> usagesDic)
        {
            var unusedRules = Rules.Where(x => !usagesDic.ContainsKey(x.Number)).ToArray();

            foreach (var rule in unusedRules)
                DeleteGrammarRule(rule);
        }

        public void Dispose()
        {
            dynamicRules.Clear();
        }
    }
}
