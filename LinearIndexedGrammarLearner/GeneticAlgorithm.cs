﻿using LinearIndexedGrammarParser;
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
            Console.WriteLine(s);
        }

        public static Stopwatch StartWatch()
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            return stopWatch;
        }


        private readonly int populationSize;
        private readonly int numberOfGenerations;
        private readonly Learner learner;
        private PriorityQueue<double, GrammarWithProbability> population = new PriorityQueue<double, GrammarWithProbability>();

        public GeneticAlgorithm(Learner l, int populationSize, int numberOfGenerations)
        {
            this.numberOfGenerations = numberOfGenerations;
            this.populationSize = populationSize;
            learner = l;

            Grammar initialGrammar = learner.CreateInitialGrammar();
            var prob = learner.Probability(initialGrammar);

            for (int i = 0; i < populationSize; i++)
                population.Enqueue(prob, new GrammarWithProbability(initialGrammar, prob));
        }

        public (double prob, Grammar g) Run()
        {
            int currentGeneration = 0;
            Queue<KeyValuePair<double, Grammar>> descendants = new Queue<KeyValuePair<double, Grammar>>();
            while (currentGeneration++ < numberOfGenerations)
            {
                if (currentGeneration % 200 == 0)
                    Console.WriteLine($"generation {currentGeneration}");
                try
                {
                    foreach (var individual in population.Values)
                    {
                        var mutatedIndividual = Mutate(individual);
                        if (mutatedIndividual.Grammar != null)
                            descendants.Enqueue(new KeyValuePair<double, Grammar>(mutatedIndividual.Probability, mutatedIndividual.Grammar));
                    }

                    InsertDescendantsIntoPopulation(descendants);
                }

                catch (Exception e)
                {
                    //Console.WriteLine(e.ToString());
                }
            }

            //choosing shortest grammar among all those with the best probability.
            (var bestProbability, var bestHypothesis) = ChooseBestHypothesis();

            var s = string.Format($"Best Hypothesis:\r\n{bestHypothesis} \r\n with probability {bestProbability}");

            Console.WriteLine(s);
            using (var sw = File.AppendText("SessionReport.txt"))
            {
                sw.WriteLine(s);
            }
            return (bestProbability, bestHypothesis);
        }

        private (double bestProbability, Grammar bestHypothesis) ChooseBestHypothesis()
        {
            double bestProbability = population.Last().Key;
            int minimalNumberOfRules = int.MaxValue;
            Grammar bestHypothesis = null;
            foreach (var bestHypothesisCandidates in population.Last().Value)
            {
                var g = bestHypothesisCandidates.Grammar;
                var ruleDistribution = learner.CollectUsages(g);
                g.PruneUnusedRules(ruleDistribution);
                int numberOfRules = g.RuleCount;
                if (numberOfRules < minimalNumberOfRules)
                {
                    bestHypothesis = g;
                    minimalNumberOfRules = numberOfRules;
                }
            }

            //rename variables names from serial generated names such as X271618 to X1, X2 etc.
            bestHypothesis.RenameVariables();

            return (bestProbability, bestHypothesis);
        }

        

        private void InsertDescendantsIntoPopulation(Queue<KeyValuePair<double, Grammar>> descendants)
        {
            while (descendants.Any())
            {
                KeyValuePair<double, Grammar> descendant;
                bool success = descendants.TryDequeue(out descendant);
                if (success)
                {

                    if (descendant.Key >= population.PeekFirstKey())
                    {
                        population.Dequeue();
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
