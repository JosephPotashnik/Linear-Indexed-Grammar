using LinearIndexedGrammarParser;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
        private GeneticAlgorithmParameters parameters = new GeneticAlgorithmParameters();
        private readonly Learner learner;
        private PriorityQueue<double, GrammarWithProbability> population = new PriorityQueue<double, GrammarWithProbability>();

        public GeneticAlgorithm(Learner l)
        {
            parameters.NumberOfGenerations = 1000;
            parameters.PopulationSize = 200;
            learner = l;

            Grammar initialGrammar = learner.CreateInitialGrammar();
            var prob = learner.Energy(initialGrammar).Probability;

            for (int i = 0; i < parameters.PopulationSize; i++)
                population.Enqueue(prob, new GrammarWithProbability(initialGrammar, prob));
        }

        public (double prob, Grammar g) Run()
        {
            int currentGeneration = 0;
            ConcurrentQueue<KeyValuePair<double, Grammar>> descendants = new ConcurrentQueue<KeyValuePair<double, Grammar>>();
            while (currentGeneration++ < parameters.NumberOfGenerations)
            {
                if (currentGeneration % 200 == 0)
                    Console.WriteLine($"generation {currentGeneration}");
                try
                {
                    Parallel.ForEach(population.Values, (individual) =>
                    {
                        var mutatedIndividual = Mutate(individual);
                        if (mutatedIndividual.Grammar != null)
                            descendants.Enqueue(new KeyValuePair<double, Grammar>(mutatedIndividual.Probability, mutatedIndividual.Grammar));
                    }
                    );

                    InsertDescendantsIntoPopulation(descendants);
                }

                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
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

            //foreach (var item in bestHypothesis.staticRulesGeneratedForCategory)
            //{
            //    if (item.ToString()[0] == 'X')
            //    {
            //    }

            //}

            return (bestProbability, bestHypothesis);
        }

        private void InsertDescendantsIntoPopulation(ConcurrentQueue<KeyValuePair<double, Grammar>> descendants)
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
