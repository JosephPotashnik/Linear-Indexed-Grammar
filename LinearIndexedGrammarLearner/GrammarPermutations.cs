﻿using System;
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
                Rule randomRule = GetRandomStackConstantRule(grammar);

                int randomChildIndex = rand.Next(randomRule.RightHandSide.Length);

                var originalRHSSymbol = randomRule.RightHandSide[randomChildIndex];

                if (originalRHSSymbol.BaseEquals(posRHS)) continue;

                var replaceRule = new Rule(randomRule);
                replaceRule.RightHandSide[randomChildIndex].SetBase(posRHS);
                if (!grammar.AddStackConstantRule(replaceRule)) continue;
                grammar.DeleteStackConstantRule(randomRule);

                return grammar;
            }
            return null;
        }

        
        public ContextSensitiveGrammar SpreadRuleLHSToRHS(ContextSensitiveGrammar grammar)
        {
            var lhsCategories = grammar.LHSCategories;

            //optimization: if there is only one LHS category (START), don't spread it.
            if (lhsCategories.Length < 2)
                return null;

            for (var i = 0; i < NumberOfRetries; i++)
            {
                //find LHS to spread:
                var rand = ThreadSafeRandom.ThisThreadsRandom;

                DerivedCategory lhs = new DerivedCategory(lhsCategories[rand.Next(lhsCategories.Length)].ToString());

                //choose random rule one of its RHS nonterminals we will replace by lhs chosen above.
                var randomRule = GetRandomRule(grammar);

                //spread LHS only to binary rules. Spreading the LHS to an unary rule
                //is equivalent to variable renaming. 
                //Note: it also excludes pop1 by definition since pop1 has epsilon as RHS
                if (randomRule.RightHandSide.Length < 2)
                    continue;
                //if (randomRule.OperationKey == MoveableOperationsKey.Pop1) continue;

                //select a right hand side category randomly.
                int randomChildIndex = GetRandomChildIndex(randomRule.RightHandSide.Length);
                var originalRHSSymbol = randomRule.RightHandSide[randomChildIndex];

                if (originalRHSSymbol.Equals(lhs)) continue;

                var replaceRule = randomRule.Clone();
                replaceRule.RightHandSide[randomChildIndex].SetBase(lhs);
                if (grammar.OnlyStartSymbolsRHS(replaceRule)) continue;

                if (randomRule is StackChangingRule r)
                {
                    //if stack changing rule, we don't change RHS of pop1 (it's epsilon, i.e. NP[NP] -> epsilon).
                    if (r.OperationKey == MoveableOperationsKey.Pop1) continue;

                    //in push rules, you can change only the spine symbol, i.e. the one that
                    //contains * symbol.
                    if (r.OperationKey == MoveableOperationsKey.Push1 &&
                        !originalRHSSymbol.Stack.Contains(ContextFreeGrammar.StarSymbol))
                        continue;
                }

                if (!replaceRule.AddRuleToGrammar(grammar)) continue;
                randomRule.DeleteRuleFromGrammar((grammar));
                return grammar;
            }
            return null;
        }
        
        public ContextSensitiveGrammar SpreadRuleLHSToLHS(ContextSensitiveGrammar grammar)
        {
            var lhsCategories = grammar.LHSCategories;
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

                if (originalLHSSymbol.BaseEquals(lhs)) continue;
                if (randomRule is StackChangingRule r)
                {
                    //you can't change the left hand side of pop1 rule (it is of the form NP[NP] ->epsilon)
                    if (r.OperationKey == MoveableOperationsKey.Pop1) continue;
                }

                var replaceRule = randomRule.Clone();
                replaceRule.LeftHandSide.SetBase(lhs);
                randomRule.DeleteRuleFromGrammar((grammar));
                replaceRule.AddRuleToGrammar(grammar, true);
                return grammar;
            }
            return null;
        }

        //DeleteRule == Delete StackConstantRule. perahps change notation.
        public ContextSensitiveGrammar DeleteRule(ContextSensitiveGrammar grammar)
        {
            Rule randomRule = GetRandomStackConstantRule(grammar);
            grammar.DeleteStackConstantRule(randomRule);
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
            var lhsCategories = grammar.LHSCategories;
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
                if (!grammar.AddStackConstantRule(newRule)) continue;

                return grammar;
            }
            return null;
        }

        public ContextSensitiveGrammar DeleteMovement(ContextSensitiveGrammar grammar)
        {
            var rand = ThreadSafeRandom.ThisThreadsRandom;
            var keys = grammar.StackChangingRules.Keys.ToArray();
            if (keys.Length == 0) return null;
            var moveable = keys[rand.Next(keys.Length)];

            if (grammar.StackChangingRules[moveable].MoveOps.ContainsKey(MoveableOperationsKey.Push1))
            {
                //if there is more than one push1 rule, delete only push1 rule, 
                //do not delete the pop1 rule.
                var oldRule = grammar.StackChangingRules[moveable].GetRandomRule(MoveableOperationsKey.Push1);
                grammar.StackChangingRules[moveable].DeleteRule(oldRule, MoveableOperationsKey.Push1);

                if (grammar.StackChangingRules[moveable].MoveOps[MoveableOperationsKey.Push1].Count > 0)
                   return grammar;
                else
                {
                    //only one push1 rule, delete both push1 and pop1 rules.
                    oldRule = grammar.StackChangingRules[moveable].GetRandomRule(MoveableOperationsKey.Pop1);
                    grammar.StackChangingRules[moveable].DeleteRule(oldRule, MoveableOperationsKey.Pop1);

                }
            }
            return null;
        }

        public ContextSensitiveGrammar InsertMovement(ContextSensitiveGrammar grammar)
        {
            var rand = ThreadSafeRandom.ThisThreadsRandom;
            var lhsCategories = grammar.LHSCategories;
            var categoriesPool = lhsCategories.Concat(PartsOfSpeechCategories).ToArray();
            string moveable = ContextFreeGrammar.StartRule;

            //Don't allow the moveable category to be START Category (for now)
            while (moveable == ContextFreeGrammar.StartRule)
                 moveable = categoriesPool[rand.Next(categoriesPool.Length)].ToString();

            if (InsertPush1Rule(grammar, moveable))
            {
                //if Inserting pop1 does not succeed (i.e, of the form NP[NP] -> epsilon)
                //it means the pop1 rule already exists, so it is still OK.
                InsertPop1Rule(grammar, moveable);
                return grammar;
            }
            return null;
        }

        private bool InsertPop1Rule(ContextSensitiveGrammar grammar, string moveable = null)
        {
            var rand = ThreadSafeRandom.ThisThreadsRandom;

            if (moveable == null)
            {
                var lhsCategories = grammar.LHSCategories;
                var categoriesPool = lhsCategories.Concat(PartsOfSpeechCategories).ToArray();
                moveable = categoriesPool[rand.Next(categoriesPool.Length)].ToString();
            }

            for (var k = 0; k < NumberOfRetries; k++)
            {
                var moveableCategory = new DerivedCategory(moveable);
                var lhs = new DerivedCategory(moveable, moveable);
                var epsilonCat = new DerivedCategory(ContextFreeGrammar.EpsilonSymbol) {StackSymbolsCount = -1};
                var r = new Rule(lhs, new[] { epsilonCat });
                var newRule = new StackChangingRule(r, MoveableOperationsKey.Pop1, moveableCategory);
                if (!newRule.AddRuleToGrammar(grammar)) continue;
                return true;
            }

            return false;
        }

        private bool InsertPush1Rule(ContextSensitiveGrammar grammar, string moveable = null)
        {
            var rand = ThreadSafeRandom.ThisThreadsRandom;
            var lhsCategories = grammar.LHSCategories;
            var categoriesPool = lhsCategories.Concat(PartsOfSpeechCategories).ToArray();

            if (moveable == null)
              moveable = categoriesPool[rand.Next(categoriesPool.Length)].ToString();

            if (DoesNumberOfLHSCategoriesExceedMax(lhsCategories)) return false;

            for (var k = 0; k < NumberOfRetries; k++)
            {
                //create a new non terminal
                string baseNonTerminal = null;
                baseNonTerminal = $"X{newNonTerminalCounter++}";

                var newCategory = new DerivedCategory(baseNonTerminal, ContextFreeGrammar.StarSymbol);

                int len = 2;
                int spinePositionInRHS = rand.Next(len);
                spinePositionInRHS = 1; //initially, constrain moveables to be the first position, spine to the right (=movement to left only)
                var moveableCategory = new DerivedCategory(moveable);
                var spineCategory = new DerivedCategory(lhsCategories[rand.Next(lhsCategories.Length)].ToString(),
                    ContextFreeGrammar.StarSymbol + moveable) {StackSymbolsCount = 1};

                //if the form is Y[*] -> X1 X1[*X1], it's also like Y -> X1 X1[X1], 
                //since there's a pop rule for the moveable X1: X1[X1] -> epsilon,
                //overall it's like inserting Y -> X1 rule in an overcomplicated way.
                if (spineCategory.BaseEquals(moveableCategory)) continue;

                var rightHandSide = new DerivedCategory[len];
                for (var i = 0; i < len; i++)
                    rightHandSide[i] = (spinePositionInRHS == i) ? spineCategory : moveableCategory;

                var r = new Rule(newCategory, rightHandSide);
                var newRule = new StackChangingRule(r, MoveableOperationsKey.Push1, moveableCategory);
                if (grammar.OnlyStartSymbolsRHS(newRule)) continue;
                if (!newRule.AddRuleToGrammar(grammar)) continue;

                return true;
            }
            return false;
        }

        public ContextSensitiveGrammar InsertUnaryRule(ContextSensitiveGrammar grammar)
        {
            var startCategory = new DerivedCategory(ContextFreeGrammar.StartRule);
            var lhsCategories = grammar.LHSCategories;

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
                if (!grammar.AddStackConstantRule(newRule)) continue;

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

        private static Rule GetRandomStackConstantRule(ContextSensitiveGrammar grammar)
        {
            var rules = grammar.StackConstantRulesArray.ToArray();
            var rand = ThreadSafeRandom.ThisThreadsRandom;
            Rule randomRule = rules[rand.Next(rules.Length)];
            return randomRule;
        }

        private static Rule GetRandomRule(ContextSensitiveGrammar grammar)
        {
            Rule[] stackConstantRules = grammar.StackConstantRulesArray;
            Rule[] stackChangingRules = grammar.StackChangingRulesArray;
            var joinedSequence = stackConstantRules.Concat(stackChangingRules).ToArray();
            var rand = ThreadSafeRandom.ThisThreadsRandom;
            var randomRule = joinedSequence[rand.Next(joinedSequence.Length)];
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