using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using LinearIndexedGrammarParser;
using Newtonsoft.Json;
using static LinearIndexedGrammarParser.MoveableOperationsKey;

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
            var rc = new RuleCoordinates
            {
                LHSIndex = ContextSensitiveGrammar.RuleSpace.GetRandomLHSIndex(),
                RHSIndex = ContextSensitiveGrammar.RuleSpace.GetRandomRHSIndex()
            };
            if (grammar.ContainsRuleWithSameRHS(rc)) return null;
            grammar.AddStackConstantRule(rc);
            return grammar;
        }

        public ContextSensitiveGrammar DeleteStackConstantRule(ContextSensitiveGrammar grammar)
        {
            grammar.DeleteStackConstantRule(grammar.GetRandomStackConstantRule());
            return grammar;
        }

        public ContextSensitiveGrammar ChangeLHS(ContextSensitiveGrammar grammar)
        {
            var rc = grammar.GetRandomStackConstantRule();
            var newLHSIndex = ContextSensitiveGrammar.RuleSpace.GetRandomLHSIndex();
            if (rc.LHSIndex == newLHSIndex) return null;

            rc.LHSIndex = newLHSIndex;
            return grammar;
        }

        public ContextSensitiveGrammar ChangeRHS(ContextSensitiveGrammar grammar)
        {
            var rc = grammar.GetRandomStackConstantRule();
            var changedRc = new RuleCoordinates
            {
                LHSIndex = rc.LHSIndex,
                RHSIndex = ContextSensitiveGrammar.RuleSpace.GetRandomRHSIndex()
            };
            if (grammar.ContainsRuleWithSameRHS(changedRc)) return null;

            rc.RHSIndex = changedRc.RHSIndex;
            return grammar;
        }

        public ContextSensitiveGrammar SwapTwoRulesLHS(ContextSensitiveGrammar grammar)
        {
            var rc1 = grammar.GetRandomStackConstantRule();
            var rc2 = grammar.GetRandomStackConstantRule();

            while (rc2.Equals(rc1))
                rc2 = grammar.GetRandomStackConstantRule();

            if (rc1.LHSIndex == rc2.LHSIndex) return null;

            var temp = new RuleCoordinates(rc1);
            rc1.LHSIndex = rc2.LHSIndex;
            rc2.LHSIndex = temp.LHSIndex;
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

        public ContextSensitiveGrammar DeleteMovement(ContextSensitiveGrammar grammar)
        {
            var rand = ThreadSafeRandom.ThisThreadsRandom;
            var keys = grammar.StackChangingRules.Keys.ToArray();
            if (keys.Length == 0) return null;
            var moveable = keys[rand.Next(keys.Length)];

            if (grammar.StackChangingRules[moveable].MoveOps.ContainsKey(Push1))
            {
                //if there is more than one push1 rule, delete only push1 rule, 
                //do not delete the pop1 rule.
                var oldRule = grammar.StackChangingRules[moveable].GetRandomRule(Push1);
                grammar.StackChangingRules[moveable].DeleteRule(oldRule, Push1);

                if (grammar.StackChangingRules[moveable].MoveOps[Push1].Count > 0) return grammar;

                //only one push1 rule, delete both push1 and pop1 rules.
                oldRule = grammar.StackChangingRules[moveable].GetRandomRule(Pop1);
                grammar.StackChangingRules[moveable].DeleteRule(oldRule, Pop1);
            }

            return null;
        }

        public ContextSensitiveGrammar InsertMovement(ContextSensitiveGrammar grammar)
        {
            //var rand = ThreadSafeRandom.ThisThreadsRandom;
            //var lhsCategories = grammar.LHSCategories;
            //var categoriesPool = lhsCategories.Concat(PartsOfSpeechCategories).ToArray();
            //string moveable = ContextFreeGrammar.StartRule;

            ////Don't allow the moveable category to be START Category (for now)
            //while (moveable == ContextFreeGrammar.StartRule)
            //     moveable = categoriesPool[rand.Next(categoriesPool.Length)].ToString();

            //if (InsertPush1Rule(grammar, moveable))
            //{
            //    //if Inserting pop1 does not succeed (i.e, of the form NP[NP] -> epsilon)
            //    //it means the pop1 rule already exists, so it is still OK.
            //    InsertPop1Rule(grammar, moveable);
            //    return grammar;
            //}
            return null;
        }

        private bool InsertPop1Rule(ContextSensitiveGrammar grammar, string moveable = null)
        {
            //    var rand = ThreadSafeRandom.ThisThreadsRandom;

            //    if (moveable == null)
            //    {
            //        var lhsCategories = grammar.LHSCategories;
            //        var categoriesPool = lhsCategories.Concat(PartsOfSpeechCategories).ToArray();
            //        moveable = categoriesPool[rand.Next(categoriesPool.Length)].ToString();
            //    }

            //    for (var k = 0; k < NumberOfRetries; k++)
            //    {
            //        var moveableCategory = new DerivedCategory(moveable);
            //        var lhs = new DerivedCategory(moveable, moveable);
            //        var epsilonCat = new DerivedCategory(ContextFreeGrammar.EpsilonSymbol) {StackSymbolsCount = -1};
            //        var r = new Rule(lhs, new[] { epsilonCat });
            //        var newRule = new StackChangingRule(r, Pop1, moveableCategory);
            //        if (!newRule.AddRuleToGrammar(grammar)) continue;
            //        return true;
            //    }

            //    return false;
            //}

            //private bool InsertPush1Rule(ContextSensitiveGrammar grammar, string moveable = null)
            //{
            //    var rand = ThreadSafeRandom.ThisThreadsRandom;
            //    var lhsCategories = grammar.LHSCategories;
            //    var categoriesPool = lhsCategories.Concat(PartsOfSpeechCategories).ToArray();

            //    if (moveable == null)
            //      moveable = categoriesPool[rand.Next(categoriesPool.Length)].ToString();

            //    if (DoesNumberOfLHSCategoriesExceedMax(lhsCategories)) return false;

            //    for (var k = 0; k < NumberOfRetries; k++)
            //    {
            //        //create a new non terminal
            //        string baseNonTerminal = null;
            //        baseNonTerminal = $"X{newNonTerminalCounter++}";

            //        var newCategory = new DerivedCategory(baseNonTerminal, ContextFreeGrammar.StarSymbol);

            //        int len = 2;
            //        int spinePositionInRHS = rand.Next(len);
            //        spinePositionInRHS = 1; //initially, constrain moveables to be the first position, spine to the right (=movement to left only)
            //        var moveableCategory = new DerivedCategory(moveable);
            //        var spineCategory = new DerivedCategory(lhsCategories[rand.Next(lhsCategories.Length)].ToString(),
            //            ContextFreeGrammar.StarSymbol + moveable) {StackSymbolsCount = 1};

            //        //if the form is Y[*] -> X1 X1[*X1], it's also like Y -> X1 X1[X1], 
            //        //since there's a pop rule for the moveable X1: X1[X1] -> epsilon,
            //        //overall it's like inserting Y -> X1 rule in an overcomplicated way.
            //        if (spineCategory.BaseEquals(moveableCategory)) continue;

            //        var rightHandSide = new DerivedCategory[len];
            //        for (var i = 0; i < len; i++)
            //            rightHandSide[i] = (spinePositionInRHS == i) ? spineCategory : moveableCategory;

            //        var r = new Rule(newCategory, rightHandSide);
            //        var newRule = new StackChangingRule(r, Push1, moveableCategory);
            //        if (grammar.OnlyStartSymbolsRHS(newRule)) continue;
            //        if (!newRule.AddRuleToGrammar(grammar)) continue;

            //        return true;
            //    }
            return false;
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