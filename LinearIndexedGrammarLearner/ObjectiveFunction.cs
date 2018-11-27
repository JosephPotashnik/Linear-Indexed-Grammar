using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LinearIndexedGrammarParser;

namespace LinearIndexedGrammarLearner
{
    public interface IObjectiveFunction<T> where T : IComparable
    {
        T Compute(ContextSensitiveGrammar currentHypothesis);
        bool ConsiderValue(T newVal);
        bool AcceptNewValue(T newVal, T oldVal, int iteration);
        bool IsMaximalValue(T val);

    }

    public class GrammarFitnessObjectiveFunction : IObjectiveFunction<double>
    {
        public const double Tolerance = 0.000001;
        public const double CoolingFactor = 0.99;

        private readonly Learner _learner;
        private double _initialTemperature = 1000;

        public GrammarFitnessObjectiveFunction(Learner l) => _learner = l;

        public bool ConsiderValue(double newval)
        {
            return (newval > 0);
        }

        public bool AcceptNewValue(double newval, double oldval, int iteration)
        {

            //improvement - accept.
            if (newval > oldval) return true;
            if (Math.Abs(newval) < Tolerance) return false;

            //neval =< oldval (our objective function is to maximize value)
            //degration - accept with a probability proportional to the delta and the iteration
            //bigger delta (bigger degradation) => lower probability.
            //bigger temperature => higher probability
            double delta = (newval - oldval) * 1000 * 10;
            double temperature = _initialTemperature * Math.Pow(CoolingFactor, iteration);

            double exponent = delta / temperature;
            double prob = Math.Exp(exponent);
            var rand = ThreadSafeRandom.ThisThreadsRandom;
            var randomThrow = rand.NextDouble();
            return (randomThrow < prob);
        }

        public bool IsMaximalValue(double val) => (Math.Abs(val - 1) < Tolerance);

        public double Compute(ContextSensitiveGrammar currentHypothesis)
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
                NLog.LogManager.GetCurrentClassLogger().Warn(s);
                return 0;
            }
            if (allParses != null)
            {
                var totalTreesCountofData = allParses.Select(x => x.Trees.Count).Sum();

                if (totalTreesCountofData != 0)
                {
                    var totalTreesCountofGrammar = _learner.GetNumberOfParseTrees(currentCFHypothesis);
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

                    int numberOfSentenceParsed = allParses.Count(x => x.Trees.Count > 0);

                    double unexplainedSentencePercentage = (1.0 - (numberOfSentenceParsed / (double)allParses.Length));

                    //pass only through fully parsable inputs.
                    if (unexplainedSentencePercentage > 0) return 0;

                    prob -= unexplainedSentencePercentage;
                    if (prob < 0) prob = 0;

                }
            }
            return prob;
        }
    }
    
}
