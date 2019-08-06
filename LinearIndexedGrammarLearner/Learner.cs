﻿using System;
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
        public readonly EarleyParser[] _sentencesParser;
        private readonly Vocabulary _voc;

        // ReSharper disable once NotAccessedField.Local
        private GrammarPermutations _gp;
        public GrammarTreeCountsCalculator _grammarTreesCalculator;

        public Learner(string[][] sentences, int minWordsInSentence, int maxWordsInSentence, HashSet<string> posInText,
            Vocabulary universalVocabulary)
        {
            _voc = universalVocabulary;
            _posInText = posInText;
            _maxWordsInSentence = maxWordsInSentence;
            _minWordsInSentence = minWordsInSentence;

            //var dict1 = sentences.GroupBy(x => x.Length).ToDictionary(g => g.Key, g => g.Count());

            var dict = sentences.GroupBy(x => string.Join(" ", x)).ToDictionary(g => g.Key, g => g.Count());

            Parses = new SentenceParsingResults[dict.Count];
            var arrayOfDesiredVals = dict.Select(x => (x.Key, x.Value)).ToArray();

            _grammarTreesCalculator =
                new GrammarTreeCountsCalculator(_posInText, _minWordsInSentence, _maxWordsInSentence);
            _sentencesParser = new EarleyParser[Parses.Length];

            for (var i = 0; i < Parses.Length; i++)
            {
                Parses[i] = new SentenceParsingResults();
                Parses[i].Sentence = arrayOfDesiredVals[i].Key.Split();
                Parses[i].Count = arrayOfDesiredVals[i].Value;
                Parses[i].Length = Parses[i].Sentence.Length;
            }
        }

        public SentenceParsingResults[] Parses { get; }

        ////We create the "promiscuous grammar" as initial grammar.
        public ContextSensitiveGrammar CreateInitialGrammar(bool isCFGGrammar)
        {
            _gp = new GrammarPermutations(isCFGGrammar);

            var rules = new List<Rule>();
            foreach (var pos in _posInText)
            {
                //rules.Add(new Rule("X1", new[] { "X1", pos }));
                rules.Add(new Rule("X1", new[] {pos, "X1"}));
                rules.Add(new Rule("X1", new[] {pos}));
            }

            rules.Add(new Rule(ContextFreeGrammar.StartSymbol, new[] {"X1"}));

            var originalGrammar = new ContextSensitiveGrammar(rules);
            return originalGrammar;
        }

        public void ParseAllSentencesFromScratch(ContextSensitiveGrammar currentHypothesis)
        {
            var currentCFHypothesis = new ContextFreeGrammar(currentHypothesis);

            if (currentCFHypothesis.ContainsCyclicUnitProduction())
                throw new Exception("initial grammar should not contain cyclic unit productions");

            for (var i = 0; i < _sentencesParser.Length; i++)
                _sentencesParser[i] =
                    new EarleyParser(currentCFHypothesis, _voc,
                        false); //parser does not check for cyclic unit productions

            try
            {
                Parallel.ForEach(Parses,
                    (sentenceItem, loopState, i) =>
                    {
                        var n = _sentencesParser[i].ParseSentence(sentenceItem.Sentence);
                        Parses[i].GammaStates = n;
                    });
            }
            catch (OperationCanceledException)
            {
                //parse tree too long to parse
                //the grammar is too recursive,
                //decision - discard it and continue.
                //string s = "parsing took too long, for the grammar:\r\n" + currentHypothesis.ToString();
                //NLog.LogManager.GetCurrentClassLogger().Info(s);
                throw new Exception("initial grammar should parse quickly enough");
            }

            AcceptChanges();
        }

        public void RefreshParses()
        {
            for (var i = 0; i < Parses.Length; i++)
                Parses[i].GammaStates = _sentencesParser[i].GetGammaStates();
        }

        public void SetOriginalGrammarBeforePermutation()
        {
            for (var i = 0; i < Parses.Length; i++)
                _sentencesParser[i]._oldGrammar = _sentencesParser[i]._grammar;
        }



        public bool ReparseWithAddition(ContextSensitiveGrammar currentHypothesis, int numberOfGeneratingRule)
        {
            var currentCFHypothesis = new ContextFreeGrammar(currentHypothesis);

            if (currentCFHypothesis.ContainsCyclicUnitProduction())
                return false;

            var rs = currentCFHypothesis.Rules.Where(x => x.NumberOfGeneratingRule == numberOfGeneratingRule).ToList();

            if (rs.Count == 0)
            {
                //Console.WriteLine($"added ");

                //rs.Count == 0 when the new rule is unreachable from the existing set of rules.
                //that means that the parser earley items are exactly the same as before.
                //we can return immediately with no change.
                return true;
            }

            try
            {

                //for (int i = 0; i < Parses.Length; i++)
                //{
                //    var n = _sentencesParser[i].ReParseSentenceWithRuleAddition(currentCFHypothesis, rs);
                //    Parses[i].GammaStates = n;
                //}
                Parallel.ForEach(Parses,
                    (sentenceItem, loopState, i) =>
                    {
                        var n = _sentencesParser[i].ReParseSentenceWithRuleAddition(currentCFHypothesis, rs);
                        Parses[i].GammaStates = n;
                    });
            }
            catch (OperationCanceledException)
            {
                //parse tree too long to parse
                //the grammar is too recursive,
                //decision - discard it and continue.
                //string s = "parsing took too long, for the grammar:\r\n" + currentHypothesis.ToString();
                //NLog.LogManager.GetCurrentClassLogger().Info(s);
                return false;
            }

            return true;
        }

        public bool ReparseWithDeletion(ContextSensitiveGrammar currentHypothesis, int numberOfGeneratingRule)
        {
            var currentCFHypothesis = new ContextFreeGrammar(currentHypothesis);

            if (currentCFHypothesis.ContainsCyclicUnitProduction())
                return false;

            Dictionary<DerivedCategory, List<Rule>> rulesExceptDeletedRule =
                new Dictionary<DerivedCategory, List<Rule>>();

            List<Rule> deletedRule = new List<Rule>();
            foreach (var kvp in _sentencesParser[0]._grammar.StaticRules)
            {
                rulesExceptDeletedRule[kvp.Key] = new List<Rule>();

                foreach (var r in kvp.Value)
                {
                    if (r.NumberOfGeneratingRule == numberOfGeneratingRule)
                        deletedRule.Add(r);
                    else
                        rulesExceptDeletedRule[kvp.Key].Add(r);
                }
            }
              
            var leftCorner = new LeftCorner();
            var predictionSet = leftCorner.ComputeLeftCorner(rulesExceptDeletedRule);

            try
            {
                Parallel.ForEach(Parses,
                    (sentenceItem, loopState, i) =>
                    {
                        var n = _sentencesParser[i]
                            .ReParseSentenceWithRuleDeletion(currentCFHypothesis, deletedRule, predictionSet);
                        Parses[i].GammaStates = n;
                    });
            }
            catch (OperationCanceledException)
            {
                //parse tree too long to parse
                //the grammar is too recursive,
                //decision - discard it and continue.
                //string s = "parsing took too long, for the grammar:\r\n" + currentHypothesis.ToString();
                //NLog.LogManager.GetCurrentClassLogger().Info(s);
                return false;
            }

            return true;
        }

        public SentenceParsingResults[] ParseAllSentencesWithDebuggingAssertion(ContextFreeGrammar currentHypothesis,
            EarleyParser[] diffparsers = null)
        {
            var sentencesWithCounts = new SentenceParsingResults[Parses.Length];

            for (var i = 0; i < Parses.Length; i++)
            {
                sentencesWithCounts[i] = new SentenceParsingResults();
                sentencesWithCounts[i].Sentence = Parses[i].Sentence;
                sentencesWithCounts[i].Count = Parses[i].Count;
                sentencesWithCounts[i].Length = Parses[i].Length;
            }

            var parsers = new EarleyParser[Parses.Length];
            for (var i = 0; i < parsers.Length; i++)
                parsers[i] =
                    new EarleyParser(currentHypothesis, _voc,
                        false); //parser does not check for cyclic unit production, you have guaranteed it before (see Objective function).

            try
            {
                Parallel.ForEach(sentencesWithCounts,
                    (sentenceItem, loopState, i) =>
                    {
                        var n = parsers[i].ParseSentence(sentenceItem.Sentence);
                        sentencesWithCounts[i].GammaStates = n;
                    });


                if (diffparsers != null)
                    for (var i = 0; i < diffparsers.Length; i++)
                    {
                        var actual = diffparsers[i].ToString();
                        var expected = parsers[i].ToString();
                        if (actual != expected)
                        {
                            var grammar = parsers[i]._grammar.ToString();
                            throw new Exception("actual parse differs from expected parse");
                        }
                    }
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

            return sentencesWithCounts;
        }

        public SentenceParsingResults[] ParseAllSentences(ContextFreeGrammar currentHypothesis)
        {
            try
            {
                Parallel.ForEach(Parses,
                    (sentenceItem, loopState, i) =>
                    {
                        var parser =
                            new EarleyParser(currentHypothesis, _voc,
                                false); //parser does not check for cyclic unit production, you have guaranteed it before (see Objective function).
                        var n = parser.ParseSentence(sentenceItem.Sentence);
                        Parses[i].GammaStates = n;
                    });
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

            return Parses;
        }

        public Dictionary<int, int> GetGrammarTrees(ContextFreeGrammar hypothesis)
        {
            var res = _grammarTreesCalculator.Recalculate(hypothesis);
            var grammarTreesPerLength = new Dictionary<int, int>();
            for (var i = 0; i < res.Length; i++)
                if (i <= _maxWordsInSentence && i >= _minWordsInSentence && res[i] > 0)
                    grammarTreesPerLength[i] = res[i];

            return grammarTreesPerLength;
        }


        public Dictionary<int, int> CollectUsages()
        {
            var usagesDic = new Dictionary<int, int>();

            if (Parses != null)
            {
                foreach (var sentenceParsingResult in Parses)
                foreach (var gammaState in sentenceParsingResult.GammaStates)
                    CollectRuleUsages(gammaState, usagesDic, sentenceParsingResult.Count);

                return usagesDic;
            }

            Console.WriteLine("returning usages dic null. meaning that all parses are zero.");
            return null;
        }

        private static void CollectRuleUsages(EarleyState state, Dictionary<int, int> ruleCounts, int sentenceCount)
        {
            if (state.Predecessor != null)
                CollectRuleUsages(state.Predecessor, ruleCounts, sentenceCount);

            if (state.Reductor != null)
                CollectRuleUsages(state.Reductor, ruleCounts, sentenceCount);

            var ruleNumber = state.Rule.NumberOfGeneratingRule;
            if (ruleNumber != 0) //SCAN_RULE_NUMBER = 0.
            {
                if (!ruleCounts.ContainsKey(ruleNumber)) ruleCounts[ruleNumber] = 0;
                ruleCounts[ruleNumber] += sentenceCount;
                //add +1 to the count of the rule, multiplied by the number of times the sentence appears in the text (sentenceCount).
            }
        }

        internal (ContextSensitiveGrammar mutatedGrammar, bool reparsed) GetNeighborAndReparse(
            ContextSensitiveGrammar currentHypothesis)
        {
            //choose mutation function in random (weighted according to weights file)
            var m = GrammarPermutations.GetWeightedRandomMutation();
            var newGrammar = new ContextSensitiveGrammar(currentHypothesis);

            //mutate the grammar.
            SetOriginalGrammarBeforePermutation();
            var (g, reparsed) = m(newGrammar, this);
            return (g, reparsed);
        }


        public void AcceptChanges()
        {
            for (var i = 0; i < _sentencesParser.Length; i++)
                _sentencesParser[i].AcceptChanges();
        }

        public void RejectChanges()
        {
            //consider switching to parallel later!
            for (var i = 0; i < _sentencesParser.Length; i++)
                _sentencesParser[i].RejectChanges();
        }
    }
}