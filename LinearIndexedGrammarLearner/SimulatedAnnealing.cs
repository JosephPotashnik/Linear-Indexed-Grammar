﻿using System;
using System.Threading;
using System.Threading.Tasks;
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
                var newValue =  _objectiveFunction.Compute(mutatedGrammar, false);

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

        public (ContextSensitiveGrammar bestGrammar, T bestValue) Run(ContextSensitiveGrammar initiaGrammar = null)
        {
            var currentIteration = 0;
            var currentGrammar = initiaGrammar ?? _learner.CreateInitialGrammars();
            var currentValue = _objectiveFunction.Compute(currentGrammar, false);

            //if current grammar is already optimal on data, no need to learn anything,
            //return immediately.
            if (_objectiveFunction.IsMaximalValue(currentValue))
                return (currentGrammar, currentValue);

            while (currentIteration++ < _numberOfIterations)
            {
                //if (currentIteration % 100 == 0)
                    LogManager.GetCurrentClassLogger().Info($"iteration {currentIteration}, probability {currentValue}");


                (currentGrammar, currentValue) = RunSingleIteration(currentGrammar, currentValue);
                if (_objectiveFunction.IsMaximalValue(currentValue)) break;
            }

            return (currentGrammar, currentValue);
        }
    }
}