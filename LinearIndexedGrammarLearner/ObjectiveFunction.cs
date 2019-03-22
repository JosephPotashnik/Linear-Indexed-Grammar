using System;
using System.Collections.Generic;
using System.Linq;
using LinearIndexedGrammarParser;

namespace LinearIndexedGrammarLearner
{
    class EarleyStateComparer : IEqualityComparer<EarleyState>
    {
        public bool Equals(EarleyState x, EarleyState y)
        {
            if (x.Rule.Number != y.Rule.Number || x.StartColumn.Index != y.StartColumn.Index || x.EndColumn.Index != y.EndColumn.Index ||
                x.DotIndex != y.DotIndex) return false;

            bool predSide = x.Predecessor == null && y.Predecessor == null;
            if (x.Predecessor != null && y.Predecessor != null)
                predSide = this.Equals(x.Predecessor, y.Predecessor);

            if (!predSide) return false;

            bool reductorSide = x.Reductor == null && y.Reductor == null;
            if (x.Reductor != null && y.Reductor != null)
                reductorSide = this.Equals(x.Reductor, y.Reductor);
            if (!reductorSide) return false;

            return true;
        }

        public int GetHashCode(EarleyState obj)
        {
            return obj.GetHashCode();
        }
    }
    
    class LengthAndEarleyStateComparer : IEqualityComparer<(int length, EarleyState state)>
    {
        static readonly EarleyStateComparer comp = new EarleyStateComparer();

        public bool Equals((int length, EarleyState state) x, (int length, EarleyState state) y)
        {
            return comp.Equals(x.state, y.state);
        }

        public int GetHashCode((int length, EarleyState state) obj)
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
        Learner GetLearner();

    }

    public class GrammarFitnessObjectiveFunction : IObjectiveFunction
    {
        private static readonly double[] powersOfMinus2 =
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

        //private static double[] powersOfMinus2 =
        //{
        //    1,
        //   1,
        //    1,
        //    1,
        //   1,
        //    1,
        //    1,
        //    1,
        //    1,
        //    1,
        //    1,
        //   1,
        //    1,
        //    1,
        //    1,
        //    1,
        //};

        public const double Tolerance = 0.000001;
        private readonly Learner _learner;
        static double maxVal;


        public Learner GetLearner()
        {
            return _learner;

        }
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
            if (val - maxVal > 0.001) return true;
            return Math.Abs(val - maxVal) < Tolerance;
        }

        public double Compute(ContextSensitiveGrammar currentHypothesis)
        {
            if (currentHypothesis == null) return 0;

            var currentCFHypothesis = new ContextFreeGrammar(currentHypothesis);

            if (currentCFHypothesis.ContainsCyclicUnitProduction())
                return 0;

            double prob = 0;

            //if (currentCFHypothesis.ToString() != _learner._sentencesParser[0]._grammar.ToString())
            //{
            //    throw new Exception("hypothesis to compute is different from hypothesis stored at the parser");
            //    int x = 1;
            //}

            //uncomment the following line ONLY to check that the differential parser works identically to the from-scratch parser.
            //var allParses1 = _learner.ParseAllSentences(currentCFHypothesis, _learner._sentencesParser);
            var allParses = _learner.Parses;

            //if (allParses != null)
            {
                var trees = new HashSet<(int, EarleyState)>(new LengthAndEarleyStateComparer());
                for (int i = 0; i < allParses.Length; i++)
                {
                    for (int j = 0; j < allParses[i].GammaStates.Count; j++)
                        trees.Add((allParses[i].Length, allParses[i].GammaStates[j]));
                }

                var dataTreesPerLength = trees.GroupBy(x => x.Item1).ToDictionary(g => g.Key, g => g.Count());

                if (trees.Count > 0)
                {
                    prob = 1;
                    var grammarTreesPerLength = _learner.GetGrammarTrees(currentCFHypothesis);

                    //if (grammarTreesPerLength.Count == 0)
                    //{
                    //    throw new Exception("grammar trees are zero although data is parsed");
                    //}
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
                        //right now: it is depth = maxWords+3. change?
                    }


                    var numberOfSentenceUnParsed = allParses.Count(x => x.GammaStates.Count == 0);
                    var unexplainedSentences = (numberOfSentenceUnParsed / (double)allParses.Length);

                    prob *= ( 1 - unexplainedSentences);
                    if (prob < 0) prob = 0;
                }
            }
            return prob;
        }
    }
}