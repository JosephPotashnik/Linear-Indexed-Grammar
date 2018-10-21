using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
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

    public static class ThreadSafeRandom
    {
        [ThreadStatic] private static Random _local;

        public static Random ThisThreadsRandom => _local ?? (_local =
                                                      new Random(unchecked(Environment.TickCount * 31 +
                                                                           Thread.CurrentThread.ManagedThreadId)));
    }

    public class GeneticAlgorithm
    {
        public const double Tolerance = 0.0001;

        private const int NumberOfSufficientSolutions = 10;
        private readonly Learner _learner;
        private readonly int _numberOfGenerations;

        private readonly PriorityQueue<double, GrammarWithProbability> _population =
            new PriorityQueue<double, GrammarWithProbability>();

        public GeneticAlgorithm(Learner l, int populationSize, int numberOfGenerations)
        {
            _numberOfGenerations = numberOfGenerations;
            _learner = l;

            var initialGrammar = _learner.CreateInitialGrammars();
            var prob = _learner.Probability(initialGrammar);

            for (var i = 0; i < populationSize; i++)
                _population.Enqueue(prob,
                    new GrammarWithProbability(new ContextSensitiveGrammar(initialGrammar), prob));
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

        public GrammarWithProbability[] Run()
        {
            var currentGeneration = 0;
            var descendants = new Queue<KeyValuePair<double, ContextSensitiveGrammar>>();
            while (currentGeneration++ < _numberOfGenerations)
            {
                if (currentGeneration % 200 == 0)
                    LogManager.GetCurrentClassLogger().Info($"generation {currentGeneration}");
                try
                {
                    foreach (var individual in _population.Values)
                    {
                        var mutatedIndividual = Mutate(individual);
                        if (mutatedIndividual.Grammar != null && mutatedIndividual.Probability > 0)
                            descendants.Enqueue(
                                new KeyValuePair<double, ContextSensitiveGrammar>(mutatedIndividual.Probability,
                                    mutatedIndividual.Grammar));
                    }

                    InsertDescendantsIntoPopulation(descendants);
                    var enoughSolutions = CheckForSufficientSolutions();
                    if (enoughSolutions) break;
                }

                catch (Exception e)
                {
                    LogManager.GetCurrentClassLogger().Warn(e.ToString());
                }
            }

            //choosing shortest grammar among all those with the best probability.
            var bestHypotheses = ChooseBestHypotheses().ToArray();

            var s =
                $"Best Hypothesis:\r\n{bestHypotheses[0].Grammar} \r\n with probability {bestHypotheses[0].Probability}";
            LogManager.GetCurrentClassLogger().Info(s);
            return bestHypotheses;
        }

        private bool CheckForSufficientSolutions()
        {
            var enoughSolutions = false;
            var bestProbability = _population.Last().Key;
            if (Math.Abs(bestProbability - 1) < Tolerance)
            {
                var numberOfSolutions = _population.Last().Value.Count();
                enoughSolutions = numberOfSolutions >= NumberOfSufficientSolutions;
            }

            return enoughSolutions;
        }

        private IEnumerable<GrammarWithProbability> ChooseBestHypotheses()
        {
            var bestGrammars = _population.Last().Value.Select(x =>
                {
                    var g = x.Grammar;
                    var ruleDistribution = _learner.CollectUsages(g);
                    g.PruneUnusedRules(ruleDistribution);
                    //rename variables names from serial generated names such as X271618 to X1, X2 etc.
                    g.RenameVariables();

                    return x;
                }
            );

            var y = bestGrammars.OrderBy(x => x.Grammar.StackConstantRulesArray.Count());
            return y;
        }


        private void InsertDescendantsIntoPopulation(Queue<KeyValuePair<double, ContextSensitiveGrammar>> descendants)
        {
            while (descendants.Any())
            {
                var success = descendants.TryDequeue(out var descendant);
                if (success)
                    if (descendant.Key >= _population.PeekFirstKey())
                    {
                        var old = _population.Dequeue();
                        old.Dispose();
                        _population.Enqueue(descendant.Key,
                            new GrammarWithProbability(descendant.Value, descendant.Key));
                    }
            }
        }

        private GrammarWithProbability Mutate(GrammarWithProbability individual)
        {
            var mutatedIndividual = _learner.GetNeighbor(individual.Grammar);
            return _learner.ComputeProbabilityForGrammar(individual, mutatedIndividual);
        }
    }
}