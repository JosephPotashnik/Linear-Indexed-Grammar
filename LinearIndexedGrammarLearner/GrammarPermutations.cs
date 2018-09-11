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
        private SyntacticCategory[] PartsOfSpeechCategories;

        public GrammarPermutations(string[] POS)
        {
            PartsOfSpeechCategories = POS.Select(x => new SyntacticCategory(x)).ToArray();
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

        public Grammar SpreadPOSToRHS(Grammar grammar)
        {
            for (var i = 0; i < NumberOfRetries; i++)
            {
                var rand = ThreadSafeRandom.ThisThreadsRandom;
                DerivedCategory posRHS = new DerivedCategory(PartsOfSpeechCategories[rand.Next(PartsOfSpeechCategories.Length)].ToString());

                //choose random rule one of its RHS nonterminals we will replace by the pos chosen above.
                Rule randomRule = GetRandomRule(grammar);
                int randomChildIndex = rand.Next(randomRule.RightHandSide.Length);

                var originalRHSSymbol = randomRule.RightHandSide[randomChildIndex];

                if (originalRHSSymbol.BaseEquals(posRHS)) continue;

                var replaceRule = new Rule(randomRule);
                replaceRule.RightHandSide[randomChildIndex] = posRHS;
                if (grammar.ContainsSameRHSRule(replaceRule, PartsOfSpeechCategories)) continue;

                grammar.DeleteGrammarRule(randomRule);
                grammar.AddGrammarRule(replaceRule);

                return grammar;
            }
            return null;
        }

        public Grammar SpreadRuleLHSToRHS(Grammar grammar)
        {
            DerivedCategory startCategory = new DerivedCategory(Grammar.StartRule);

            var lhsCategories = grammar.dynamicRules.Keys.ToArray();
            if (lhsCategories.Length < 2)
                return null;

            for (var i = 0; i < NumberOfRetries; i++)
            {
                //find LHS to spread:
                var rand = ThreadSafeRandom.ThisThreadsRandom;

                DerivedCategory lhs = new DerivedCategory(Grammar.StartRule);
                while (lhs.BaseEquals(startCategory))
                    lhs = new DerivedCategory(lhsCategories[rand.Next(lhsCategories.Length)].ToString());

                //choose random rule one of its RHS nonterminals we will replace by lhs chosen above.
                Rule randomRule = GetRandomRule(grammar);

                //spread LHS only to binary rules. Spreading the LHS to an unary rule
                //is equivalent to variable renaming.

                if (randomRule.RightHandSide.Length < 2)
                    continue;

                //select a right hand side category randomly.
                int randomChildIndex = GetRandomChildIndex();
                var originalRHSSymbol = randomRule.RightHandSide[randomChildIndex];

                if (originalRHSSymbol.Equals(lhs)) continue;

                var replaceRule = new Rule(randomRule);
                replaceRule.RightHandSide[randomChildIndex] = lhs;
                if (grammar.ContainsSameRHSRule(replaceRule, PartsOfSpeechCategories)) continue;

                grammar.DeleteGrammarRule(randomRule);
                grammar.AddGrammarRule(replaceRule);

                return grammar;
            }
            return null;
        }
        
        public Grammar SpreadRuleLHSToLHS(Grammar grammar)
        {
            var lhsCategories = grammar.dynamicRules.Keys.ToArray();
            if (lhsCategories.Length < 2)
                return null;

            for (var i = 0; i < NumberOfRetries; i++)
            {
                //find LHS to spread:
                var rand = ThreadSafeRandom.ThisThreadsRandom;

                DerivedCategory lhs = new DerivedCategory(lhsCategories[rand.Next(lhsCategories.Length)].ToString());

                //choose random rule whose LHS we will replace by lhs chosen above.
                Rule randomRule = GetRandomRule(grammar);
                var originalLHSSymbol = randomRule.LeftHandSide;

                if (originalLHSSymbol.Equals(lhs)) continue;

                var replaceRule = new Rule(randomRule);
                replaceRule.LeftHandSide = lhs;
                grammar.DeleteGrammarRule(randomRule);
                grammar.AddGrammarRule(replaceRule);

                return grammar;
            }
            return null;
        }

        public Grammar DeleteRule(Grammar grammar)
        {
            Rule randomRule = GetRandomRule(grammar);
            grammar.DeleteGrammarRule(randomRule);
            return grammar;
        }

        public Grammar InsertBinaryRule(Grammar grammar)
        {
            var startCategory = new DerivedCategory(Grammar.StartRule);
            var lhsCategories = grammar.dynamicRules.Keys.ToArray();
            var categoriesPool = lhsCategories.Concat(PartsOfSpeechCategories).ToArray();

            //we cannot insert a new rule if the number of left hand sided symbols
            //exceeds a certain amount, determined by the number of parts of speech.

            //a full binary tree with N different parts of speech as leaves
            //requires N-1 non-terminals to parse.
            //so at best case, the number of LHS symbols is in the order of the number
            //of different Parts of speech. 
            //we will no assume a full binary tree, so we can increase the upper bound to allow flexibility.
            int RelationOfLHSToPOS = 2;
            int upperBoundNonTerminals = PartsOfSpeechCategories.Length * RelationOfLHSToPOS;
            //do not consider START in the upper bound.
            if ((lhsCategories.Length - 1) >= upperBoundNonTerminals)
                return null;

            for (var k = 0; k < NumberOfRetries; k++)
            {
                //create a new non terminal
                string baseNonTerminal = null;
                baseNonTerminal = $"X{newNonTerminalCounter++}";

                var newCategory = new DerivedCategory(baseNonTerminal, Grammar.StarSymbol);

                //create a new Rule, whose LHS is the new category. 
                //the right hand side of the new rule is chosen randomly
                //from existing LHS symbols of the grammar, or from POS.

                //assuming binary rules.
                int len = 2;
                var rand = ThreadSafeRandom.ThisThreadsRandom;
                bool addStarToRightHandSide = rand.Next(len) == 1;

                var rightHandSide = new DerivedCategory[len];
                for (var i = 0; i < len; i++)
                {
                    DerivedCategory randomRightHandSideCategory = new DerivedCategory(Grammar.StartRule);
                    while (randomRightHandSideCategory.BaseEquals(startCategory))
                        randomRightHandSideCategory = new DerivedCategory(categoriesPool[rand.Next(categoriesPool.Length)].ToString());

                    rightHandSide[i] = randomRightHandSideCategory;
                    if (addStarToRightHandSide)
                        rightHandSide[i].Stack = Grammar.StarSymbol;
                    
                    addStarToRightHandSide = !addStarToRightHandSide;
                }

                var newRule = new Rule(newCategory, rightHandSide);

                if (grammar.ContainsSameRHSRule(newRule, PartsOfSpeechCategories)) continue;

                grammar.AddGrammarRule(newRule);

                return grammar;
            }
            return null;
        }

        public Grammar InsertUnaryRule(Grammar grammar)
        {
            var startCategory = new DerivedCategory(Grammar.StartRule);
            var lhsCategories = grammar.dynamicRules.Keys.ToArray();
            var categoriesPool = lhsCategories.Concat(PartsOfSpeechCategories).ToArray();


            //we cannot insert a new rule if the number of left hand sided symbols
            //exceeds a certain amount, determined by the number of parts of speech.

            //a full binary tree with N different parts of speech as leaves
            //requires N-1 non-terminals to parse.
            //so at best case, the number of LHS symbols is in the order of the number
            //of different Parts of speech. 
            //we will no assume a full binary tree, so we can increase the upper bound to allow flexibility.
            int RelationOfLHSToPOS = 2;
            int upperBoundNonTerminals = PartsOfSpeechCategories.Length * RelationOfLHSToPOS;
            //do not consider START in the upper bound.
            if ((lhsCategories.Length - 1) >= upperBoundNonTerminals)
                return null;

            for (var k = 0; k < NumberOfRetries; k++)
            {
                //create a new non terminal
                string baseNonTerminal = null;
                baseNonTerminal = $"X{newNonTerminalCounter++}";

                var newCategory = new DerivedCategory(baseNonTerminal, Grammar.StarSymbol);

                //create a new unary Rule, whose LHS is the new category. 
                //the right hand side of the new rule is chosen randomly from POS.

                //assuming unary rules.
                int len = 1;
                var rand = ThreadSafeRandom.ThisThreadsRandom;
                bool addStarToRightHandSide = true;

                var rightHandSide = new DerivedCategory[len];
                for (var i = 0; i < len; i++)
                {
                    DerivedCategory randomRightHandSideCategory = new DerivedCategory(PartsOfSpeechCategories[rand.Next(PartsOfSpeechCategories.Length)].ToString());

                    rightHandSide[i] = randomRightHandSideCategory;
                    if (addStarToRightHandSide)
                        rightHandSide[i].Stack = Grammar.StarSymbol;

                    addStarToRightHandSide = !addStarToRightHandSide;
                }

                var newRule = new Rule(newCategory, rightHandSide);

                if (grammar.ContainsSameRHSRule(newRule, PartsOfSpeechCategories)) continue;

                grammar.AddGrammarRule(newRule);

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

      

        private static Rule GetRandomRule(Grammar grammar)
        {
            var rules = grammar.Rules.ToArray();
            var rand = ThreadSafeRandom.ThisThreadsRandom;
            Rule randomRule = rules[rand.Next(rules.Length)];
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