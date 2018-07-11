using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using LinearIndexedGrammarParser;

namespace LinearIndexedGrammarLearner
{

    public class Learner
    {
        private readonly Dictionary<string, int> sentencesWithCounts;
        public Grammar originalGrammar;
        private GrammarPermutations gp = new GrammarPermutations();
        private Vocabulary voc;
        private int maxWordsInSentence;




        public Learner(string[] sentences, int maxWordsInSentence)
        {
            this.maxWordsInSentence = maxWordsInSentence;
            gp.ReadPermutationWeightsFromFile();
            sentencesWithCounts = sentences.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
        }

        ////We create the "promiscuous grammar" as initial grammar.
        public Grammar CreateInitialGrammar(Vocabulary voc)
        {
            originalGrammar = new Grammar();
            this.voc = voc;
            var posInText = voc.POSWithPossibleWords.Keys;

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
            return Math.Ceiling(-Math.Log(probability, 2));
        }

        public Energy Energy(Grammar currentHypothesis)
        {
            var energy = new Energy();
            var allParses = ParseAllSentences(currentHypothesis);
            if (allParses != null)
            {
                //TODO: same tree could be produced for different sentences (ambiguity)
                //here you will count it several times instead of once.
                // fix - count them only once.
                var totalTreesCountofData = allParses.Select(x => x.Trees.Count).Sum();
                var generator = new EarleyGenerator(currentHypothesis);
                var possibleTreesOfGrammar = generator.ParseSentence("", maxWordsInSentence);

                var totalTreesCountofGrammar = possibleTreesOfGrammar.Count;

                double probabilityOfInputGivenGrammar = (totalTreesCountofData) / (double)(totalTreesCountofGrammar);

                if (probabilityOfInputGivenGrammar < 0 || probabilityOfInputGivenGrammar > 1)
                {
                    throw new Exception("probability is wrong!");
                }


                energy.DataEnergy = (int)(NegativeLogProbability(probabilityOfInputGivenGrammar) * 100);
                return energy;
            }
            return null;
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
    }
}