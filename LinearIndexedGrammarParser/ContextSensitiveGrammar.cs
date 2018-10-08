using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LinearIndexedGrammarParser
{

    public enum MoveableOperationsKey
    {
        Push1,
        Pop1,
        Pop2
    }

    public class MoveableOperations
    {
        public readonly Dictionary<MoveableOperationsKey, List<StackChangingRule>> MoveOps = new Dictionary<MoveableOperationsKey, List<StackChangingRule>>();

        public bool AddRule(StackChangingRule r, MoveableOperationsKey k, int ruleCounter)
        {
            if (!MoveOps.ContainsKey(k))
                MoveOps[k] = new List<StackChangingRule>();

            if (ContextSensitiveGrammar.ContainsRule(r, MoveOps[k])) return false;

            StackChangingRule newRule = new StackChangingRule(r);
            newRule.Number = ++ruleCounter;
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
        public readonly Dictionary<SyntacticCategory, MoveableOperations> stackChangingRules = new Dictionary<SyntacticCategory, MoveableOperations>();
        public readonly Dictionary<SyntacticCategory, List<Rule>> stackConstantRules = new Dictionary<SyntacticCategory, List<Rule>>();
        private int ruleCounter = 0;

        public ContextSensitiveGrammar() { }

        public SyntacticCategory[] LHSCategories
        {
            //LHS categories are all LHS categories of the stack constant rules,
            // plus all LHS from non-epsilon rules of the stack changing rules 
            //(i,e, X1 or X3 from rules such as X1[*] -> NP X2[*NP] (push)
            //                                  X3[*NP] -> NP[NP] VP[*] (pop)

            //the epsilon rule of the movement operation pop1 such as NP[NP] -> epsilon
            //is not a rule that serves as a possible development of the grammar. why?
            //because it is intended to terminate with epsilon by definition, and not
            // have its LHS to be with a subtree of arbitrary depth.
            get
            {
                var lhsCategories = stackConstantRules.Keys.ToHashSet();

                var xy = stackChangingRules.Values.SelectMany(x => x.MoveOps.Values);
                var xyz = xy.SelectMany(x => x).Where(x => !x.IsEpsilonRule());
                foreach (var rule in xyz)
                {
                    lhsCategories.Add(new SyntacticCategory(rule.LeftHandSide));
                }

                return lhsCategories.ToArray();
            }
        }

        public ContextSensitiveGrammar(ContextSensitiveGrammar otherGrammar)
        {
            stackConstantRules = otherGrammar.stackConstantRules.ToDictionary(x => x.Key, x => x.Value.Select(y => new Rule(y)).ToList());
            ruleCounter = 0;

            foreach (var rule in StackConstantRules)
                rule.Number = ++ruleCounter;

            foreach (var moveable in otherGrammar.stackChangingRules.Keys)
            {
                stackChangingRules[moveable] = new MoveableOperations();
                foreach (var moveOp in otherGrammar.stackChangingRules[moveable].MoveOps.Keys)
                {
                    stackChangingRules[moveable].MoveOps[moveOp] = new List<StackChangingRule>();

                    foreach (var rule in otherGrammar.stackChangingRules[moveable].MoveOps[moveOp])
                    {
                        var newRule = new StackChangingRule(rule);
                        newRule.Number = ++ruleCounter;
                        stackChangingRules[moveable].MoveOps[moveOp].Add(newRule);
                    }
                }
            }
        }

        public IEnumerable<Rule> StackConstantRules
        {
            //Note: SelectMany here does not deep-copy, we get the reference to the grammar rules.
            get { return stackConstantRules.Values.SelectMany(x => x); }
        }

        public IEnumerable<Rule> StackChangingRules
        {
            //Note: SelectMany here does not deep-copy, we get the reference to the grammar rules.
            get { return stackChangingRules.Values.SelectMany(x => x.MoveOps.Values).SelectMany(x => x); }
        }


        public override string ToString()
        {
            var allConstantRules = StackConstantRules;
            string s1 = "Stack Constant Rules:\r\n" + string.Join("\r\n", StackConstantRules);
            string s2 = "Stack Changing Rules:\r\n" + string.Join("\r\n", StackChangingRules);
            return s1 + "\r\n" + s2;
        }

        public void DeleteStackConstantRule(Rule oldRule)
        {
            var synCat = new SyntacticCategory(oldRule.LeftHandSide);

            var rules = stackConstantRules[synCat];
            rules.Remove(oldRule);

            if (rules.Count == 0)
                stackConstantRules.Remove(synCat);
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
        
        public static bool ContainsRule(Rule newRule, IEnumerable<Rule> ruleList)
        {
            bool bFoundIdentical = false;
            //assuming compositionality.
            // if found rule with the same right hand side, do not re-add it.
            //the nonterminal of the left hand side does not matter.

            if (!newRule.RightHandSide[0].IsEpsilon())
            {
                foreach (var rule in ruleList)
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
            }
            else
            {

                foreach (var rule in ruleList)
                {
                    if (rule.RightHandSide[0].IsEpsilon() && rule.LeftHandSide.BaseEquals(newRule.LeftHandSide))
                    {
                        bFoundIdentical = true;
                        break;
                    }
                }

            }
            return bFoundIdentical;
        }

        public bool AddStackConstantRule(Rule r, bool forceAdd = false)
        {
            if (!forceAdd && ContainsRule(r, StackConstantRules)) return false;

            var newRule = new Rule(r);
            newRule.Number = ++ruleCounter;

            var newSynCat = new SyntacticCategory(newRule.LeftHandSide);
            if (!stackConstantRules.ContainsKey(newSynCat))
                stackConstantRules[newSynCat] = new List<Rule>();

            stackConstantRules[newSynCat].Add(newRule);
            return true;
        }

        public bool AddStackChangingRule(SyntacticCategory moveable, StackChangingRule r, MoveableOperationsKey key)
        {
            if (!stackChangingRules.ContainsKey(moveable))
                stackChangingRules[moveable] = new MoveableOperations();

            return stackChangingRules[moveable].AddRule(r, key, ruleCounter);
        }

        public void DeleteStackChangingRule(SyntacticCategory moveable, StackChangingRule oldRule, MoveableOperationsKey key)
        {
            stackChangingRules[moveable].DeleteRule(oldRule, key);
        }

        public void RenameVariables()
        {
            var xs = stackConstantRules.Keys.Where(x => x.ToString()[0] == 'X').ToList();
            var replacedx = new List<SyntacticCategory>();
            for (int i = 0; i < xs.Count; i++)
                replacedx.Add(new SyntacticCategory($"X{i + 1}"));
            var replaceDic = xs.Zip(replacedx, (x, y) => new { key = x, value = y }).ToDictionary(x => x.key, x => x.value);
            ReplaceVariables(replaceDic);
        }


        private void ReplaceVariables(Dictionary<SyntacticCategory, SyntacticCategory> replaceDic)
        {

            foreach (var rule in StackConstantRules)
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
            var unusedRules = StackConstantRules.Where(x => !usagesDic.ContainsKey(x.Number)).ToArray();

            foreach (var rule in unusedRules)
                DeleteStackConstantRule(rule);
        }

        public void Dispose()
        {
            stackConstantRules.Clear();
        }
    }
}
