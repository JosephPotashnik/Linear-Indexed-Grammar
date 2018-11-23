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
        bool ConsiderValue(T val);
        bool IsMaximalValue(T val);

    }

    public class GrammarFitnessObjectiveFunction : IObjectiveFunction<double>
    {
        public const double Tolerance = 0.0001;

        private readonly Learner _learner;

        public GrammarFitnessObjectiveFunction(Learner l) => _learner = l;

        public bool ConsiderValue(double val) => (val > 0);
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

                    //int numberOfSentenceParsed = allParses.Count(x => x.Trees.Count > 0);


                    //double unexplainedSentencePercentage = (1.0 - (numberOfSentenceParsed / (double)allParses.Length));
                    //prob -= unexplainedSentencePercentage;
                    //if (prob < 0) prob = 0;

                }
            }
            return prob;
        }
    }
    
}
