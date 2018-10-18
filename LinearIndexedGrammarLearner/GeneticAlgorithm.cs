using LinearIndexedGrammarParser;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace LinearIndexedGrammarLearner
{
    [JsonObject(MemberSerialization.OptIn)]
    public class GeneticAlgorithmParameters
    {
        [JsonProperty]
        public float NumberOfGenerations { get; set; }

        [JsonProperty]
        public float PopulationSize { get; set; }
    }
    public static class ThreadSafeRandom
    {
        [ThreadStatic] private static Random Local;

        public static Random ThisThreadsRandom
        {
            get { return Local ?? (Local = new Random(unchecked(Environment.TickCount * 31 + Thread.CurrentThread.ManagedThreadId))); }
        }
    }
    public class GeneticAlgorithm
    {
        public static void StopWatch(Stopwatch stopWatch)
        {
            stopWatch.Stop();
            var ts = stopWatch.Elapsed;
            var elapsedTime = $"{ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}.{ts.Milliseconds / 10:00}";
            var s = "Overall session RunTime " + elapsedTime;
            NLog.LogManager.GetCurrentClassLogger().Info(s);
        }

        public static Stopwatch StartWatch()
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            return stopWatch;
        }

        private const int numberOfSufficientSolutions = 10;
        private readonly int populationSize;
        private readonly int numberOfGenerations;
        private readonly Learner learner;
        private PriorityQueue<double, GrammarWithProbability> population = new PriorityQueue<double, GrammarWithProbability>();

        public GeneticAlgorithm(Learner l, int populationSize, int numberOfGenerations)
        {
            this.numberOfGenerations = numberOfGenerations;
            this.populationSize = populationSize;
            learner = l;

            ContextSensitiveGrammar initialGrammar = learner.CreateInitialGrammars();
            var prob = learner.Probability(initialGrammar);

            for (int i = 0; i < populationSize; i++)
                population.Enqueue(prob, new GrammarWithProbability(new ContextSensitiveGrammar(initialGrammar), prob));
        }

        public GrammarWithProbability[] Run()
        {
            int currentGeneration = 0;
            var descendants = new Queue<KeyValuePair<double, ContextSensitiveGrammar>>();
            while (currentGeneration++ < numberOfGenerations)
            {
                if (currentGeneration % 200 == 0)
                    NLog.LogManager.GetCurrentClassLogger().Info($"generation {currentGeneration}");
                try
                {
                    foreach (var individual in population.Values)
                    {
                        var mutatedIndividual = Mutate(individual);
                        if (mutatedIndividual.Grammar != null && mutatedIndividual.Probability > 0)
                            descendants.Enqueue(new KeyValuePair<double, ContextSensitiveGrammar>(mutatedIndividual.Probability, mutatedIndividual.Grammar));
                    }

                    InsertDescendantsIntoPopulation(descendants);
                    bool enoughSolutions = CheckForSufficientSolutions();
                    if (enoughSolutions) break;
                }

                catch (Exception e)
                {
                    NLog.LogManager.GetCurrentClassLogger().Warn(e.ToString());
                }
            }

            //choosing shortest grammar among all those with the best probability.
            var bestHypotheses = ChooseBestHypotheses().ToArray();

            var s = $"Best Hypothesis:\r\n{bestHypotheses[0].Grammar} \r\n with probability {bestHypotheses[0].Probability}";
            NLog.LogManager.GetCurrentClassLogger().Info(s);
            return bestHypotheses;
        }

        private bool CheckForSufficientSolutions()
        {
            bool enoughSolutions = false;
            double bestProbability = population.Last().Key;
            if (bestProbability == 1)
            {
                var numberOfSolutions = population.Last().Value.Count();
                enoughSolutions = (numberOfSolutions >= GeneticAlgorithm.numberOfSufficientSolutions);
            }

            return enoughSolutions;
        }
        private IEnumerable<GrammarWithProbability> ChooseBestHypotheses()
        {
            var bestGrammars = population.Last().Value.Select(x =>
            {
                var g = x.Grammar;
                var ruleDistribution = learner.CollectUsages(g);
                g.PruneUnusedRules(ruleDistribution);
                //rename variables names from serial generated names such as X271618 to X1, X2 etc.
                g.RenameVariables();

                return x;
            }
            );

            var y = bestGrammars.OrderBy(x => x.Grammar.StackConstantRules.Count());
            return y;
        }

        

        private void InsertDescendantsIntoPopulation(Queue<KeyValuePair<double, ContextSensitiveGrammar>> descendants)
        {
            while (descendants.Any())
            {
                KeyValuePair<double, ContextSensitiveGrammar> descendant;
                bool success = descendants.TryDequeue(out descendant);
                if (success)
                {
                    //if the probability of the descendant (its key) is higher than the lowest probability
                    //in the population (population is sorted by increasing probability), remove the old
                    //item and enqueue the descendant.
                    if (descendant.Key >= population.PeekFirstKey())
                    {
                        var old = population.Dequeue();
                        old.Dispose();
                        population.Enqueue(descendant.Key, new GrammarWithProbability(descendant.Value, descendant.Key));
                    }

                }
            }
        }

        private GrammarWithProbability Mutate(GrammarWithProbability individual)
        {
            var mutatedIndividual = learner.GetNeighbor(individual.Grammar);
            return learner.ComputeProbabilityForGrammar(individual, mutatedIndividual);
            
        }

    }
}
