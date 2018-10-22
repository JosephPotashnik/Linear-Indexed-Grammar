using System;
using System.Collections.Generic;
using System.Linq;

namespace LinearIndexedGrammarParser
{


    public enum MoveableOperationsKey
    {
        NoOp,
        Push1,
        Pop1,
        Pop2
    }

    public class MoveableOperations
    {
        public readonly Dictionary<MoveableOperationsKey, List<StackChangingRule>> MoveOps =
            new Dictionary<MoveableOperationsKey, List<StackChangingRule>>();

        public bool AddRule(StackChangingRule r, MoveableOperationsKey k, int ruleCounter, bool forceAdd = false)
        {
            if (!MoveOps.ContainsKey(k))
                MoveOps[k] = new List<StackChangingRule>();

            // ReSharper disable once CoVariantArrayConversion
            if (!forceAdd && ContextSensitiveGrammar.ContainsRule(r, MoveOps[k].ToArray())) return false;

            var newRule = new StackChangingRule(r) {Number = ++ruleCounter};
            MoveOps[k].Add(newRule);

            return true;
        }

        public void DeleteRule(StackChangingRule oldRule, MoveableOperationsKey k)
        {
            var rules = MoveOps[k];
            rules.Remove(oldRule);
        }

        public StackChangingRule GetRandomRule(MoveableOperationsKey k)
        {
            var rules = MoveOps[k];
            var rand = new Random();
            StackChangingRule randomRule = null;
            if (rules.Count > 0)
                randomRule = rules[rand.Next(rules.Count)];
            return randomRule;
        }
    }

    public class ContextSensitiveGrammar : IDisposable
    {
        public readonly Dictionary<SyntacticCategory, MoveableOperations> StackChangingRules =
            new Dictionary<SyntacticCategory, MoveableOperations>();

        public readonly Dictionary<SyntacticCategory, List<Rule>> StackConstantRules =
            new Dictionary<SyntacticCategory, List<Rule>>();

        private int _ruleCounter;

        public ContextSensitiveGrammar()
        {
        }

        public ContextSensitiveGrammar(ContextSensitiveGrammar otherGrammar)
        {
            StackConstantRules =
                otherGrammar.StackConstantRules.ToDictionary(x => x.Key,
                    x => x.Value.Select(y => new Rule(y)).ToList());
            _ruleCounter = 0;

            foreach (var rule in StackConstantRulesArray)
                rule.Number = ++_ruleCounter;

            foreach (var moveable in otherGrammar.StackChangingRules.Keys)
            {
                StackChangingRules[moveable] = new MoveableOperations();
                foreach (var moveOp in otherGrammar.StackChangingRules[moveable].MoveOps.Keys)
                {
                    StackChangingRules[moveable].MoveOps[moveOp] = new List<StackChangingRule>();

                    foreach (var rule in otherGrammar.StackChangingRules[moveable].MoveOps[moveOp])
                    {
                        var newRule = new StackChangingRule(rule) {Number = ++_ruleCounter};
                        StackChangingRules[moveable].MoveOps[moveOp].Add(newRule);
                    }
                }
            }
        }

        public SyntacticCategory[] LHSCategories
        {
            //LHS categories are all LHS categories of the stack constant rules,
            // plus all LHS from non-epsilon rules of the stack changing rules 
            //                                  X3[*NP] -> NP[NP] VP[*] (pop)
            //(i,e, X1 or X3 from rules such as X1[*] -> NP X2[*NP] (push)

            //the epsilon rule of the movement operation pop1 such as NP[NP] -> epsilon
            //is not a rule that serves as a possible development of the grammar. why?
            //because it is intended to terminate with epsilon by definition, and not
            // have its LHS to be with a subtree of arbitrary depth.
            get
            {
                var lhsCategories = StackConstantRules.Keys.ToHashSet();

                var xy = StackChangingRules.Values.SelectMany(x => x.MoveOps.Values);
                var xyz = xy.SelectMany(x => x).Where(x => !x.IsEpsilonRule());
                foreach (var rule in xyz) lhsCategories.Add(new SyntacticCategory(rule.LeftHandSide));

                return lhsCategories.ToArray();
            }
        }

        public Rule[] StackConstantRulesArray
        {
            //Note: SelectMany here does not deep-copy, we get the reference to the grammar rules.
            get { return StackConstantRules.Values.SelectMany(x => x).ToArray(); }
        }

        public StackChangingRule[] StackChangingRulesArray
        {
            //Note: SelectMany here does not deep-copy, we get the reference to the grammar rules.
            get { return StackChangingRules.Values.SelectMany(x => x.MoveOps.Values).SelectMany(x => x).ToArray(); }
        }

        public void Dispose()
        {
            StackConstantRules.Clear();
        }


        public override string ToString()
        {
            var s1 = "Stack Constant Rules:\r\n" +
                     string.Join("\r\n", StackConstantRulesArray.Select(x => x.ToString()));
            var s2 = "Stack Changing Rules:\r\n" +
                     string.Join("\r\n", StackChangingRulesArray.Select(x => x.ToString()));
            return s1 + "\r\n" + s2;
        }

        public void DeleteStackConstantRule(Rule oldRule)
        {
            var synCat = new SyntacticCategory(oldRule.LeftHandSide);

            var rules = StackConstantRules[synCat];
            rules.Remove(oldRule);

            if (rules.Count == 0)
                StackConstantRules.Remove(synCat);
        }

        public bool OnlyStartSymbolsRHS(Rule newRule)
        {
            var onlyStartSymbols = true;
            var startCategory = new DerivedCategory(ContextFreeGrammar.StartRule);

            for (var i = 0; i < newRule.RightHandSide.Length; i++)
                if (!newRule.RightHandSide[i].BaseEquals(startCategory))
                {
                    onlyStartSymbols = false;
                    break;
                }

            return onlyStartSymbols;
        }

        public static bool ContainsRule(Rule newRule, Rule[] ruleList)
        {
            var bFoundIdentical = false;
            //assuming compositionality.
            // if found rule with the same right hand side, do not re-add it.
            //the nonterminal of the left hand side does not matter.

            if (!newRule.RightHandSide[0].IsEpsilon())
                foreach (var rule in ruleList)
                    if (!rule.RightHandSide[0].IsEpsilon() && rule.RightHandSide.Length == newRule.RightHandSide.Length)
                    {
                        bFoundIdentical = true;
                        for (var i = 0; i < rule.RightHandSide.Length; i++)
                            if (!rule.RightHandSide[i].BaseEquals(newRule.RightHandSide[i]))
                                bFoundIdentical = false;
                        if (bFoundIdentical) break;
                    }
                    else
                    {
                        foreach (var rule1 in ruleList)
                            if (rule1.RightHandSide[0].IsEpsilon() &&
                                rule1.LeftHandSide.BaseEquals(newRule.LeftHandSide))
                            {
                                bFoundIdentical = true;
                                break;
                            }
                    }

            return bFoundIdentical;
        }

        public bool AddStackConstantRule(Rule r, bool forceAdd = false)
        {
            if (!forceAdd && ContainsRule(r, StackConstantRulesArray)) return false;

            var newRule = new Rule(r) {Number = ++_ruleCounter};

            var newSynCat = new SyntacticCategory(newRule.LeftHandSide);
            if (!StackConstantRules.ContainsKey(newSynCat))
                StackConstantRules[newSynCat] = new List<Rule>();

            StackConstantRules[newSynCat].Add(newRule);
            return true;
        }

        public bool AddStackChangingRule(SyntacticCategory moveable, StackChangingRule r, MoveableOperationsKey key,
            bool forceAdd = false)
        {
            if (!StackChangingRules.ContainsKey(moveable))
                StackChangingRules[moveable] = new MoveableOperations();

            var isAdded = StackChangingRules[moveable].AddRule(r, key, _ruleCounter, forceAdd);
            if (isAdded) _ruleCounter++;
            return isAdded;
        }

        public void DeleteStackChangingRule(SyntacticCategory moveable, StackChangingRule oldRule,
            MoveableOperationsKey key)
        {
            StackChangingRules[moveable].DeleteRule(oldRule, key);
        }

        public void DeleteStackChangingRule(StackChangingRule oldRule)
        {
            foreach (var moveable in StackChangingRules.Keys)
            {
                var moveops = StackChangingRules[moveable];
                foreach (var opKey in moveops.MoveOps.Keys)
                    DeleteStackChangingRule(moveable, oldRule, opKey);
            }
        }

        public void RenameVariables()
        {
            var xs = LHSCategories.Select(x => x.ToString()).Where(x => x[0] == 'X').ToList();
            var replacedx = new List<string>();
            for (var i = 0; i < xs.Count; i++)
                replacedx.Add($"X{i + 1}");
            var replaceDic = xs.Zip(replacedx, (x, y) => new {key = x, value = y})
                .ToDictionary(x => x.key, x => x.value);
            ReplaceVariables(replaceDic);
        }


        private void ReplaceVariables(Dictionary<string, string> replaceDic)
        {
            var joinedSequences = StackConstantRulesArray.Concat(StackChangingRulesArray);

            foreach (var rule in joinedSequences)
            {
                rule.LeftHandSide.Replace(replaceDic);

                for (var i = 0; i < rule.RightHandSide.Length; i++)
                    rule.RightHandSide[i].Replace(replaceDic);
            }
        }

        public void PruneUnusedRules(Dictionary<int, int> usagesDic)
        {
            var unusedConstantRules = StackConstantRulesArray.Where(x => !usagesDic.ContainsKey(x.Number)).ToArray();

            foreach (var rule in unusedConstantRules)
                DeleteStackConstantRule(rule);

            var unusedStackRules = StackChangingRulesArray.Where(x => !usagesDic.ContainsKey(x.Number)).ToArray();

            foreach (var rule in unusedStackRules)
                DeleteStackChangingRule(rule);
        }
    }
}