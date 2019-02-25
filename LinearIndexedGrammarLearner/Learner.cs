using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LinearIndexedGrammarParser;

namespace LinearIndexedGrammarLearner
{
    public class Learner
    {
        private readonly int _maxWordsInSentence;
        private readonly int _minWordsInSentence;

        private readonly HashSet<string> _posInText;
        private readonly SentenceParsingResults[] _sentencesWithCounts;
        private readonly Vocabulary _voc;
        // ReSharper disable once NotAccessedField.Local
        private GrammarPermutations _gp;
        public const int InitialTimeOut = 1500;
        public static int ParsingTimeOut = InitialTimeOut; //in milliseconds
        public const int GrammarTreeCountCalculationTimeOut = 500; //in milliseconds
        public GrammarTreeCountsCalculator _grammarTreesCalculator;

        public Learner(string[] sentences,  int minWordsInSentence, int maxWordsInSentence, HashSet<string> posInText, Vocabulary universalVocabulary)
        {
            _voc = universalVocabulary;
            _posInText = posInText;
            _maxWordsInSentence = maxWordsInSentence;
            _minWordsInSentence = minWordsInSentence;
            var dict = sentences.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
            _sentencesWithCounts = new SentenceParsingResults[dict.Count];
            var arrayOfDesiredVals = dict.Select(x => (x.Key, x.Value, x.Key.Split().Length)).ToArray();
            _grammarTreesCalculator = new GrammarTreeCountsCalculator(_posInText, _minWordsInSentence, _maxWordsInSentence);

            for (int i = 0; i < _sentencesWithCounts.Length; i++)
            {
                _sentencesWithCounts[i] = new SentenceParsingResults();
                _sentencesWithCounts[i].Sentence = arrayOfDesiredVals[i].Key;
                _sentencesWithCounts[i].Count = arrayOfDesiredVals[i].Value;
                _sentencesWithCounts[i].Length = arrayOfDesiredVals[i].Length;

            }
        }

        ////We create the "promiscuous grammar" as initial grammar.
        public ContextSensitiveGrammar CreateInitialGrammar(bool isCFGGrammar)
        {
            _gp = new GrammarPermutations(isCFGGrammar);

            var rules = new List<Rule>();
            foreach (var pos in _posInText)
            {
                rules.Add(new Rule("X1", new[] {pos, "X1"}));
                rules.Add(new Rule("X1", new[] {pos}));
            }

            rules.Add(new Rule(ContextFreeGrammar.StartSymbol, new[] {"X1"}));

            var originalGrammar = new ContextSensitiveGrammar(rules);

            return originalGrammar;
        }

        public SentenceParsingResults[] ParseAllSentences(ContextFreeGrammar currentHypothesis)
        {
            try
            {
                var cts = new CancellationTokenSource(ParsingTimeOut);
                var po = new ParallelOptions {CancellationToken = cts.Token};
                Parallel.ForEach(_sentencesWithCounts, po,
                    (sentenceItem, loopState, i) =>
                    {
                        var parser = new EarleyParser(currentHypothesis, _voc);
                        var n = parser.ParseSentence(sentenceItem.Sentence, cts);
                        _sentencesWithCounts[i].Trees = n;
                        po.CancellationToken.ThrowIfCancellationRequested();
                    });

                return _sentencesWithCounts;
            }
            catch (OperationCanceledException)
            {
                //parse tree too long to parse
                //the grammar is too recursive,
                //decision - discard it and continue.
                //string s = "parsing took too long, for the grammar:\r\n" + currentHypothesis.ToString();
                //NLog.LogManager.GetCurrentClassLogger().Info(s);
                return null; //parsing failed.
            }
            catch (AggregateException e) when (e.InnerExceptions.OfType<InfiniteParseException>().Any())
            {
                throw;
            }

        }


        public Dictionary<int,int> GetGrammarTrees(ContextFreeGrammar hypothesis)
        {
            return _grammarTreesCalculator.Recalculate(hypothesis);
        }


        public Dictionary<int, int> CollectUsages(ContextSensitiveGrammar currentHypothesis)
        {
            var cfGrammar = new ContextFreeGrammar(currentHypothesis);

            //store original parsing timeout aside, allow this parse to take as long as
            //required (because the optimal parse might surpass the parsing time out due to
            // accidental localized cpu unavailability.
            //and we don't want the accepted grammar to fail here -- the accepted grammar
            //already survived the same timeout previously. 
            int parsingTimeout = ParsingTimeOut;
            ParsingTimeOut = int.MaxValue;
            var allParses = ParseAllSentences(cfGrammar);
            ParsingTimeOut = parsingTimeout;

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
            Console.WriteLine("returning usages dic null. meaning that all parses are zero.");
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

        internal ContextSensitiveGrammar GetNeighbor(ContextSensitiveGrammar currentHypothesis)
        {
            //choose mutation function in random (weighted according to weights file)
            var m = GrammarPermutations.GetWeightedRandomMutation();
            var newGrammar = new ContextSensitiveGrammar(currentHypothesis);

            //mutate the grammar.
            var g = m(newGrammar);
            return g;
        }
    }
}