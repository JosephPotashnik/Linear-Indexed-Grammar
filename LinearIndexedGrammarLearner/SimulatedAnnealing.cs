using System;
using System.IO;
using LinearIndexedGrammarParser;
using Newtonsoft.Json;

namespace LinearIndexedGrammarLearner
{
    public class SimulatedAnnealing
    {
        SimulatedAnnealingRunningParameters simulatedAnnealingParams;
        private readonly Learner learner;
        private Grammar bestHypothesis, currentHypothesis;
        private Energy currentEnergy, bestEnergy;
        private int currentIteration, bestIteration;
        private double currentTemp;

        public SimulatedAnnealing(Learner l, Vocabulary voc)
        {
            simulatedAnnealingParams = ReadSimulatedAnnealingParametersFromFile();
            learner = l;
            currentIteration = bestIteration = 1;
            currentHypothesis = bestHypothesis = learner.CreateInitialGrammar(voc);
            currentEnergy = bestEnergy = learner.Energy(currentHypothesis);

            currentTemp = currentEnergy.DataEnergy * simulatedAnnealingParams.InitialTemperatureTimesInitialEnegrgy;
            using (var sw = File.AppendText("SessionReport.txt"))
            {
                sw.WriteLine(string.Format("cooling factor: {0}, initial energy: {1}, initial temperature: {2}",
                    simulatedAnnealingParams.CoolingFactor, currentEnergy, currentTemp));
            }
        }

        private static SimulatedAnnealingRunningParameters ReadSimulatedAnnealingParametersFromFile()
        {
            SimulatedAnnealingRunningParameters pam;
            using (var file = File.OpenText(@"SimulatedAnnealingParameters.json"))
            {
                var serializer = new JsonSerializer();
                pam =
                    (SimulatedAnnealingRunningParameters)
                        serializer.Deserialize(file, typeof(SimulatedAnnealingRunningParameters));
            }

            return pam;
        }

        private double P(Energy currStateEnergy, Energy possibleStateEnergy, double temp)
        {
            var calcValue = Math.Exp((currStateEnergy.DataEnergy - possibleStateEnergy.DataEnergy) / temp);
            return Math.Min(1.0, calcValue);
        }

        public (Energy e, Grammar g) Run()
        {
            var rand = new Random();
            while (currentTemp > simulatedAnnealingParams.ThresholdTemperature)
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
                    if (currentIteration % simulatedAnnealingParams.ReportEveryNIteration == 0)
                        Console.WriteLine("Iteration {0}", currentIteration);
                    currentTemp *= simulatedAnnealingParams.CoolingFactor;
                }

                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }


            var s = string.Format("{0}. ({1}) \r\nBest Hypothesis: \r\n{2}\r\nBest so far: #{3} with energy: {4}\r\n",
                currentIteration, currentTemp, bestHypothesis,bestIteration, bestEnergy);

            Console.WriteLine(s);
            using (var sw = File.AppendText("SessionReport.txt"))
            {
                sw.WriteLine(s);
            }
            return (bestEnergy, bestHypothesis);
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