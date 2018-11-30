﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace LinearIndexedGrammarParser
{
    public class RuleCoordinates : IEquatable<RuleCoordinates>
    {
        public RuleCoordinates()
        {
        }

        public RuleCoordinates(RuleCoordinates other)
        {
            LHSIndex = other.LHSIndex;
            RHSIndex = other.RHSIndex;
            RuleType = other.RuleType;
        }
        
        public int RuleType { get; set; }

        //the LHS index in the Rule Space (index 0 = LHS "X1", index 1 = LHS "X2", etc)
        public int LHSIndex { get; set; }

        //the RHS index in the Rule Space ( same RHS index for different LHS indices means they share the same RHS)
        public int RHSIndex { get; set; }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = RuleType;
                hashCode = (hashCode * 397) ^ LHSIndex;
                hashCode = (hashCode * 397) ^ RHSIndex;
                return hashCode;
            }
        }

        public bool Equals(RuleCoordinates other)
        {
            return RuleType == other.RuleType && LHSIndex == other.LHSIndex && RHSIndex == other.RHSIndex;
        }
    }

    public class ContextSensitiveGrammar
    {
        //Rule space is a 3D array,
        //zero index is the rule type: 0 = CFG rule table, 1 = Push LIG rule table, 2 = Pop rule table.
        //first index is LHS, the column in the relevant rule table ([0] = "X1", [1] = "X2", [2] = "X3" etc)
        //second index is RHS, the row in the relevant rule table such that [0][i] = [1][i] = [2][i] etc. 
        //(note, RHS index 0 is a special case, see RuleSpace class).
        public static RuleSpace RuleSpace;
        public readonly List<RuleCoordinates> StackConstantRules = new List<RuleCoordinates>();
        public readonly List<RuleCoordinates> StackChangingRules = new List<RuleCoordinates>();

        public ContextSensitiveGrammar()
        {
        }

        public ContextSensitiveGrammar(Rule[] grammarRules)
        {
            foreach (var rule in grammarRules)
            {
                var rc = RuleSpace.FindRule(rule);
                StackConstantRules.Add(rc);
            }
        }

        public ContextSensitiveGrammar(ContextSensitiveGrammar otherGrammar)
        {
            StackConstantRules = otherGrammar.StackConstantRules.Select(x => new RuleCoordinates(x)).ToList();
            StackChangingRules = otherGrammar.StackChangingRules.Select(x => new RuleCoordinates(x)).ToList();
        }
        
        public override string ToString()
        {
            var s1 = "Stack Constant Rules:\r\n" +
                     string.Join("\r\n", StackConstantRules.Select(x => RuleSpace[x].ToString()));
            var s2 = "Stack Changing Rules:\r\n" +
                     string.Join("\r\n", StackChangingRules.Select(x => RuleSpace[x].ToString()));
            return s1 + "\r\n" + s2;
        }


        public RuleCoordinates GetRandomRule(List<RuleCoordinates> rules)
        {
            var rand = ThreadSafeRandom.ThisThreadsRandom;
            return rules[rand.Next(rules.Count)];
        }

        //if there is a rule that has the same RHS side, i.e. the same RHS index
        public bool ContainsRuleWithSameRHS(RuleCoordinates rc, List<RuleCoordinates> rules)
        {
            //RHSIndex 0 is a special index, reserved for rules of the type
            //S -> X1, S -> X2, S -> X3 (CFG table) or X1[X1] -> epsilon, X2[X2]->epsilon (Pop table)
            //so if RHSIndex = 0 we check that exactly the same rule (same LHS) is in the grammar)
            if (rc.RHSIndex == 0)
                return rules.Contains(rc);

            //else we just need to check that the same RHS index appears in some other rule.
            foreach (var ruleCoord in rules)
                if (ruleCoord.RHSIndex == rc.RHSIndex && ruleCoord.RuleType == rc.RuleType)
                    return true;

            return false;
        }

        public void PruneUnusedRules(Dictionary<int, int> usagesDic)
        {
            var unusedConstantRules =
                StackConstantRules.Where(x => !usagesDic.ContainsKey(RuleSpace[x].Number)).ToArray();

            foreach (var unusedRule in unusedConstantRules.ToList())
                StackConstantRules.Remove(unusedRule);

            var unusedChangingRules =
                StackChangingRules.Where(x => !usagesDic.ContainsKey(RuleSpace[x].Number)).ToArray();

            foreach (var unusedRule in unusedChangingRules.ToList())
                StackChangingRules.Remove(unusedRule);

        }
    }
}