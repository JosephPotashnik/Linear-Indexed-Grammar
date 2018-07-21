using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using LinearIndexedGrammarParser;

namespace LinearIndexedGrammarLearner
{
    public class GrammarWithProbability
    {
        public readonly Grammar Grammar;
        public readonly double Probability;

        public GrammarWithProbability(Grammar g, double probability)
        {
            this.Grammar = g;
            this.Probability = probability;
        }
    }
    public class Learner
    {
        private readonly Dictionary<string, int> sentencesWithCounts;
        public Grammar originalGrammar;
        private GrammarPermutations gp;
        private Vocabulary voc;
        private int maxWordsInSentence;

        public Learner(string[] sentences, int maxWordsInSentence)
        {
            this.maxWordsInSentence = maxWordsInSentence;
            sentencesWithCounts = sentences.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
        }

        ////We create the "promiscuous grammar" as initial grammar.
        public Grammar CreateInitialGrammar(Vocabulary voc)
        {
            this.voc = voc;
            originalGrammar = new Grammar();
            var posInText = voc.POSWithPossibleWords.Keys;
            gp = new GrammarPermutations(posInText.ToArray());
            gp.ReadPermutationWeightsFromFile();

            foreach (var pos in posInText)
            {
                originalGrammar.AddGrammarRule(new Rule(Grammar.StartRule, new[] { pos, Grammar.StartRule }));
                originalGrammar.AddGrammarRule(new Rule(Grammar.StartRule, new[] { pos }));
            }

            return originalGrammar;
        }

        private SentenceParsingResults[] ParseAllSentences(Grammar currentHypothesis)
        {
            SentenceParsingResults[] allParses = new SentenceParsingResults[sentencesWithCounts.Count];

            try
            {
                Parallel.ForEach(sentencesWithCounts, (sentenceItem, state, i) =>
                {
                    var parser = new EarleyParser(currentHypothesis, this.voc);
                    var n = parser.ParseSentence(sentenceItem.Key);
                    allParses[i] = new SentenceParsingResults()
                    {
                        Sentence = sentenceItem.Key,
                        Trees = n,
                        Count = sentenceItem.Value
                    };
                });

                return allParses;
            }
            catch (Exception)
            {
                return null; //parsing failed.
            }
        }

        public static double NegativeLogProbability(double probability)
        {
            return -Math.Log(probability, 2);
        }

        public Energy Energy(Grammar currentHypothesis)
        {
            var energy = new Energy();
            var allParses = ParseAllSentences(currentHypothesis);
            if (allParses != null)
            {
                //TODO: same tree could be produced for different sentences by chance.
                //here you will count the same parse tree several times instead of once.
                // fix - count them only once.
                var totalTreesCountofData = allParses.Select(x => x.Trees.Count).Sum();

                if (totalTreesCountofData == 0)
                {
                    energy.DataEnergy = int.MaxValue;
                    energy.Probability = 0.0;
                }
                else
                {
                    var generator = new EarleyGenerator(currentHypothesis);
                    var possibleTreesOfGrammar = generator.ParseSentence("", maxWordsInSentence);

                    var totalTreesCountofGrammar = possibleTreesOfGrammar.Count;

                    double probabilityOfInputGivenGrammar = (totalTreesCountofData) / (double)(totalTreesCountofGrammar);
                    energy.Probability = probabilityOfInputGivenGrammar;

                    if (probabilityOfInputGivenGrammar < 0 || probabilityOfInputGivenGrammar > 1)
                    {
                        throw new Exception("probability is wrong!");
                    }


                    energy.DataEnergy = (int)(NegativeLogProbability(probabilityOfInputGivenGrammar) * 100);
                    if (energy.DataEnergy < 0)
                    {
                        throw new Exception("energy is wrong!");
                    }
                }
                return energy;
            }
            return null;
        }
        public Dictionary<int, int> CollectUsages(Grammar currentHypothesis)
        {
            var allParses = ParseAllSentences(currentHypothesis);
            var usagesDic = new Dictionary<int, int>();

            if (allParses != null)
            {
                foreach (var sentenceParsingResult in allParses)
                {
                    foreach (var tree in sentenceParsingResult.Trees)
                        CollectRuleUsages(tree, usagesDic, sentenceParsingResult.Count);
                }
                return usagesDic;
            }

            return null;
        }

        private static void CollectRuleUsages(EarleyNode n, Dictionary<int, int> ruleCounts, int sentenceCount)
        {
            if (n.Children != null)
            {
                foreach (var child in n.Children)
                    CollectRuleUsages(child, ruleCounts, sentenceCount);
            }

            if (n.RuleNumber != 0) //SCAN_RULE_NUMBER = 0.
            {
                if (!ruleCounts.ContainsKey(n.RuleNumber)) ruleCounts[n.RuleNumber] = 0;
                ruleCounts[n.RuleNumber] += sentenceCount;
                //add +1 to the count of the rule, multiplied by the number of times the sentence appears in the text (sentenceCount).
            }
        }

        internal Grammar GetNeighbor(Grammar currentHypothesis)
        {
            //choose mutation function in random (weighted according to weights file)
            var m = GrammarPermutations.GetWeightedRandomMutation();

            //deep copy the grammr
            var newGrammar = new Grammar(currentHypothesis);

            //mutate the grammar.
            return m(newGrammar);
        }

        internal GrammarWithProbability ComputeProbabilityForGrammar(GrammarWithProbability originalGrammar, Grammar mutatedGrammar)
        {
            double prob = 0.0;
            if (mutatedGrammar != null)
            {
                //assuming: insertion of rule adds as of yet unused rule
                //so it does not affect the parsibility of the grammar nor its probability.
                if (mutatedGrammar.RuleCount > originalGrammar.Grammar.RuleCount )
                    return new GrammarWithProbability(mutatedGrammar, originalGrammar.Probability);

                Energy newEnergy = null;
                newEnergy = Energy(mutatedGrammar);

                if (newEnergy != null)
                    prob = newEnergy.Probability;
            }
            return new GrammarWithProbability(mutatedGrammar, prob);
        }
    }
}