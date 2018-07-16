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
            parameters.NumberOfGenerations = 100;
            parameters.PopulationSize = 100;
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
                //Console.WriteLine($"generation {currentGeneration}");
                try
                {
                    //mutate each grammar and push the result into the descendants list.
                    Parallel.ForEach(population.Values, (individual) =>
                    {

                        (double prob, Grammar mutatedIndividual) = Mutate(individual);
                        if (mutatedIndividual != null)
                            descendants.Enqueue(new KeyValuePair<double, Grammar>(prob, mutatedIndividual));
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

                        (double prob, Grammar child) = Crossover(descendantsList[i].Value, descendantsList[i + halfDescendantssize].Value);
                        if (child != null)
                            descendants.Enqueue(new KeyValuePair<double, Grammar>(prob, child));
                    }
                    );

                    InsertDescendantsIntoPopulation(descendants);

                    bestProbability = population.Last().Key;

                    if (bestProbability - bestPreviousProbability < 0.0001)
                    {
                        generationsWithoutChange++;
                        if (generationsWithoutChange > 50)
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

        private (double probability, Grammar mutatedIndividual) Mutate(Grammar individual)
        {
            double prob = 0.0;
            var mutatedIndividual = learner.GetNeighbor(individual);
            Energy newEnergy = null;
            if (mutatedIndividual != null)
                newEnergy = learner.Energy(mutatedIndividual);

            if (newEnergy != null)
                prob = newEnergy.Probability;

            return (prob, mutatedIndividual);
        }

        private (double probability, Grammar mutatedIndividual) Crossover(Grammar parent1, Grammar parent2)
        {
            double prob = 0.0;
            var child = learner.GetChild(parent1, parent2);
            Energy newEnergy = null;
            if (child != null)
                newEnergy = learner.Energy(child);

            if (newEnergy != null)
                prob = newEnergy.Probability;

            return (prob, child);
        }
    }
}
