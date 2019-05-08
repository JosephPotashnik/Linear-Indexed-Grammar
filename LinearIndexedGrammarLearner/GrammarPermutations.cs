using System;
using System.Collections.Generic;
using System.Reflection;
using LinearIndexedGrammarParser;
using Newtonsoft.Json;

#pragma warning disable 649

namespace LinearIndexedGrammarLearner
{

    public class GrammarPermutations
    {
        public delegate (ContextSensitiveGrammar mutatedGrammar, bool reparsed)
            GrammarMutation(ContextSensitiveGrammar grammar, Learner learner);

        public const int CFGOperationWeight = 20;
        public const int LIGOperationWeight = 5;

        private static Tuple<GrammarMutation, int>[] _mutations;
        private static int _totalWeights;

        public GrammarPermutations(bool isCFGGrammar)
        {
            var l = new List<GrammarMutationData>();

            l.Add(new GrammarMutationData("InsertStackConstantRule", CFGOperationWeight));
            l.Add(new GrammarMutationData("DeleteStackConstantRule", CFGOperationWeight));
            //l.Add(new GrammarMutationData("ChangeLHS", CFGOperationWeight));
            //l.Add(new GrammarMutationData("ChangeRHS", CFGOperationWeight));

            var LIGWeight = isCFGGrammar ? 0 : LIGOperationWeight;
            l.Add(new GrammarMutationData("InsertMovement", LIGWeight));
            l.Add(new GrammarMutationData("DeleteMovement", LIGWeight));
            //l.Add(new GrammarMutationData("ChangeLHSPush", LIGWeight));
            //l.Add(new GrammarMutationData("ChangeRHSPush", LIGWeight));

            var typeInfo = GetType().GetTypeInfo();
            _mutations = new Tuple<GrammarMutation, int>[l.Count];
            for (var i = 0; i < l.Count; i++)
                foreach (var method in typeInfo.GetDeclaredMethods(l[i].Mutation))
                {
                    var m = (GrammarMutation) method.CreateDelegate(typeof(GrammarMutation), this);
                    _mutations[i] = new Tuple<GrammarMutation, int>(m, l[i].MutationWeight);
                }

            _totalWeights = 0;
            foreach (var mutation in _mutations)
                _totalWeights += mutation.Item2;
        }

        public static GrammarMutation GetWeightedRandomMutation()
        {
            var rand = ThreadSafeRandom.ThisThreadsRandom;
            var r = rand.Next(_totalWeights);

            var sum = 0;
            foreach (var mutation in _mutations)
            {
                if (sum + mutation.Item2 > r)
                    return mutation.Item1;
                sum += mutation.Item2;
            }

            return null;
        }

        public (ContextSensitiveGrammar mutatedGrammar, bool reparsed)
            InsertStackConstantRule(ContextSensitiveGrammar grammar, Learner learner)
        {
            var rc = CreateRandomRule(RuleType.CFGRules);
            if (grammar.ContainsRuleWithSameRHS(rc, grammar.StackConstantRules))
                return (null, false);
            grammar.StackConstantRules.Add(rc);

            var r = ContextSensitiveGrammar.RuleSpace[rc];

            //Console.WriteLine($"added {r}");
            bool reparsed = learner.ReparseWithAddition(grammar, r.NumberOfGeneratingRule);

            return (grammar, reparsed);
        }

        private RuleCoordinates CreateRandomRule(int ruleType)
        {
            return new RuleCoordinates
            {
                RuleType = ruleType,
                LHSIndex = ContextSensitiveGrammar.RuleSpace.GetRandomLHSIndex(),
                RHSIndex = ContextSensitiveGrammar.RuleSpace.GetRandomRHSIndex(ruleType)
            };
        }

        public (ContextSensitiveGrammar mutatedGrammar, bool reparsed)
            DeleteStackConstantRule(ContextSensitiveGrammar grammar, Learner learner)
        {
            var rc = grammar.GetRandomRule(grammar.StackConstantRules);
            grammar.StackConstantRules.Remove(rc);
            var r = ContextSensitiveGrammar.RuleSpace[rc];

            bool reparsed = learner.ReparseWithDeletion(grammar, r.NumberOfGeneratingRule);
            return (grammar, reparsed);
        }

        //public ContextSensitiveGrammar ChangeLHS(ContextSensitiveGrammar grammar)
        //{
        //    var rc = grammar.GetRandomRule(grammar.StackConstantRules);
        //    var newLHSIndex = ContextSensitiveGrammar.RuleSpace.GetRandomLHSIndex();
        //    if (rc.LHSIndex == newLHSIndex) return null;

        //    rc.LHSIndex = newLHSIndex;
        //    return grammar;
        //}

        //public ContextSensitiveGrammar ChangeLHSPush(ContextSensitiveGrammar grammar)
        //{
        //    if (grammar.StackPush1Rules.Count == 0) return null;

        //    var rc = grammar.GetRandomRule(grammar.StackPush1Rules);
        //    var newLHSIndex = ContextSensitiveGrammar.RuleSpace.GetRandomLHSIndex();
        //    if (rc.LHSIndex == newLHSIndex) return null;

        //    rc.LHSIndex = newLHSIndex;
        //    return grammar;
        //}

        //public ContextSensitiveGrammar ChangeRHS(ContextSensitiveGrammar grammar)
        //{
        //    var rc = grammar.GetRandomRule(grammar.StackConstantRules);
        //    var changedRc = new RuleCoordinates
        //    {
        //        LHSIndex = rc.LHSIndex,
        //        RHSIndex = ContextSensitiveGrammar.RuleSpace.GetRandomRHSIndex(RuleType.CFGRules)
        //    };
        //    if (grammar.ContainsRuleWithSameRHS(changedRc, grammar.StackConstantRules)) return null;

        //    rc.RHSIndex = changedRc.RHSIndex;
        //    return grammar;
        //}

        //public ContextSensitiveGrammar ChangeRHSPush(ContextSensitiveGrammar grammar)
        //{
        //    if (grammar.StackPush1Rules.Count == 0) return null;

        //    var rc = grammar.GetRandomRule(grammar.StackPush1Rules);
        //    var changedRc = new RuleCoordinates
        //    {
        //        LHSIndex = rc.LHSIndex,
        //        RHSIndex = ContextSensitiveGrammar.RuleSpace.GetRandomRHSIndex(RuleType.Push1Rules)
        //    };
        //    if (grammar.ContainsRuleWithSameRHS(changedRc, grammar.StackPush1Rules)) return null;

        //    grammar.DeleteCorrespondingPopRule(rc);
        //    rc.RHSIndex = changedRc.RHSIndex;
        //    grammar.AddCorrespondingPopRule(rc);

        //    return grammar;
        //}

        public (ContextSensitiveGrammar mutatedGrammar, bool reparsed) DeleteMovement(
            ContextSensitiveGrammar grammar, Learner learner)
        {
            if (grammar.StackPush1Rules.Count == 0) return (null, false);

            var rc = grammar.GetRandomRule(grammar.StackPush1Rules);
            grammar.DeleteCorrespondingPopRule(rc);
            grammar.StackPush1Rules.Remove(rc);

            var r = ContextSensitiveGrammar.RuleSpace[rc];

            bool reparsed = learner.ReparseWithDeletion(grammar, r.NumberOfGeneratingRule);

            return (grammar, reparsed);
        }

        public (ContextSensitiveGrammar mutatedGrammar, bool reparsed) InsertMovement(
            ContextSensitiveGrammar grammar, Learner learner)
        {
            var rc = CreateRandomRule(RuleType.Push1Rules);
            if (grammar.ContainsRuleWithSameRHS(rc, grammar.StackPush1Rules))
                return (null, false);
            grammar.StackPush1Rules.Add(rc);
            grammar.AddCorrespondingPopRule(rc);

            var r = ContextSensitiveGrammar.RuleSpace[rc];

            //Console.WriteLine($"added {r}");
            bool reparsed = learner.ReparseWithAddition(grammar, r.NumberOfGeneratingRule);
            return (grammar, reparsed);

        }

        [JsonObject(MemberSerialization.OptIn)]
        public class GrammarMutationData
        {
            public GrammarMutationData()
            {
            }

            public GrammarMutationData(string m, int w)
            {
                Mutation = m;
                MutationWeight = w;
            }

            [JsonProperty] public string Mutation { get; set; }

            [JsonProperty] public int MutationWeight { get; set; }
        }
    }
}