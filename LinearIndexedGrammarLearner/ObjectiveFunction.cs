﻿using System;
using System.Linq;
using LinearIndexedGrammarParser;
using NLog;

namespace LinearIndexedGrammarLearner
{
    public interface IObjectiveFunction<T> where T : IComparable
    {
        T Compute(ContextSensitiveGrammar currentHypothesis, bool considerPartialParsing);
        bool AcceptNewValue(T newValue, T oldValue, double temperature);
        bool IsMaximalValue(T val);
        bool AllowInfeasibleSolutions(T newVal);
        bool ConsiderValue(T newVal);
        T GetMinimalValue();
    }

    public class GrammarFitnessObjectiveFunction : IObjectiveFunction<double>
    {
        public const double Tolerance = 0.000001;
        private readonly Learner _learner;

        public GrammarFitnessObjectiveFunction(Learner l)
        {
            _learner = l;
        }

        public bool AllowInfeasibleSolutions(double value)
        {
            return value < 100;
        }

        public bool ConsiderValue(double newval)
        {
            return newval > 0;
        }

        public double GetMinimalValue()
        {
            return 0;
        }

        public bool AcceptNewValue(double newValue, double oldValue, double temperature)
        {
            if (newValue > oldValue) return true; //any positive improvement - accept.
            if (newValue < Tolerance) return false; //if newValue = 0, reject.

            //if the change is too small or 0, reject.
            //many times the new grammar does not change the value,
            //experimentally, I found out we don't want to accept that move. 
            //or maybe accept the move with some probability? definitely not 100%!
            if (oldValue - newValue < Tolerance) return false;

            //neval =< oldValue (our objective function is to maximize value)
            //degration - accept with a probability proportional to the delta and the iteration
            //bigger delta (bigger degradation) => lower probability.
            //bigger temperature => higher probability
            var delta = (newValue - oldValue) * 1000 * 10;
            var exponent = delta / temperature;
            var prob = Math.Exp(exponent);
            var rand = ThreadSafeRandom.ThisThreadsRandom;
            var randomThrow = rand.NextDouble();
            return randomThrow < prob;
        }

        public bool IsMaximalValue(double val)
        {
            return Math.Abs(val - 1) < Tolerance;
        }

        public double Compute(ContextSensitiveGrammar currentHypothesis, bool considerPartialParsing)
        {
            if (currentHypothesis == null) return 0;

            var currentCFHypothesis = new ContextFreeGrammar(currentHypothesis);
            if (currentCFHypothesis.ContainsCyclicUnitProduction())
                return 0;

            SentenceParsingResults[] allParses;
            double prob = 0;
            try
            {
                allParses = _learner.ParseAllSentences(currentCFHypothesis);
            }
            catch (AggregateException e) when (e.InnerExceptions.OfType<InfiniteParseException>().Any())
            {
                var s = e.ToString();
                LogManager.GetCurrentClassLogger().Warn(s);
                return 0;
            }

            if (allParses != null)
            {
                var totalTreesCountofData = allParses.Select(x => x.Trees.Count).Sum();

                if (totalTreesCountofData != 0)
                {
                    var totalTreesCountofGrammar = _learner.GetNumberOfParseTrees(currentCFHypothesis);
                    prob = totalTreesCountofData / (double) totalTreesCountofGrammar;
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

                    var numberOfSentenceParsed = allParses.Count(x => x.Trees.Count > 0);
                    var unexplainedSentencePercentage = 1.0 - numberOfSentenceParsed / (double) allParses.Length;

                    if (!considerPartialParsing && unexplainedSentencePercentage > 0) return 0;

                    prob -= unexplainedSentencePercentage;
                    if (prob < 0) prob = 0;
                }
            }

            return prob;
        }
    }
}