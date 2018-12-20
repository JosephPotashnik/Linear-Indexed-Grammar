using System;
using System.Linq;
using LinearIndexedGrammarParser;
using NLog;

namespace LinearIndexedGrammarLearner
{
    public interface IObjectiveFunction
    {
        double Compute(ContextSensitiveGrammar currentHypothesis);
        bool AcceptNewValue(double newValue, double oldValue, double temperature);
        bool IsMaximalValue(double val);
    }

    public class GrammarFitnessObjectiveFunction : IObjectiveFunction
    {
        public const double Tolerance = 0.000001;
        private readonly Learner _learner;

        public GrammarFitnessObjectiveFunction(Learner l)
        {
            _learner = l;
        }

        public bool ConsiderValue(double newval)
        {
            return newval > 0;
        }


        public bool AcceptNewValue(double newValue, double oldValue, double temperature)
        {
            if (newValue > oldValue) return true; //any positive improvement - accept.

            //neval =< oldValue (our objective function is to maximize value)
            //degradation - accept with a probability proportional to the delta and the iteration
            //bigger delta (bigger degradation) => lower probability.
            //bigger temperature => higher probability
            var exponent = 100* (Math.Log(newValue) - Math.Log(oldValue)) / temperature;
            var prob = Math.Exp(exponent);
            var rand = ThreadSafeRandom.ThisThreadsRandom;
            var randomThrow = rand.NextDouble();
            return randomThrow < prob;
        }

        public bool IsMaximalValue(double val)
        {
            return Math.Abs(val - 1) < Tolerance;
        }

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
                LogManager.GetCurrentClassLogger().Warn(s);
                return 0;
            }

            if (allParses != null)
            {

                var trees = allParses.SelectMany(x => x.Trees);
                var representations = trees.Select(x => x.GetBracketedRepresentation()).Distinct(StringComparer.Ordinal).ToArray();
                var totalTreesCountofData = representations.Length;
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
                    var explainedSentences = (numberOfSentenceParsed / (double)allParses.Length);
                    prob *= explainedSentences;
                    //prob -= unexplainedSentencePercentage;
                    if (prob < 0) prob = 0;
                }
            }

            return prob;
        }
    }
}