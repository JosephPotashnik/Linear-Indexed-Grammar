using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using LinearIndexedGrammarParser;
using Newtonsoft.Json;

namespace LinearIndexedGrammarLearner
{
    public class GrammarPermutations
    {
        public delegate ContextSensitiveGrammar GrammarMutation(ContextSensitiveGrammar grammar);

        private static Tuple<GrammarMutation, int>[] _mutations;
        private static int _totalWeights;
        private readonly SyntacticCategory[] _partsOfSpeechCategories;

        public GrammarPermutations(string[] pos)
        {
            _partsOfSpeechCategories = pos.Select(x => new SyntacticCategory(x)).ToArray();
        }

        public void ReadPermutationWeightsFromFile()
        {
            List<GrammarMutationData> l;
            using (var file = File.OpenText(@"MutationWeights.json"))
            {
                var serializer = new JsonSerializer();
                l = (List<GrammarMutationData>) serializer.Deserialize(file, typeof(List<GrammarMutationData>));
            }

            _mutations = new Tuple<GrammarMutation, int>[l.Count];

            var typeInfo = GetType().GetTypeInfo();

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

        public ContextSensitiveGrammar InsertStackConstantRule(ContextSensitiveGrammar grammar)
        {
            var rc = CreateRandomRule(RuleType.CFGRules);
            if (grammar.ContainsRuleWithSameRHS(rc, grammar.StackConstantRules)) return null;
            grammar.StackConstantRules.Add(rc);
            return grammar;
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

        public ContextSensitiveGrammar DeleteStackConstantRule(ContextSensitiveGrammar grammar)
        {
            var rc = grammar.GetRandomRule(grammar.StackConstantRules);
            grammar.StackConstantRules.Remove(rc);
            return grammar;
        }

        public ContextSensitiveGrammar ChangeLHS(ContextSensitiveGrammar grammar)
        {
            var rc = grammar.GetRandomRule(grammar.StackConstantRules);
            var newLHSIndex = ContextSensitiveGrammar.RuleSpace.GetRandomLHSIndex();
            if (rc.LHSIndex == newLHSIndex) return null;

            rc.LHSIndex = newLHSIndex;
            return grammar;
        }

        public ContextSensitiveGrammar ChangeRHS(ContextSensitiveGrammar grammar)
        {
            var rc = grammar.GetRandomRule(grammar.StackConstantRules);
            var changedRc = new RuleCoordinates
            {
                LHSIndex = rc.LHSIndex,
                RHSIndex = ContextSensitiveGrammar.RuleSpace.GetRandomRHSIndex(RuleType.CFGRules)
            };
            if (grammar.ContainsRuleWithSameRHS(changedRc, grammar.StackConstantRules)) return null;

            rc.RHSIndex = changedRc.RHSIndex;
            return grammar;
        }

        public ContextSensitiveGrammar SwapTwoRulesLHS(ContextSensitiveGrammar grammar)
        {
            var rc1 = grammar.GetRandomRule(grammar.StackConstantRules);
            var rc2 = grammar.GetRandomRule(grammar.StackConstantRules);

            while (rc2.Equals(rc1))
                rc2 = grammar.GetRandomRule(grammar.StackConstantRules);

            if (rc1.LHSIndex == rc2.LHSIndex) return null;

            var temp = new RuleCoordinates(rc1);
            rc1.LHSIndex = rc2.LHSIndex;
            rc2.LHSIndex = temp.LHSIndex;
            return grammar;
        }

        public ContextSensitiveGrammar DeleteMovement(ContextSensitiveGrammar grammar)
        {
            if (grammar.StackPush1Rules.Count == 0) return null;

            var rc = grammar.GetRandomRule(grammar.StackPush1Rules);
            grammar.DeleteCorrespondingPopRule(rc);
            grammar.StackPush1Rules.Remove(rc);
            return grammar;
        }

        public ContextSensitiveGrammar InsertMovement(ContextSensitiveGrammar grammar)
        {
            var rc = CreateRandomRule(RuleType.Push1Rules);
            if (grammar.ContainsRuleWithSameRHS(rc, grammar.StackPush1Rules)) return null;
            grammar.StackPush1Rules.Add(rc);
            grammar.AddCorrespondingPopRule(rc);
            return grammar;
        }

        

        private bool DoesNumberOfLHSCategoriesExceedMax(SyntacticCategory[] lhsCategories)
        {
            //we cannot insert a new rule if the number of left hand sided symbols
            //exceeds a certain amount, determined by the number of parts of speech.

            //a full binary tree with N different parts of speech as leaves
            //requires N-1 non-terminals to parse.
            //so at best case, the number of LHS symbols is in the order of the number
            //of different Parts of speech. 
            //we will no assume a full binary tree, so we can increase the upper bound to allow flexibility.
            var RelationOfLHSToPOS = 2;
            var upperBoundNonTerminals = _partsOfSpeechCategories.Length * RelationOfLHSToPOS;
            if (upperBoundNonTerminals < 6) upperBoundNonTerminals = 6;

            //do not consider START in the upper bound.
            return lhsCategories.Length - 1 >= upperBoundNonTerminals;
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