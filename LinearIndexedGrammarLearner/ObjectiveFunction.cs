using LinearIndexedGrammarParser;
using System;
using System.Collections.Generic;

namespace LinearIndexedGrammarLearner
{

    public class GrammarFitnessObjectiveFunction
    {
        public const double Tolerance = 0.0001;
        public int PenaltyCoefficient { get; set; }
        public double NoiseTolerance { get; set; }

        private static readonly double[] exponential =
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
            Math.Pow(2, -15)
        };

        private static readonly double[] uniform =
        {
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1
        };

        private static readonly double[] harmonic =
        {
            1,
            1 / 2f,
            1 / 3f,
            1 / 4f,
            1 / 5f,
            1 / 6f,
            1 / 7f,
            1 / 8f,
            1 / 9f,
            1 / 10f,
            1 / 11f,
            1 / 12f,
            1 / 13f,
            1 / 14f,
            1 / 15f,
            1 / 16f
        };

        private static double maxVal;
        private readonly Learner _learner;

        public GrammarFitnessObjectiveFunction(Learner l, double noiseTolerance)
        {
            PenaltyCoefficient = 1;
            NoiseTolerance = noiseTolerance;
            _learner = l;
        }


        public Learner GetLearner()
        {
            return _learner;
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
            var exponent = 100 * (Math.Log(newValue) - Math.Log(oldValue)) / temperature;
            var prob = Math.Exp(exponent);
            var randomThrow = Pseudorandom.NextDouble();

            return randomThrow < prob;
        }

        public bool IsMaximalValue(double val)
        {
            if (val >= maxVal) return true;
            return Math.Abs(val - maxVal) < Tolerance;
        }

        public (double val, bool feasible) Compute(ContextSensitiveGrammar currentHypothesis)
        {
            var probabilityMassOfLength = uniform;

            if (currentHypothesis == null) return (0, false);

            var currentCFHypothesis = new ContextFreeGrammar(currentHypothesis);

            //checking in ReparseWith Addition/ ReparseWithDeletion / ParseFromScratch
            //if (currentCFHypothesis.ContainsCyclicUnitProduction())
            //    throw new Exception(
            //        "Cyclic Unit Production Encountered at unexpected place, in preparation to remove the check for cyclic");

            double prob = 0;

            var allParses = _learner.Parses;
            int numberOfSentenceUnParsed = 0;
            //var trees = new HashSet<(int, string)>();
            var treesDic = new Dictionary<int, HashSet<string>>();
            for (var i = 0; i < allParses.Length; i++)
            {
                if (!treesDic.TryGetValue(allParses[i].Length, out var set))
                {
                    set = new HashSet<string>();
                    treesDic.Add(allParses[i].Length, set);
                }

                set.UnionWith(_learner._sentencesParser[i].BracketedRepresentations);
                if (_learner._sentencesParser[i].BracketedRepresentations.Count == 0)
                    numberOfSentenceUnParsed++;
            }

            var minLength = 1;
            var dataTreesPerLength = new Dictionary<int, int>();
            foreach (var length in treesDic.Keys)
            {
                dataTreesPerLength[length] = treesDic[length].Count;
                if (length < minLength)
                    minLength = length;
            }

            if (treesDic.Count > 0)
            {
                prob = 1;
                var grammarTreesPerLength = _learner.GetGrammarTrees(currentCFHypothesis);

                double totalProbabilityOfGrammarTrees = 0;
                foreach (var length in grammarTreesPerLength.Keys)
                    totalProbabilityOfGrammarTrees += probabilityMassOfLength[length];

                foreach (var length in grammarTreesPerLength.Keys)
                {
                    dataTreesPerLength.TryGetValue(length, out var dataTreesInLength);
                    var allGrammarTreesInLength = grammarTreesPerLength[length];

                    var diff = allGrammarTreesInLength - dataTreesInLength;
                    if (diff > 0)
                        prob -= diff / (double)allGrammarTreesInLength * probabilityMassOfLength[length] /
                                totalProbabilityOfGrammarTrees;

                }

                if (prob > 1)
                {
                    return (0, false);
                    //the case where probabilityOfInputGivenGrammar > 1 arises when
                    //totalTreesCountofData > totalTreesCountofGrammar, which can happen because totalTreesCountofGrammar
                    //is computed only up to a certain depth of the tree.
                    //so it's possible that the input data is parsed in a tree whose depth exceeds the depth we have allowed above.

                    //assumption: we will reject grammars with data parsed too deep.
                    //discuss: what is the upper bound of tree depth as a function of the number of words in the sentence?
                    //right now: it is depth = maxWords+3. change?
                }

                var unexplainedSentences = PenaltyCoefficient * numberOfSentenceUnParsed / (double)allParses.Length;

                prob *= 1 - unexplainedSentences;
                if (prob < 0) prob = 0;
            }

            return (prob, numberOfSentenceUnParsed <= allParses.Length * NoiseTolerance);
        }
    }
}