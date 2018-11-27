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
            //if (!forceAdd && ContextSensitiveGrammar.ContainsRule(r, MoveOps[k].ToArray())) return false;

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

    
    public class RuleCoordinates
    {
        //the LHS index in the Rule Space (index 0 = LHS "X1", index 1 = LHS "X2", etc)
        public int LHSIndex { get; set; }
        //the RHS index in the Rule Space ( same RHS index for different LHS indices means they share the same RHS)
        public int RHSIndex { get; set; }

        public RuleCoordinates() { }
        public RuleCoordinates(RuleCoordinates other)
        {
            LHSIndex = other.LHSIndex;
            RHSIndex = other.RHSIndex;
        }

        public override bool Equals(object obj)
        {
            if (obj is RuleCoordinates other)
                return (LHSIndex == other.LHSIndex && RHSIndex == other.RHSIndex);

            return false;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 23 + LHSIndex;
                hash = hash * 23 + RHSIndex;
                return hash;
            }
        }
    }

    public class ContextSensitiveGrammar
    {
        //Rule space is a 2D array,
        //first index is LHS ([0] = "X1", [1] = "X2", [2] = "X3" etc)
        //second index is RHS, such that [0][i] = [1][i] = [2][i] etc. 
        public static RuleSpace RuleSpace;

        public readonly Dictionary<SyntacticCategory, MoveableOperations> StackChangingRules =
            new Dictionary<SyntacticCategory, MoveableOperations>();

        public readonly List<RuleCoordinates> StackConstantRules = new List<RuleCoordinates>();

        public ContextSensitiveGrammar()
        {
                
        }
        public ContextSensitiveGrammar(Rule[] grammarRules)
        {
            foreach (var rule in grammarRules)
            {
                if (rule is StackChangingRule r)
                {

                }
                else
                {
                    var rc = RuleSpace.FindRule(rule);
                    StackConstantRules.Add(rc);
                }
            }
        }

        public ContextSensitiveGrammar(ContextSensitiveGrammar otherGrammar)
        {
            StackConstantRules = otherGrammar.StackConstantRules.Select(x => new RuleCoordinates(x)).ToList();

            foreach (var moveable in otherGrammar.StackChangingRules.Keys)
            {
                StackChangingRules[moveable] = new MoveableOperations();
                foreach (var moveOp in otherGrammar.StackChangingRules[moveable].MoveOps.Keys)
                {
                    StackChangingRules[moveable].MoveOps[moveOp] = new List<StackChangingRule>();

                    foreach (var rule in otherGrammar.StackChangingRules[moveable].MoveOps[moveOp])
                    {
                        var newRule = new StackChangingRule(rule);
                        StackChangingRules[moveable].MoveOps[moveOp].Add(newRule);
                    }
                }
            }
        }
        
        public StackChangingRule[] StackChangingRulesArray
        {
            //Note: SelectMany here does not deep-copy, we get the reference to the grammar rules.
            get { return StackChangingRules.Values.SelectMany(x => x.MoveOps.Values).SelectMany(x => x).ToArray(); }
        }

        
        public override string ToString()
        {
            var s1 = "Stack Constant Rules:\r\n" +
                     string.Join("\r\n", StackConstantRules.Select(x => RuleSpace[x].ToString()));
            var s2 = "Stack Changing Rules:\r\n" +
                     string.Join("\r\n", StackChangingRulesArray.Select(x => x.ToString()));
            return s1 + "\r\n" + s2;
        }

        public void AddStackConstantRule(RuleCoordinates rc) => StackConstantRules.Add(rc);
        public void DeleteStackConstantRule(RuleCoordinates rc) => StackConstantRules.Remove(rc);

        public RuleCoordinates GetRandomStackConstantRule()
        {
            var rand = ThreadSafeRandom.ThisThreadsRandom;
            return StackConstantRules[rand.Next(StackConstantRules.Count)];
        }

        //if there is a rule that has the same RHS side, i.e. the same RHS index
        public bool ContainsRuleWithSameRHS(RuleCoordinates rc)
        {
            //RHSIndex 0 is a special index, reserved for rules of the type
            //S -> X1, S -> X2, S -> X3. so if RHSIndex = 0 we check that exactly the same rule (same LHS) is in the grammar)
            if (rc.RHSIndex == 0)
                return (StackConstantRules.Contains(rc));

            foreach (var ruleCoord in StackConstantRules)
                if (ruleCoord.RHSIndex == rc.RHSIndex) return true;

            return false;
        }


        public bool AddStackChangingRule(SyntacticCategory moveable, StackChangingRule r, MoveableOperationsKey key,
            bool forceAdd = false)
        {
            if (!StackChangingRules.ContainsKey(moveable))
                StackChangingRules[moveable] = new MoveableOperations();

            var isAdded = StackChangingRules[moveable].AddRule(r, key, 0, forceAdd);
            //if (isAdded) _ruleCounter++;
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

        
        public void PruneUnusedRules(Dictionary<int, int> usagesDic)
        {
            var unusedConstantRules = StackConstantRules.Where(x => !usagesDic.ContainsKey(RuleSpace[x].Number)).ToArray();

            foreach (var unusedRule in unusedConstantRules.ToList())
                StackConstantRules.Remove(unusedRule);

            var unusedStackRules = StackChangingRulesArray.Where(x => !usagesDic.ContainsKey(x.Number)).ToArray();

            foreach (var rule in unusedStackRules)
                DeleteStackChangingRule(rule);
        }
    }
}