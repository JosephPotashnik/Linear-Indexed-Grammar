using System;
using System.Collections.Generic;
using System.Linq;
using LinearIndexedGrammarParser;
using NLog;

namespace LinearIndexedGrammarLearner
{
    class FullTreeComparer : IEqualityComparer<EarleyNode>
    {
        public bool Equals(EarleyNode  x, EarleyNode  y)
        {
            
            if (x.Name != y.Name) return false;
            if (x.Children?.Count != y.Children?.Count) return false;
            if (x.Children != null)
            {
                for (int i = 0; i < x.Children.Count; i++)
                {
                    var isChildEqual = this.Equals(x.Children[i], y.Children[i]);
                    if (!isChildEqual) return false;
                }
            }

            return true;
        }

        public int GetHashCode(EarleyNode  obj)
        {
            return obj.GetHashCode();
        }
    }

    class LengthAndEarleyNodeComparer : IEqualityComparer<(int length, EarleyNode node)>
    {
        static readonly FullTreeComparer comp = new FullTreeComparer();

        public bool Equals((int length, EarleyNode node) x, (int length, EarleyNode node) y)
        {
            return comp.Equals(x.node, y.node);
        }

        public int GetHashCode((int length, EarleyNode node) obj)
        {
            return obj.length;
        }
    }

    public interface IObjectiveFunction
    {
        double Compute(ContextSensitiveGrammar currentHypothesis);
        bool AcceptNewValue(double newValue, double oldValue, double temperature);
        bool IsMaximalValue(double val);
        void SetMaximalValue(double val);

    }

    public class GrammarFitnessObjectiveFunction : IObjectiveFunction
    {
        private static double[] powersOfMinus2 =
        {
            Math.Pow(2, 0),
            Math.Pow(2, -1),
            Math.Pow(2, -2),
            Math.Pow(2, -3),
            Math.Pow(2, -4),
            Math.Pow(2, -5),
            Math.Pow(2, -6),
            Math.Pow(2, -7),
            Math.Pow(2, -8),
            Math.Pow(2, -9),
            Math.Pow(2, -10),
            Math.Pow(2, -11),
            Math.Pow(2, -12),
            Math.Pow(2, -13),
            Math.Pow(2, -14),
            Math.Pow(2, -15),
        };

        public const double Tolerance = 0.000001;
        private readonly Learner _learner;
        static double maxVal;

        public GrammarFitnessObjectiveFunction(Learner l)
        {
            _learner = l;
        }

        public void SetMaximalValue(double val)
        {
            maxVal = val;
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
            if (val - maxVal > 0.01) return true;
            return Math.Abs(val - maxVal) < Tolerance;
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
                //var pairs = allParses.SelectMany(x => x.Trees.Select(y => (x.Sentence, y.GetNonTerminalStringUnderNode()))).ToArray();
                //using (System.IO.StreamWriter file =
                //    new System.IO.StreamWriter(@"PossibleTrees.txt"))
                //{
                //    for (int i = 0; i < pairs.Length; i++)
                //    {
                //        file.WriteLine($"{pairs[i].Sentence} , {pairs[i].Item2}");
                //    }
                //}

                var trees = new HashSet<(int, EarleyNode)>(new LengthAndEarleyNodeComparer());
                for (int i = 0; i < allParses.Length; i++)
                {
                    for (int j = 0; j < allParses[i].Trees.Count; j++)
                        trees.Add((allParses[i].Length, allParses[i].Trees[j]));
                }

                var dataTreesPerLength = trees.GroupBy(x => x.Item1).ToDictionary(g => g.Key, g => g.Count());

                if (trees.Any())
                {
                    prob = 1;
                    var grammarTreesPerLength = _learner.GetGrammarTrees(currentCFHypothesis);
                    double totalProbabilityOfGrammarTrees = 0;
                    foreach (var length in grammarTreesPerLength.Keys)
                        totalProbabilityOfGrammarTrees += powersOfMinus2[length];

                    foreach (var length in grammarTreesPerLength.Keys)
                    {
                        dataTreesPerLength.TryGetValue(length, out int dataTreesInLength);
                        var grammarTreesInLength = grammarTreesPerLength[length];
                        int diff = grammarTreesInLength - dataTreesInLength;
                        if (diff > 0)
                        {
                            prob -= diff / (double)grammarTreesInLength * powersOfMinus2[length] /
                                totalProbabilityOfGrammarTrees;
                        }
                    }

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


                    var numberOfSentenceUnParsed = allParses.Count(x => x.Trees.Count == 0);
                    var unexplainedSentences = (numberOfSentenceUnParsed / (double)allParses.Length);

                    prob *= ( 1 - unexplainedSentences);
                    if (prob < 0) prob = 0;
                }
            }
            return prob;
        }
    }
}