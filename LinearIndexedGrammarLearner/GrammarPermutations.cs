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
        public delegate ContextSensitiveGrammar GrammarMutation(ContextSensitiveGrammar grammar);
        private const int NumberOfRetries = 3;
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

        public ContextSensitiveGrammar SpreadPOSToRHS(ContextSensitiveGrammar grammar)
        {
            for (var i = 0; i < NumberOfRetries; i++)
            {
                var rand = ThreadSafeRandom.ThisThreadsRandom;
                DerivedCategory posRHS = new DerivedCategory(PartsOfSpeechCategories[rand.Next(PartsOfSpeechCategories.Length)].ToString());

                //choose random rule one of its RHS nonterminals we will replace by the pos chosen above.
                Rule randomRule = GetRandomNonStackChangingRule(grammar);
                int randomChildIndex = rand.Next(randomRule.RightHandSide.Length);

                var originalRHSSymbol = randomRule.RightHandSide[randomChildIndex];

                if (originalRHSSymbol.BaseEquals(posRHS)) continue;

                var replaceRule = new Rule(randomRule);
                replaceRule.RightHandSide[randomChildIndex].SetBase(posRHS);
                if (grammar.ContainsSameRHSRule(replaceRule, PartsOfSpeechCategories)) continue;

                grammar.DeleteGrammarRule(randomRule);
                grammar.AddGrammarRule(replaceRule);

                return grammar;
            }
            return null;
        }

        public ContextSensitiveGrammar SpreadRuleLHSToRHS(ContextSensitiveGrammar grammar)
        {
            var lhsCategories = grammar.dynamicRules.Keys.ToArray();

            //optimization: if there is only one LHS category (START), don't spread it.
            if (lhsCategories.Length < 2)
                return null;

            for (var i = 0; i < NumberOfRetries; i++)
            {
                //find LHS to spread:
                var rand = ThreadSafeRandom.ThisThreadsRandom;

                DerivedCategory lhs = new DerivedCategory(lhsCategories[rand.Next(lhsCategories.Length)].ToString());

                //choose random rule one of its RHS nonterminals we will replace by lhs chosen above.
                Rule randomRule = GetRandomNonStackChangingRule(grammar);

                //spread LHS only to binary rules. Spreading the LHS to an unary rule
                //is equivalent to variable renaming.

                if (randomRule.RightHandSide.Length < 2)
                    continue;

                //select a right hand side category randomly.
                int randomChildIndex = GetRandomChildIndex(randomRule.RightHandSide.Length);
                var originalRHSSymbol = randomRule.RightHandSide[randomChildIndex];

                if (originalRHSSymbol.Equals(lhs)) continue;

                var replaceRule = new Rule(randomRule);
                replaceRule.RightHandSide[randomChildIndex].SetBase(lhs);

                if (grammar.OnlyStartSymbolsRHS(replaceRule)) continue;
                if (grammar.ContainsSameRHSRule(replaceRule, PartsOfSpeechCategories)) continue;

                grammar.DeleteGrammarRule(randomRule);
                grammar.AddGrammarRule(replaceRule);

                return grammar;
            }
            return null;
        }
        
        public ContextSensitiveGrammar SpreadRuleLHSToLHS(ContextSensitiveGrammar grammar)
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
                replaceRule.LeftHandSide.SetBase(lhs);
                grammar.DeleteGrammarRule(randomRule);
                grammar.AddGrammarRule(replaceRule);

                return grammar;
            }
            return null;
        }

        public ContextSensitiveGrammar DeleteRule(ContextSensitiveGrammar grammar)
        {
            Rule randomRule = GetRandomRule(grammar);
            grammar.DeleteGrammarRule(randomRule);
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
            int RelationOfLHSToPOS = 2;
            int upperBoundNonTerminals = PartsOfSpeechCategories.Length * RelationOfLHSToPOS;
            if (upperBoundNonTerminals < 6) upperBoundNonTerminals = 6;

            //do not consider START in the upper bound.
            return ((lhsCategories.Length - 1) >= upperBoundNonTerminals);
            
        }
        public ContextSensitiveGrammar InsertBinaryRule(ContextSensitiveGrammar grammar)
        {
            var lhsCategories = grammar.dynamicRules.Keys.ToArray();
            var categoriesPool = lhsCategories.Concat(PartsOfSpeechCategories).ToArray();

            if (DoesNumberOfLHSCategoriesExceedMax(lhsCategories)) return null;

            for (var k = 0; k < NumberOfRetries; k++)
            {
                //create a new non terminal
                string baseNonTerminal = null;
                baseNonTerminal = $"X{newNonTerminalCounter++}";

                var newCategory = new DerivedCategory(baseNonTerminal, ContextFreeGrammar.StarSymbol);

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
                    DerivedCategory randomRightHandSideCategory = new DerivedCategory(categoriesPool[rand.Next(categoriesPool.Length)].ToString());

                    rightHandSide[i] = randomRightHandSideCategory;
                    if (addStarToRightHandSide)
                        rightHandSide[i].Stack = ContextFreeGrammar.StarSymbol;
                    
                    addStarToRightHandSide = !addStarToRightHandSide;
                }

                var newRule = new Rule(newCategory, rightHandSide);
                if (grammar.OnlyStartSymbolsRHS(newRule)) continue;
                if (grammar.ContainsSameRHSRule(newRule, PartsOfSpeechCategories)) continue;

                grammar.AddGrammarRule(newRule);

                return grammar;
            }
            return null;
        }
        public ContextSensitiveGrammar InsertPop1Rule(ContextSensitiveGrammar grammar)
        {
            var rand = ThreadSafeRandom.ThisThreadsRandom;

            var lhsCategories = grammar.dynamicRules.Keys.ToArray();
            var categoriesPool = lhsCategories.Concat(PartsOfSpeechCategories).ToArray();

            for (var k = 0; k < NumberOfRetries; k++)
            {
                string moveable = categoriesPool[rand.Next(categoriesPool.Length)].ToString();
                var lhs = new DerivedCategory(moveable, moveable);
                var epsiloncat = new DerivedCategory("Epsilon");
                epsiloncat.StackSymbolsCount = -1;
                var newRule = new StackChangingRule(lhs, new[] { epsiloncat });

                if (grammar.ContainsSameEpsilonRule(newRule)) continue;
                grammar.AddGrammarRule(newRule);
                return grammar;
            }

            return null;
        }

        public ContextSensitiveGrammar InsertPush1Rule(ContextSensitiveGrammar grammar)
        {
            var lhsCategories = grammar.dynamicRules.Keys.ToArray();
            var categoriesPool = lhsCategories.Concat(PartsOfSpeechCategories).ToArray();

            if (DoesNumberOfLHSCategoriesExceedMax(lhsCategories)) return null;

            for (var k = 0; k < NumberOfRetries; k++)
            {
                //create a new non terminal
                string baseNonTerminal = null;
                baseNonTerminal = $"X{newNonTerminalCounter++}";

                var newCategory = new DerivedCategory(baseNonTerminal, ContextFreeGrammar.StarSymbol);

                int len = 2;
                var rand = ThreadSafeRandom.ThisThreadsRandom;
                int spinePositionInRHS = rand.Next(len);
                spinePositionInRHS = 1; //initially, constrain moveables to be the first position, spine to the right (=movement to left only)

                string moveable = categoriesPool[rand.Next(categoriesPool.Length)].ToString();
                var moveableCategory = new DerivedCategory(moveable);
                var spineCategory = new DerivedCategory(lhsCategories[rand.Next(lhsCategories.Length)].ToString(), ContextFreeGrammar.StarSymbol + moveable);
                spineCategory.StackSymbolsCount = 1;

                var rightHandSide = new DerivedCategory[len];
                for (var i = 0; i < len; i++)
                    rightHandSide[i] = (spinePositionInRHS == i) ? spineCategory : moveableCategory;

                var newRule = new StackChangingRule(newCategory, rightHandSide);
                if (grammar.OnlyStartSymbolsRHS(newRule)) continue;
                if (grammar.ContainsSameRHSRule(newRule, PartsOfSpeechCategories)) continue;

                grammar.AddGrammarRule(newRule);
                return grammar;
            }
            return null;
        }

        public ContextSensitiveGrammar InsertUnaryRule(ContextSensitiveGrammar grammar)
        {
            var startCategory = new DerivedCategory(ContextFreeGrammar.StartRule);
            var lhsCategories = grammar.dynamicRules.Keys.ToArray();
            var categoriesPool = lhsCategories.Concat(PartsOfSpeechCategories).ToArray();
            if (DoesNumberOfLHSCategoriesExceedMax(lhsCategories)) return null;

            for (var k = 0; k < NumberOfRetries; k++)
            {
                //create a new non terminal
                string baseNonTerminal = null;
                baseNonTerminal = $"X{newNonTerminalCounter++}";

                var newCategory = new DerivedCategory(baseNonTerminal, ContextFreeGrammar.StarSymbol);

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
                        rightHandSide[i].Stack = ContextFreeGrammar.StarSymbol;

                    addStarToRightHandSide = !addStarToRightHandSide;
                }

                var newRule = new Rule(newCategory, rightHandSide);

                if (grammar.ContainsSameRHSRule(newRule, PartsOfSpeechCategories)) continue;

                grammar.AddGrammarRule(newRule);

                return grammar;
            }
            return null;
        }

        private static int GetRandomChildIndex(int rhsLength)
        {
            int randomChildIndex = 0;
            var rand = ThreadSafeRandom.ThisThreadsRandom;
            randomChildIndex = rand.Next(rhsLength);
            return randomChildIndex;
        }

      

        private static Rule GetRandomRule(ContextSensitiveGrammar grammar)
        {
            var rules = grammar.Rules.ToArray();
            var rand = ThreadSafeRandom.ThisThreadsRandom;
            Rule randomRule = rules[rand.Next(rules.Length)];
            return randomRule;
        }

        private static Rule GetRandomNonStackChangingRule(ContextSensitiveGrammar grammar)
        {
            var rules = grammar.Rules.Where(x => x.GetType() != typeof(StackChangingRule)).ToArray();
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