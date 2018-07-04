using System;
using System.IO;
using LinearIndexedGrammarParser;
using Newtonsoft.Json;

namespace LinearIndexedGrammarLearner
{
    public class SimulatedAnnealing
    {
        private readonly float coolingFactor;
        private readonly Learner learner;
        private readonly int reportEveryNIterations;
        private readonly float threshold;
        private Grammar bestHypothesis, currentHypothesis;
        private Energy currentEnergy, bestEnergy;
        private int currentIteration, bestIteration;
        private double currentTemp;

        public SimulatedAnnealing(Learner l, Vocabulary voc)
        {
            SimulatedAnnealingRunningParameters pam;
            using (var file = File.OpenText(@"SimulatedAnnealingParameters.json"))
            {
                var serializer = new JsonSerializer();
                pam =
                    (SimulatedAnnealingRunningParameters)
                        serializer.Deserialize(file, typeof(SimulatedAnnealingRunningParameters));
            }

            learner = l;

            threshold = pam.ThresholdTemperature;
            coolingFactor = pam.CoolingFactor;
            currentIteration = bestIteration = 1;
            reportEveryNIterations = pam.ReportEveryNIteration;

            currentHypothesis = bestHypothesis = learner.CreateInitialGrammar(voc);
            currentEnergy = bestEnergy = learner.Energy(currentHypothesis);

            currentTemp = currentEnergy.DataEnergy * pam.InitialTemperatureTimesInitialEnegrgy;
            using (var sw = File.AppendText("SessionReport.txt"))
            {
                sw.WriteLine(string.Format("cooling factor: {0}, initial energy: {1}, initial temperature: {2}",
                    coolingFactor, currentEnergy, currentTemp));
            }
        }

        public int Energy { get; set; }

        private double P(Energy currStateEnergy, Energy possibleStateEnergy, double temp)
        {
            var calcValue = Math.Exp((currStateEnergy.DataEnergy - possibleStateEnergy.DataEnergy) / temp);
            return Math.Min(1.0, calcValue);
        }

        public Tuple<Energy, Grammar> Run()
        {
            var rand = new Random();
            while (currentTemp > threshold)
            {
                try
                {
                    var newHypothesis = learner.GetNeighbor(currentHypothesis);

                    Energy newEnergy = null;
                    if (newHypothesis != null)
                    {
                        newEnergy = learner.Energy(newHypothesis);
                    }

                    if (newEnergy != null)
                    {
                        if (newEnergy < bestEnergy)
                        {
                            bestEnergy = newEnergy;
                            bestHypothesis = newHypothesis;
                            bestIteration = currentIteration;
                        }

                        var prob = P(currentEnergy, newEnergy, currentTemp);

                        if (rand.NextDouble() < prob)
                        {
                            // moved to new hypothesis
                            currentHypothesis = newHypothesis;
                            currentEnergy = newEnergy;
                        }
                    }
                    currentIteration++;
                    if (currentIteration % reportEveryNIterations == 0)
                        Console.WriteLine("Iteration {0}", currentIteration);
                    currentTemp *= coolingFactor;
                }

                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }


            var s = string.Format("{0}. ({1}) \r\nBest Hypothesis: \r\n{2}Best so far: #{3} with energy: {4}\r\n",
                currentIteration, currentTemp, bestHypothesis,bestIteration, bestEnergy);

            Console.WriteLine(s);
            using (var sw = File.AppendText("SessionReport.txt"))
            {
                sw.WriteLine(s);
            }
            return new Tuple<Energy, Grammar>(bestEnergy, bestHypothesis);
        }

        [JsonObject(MemberSerialization.OptIn)]
        public class SimulatedAnnealingRunningParameters
        {
            [JsonProperty]
            public float CoolingFactor { get; set; }

            [JsonProperty]
            public float InitialTemperatureTimesInitialEnegrgy { get; set; }

            [JsonProperty]
            public int ReportEveryNIteration { get; set; }

            [JsonProperty]
            public int ThresholdTemperature { get; set; }
        }
    }
}