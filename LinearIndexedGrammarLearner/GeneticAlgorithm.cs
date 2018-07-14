using LinearIndexedGrammarParser;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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

    public class GeneticAlgorithm
    {
        private GeneticAlgorithmParameters parameters = new GeneticAlgorithmParameters();
        private readonly Learner learner;
        private PriorityQueue<double, Grammar> population = new PriorityQueue<double, Grammar>();

        public GeneticAlgorithm(Learner l, Vocabulary voc)
        {
            parameters.NumberOfGenerations = 100;
            parameters.PopulationSize = 50;
            learner = l;

            Grammar initialGrammar = learner.CreateInitialGrammar(voc);
            var prob = learner.Energy(initialGrammar).Probability;

            for (int i = 0; i < parameters.PopulationSize; i++)
                population.Enqueue(prob, new Grammar(initialGrammar));
        }
        public (Energy e, Grammar g) Run()
        {
            int currentGeneration = 0;
            ConcurrentQueue<KeyValuePair<double, Grammar>> descendants = new ConcurrentQueue<KeyValuePair<double, Grammar>>();

            while (currentGeneration++ < parameters.NumberOfGenerations)
            {
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


                    //read off descendants list and push into population every grammar
                    //that is better from the grammar with the current lowest probability in the population.
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

                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }

            var bestHypothesis = population.Last().Value.First();
            var bestProbability = population.Last().Key;
            var s = string.Format($"Best Hypothesis:\r\n{bestHypothesis} \r\n with probability {bestProbability}");

            Console.WriteLine(s);
            using (var sw = File.AppendText("SessionReport.txt"))
            {
                sw.WriteLine(s);
            }
            return (null, bestHypothesis);
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
    }
}
