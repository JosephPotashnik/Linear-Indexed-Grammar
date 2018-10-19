﻿using LinearIndexedGrammarLearner;
using LinearIndexedGrammarParser;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace LinearIndexedGrammar
{
    [JsonObject(MemberSerialization.OptIn)]
    public class ProgramParamsList
    {
        [JsonProperty]
        public ProgramParams[] ProgramsToRun { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class ProgramParams
    {
        [JsonProperty]
        public int PopulationSize { get; set; }

        [JsonProperty]
        public int NumberOfGenerations { get; set; }

        [JsonProperty]
        public int NumberOfRuns { get; set; }

        [JsonProperty]
        public string GrammarFileName { get; set; }
    }

    class Program
    {
        static void Main()
        {
            ConfigureLogger();
            Learn();
        }

        private static void ConfigureLogger()
        {
            var config = new NLog.Config.LoggingConfiguration();

            var logfile = new NLog.Targets.FileTarget("logfile") { FileName = "SessionReport.txt" };
            var logConsole = new NLog.Targets.ConsoleTarget("logConsole");

            config.AddRule(LogLevel.Info, LogLevel.Fatal, logConsole);
            config.AddRule(LogLevel.Debug, LogLevel.Fatal, logfile);

            LogManager.Configuration = config;
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


        private static void Learn(int maxWordsInSentence = 6)
        {
            string fileName = @"ProgramsToRun.json";
            //fileName = @"NightRunFull.json";

            var programParamsList = ReadProgramParamsFromFile(fileName);
            Vocabulary universalVocabulary = Vocabulary.ReadVocabularyFromFile(@"Vocabulary.json");

            foreach (var programParams in programParamsList.ProgramsToRun)
            {
                RunProgram(programParams, maxWordsInSentence, universalVocabulary);

            }
        }

        private static void RunProgram(ProgramParams programParams, int maxWordsInSentence, Vocabulary universalVocabulary)
        {
            var (nodeList, targetGrammar) = GrammarFileReader.GenerateSentenceAccordingToGrammar(programParams.GrammarFileName, maxWordsInSentence);
            var (data, dataVocabulary) = GrammarFileReader.GetSentencesOfGenerator(nodeList, universalVocabulary);


            var s = "-------------------\r\n" +
                        $"Session {DateTime.Now:MM/dd/yyyy h:mm tt}\r\n" +
                        $"runs: { programParams.NumberOfRuns}, population size: {programParams.PopulationSize}, number of generations: {programParams.NumberOfGenerations}\r\n";

            LogManager.GetCurrentClassLogger().Info(s);
            var stopWatch = StartWatch();

            var learner = new Learner(data, maxWordsInSentence, dataVocabulary);
            var targetProb = learner.Probability(targetGrammar);

            s = $"Target Hypothesis:\r\n{targetGrammar}\r\n. Verifying probability of target grammar (should be 1): {targetProb}\r\n";
            LogManager.GetCurrentClassLogger().Info(s);
            if (targetProb < 1)
            {
                LogManager.GetCurrentClassLogger().Fatal("probablity incorrect. exit!");
                return;
            }

            var probs = new List<double>();
            for (var i = 0; i < programParams.NumberOfRuns; i++)
            {
                LogManager.GetCurrentClassLogger().Info($"Run {i + 1}:");
                var ga = new GeneticAlgorithm(learner, programParams.PopulationSize, programParams.NumberOfGenerations);
                var hypotheses = ga.Run();
                probs.Add(hypotheses[0].Probability);
                //if (Math.Abs(hypotheses[0].Probability - 1) < Tolerance) break;
            }

            int numTimesAchieveProb1 = probs.Count(x => Math.Abs(x - 1) < Tolerance);
            double averageProb = probs.Average();
            s = $"Average probability is: {averageProb}\r\n" +
                $"Achieved Probability=1 in {numTimesAchieveProb1} times out of {programParams.NumberOfRuns} runs";
            LogManager.GetCurrentClassLogger().Info(s);
            StopWatch(stopWatch);
        }

        public const double Tolerance = 0.0001;

        private static ProgramParamsList ReadProgramParamsFromFile(string fileName)
        {
            ProgramParamsList programParamsList;
            using (var file = File.OpenText(fileName))
            {
                var serializer = new JsonSerializer();
                programParamsList = (ProgramParamsList)serializer.Deserialize(file, typeof(ProgramParamsList));
            }

            return programParamsList;
        }


       
    }
}
