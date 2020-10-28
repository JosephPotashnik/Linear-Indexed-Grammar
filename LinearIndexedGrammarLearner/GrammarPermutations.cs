using LinearIndexedGrammarParser;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Reflection;

#pragma warning disable 649

namespace LinearIndexedGrammarLearner
{
    public class GrammarPermutations
    {
        public delegate (ContextSensitiveGrammar mutatedGrammar, bool reparsed)
            GrammarMutation(ContextSensitiveGrammar grammar, Learner learner);

        public const int CFGOperationWeight = 24;
        public const int LIGOperationWeight = 5;

        private static Tuple<GrammarMutation, int>[] _mutations;
        private static int _totalWeights;

        public GrammarPermutations(bool isCFGGrammar)
        {
            var l = new List<GrammarMutationData>();

            if (isCFGGrammar)
            {
                l.Add(new GrammarMutationData("InsertStackConstantRule", CFGOperationWeight ));
                l.Add(new GrammarMutationData("DeleteStackConstantRule", CFGOperationWeight));
                l.Add(new GrammarMutationData("ChangeLHS", CFGOperationWeight));
                l.Add(new GrammarMutationData("ChangeRHS", CFGOperationWeight));
                l.Add(new GrammarMutationData("InsertPrefixExtendingStackConstantRule", 0 ));
            }
            else
            {
                l.Add(new GrammarMutationData("InsertStackConstantRule", CFGOperationWeight));
                l.Add(new GrammarMutationData("DeleteStackConstantRule", CFGOperationWeight));
                l.Add(new GrammarMutationData("InsertPrefixExtendingStackConstantRule", CFGOperationWeight / 2));

                //l.Add(new GrammarMutationData("ChangeLHS", CFGOperationWeight));
                //l.Add(new GrammarMutationData("ChangeRHS", CFGOperationWeight));
                l.Add(new GrammarMutationData("InsertMovement", LIGOperationWeight));
                l.Add(new GrammarMutationData("DeleteMovement", LIGOperationWeight));

                //TODO at the moment, bugs when changeRHS/changeLHS, changeLHSPush, changeRHSPush
                //when grammar is LIG. 
                //l.Add(new GrammarMutationData("ChangeLHSPush", LIGWeight));
                //l.Add(new GrammarMutationData("ChangeRHSPush", LIGWeight));
                //l.Add(new GrammarMutationData("ChangeLHS", CFGOperationWeight));
                //l.Add(new GrammarMutationData("ChangeRHS", CFGOperationWeight));
            }

            var typeInfo = GetType().GetTypeInfo();
            _mutations = new Tuple<GrammarMutation, int>[l.Count];
            for (var i = 0; i < l.Count; i++)
                foreach (var method in typeInfo.GetDeclaredMethods(l[i].Mutation))
                {
                    var m = (GrammarMutation)method.CreateDelegate(typeof(GrammarMutation), this);
                    _mutations[i] = new Tuple<GrammarMutation, int>(m, l[i].MutationWeight);
                }

            _totalWeights = 0;
            foreach (var mutation in _mutations)
                _totalWeights += mutation.Item2;
        }

        public static GrammarMutation GetWeightedRandomMutation()
        {
            var r = Pseudorandom.NextInt(_totalWeights);

            var sum = 0;
            foreach (var mutation in _mutations)
            {
                if (sum + mutation.Item2 > r)
                    return mutation.Item1;
                sum += mutation.Item2;
            }

            return null;
        }

        public RuleCoordinates GetRandomRule(List<RuleCoordinates> rules)
        {
            var r = Pseudorandom.NextInt(rules.Count);
            return rules[r];
        }


        public (ContextSensitiveGrammar mutatedGrammar, bool reparsed)
            InsertStackConstantRule(ContextSensitiveGrammar grammar, Learner learner)
        {
            var rc = CreateRandomRule(RuleType.CFGRules);
            return InnerInsertStackConstantRule(grammar, learner, rc);
        }

        public (ContextSensitiveGrammar mutatedGrammar, bool reparsed) InsertPrefixExtendingStackConstantRule(
            ContextSensitiveGrammar grammar, Learner learner)
        {

            for (int i = 0; i < learner._sentencesParser.Length; i++)
            {
                if (learner._sentencesParser[i].BracketedRepresentations.Count == 0)
                {
                    var rhs = learner._sentencesParser[i].SuggestRHSForCompletion();
                    if (rhs == null)
                        return (null, false);
                    var lhs = ContextSensitiveGrammar.RuleSpace.GetRandomLHSIndex();

                    //Console.WriteLine($"Suggestion based on extending prefix with lhs {lhs} and rhs {rhs[0]} {rhs[1]}");

                    var rc = new RuleCoordinates
                    {
                        RuleType = RuleType.CFGRules,
                        LHSIndex = lhs,
                        RHSIndex = ContextSensitiveGrammar.RuleSpace.FindRHSIndex(rhs)
                    };

                    var res = InnerInsertStackConstantRule(grammar, learner, rc);
                    //if (res.mutatedGrammar == null)
                    //    Console.WriteLine("rejected suggestion, rhs exists");
                    //else
                    //    Console.WriteLine("accepted suggestion");
                    return res;
                }
            }

            return (null, false);
        }

        private static (ContextSensitiveGrammar mutatedGrammar, bool reparsed) InnerInsertStackConstantRule(
            ContextSensitiveGrammar grammar, Learner learner, RuleCoordinates rc)
        {
            if (grammar.ContainsRuleWithSameRHS(rc, grammar.StackConstantRules))
                return (null, false);
            grammar.StackConstantRules.Add(rc);

            var r = ContextSensitiveGrammar.RuleSpace[rc];

            //Console.WriteLine($"added {r}");
            var reparsed = learner.ReparseWithAddition(grammar, r.NumberOfGeneratingRule);

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
            var rc = GetRandomRule(grammar.StackConstantRules);
            return InnerDeleteStackConstantRule(grammar, learner, rc);
        }

        private static (ContextSensitiveGrammar mutatedGrammar, bool reparsed) InnerDeleteStackConstantRule(
            ContextSensitiveGrammar grammar, Learner learner, RuleCoordinates rc)
        {
            grammar.StackConstantRules.Remove(rc);
            var r = ContextSensitiveGrammar.RuleSpace[rc];
            //Console.WriteLine($"removed {r}");

            var reparsed = learner.ReparseWithDeletion(grammar, r.NumberOfGeneratingRule);
            return (grammar, reparsed);
        }

        public (ContextSensitiveGrammar mutatedGrammar, bool reparsed)
            ChangeLHS(ContextSensitiveGrammar grammar, Learner learner)
        {
            bool reparsed1, reparsed2;

            var rcOld = GetRandomRule(grammar.StackConstantRules);
            if (rcOld.RHSIndex == 0) return (null, false); //do not change LHS for rules of the form START -> Xi

            var rcNew = new RuleCoordinates
            {
                RHSIndex = rcOld.RHSIndex,
                LHSIndex = ContextSensitiveGrammar.RuleSpace.GetRandomLHSIndex(),
                RuleType = rcOld.RuleType
            };
            if (rcOld.LHSIndex == rcNew.LHSIndex) return (null, false);
            //Console.WriteLine($"in lhs change part1");

            (grammar, reparsed1) = InnerDeleteStackConstantRule(grammar, learner, rcOld);
            if (reparsed1 == false) return (null, false);
            //Console.WriteLine($"in lhs change part2");

            (grammar, reparsed2) = InnerInsertStackConstantRule(grammar, learner, rcNew);
            if (reparsed2 == false)
                throw new Exception("changeLHS aborted half-way! WRONG");
            return (grammar, reparsed2);
        }

        public (ContextSensitiveGrammar mutatedGrammar, bool reparsed)
            ChangeRHS(ContextSensitiveGrammar grammar, Learner learner)
        {
            bool reparsed1, reparsed2;

            var rcOld = GetRandomRule(grammar.StackConstantRules);
            var rcNew = new RuleCoordinates
            {
                RHSIndex = ContextSensitiveGrammar.RuleSpace.GetRandomRHSIndex(RuleType.CFGRules),
                LHSIndex = rcOld.LHSIndex,
                RuleType = rcOld.RuleType
            };


            if (grammar.ContainsRuleWithSameRHS(rcNew, grammar.StackConstantRules)) return (null, false);
            //Console.WriteLine($"in rhs change part1");

            (grammar, reparsed1) = InnerDeleteStackConstantRule(grammar, learner, rcOld);
            if (reparsed1 == false) return (null, false);
            //Console.WriteLine($"in rhs change part2");

            (grammar, reparsed2) = InnerInsertStackConstantRule(grammar, learner, rcNew);
            if (reparsed2 == false)
                throw new Exception("changeRHS aborted half-way! WRONG");
            return (grammar, reparsed2);
        }

        //public ContextSensitiveGrammar ChangeLHSPush(ContextSensitiveGrammar grammar)
        //{
        //    if (grammar.StackPush1Rules.Count == 0) return null;

        //    var rc = grammar.GetRandomRule(grammar.StackPush1Rules);
        //    var newLHSIndex = ContextSensitiveGrammar.RuleSpace.GetRandomLHSIndex();
        //    if (rc.LHSIndex == newLHSIndex) return null;

        //    rc.LHSIndex = newLHSIndex;
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

            var rc = GetRandomRule(grammar.StackPush1Rules);
            grammar.DeleteCorrespondingPopRule(rc);
            grammar.StackPush1Rules.Remove(rc);

            var r = ContextSensitiveGrammar.RuleSpace[rc];

            var reparsed = learner.ReparseWithDeletion(grammar, r.NumberOfGeneratingRule);

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
            var reparsed = learner.ReparseWithAddition(grammar, r.NumberOfGeneratingRule);
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