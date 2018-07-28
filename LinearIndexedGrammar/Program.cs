using LinearIndexedGrammarLearner;
using LinearIndexedGrammarParser;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace LinearIndexedGrammar
{
    [JsonObject(MemberSerialization.OptIn)]
    public class ProgramParams
    {
        [JsonProperty]
        public int PopulationSize { get; set; }

        [JsonProperty]
        public int NumberOfGenerations { get; set; }

        [JsonProperty]
        public int NumberOfRuns { get; set; }
    }

    class Program
    {
        static void Main(string[] args)
        {
            Learn();
        }

        public static void StopWatch(Stopwatch stopWatch)
        {
            stopWatch.Stop();
            var ts = stopWatch.Elapsed;
            var elapsedTime = $"{ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}.{ts.Milliseconds / 10:00}";
            var s = "Overall session RunTime " + elapsedTime;
            Console.WriteLine(s);
            using (var sw = File.AppendText("SessionReport.txt"))
            {
                sw.WriteLine(s);
            }
        }

        public static Stopwatch StartWatch()
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            return stopWatch;
        }


        private static void Learn(int maxWordsInSentence = 6)
        {
            ProgramParams programParams = ReadProgramParamsFromFile();
            Vocabulary universalVocabulary = Vocabulary.ReadVocabularyFromFile(@"Vocabulary.json");

            (var n, var targetGrammar) = GrammarFileReader.GenerateSentenceAccordingToGrammar("SimpleCFG.txt", maxWordsInSentence);
            (var data, var dataVocabulary) = GrammarFileReader.GetSentencesOfGenerator(n, universalVocabulary);

            using (var sw = File.AppendText("SessionReport.txt"))
            {
                sw.WriteLine("-------------------");
                sw.WriteLine("Session {0} ", DateTime.Now.ToString("MM/dd/yyyy h:mm tt"));
                sw.WriteLine("runs: {0}, population size: {1}, number of generations: {2}", programParams.NumberOfRuns, programParams.PopulationSize, programParams.NumberOfGenerations);
            }


            var stopWatch = StartWatch();

            var learner = new Learner(data, maxWordsInSentence, dataVocabulary);
            var targetProb = learner.Probability(targetGrammar);
            var s = string.Format("Target Hypothesis:\r\n{0}\r\n. Verifying probability of target grammar (should be 1): {1}\r\n", targetGrammar, targetProb);
            Console.WriteLine(s);
            if (targetProb < 1)
            {
                Console.WriteLine("probablity incorrect. exit!");
                return;
            }

            //(var n1, var targetGrammar1) = GrammarFileReader.GenerateSentenceAccordingToGrammar("SolutionCFG.txt", maxWordsInSentence);
            //(var data1, var dataVocabulary1) = GrammarFileReader.GetSentencesOfGenerator(n1, universalVocabulary);
            //var energy1 = learner.Energy(targetGrammar1);
            //var ruleDistribution = learner.CollectUsages(targetGrammar1);
            //targetGrammar1.PruneUnusedRules(ruleDistribution);
            //Console.WriteLine(targetGrammar1);


            using (var sw = File.AppendText("SessionReport.txt"))
            {
                sw.WriteLine(s);
            }

            List<double> probs = new List<double>();
            for (var i = 0; i < programParams.NumberOfRuns; i++)
            {
                var GA = new GeneticAlgorithm(learner, programParams.PopulationSize, programParams.NumberOfGenerations);
                (var prob, var bestHypothesis) = GA.Run();
                probs.Add(prob);
            }
            using (var sw = File.AppendText("SessionReport.txt"))
            {
                int numTimesAchieveProb1 = probs.Where(x => x == 1).Count();
                double averageProb = probs.Average();
                string s1 = $"Average probability is: {averageProb}";
                string s2 = $"Achieved Probability=1 in {numTimesAchieveProb1} times out of {programParams.NumberOfRuns} runs";
                sw.WriteLine(s1);
                sw.WriteLine(s2);
                Console.WriteLine(s1);
                Console.WriteLine(s2);

            }
            StopWatch(stopWatch);
        }

        private static ProgramParams ReadProgramParamsFromFile()
        {
            ProgramParams programParams;
            using (var file = File.OpenText(@"ProgramParameters.json"))
            {
                var serializer = new JsonSerializer();
                programParams = (ProgramParams)serializer.Deserialize(file, typeof(ProgramParams));
            }

            return programParams;
        }


       
    }
}
