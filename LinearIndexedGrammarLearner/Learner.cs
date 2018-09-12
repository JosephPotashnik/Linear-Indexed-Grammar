using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using LinearIndexedGrammarParser;
using System.Threading;
using System.IO;

namespace LinearIndexedGrammarLearner
{
    public class GrammarWithProbability : IDisposable
    {
        public readonly Grammar Grammar;
        public readonly double Probability;

        public GrammarWithProbability(Grammar g, double probability)
        {
            this.Grammar = g;
            this.Probability = probability;
        }

        public void Dispose()
        {
            Grammar.Dispose();
        }
    }
    public class Learner
    {
        private readonly Dictionary<string, int> sentencesWithCounts;
        public Grammar originalGrammar;
        private GrammarPermutations gp;
        private Vocabulary voc;
        private int maxWordsInSentence;

        public Learner(string[] sentences, int maxWordsInSentence, Vocabulary voc)
        {
            this.voc = voc;
            this.maxWordsInSentence = maxWordsInSentence;
            sentencesWithCounts = sentences.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
        }

        ////We create the "promiscuous grammar" as initial grammar.
        public Grammar CreateInitialGrammar()
        {
            originalGrammar = new Grammar();
            var posInText = voc.POSWithPossibleWords.Keys;
            gp = new GrammarPermutations(posInText.ToArray());
            gp.ReadPermutationWeightsFromFile();

            foreach (var pos in posInText)
            {
                originalGrammar.AddGrammarRule(new Rule(Grammar.StartRule, new[] { pos, Grammar.StartRule }));
                originalGrammar.AddGrammarRule(new Rule(Grammar.StartRule, new[] { pos }));
            }

            //originalGrammar.RenameStartVariable();
            originalGrammar.GenerateAllStaticRulesFromDynamicRules();
            return originalGrammar;
        }

        private SentenceParsingResults[] ParseAllSentences(Grammar currentHypothesis)
        {
            SentenceParsingResults[] allParses = new SentenceParsingResults[sentencesWithCounts.Count];

            try
            {
                var timeout = 500; // 0.5 seconds
                var cts = new CancellationTokenSource(timeout);
                Parallel.ForEach(sentencesWithCounts, new ParallelOptions { CancellationToken = cts.Token}, (sentenceItem, loopState, i) =>
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
            catch (OperationCanceledException e)
            {
                //parse tree too long to parse
                //the grammar is too recursive,
                //decision - discard it and continue.

                string s = "parsing took too long (0.5 second), for the grammar:\r\n" + currentHypothesis.ToString();
                NLog.LogManager.GetCurrentClassLogger().Info(s);

                return null; //parsing failed.

            }
            catch (Exception)
            {
                return null; //parsing failed.
            }
        }


        public int GetNumberOfParseTrees(Grammar hypothesis, int maxWordsInSentence)
        {
            //best case: tree depth = log (maxWordsInSentence) for a fully balanced tree
            //if the tree is totally binary but extremely non-balanced (fully right or left branching), tree depth = words(leaves) -1.

            //what happens when there are abstract non-terminals that do not correspond to input nodes?
            //i.e, say, category I (auxiliary syntactic poition, not always phonteically overt)
            //or category C (complementizer syntactic position ,not always phonetically overt)

            //working assumption:
            var treeDepth = maxWordsInSentence + 3;
            //TODO: find a safe upper bound to tree depth, which will be a function of
            //max words in sentence, possibly also a function of the number of different POS.
            var posInText = voc.POSWithPossibleWords.Keys.ToHashSet();
            Task<SubtreeCountsWithNumberOfWords> t = Task.Run(() =>
            {
                SubTreeCountsCache cache = new SubTreeCountsCache(hypothesis, treeDepth);
                GrammarTreeCountsCalculator treeCalculator = new GrammarTreeCountsCalculator(hypothesis, posInText, cache);
                return treeCalculator.NumberOfParseTreesPerWords(treeDepth);
            });

            if (!t.Wait(500))
            {
                //string s = "computing all parse trees took too long (2.5 seconds), for the grammar:\r\n" + hypothesis.ToString();
                //NLog.LogManager.GetCurrentClassLogger().Info(s);

                //throw new Exception();
            }
            var parseTreesCountPerWords = t.Result;
            var numberOfParseTreesBelowMaxWords = parseTreesCountPerWords.WordsTreesDic.Values.Where(x => x.WordsCount <= maxWordsInSentence).Select(x => x.TreesCount).Sum();

            return numberOfParseTreesBelowMaxWords;
        }

        public double Probability(Grammar currentHypothesis)
        {
            double prob = 0;
            var allParses = ParseAllSentences(currentHypothesis);
            if (allParses != null)
            {
                //NOT SURE that the statement below is correct.
                //can a same tree represent two different sentences?
                //if not - ignore the TODO below. think.

                //TODO: same tree could be produced for different sentences by chance.
                //here you will count the same parse tree several times instead of once.
                // fix - count them only once.
                var totalTreesCountofData = allParses.Select(x => x.Trees.Count).Sum();

                if (totalTreesCountofData != 0)
                {
                    var totalTreesCountofGrammar = GetNumberOfParseTrees(currentHypothesis, maxWordsInSentence);
                    prob = (totalTreesCountofData) / (double)(totalTreesCountofGrammar);
                
                    if (prob > 1)
                    {
                        return 0;
                        //the case where probabilityOfInputGivenGrammar > 1 arises when
                        //totalTreesCountofData > totalTreesCountofGrammar, which can happen because totalTreesCountofGrammar
                        //is computed only up to a certain depth of the tree.
                        //so it's possible that the input data is parsed in a tree whose depth exceeds the depth we have allowed above.
                        
                        //assumption: we will reject grammars with data parsed too deep.
                        //discuss: what is the upper bound of tree depth as a function of the number of words in the sentence?
                        //right now: it is depth = maxWords+2. change?


                        //throw new Exception("probability is wrong!");
                    }
                }
            }
            return prob;
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

            var newGrammar = new Grammar(currentHypothesis);


            //mutate the grammar.
            var g =  m(newGrammar);
            g?.GenerateAllStaticRulesFromDynamicRules();
            return g;

        }

        internal GrammarWithProbability ComputeProbabilityForGrammar(GrammarWithProbability originalGrammar, Grammar mutatedGrammar)
        {
            double prob = 0;
            if (mutatedGrammar != null)
            {
                //assuming: insertion of rule adds as of yet unused rule
                //so it does not affect the parsibility of the grammar nor its probability.
                if (mutatedGrammar.Rules.Count() > originalGrammar.Grammar.Rules.Count() )
                    return new GrammarWithProbability(mutatedGrammar, originalGrammar.Probability);

                prob = Probability(mutatedGrammar);

            }
            return new GrammarWithProbability(mutatedGrammar, prob);
        }
    }
}