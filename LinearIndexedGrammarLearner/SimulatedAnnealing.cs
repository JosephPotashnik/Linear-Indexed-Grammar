using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LinearIndexedGrammarParser;
using NLog;

namespace LinearIndexedGrammarLearner
{
    public class SimulatedAnnealing
    {
        private readonly double _coolingFactor;
        private readonly double _initialTemp;
        private readonly Learner _learner;
        private readonly int _numberOfIterations;
        private readonly IObjectiveFunction _objectiveFunction;

        public SimulatedAnnealing(Learner l, int numberOfIterations, double coolingFactor, double initialTemp,
            IObjectiveFunction objectiveFunction)
        {
            _learner = l;
            _numberOfIterations = numberOfIterations;
            _initialTemp = initialTemp;
            _coolingFactor = coolingFactor;
            _objectiveFunction = objectiveFunction;
        }

        private (ContextSensitiveGrammar bestGrammar, double bestValue) RunSingleIteration(
            ContextSensitiveGrammar initialGrammar, double initialValue)
        {
            var currentTemp = _initialTemp;
            var currentValue = initialValue;
            var currentGrammar = initialGrammar;

            while (currentTemp > 0.3)
            { 
                var mutatedGrammar = _learner.GetNeighbor(currentGrammar);
                currentTemp *= _coolingFactor;
                if (mutatedGrammar == null) continue;

                //var t = Task.Run(() => _objectiveFunction.Compute(mutatedGrammar, false));
                //if (!t.Wait(1500))
                //{
                //    string s = "computing all parse trees took too long (1.5 seconds), for the grammar:\r\n" + mutatedGrammar.ToString();
                //    NLog.LogManager.GetCurrentClassLogger().Info(s);
                //    //throw new Exception();
                //}

                //var newValue = t.Result;
                var newValue =  _objectiveFunction.Compute(mutatedGrammar, true);

                var accept = _objectiveFunction.AcceptNewValue(newValue, currentValue, currentTemp);
                if (accept)
                {
                    currentValue = newValue;
                    currentGrammar = mutatedGrammar;
                }

                if (_objectiveFunction.IsMaximalValue(currentValue)) break;
            }



            var ruleDistribution = _learner.CollectUsages(currentGrammar);
            currentGrammar.PruneUnusedRules(ruleDistribution);
            return (currentGrammar, currentValue);
        }

        public (ContextSensitiveGrammar bestGrammar, double bestValue) Run(ContextSensitiveGrammar initiaGrammar = null)
        {
            var currentIteration = 0;
            var currentGrammar = initiaGrammar ?? _learner.CreateInitialGrammars();
            var currentValue = _objectiveFunction.Compute(currentGrammar, false);

            if (_objectiveFunction.IsMaximalValue(currentValue))
                return (currentGrammar, currentValue);

            PriorityQueue<double, ContextSensitiveGrammar> bestGrammars = new PriorityQueue<double, ContextSensitiveGrammar>();
            int numberOfBestGrammarsToKeep = 10;
            for (int i = 0; i < numberOfBestGrammarsToKeep; i++)
                bestGrammars.Enqueue(currentValue, new ContextSensitiveGrammar(currentGrammar));

            double smallestBestValue = bestGrammars.PeekFirstKey();
            int noImprovemetCounter = 0;
            //if current grammar is already optimal on data, no need to learn anything,
            //return immediately.
            while (currentIteration++ < _numberOfIterations)
            {
                //if (currentIteration % 100 == 0)
                    LogManager.GetCurrentClassLogger().Info($"iteration {currentIteration}, probability {currentValue}");


                (currentGrammar, currentValue) = RunSingleIteration(currentGrammar, currentValue);
                if (_objectiveFunction.IsMaximalValue(currentValue)) break;

                if (smallestBestValue < currentValue)
                {
                    noImprovemetCounter = 0;
                    bestGrammars.Dequeue();
                    bestGrammars.Enqueue(currentValue, currentGrammar);
                    smallestBestValue = bestGrammars.PeekFirstKey();

                }
                else
                {
                    noImprovemetCounter++;
                }

                if (noImprovemetCounter == 50)
                {
                    var rand = new Random();
                    int randomPastBestGrammar = rand.Next(numberOfBestGrammarsToKeep);
                    var candidates = bestGrammars.KeyValuePairs.ToArray();
                    noImprovemetCounter = 0;
                    currentValue = candidates[randomPastBestGrammar].Item1;
                    currentGrammar = candidates[randomPastBestGrammar].Item2;
                    LogManager.GetCurrentClassLogger().Info($"reverting to random previous best grammar that has probability {currentValue}");

                }
            }

            return (currentGrammar, currentValue);
        }
    }
}