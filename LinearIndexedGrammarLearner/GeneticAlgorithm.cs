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
        private PriorityQueue<double, Grammar> population = new PriorityQueue<double, Grammar>();

        public GeneticAlgorithm(Learner l, Vocabulary voc)
        {
            parameters.NumberOfGenerations = 300;
            parameters.PopulationSize = 300;
            learner = l;

            Grammar initialGrammar = learner.CreateInitialGrammar(voc);
            var prob = learner.Energy(initialGrammar).Probability;

            for (int i = 0; i < parameters.PopulationSize; i++)
                population.Enqueue(prob, new Grammar(initialGrammar));
        }

        public static void Shuffle(List<KeyValuePair<double, Grammar>> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = ThreadSafeRandom.ThisThreadsRandom.Next(n + 1);
                KeyValuePair<double, Grammar> value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }
        public (Energy e, Grammar g) Run()
        {
            int currentGeneration = 0;
            ConcurrentQueue<KeyValuePair<double, Grammar>> descendants = new ConcurrentQueue<KeyValuePair<double, Grammar>>();
            double bestProbability;
            double bestPreviousProbability;
            bestProbability = population.Last().Key;
            int generationsWithoutChange = 0;
            while (currentGeneration++ < parameters.NumberOfGenerations)
            {
                bestPreviousProbability = population.Last().Key;
                Console.WriteLine($"generation {currentGeneration}");
                try
                {
                    //mutate each grammar and push the result into the descendants list.
                    Parallel.ForEach(population.Values, (individual) =>
                    {
                        var mutatedIndividual = Mutate(individual);
                        if (mutatedIndividual.Grammar != null)
                            descendants.Enqueue(new KeyValuePair<double, Grammar>(mutatedIndividual.Probability, mutatedIndividual.Grammar));
                    }
                    );
                    var descendantsList = descendants.ToList();

                    Shuffle(descendantsList);

                    //read off descendants list and push into population every grammar
                    //that is better from the grammar with the current lowest probability in the population.
                    InsertDescendantsIntoPopulation(descendants);

                    int halfDescendantssize = descendantsList.Count / 2;
                    Parallel.For(0, halfDescendantssize, (i) =>
                    {

                        var childrenList = Crossover(descendantsList[i].Value, descendantsList[i + halfDescendantssize].Value);
                        foreach (var child in childrenList)
                        {
                            if (child.Grammar != null)
                                descendants.Enqueue(new KeyValuePair<double, Grammar>(child.Probability, child.Grammar));
                        }
                    }
                    );

                    InsertDescendantsIntoPopulation(descendants);

                    bestProbability = population.Last().Key;

                    if (bestProbability - bestPreviousProbability < 0.001)
                    {
                        generationsWithoutChange++;
                        if (generationsWithoutChange > 300)
                            break;
                    }
                    else
                    {
                        generationsWithoutChange = 0;
                    }

                }

                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }

            var bestHypothesis = population.Last().Value.First();
            bestHypothesis.PruneUnusedRulesLHS();
            bestHypothesis.PruneUnusedRulesRHS();

            bestProbability = population.Last().Key;
            var s = string.Format($"Best Hypothesis:\r\n{bestHypothesis} \r\n with probability {bestProbability}");

            Console.WriteLine(s);
            using (var sw = File.AppendText("SessionReport.txt"))
            {
                sw.WriteLine(s);
            }
            return (null, bestHypothesis);
        }

        private void InsertDescendantsIntoPopulation(ConcurrentQueue<KeyValuePair<double, Grammar>> descendants)
        {
            while (descendants.Any())
            {
                KeyValuePair<double, Grammar> descendant;
                bool success = descendants.TryDequeue(out descendant);
                if (success)
                {

                    if (descendant.Key > population.PeekFirstKey())
                    {
                        population.Dequeue();
                        population.Enqueue(descendant.Key, descendant.Value);
                    }

                }
            }
        }

        private GrammarWithProbability Mutate(Grammar individual)
        {
            var mutatedIndividual = learner.GetNeighbor(individual);
            return learner.ComputeProbabilityForGrammar(mutatedIndividual);
        }

        private List<GrammarWithProbability> Crossover(Grammar parent1, Grammar parent2)
        {
            var childrenList = new List<GrammarWithProbability>();

            (var child1, var child2) = learner.GetChild(parent1, parent2);

            childrenList.Add(learner.ComputeProbabilityForGrammar(child1));
            childrenList.Add(learner.ComputeProbabilityForGrammar(child2));

            return childrenList;
        }



    }
}
