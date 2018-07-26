using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using LinearIndexedGrammarParser;

namespace LinearIndexedGrammarLearner
{
    public class GrammarPermutations
    {
        public delegate Grammar GrammarMutation(Grammar grammar);
        private const int NumberOfRetries = 10;
        private static Tuple<GrammarMutation, int>[] mutations;
        private static int totalWeights;
        private static int newNonTerminalCounter = 1;
        private DerivedCategory[] PartsOfSpeechCategories;

        public GrammarPermutations(string[] POS)
        {
            PartsOfSpeechCategories = POS.Select(x => new DerivedCategory(x)).ToArray();
        }
        public void ReadPermutationWeightsFromFile()
        {
            List<GrammarMutationData> l;
            using (var file = File.OpenText(@"MutationWeights.json"))
            {
                var serializer = new JsonSerializer();
                l = (List<GrammarMutationData>)serializer.Deserialize(file, typeof(List<GrammarMutationData>));
            }
            mutations = new Tuple<GrammarMutation, int>[l.Count];

            var typeInfo = GetType().GetTypeInfo();

            for (var i = 0; i < l.Count; i++)
            {
                foreach (var method in typeInfo.GetDeclaredMethods(l[i].Mutation))
                {
                    var m = (GrammarMutation)method.CreateDelegate(typeof(GrammarMutation), this);
                    mutations[i] = new Tuple<GrammarMutation, int>(m, l[i].MutationWeight);
                }
            }

            totalWeights = 0;
            foreach (var mutation in mutations)
                totalWeights += mutation.Item2;
        }

        public static GrammarMutation GetWeightedRandomMutation()
        {
            int r = 0;
            var rand = ThreadSafeRandom.ThisThreadsRandom;
            r = rand.Next(totalWeights);


            var sum = 0;
            foreach (var mutation in mutations)
            {
                if (sum + mutation.Item2 > r)
                    return mutation.Item1;
                sum += mutation.Item2;
            }
            return null;
        }

        //generate a new rule from random existing productions.
        public Grammar InsertRule(Grammar grammar)
        {
            var startCategory = new DerivedCategory(Grammar.StartRule);
            //we cannot insert a new rule if the number of left hand sided symbols
            //exceeds a certain amount, determined by the number of parts of speech.

            //a full binary tree with N different parts of speech as leaves
            //requires N-1 non-terminals to parse.
            //so at best case, the number of LHS symbols is in the order of the number
            //of different Parts of speech. 
            //we will no assume a full binary tree, so we can increase the upper bound to allow flexibility.
            int RelationOfLHSToPOS = 2;
            var lhsCategories = grammar.staticRulesGeneratedForCategory.ToArray();
            var rightHandSidePOOL = lhsCategories.Concat(PartsOfSpeechCategories).ToArray();

            int upperBoundNonTerminals = PartsOfSpeechCategories.Length * RelationOfLHSToPOS;

            if (lhsCategories.Length >= upperBoundNonTerminals)
                return null;

            for (var k = 0; k < NumberOfRetries; k++)
            {
             
                //create a new non terminal
                string baseNonTerminal = null;
                baseNonTerminal = $"X{newNonTerminalCounter++}";

                var newCategory = new DerivedCategory(baseNonTerminal);

                //create a new Rule, whose LHS is the new category. 
                //the right hand side of the new rule is chosen randomly
                //from existing LHS symbols of the grammar, or from POS.
                var rightHandSide = new DerivedCategory[2];
                for (var i = 0; i < 2; i++)
                {
                    DerivedCategory randomRightHandSideCategory = GetRandomRightHandSideCategory(startCategory, rightHandSidePOOL);
                    rightHandSide[i] = randomRightHandSideCategory;
                }
                var newRule = new Rule(newCategory, rightHandSide);

                if (grammar.ContainsSameRHSRule(newRule)) continue;

                grammar.AddGrammarRule(newRule);

                return grammar;
            }
            return null;
        }

        public Grammar SpreadRuleLHSToRHS(Grammar grammar)
        {
            var startCategory = new DerivedCategory(Grammar.StartRule);
            var lhsCategories = grammar.staticRulesGeneratedForCategory.ToArray();
            if (lhsCategories.Length < 2)
                return null;

            for (var i = 0; i < NumberOfRetries; i++)
            {
                //find LHS to spread:
                DerivedCategory lhs = new DerivedCategory(startCategory);
                var rand = ThreadSafeRandom.ThisThreadsRandom;


                while (lhs.Equals(startCategory))
                        lhs = lhsCategories[rand.Next(lhsCategories.Length)];


                //choose random rule whose LHS we will replace by lhs chosen above.
                Rule randomRule = GetRandomRule(grammar);

                //choose for now only binary rules.
                if (randomRule.RightHandSide.Length < 2)
                    continue;

                //select a right hand side category randomly.
                int randomChildIndex = GetRandomChildIndex();
                var originalRHSSymbol = randomRule.RightHandSide[randomChildIndex];

                if (originalRHSSymbol.Equals(lhs)) continue;

                //randomRule.RightHandSide[randomChildIndex] = new DerivedCategory(lhs);

                var replaceRule = new Rule(randomRule);
                replaceRule.RightHandSide[randomChildIndex] = new DerivedCategory(lhs);
                if (grammar.ContainsSameRHSRule(replaceRule)) continue;

                grammar.DeleteGrammarRule(randomRule);
                grammar.AddGrammarRule(replaceRule);

                return grammar;
            }
            return null;
        }
        
        public Grammar SpreadRuleLHSToLHS(Grammar grammar)
        {
            var startCategory = new DerivedCategory(Grammar.StartRule);
            var lhsCategories = grammar.staticRulesGeneratedForCategory.ToArray();
            if (lhsCategories.Length < 2)
                return null;

            for (var i = 0; i < NumberOfRetries; i++)
            {
                //find LHS to spread:
                DerivedCategory lhs = new DerivedCategory(startCategory);
                var rand = ThreadSafeRandom.ThisThreadsRandom;


                while (lhs.Equals(startCategory))
                        lhs = lhsCategories[rand.Next(lhsCategories.Length)];

                //choose random rule whose LHS we will replace by lhs chosen above.
                Rule randomRule = GetRandomRule(grammar);
                var originalLHSSymbol = randomRule.LeftHandSide;

                if (originalLHSSymbol.Equals(lhs)) continue;

                var replaceRule = new Rule(randomRule);
                replaceRule.LeftHandSide = new DerivedCategory(lhs);
                grammar.DeleteGrammarRule(randomRule);
                grammar.AddGrammarRule(replaceRule);

                return grammar;
            }
            return null;
        }

        private static int GetRandomChildIndex()
        {
            int randomChildIndex = 0;
            var rand = ThreadSafeRandom.ThisThreadsRandom;

            randomChildIndex = rand.Next(2);
            return randomChildIndex;
        }

        private static DerivedCategory GetRandomRightHandSideCategory(DerivedCategory startCategory, DerivedCategory[] rightHandSidePOOL)
        {
            DerivedCategory randomRightHandSideCategory = startCategory;
            var rand = ThreadSafeRandom.ThisThreadsRandom;


            while (randomRightHandSideCategory.Equals(startCategory))
                    randomRightHandSideCategory = rightHandSidePOOL[rand.Next(rightHandSidePOOL.Length)];

            return randomRightHandSideCategory;
        }

        public Grammar DeleteRule(Grammar grammar)
        {
            Rule randomRule = GetRandomRule(grammar);
            grammar.DeleteGrammarRule(randomRule);
            return grammar;
        }

        private static Rule GetRandomRule(Grammar grammar)
        {
            var rules = grammar.Rules.ToList();
            var rand = ThreadSafeRandom.ThisThreadsRandom;
            Rule randomRule = rules[rand.Next(rules.Count)];
            return randomRule;
        }

        [JsonObject(MemberSerialization.OptIn)]
        public class GrammarMutationData
        {
            public GrammarMutationData() { }

            public GrammarMutationData(string m, int w)
            {
                Mutation = m;
                MutationWeight = w;
            }

            [JsonProperty]
            public string Mutation { get; set; }

            [JsonProperty]
            public int MutationWeight { get; set; }
        }
    }
}