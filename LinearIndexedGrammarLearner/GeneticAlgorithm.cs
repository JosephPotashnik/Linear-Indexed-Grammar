using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using LinearIndexedGrammarParser;
using Newtonsoft.Json;
using NLog;

namespace LinearIndexedGrammarLearner
{
    [JsonObject(MemberSerialization.OptIn)]
    public class GeneticAlgorithmParameters
    {
        [JsonProperty] public float NumberOfGenerations { get; set; }

        [JsonProperty] public float PopulationSize { get; set; }
    }


    public class GeneticAlgorithm<T> where T : IComparable
    {
        public const double Tolerance = 0.0001;

        private const int NumberOfSufficientSolutions = 10;
        private readonly Learner _learner;
        private readonly int _numberOfGenerations;
        private readonly IObjectiveFunction<T> _objectiveFunction;

        private readonly PriorityQueue<T, ContextSensitiveGrammar> _population =
            new PriorityQueue<T, ContextSensitiveGrammar>();

        public GeneticAlgorithm(Learner l, int populationSize, int numberOfGenerations,
            IObjectiveFunction<T> objectiveFunction)
        {
            _numberOfGenerations = numberOfGenerations;
            _learner = l;
            _objectiveFunction = objectiveFunction;
            var initialGrammar = _learner.CreateInitialGrammars();
            var objectiveFunctionValue = _objectiveFunction.Compute(initialGrammar, false);

            for (var i = 0; i < populationSize; i++)
                _population.Enqueue(objectiveFunctionValue,
                    new ContextSensitiveGrammar(initialGrammar));
        }

        public static void StopWatch(Stopwatch stopWatch)
        {
            stopWatch.Stop();
            var ts = stopWatch.Elapsed;
            var elapsedTime = $"{ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}.{ts.Milliseconds / 10:00}";
            var s = "Overall session RunTime " + elapsedTime;
            LogManager.GetCurrentClassLogger().Info(s);
        }

        public static Stopwatch StartWatch()
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            return stopWatch;
        }

        public (ContextSensitiveGrammar bestGrammar, T bestValue) Run()
        {
            var currentGeneration = 0;
            T objectiveFunctionValue;
            var descendants = new Queue<KeyValuePair<T, ContextSensitiveGrammar>>();
            while (currentGeneration++ < _numberOfGenerations)
            {
                if (currentGeneration % 200 == 0)
                    LogManager.GetCurrentClassLogger().Info($"generation {currentGeneration}");
                try
                {
                    foreach (var individualkeyValuePair in _population.KeyValuePairs)
                    {
                        var originalGrammar = individualkeyValuePair.Item2;
                        var originalGrammarValue = individualkeyValuePair.Item1;

                        var mutatedGrammar = _learner.GetNeighbor(originalGrammar);

                        if (mutatedGrammar == null) continue;
                        objectiveFunctionValue =
                            EvaluateObjectiveFunction(mutatedGrammar, originalGrammar, originalGrammarValue);

                        if (_objectiveFunction.ConsiderValue(objectiveFunctionValue))
                            descendants.Enqueue(
                                new KeyValuePair<T, ContextSensitiveGrammar>(objectiveFunctionValue,
                                    mutatedGrammar));
                    }

                    InsertDescendantsIntoPopulation(descendants, currentGeneration);
                    var enoughSolutions = CheckForSufficientSolutions();
                    if (enoughSolutions) break;
                }

                catch (Exception e)
                {
                    LogManager.GetCurrentClassLogger().Warn(e.ToString());
                }
            }

            //choosing shortest grammar among all those with the best probability.
            var (value, bestGrammars) = ChooseBestHypotheses();
            return (bestGrammars.First(), value);
        }

        private T EvaluateObjectiveFunction(ContextSensitiveGrammar mutatedGrammar,
            ContextSensitiveGrammar originalGrammar,
            T originalGrammarValue)
        {
            var objectiveFunctionValue = _objectiveFunction.Compute(mutatedGrammar, false);
            return objectiveFunctionValue;
        }

        private bool CheckForSufficientSolutions()
        {
            var enoughSolutions = false;
            var bestProbability = _population.Last().Key;
            if (_objectiveFunction.IsMaximalValue(bestProbability))
            {
                var numberOfSolutions = _population.Last().Value.Count();
                enoughSolutions = numberOfSolutions >= NumberOfSufficientSolutions;
            }

            return enoughSolutions;
        }

        private (T value, IEnumerable<ContextSensitiveGrammar> bestGrammars) ChooseBestHypotheses()
        {
            var bestGrammars = _population.Last().Value.Select(x =>
                {
                    var ruleDistribution = _learner.CollectUsages(x);
                    x.PruneUnusedRules(ruleDistribution);
                    return x;
                }
            );

            var y = bestGrammars.OrderBy(x => x.StackConstantRules.Count());
            return (_population.Last().Key, y);
        }


        private void InsertDescendantsIntoPopulation(Queue<KeyValuePair<T, ContextSensitiveGrammar>> descendants,
            int iteration)
        {
            while (descendants.Any())
            {
                var success = descendants.TryDequeue(out var descendant);
                if (success)
                    if (descendant.Key.CompareTo(_population.PeekFirstKey()) > 0)
                    {
                        var old = _population.Dequeue();
                        _population.Enqueue(descendant.Key, descendant.Value);
                    }
            }
        }
    }
}