using System;
using LinearIndexedGrammarParser;
using NLog;

namespace LinearIndexedGrammarLearner
{
    public class SimulatedAnnealing<T> where T : IComparable
    {
        private readonly double _coolingFactor;
        private readonly double _initialTemp;
        private readonly Learner _learner;
        private readonly int _numberOfIterations;
        private readonly IObjectiveFunction<T> _objectiveFunction;

        public SimulatedAnnealing(Learner l, int numberOfIterations, double coolingFactor, double initialTemp,
            IObjectiveFunction<T> objectiveFunction)
        {
            _learner = l;
            _numberOfIterations = numberOfIterations;
            _initialTemp = initialTemp;
            _coolingFactor = coolingFactor;
            _objectiveFunction = objectiveFunction;
        }

        private (ContextSensitiveGrammar bestGrammar, T bestValue) RunSingleIteration(
            ContextSensitiveGrammar initialGrammar, T initialValue)
        {
            var currentTemp = _initialTemp;
            var currentValue = initialValue;
            var currentGrammar = initialGrammar;

            while (currentTemp > 2.0)
                try
                {
                    var mutatedGrammar = _learner.GetNeighbor(currentGrammar);
                    currentTemp *= _coolingFactor;
                    if (mutatedGrammar == null) continue;

                    //var newValue = _objectiveFunction.Compute(mutatedGrammar, (currentTemp < 100));
                    var newValue = _objectiveFunction.Compute(mutatedGrammar, false);

                    var accept = _objectiveFunction.AcceptNewValue(newValue, currentValue, currentTemp);
                    if (accept)
                    {
                        currentValue = newValue;
                        currentGrammar = mutatedGrammar;
                    }

                    if (_objectiveFunction.IsMaximalValue(currentValue)) break;
                }

                catch (Exception e)
                {
                    LogManager.GetCurrentClassLogger().Warn(e.ToString());
                }

            var ruleDistribution = _learner.CollectUsages(currentGrammar);
            currentGrammar.PruneUnusedRules(ruleDistribution);
            return (currentGrammar, currentValue);
        }

        public (ContextSensitiveGrammar bestGrammar, T bestValue) Run()
        {
            var currentIteration = 0;
            var currentGrammar = _learner.CreateInitialGrammars();
            var currentValue = _objectiveFunction.Compute(currentGrammar, false);

            while (currentIteration++ < _numberOfIterations)
            {
                if (currentIteration % 10 == 0)
                    LogManager.GetCurrentClassLogger().Info($"generation {currentIteration}");

                (currentGrammar, currentValue) = RunSingleIteration(currentGrammar, currentValue);
                if (_objectiveFunction.IsMaximalValue(currentValue)) break;
            }

            return (currentGrammar, currentValue);
        }
    }
}