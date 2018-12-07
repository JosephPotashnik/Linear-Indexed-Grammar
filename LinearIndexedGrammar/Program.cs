using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using LinearIndexedGrammarLearner;
using LinearIndexedGrammarParser;
using Newtonsoft.Json;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace LinearIndexedGrammar
{
    [JsonObject(MemberSerialization.OptIn)]
    public class ProgramParamsList
    {
        [JsonProperty] public ProgramParams[] ProgramsToRun { get; set; }
    }

    //TODO: add new unit tests for rule space generation.
    //int ans = ruleSpace.FindRHSIndex(new[] { "D", "N" });
    //ans = ruleSpace.FindRHSIndex(new[] { "D", "X2" });
    //var ans2 = ruleSpace.FindRHSIndex(new[] { "X3", "P" });
    //ans = ruleSpace.FindRHSIndex(new[] { "X3" });
    //ans = ruleSpace.FindRHSIndex(new[] { "D" });
    //ans = ruleSpace.FindLHSIndex("X1");
    //ans = ruleSpace.FindLHSIndex("X3");

    //var r = new Rule("X2", new[] { "V", "X3" });
    //var rc = ruleSpace.FindRule(r);
    //var res = ruleSpace[rc];



    [JsonObject(MemberSerialization.OptIn)]
    public class ProgramParams
    {
        [JsonProperty] public int PopulationSize { get; set; }

        [JsonProperty] public int NumberOfGenerations { get; set; }

        [JsonProperty] public int NumberOfRuns { get; set; }

        [JsonProperty] public string GrammarFileName { get; set; }
    }

    internal class Program
    {
        private static void Learn()
        {
            var fileName = @"ProgramsToRun.json";
            fileName = @"NightRunFull.json";

            var programParamsList = ReadProgramParamsFromFile(fileName);
            foreach (var programParams in programParamsList.ProgramsToRun)
                RunProgram(programParams);
        }

        private static void Main()
        {
            ConfigureLogger();
            Learn();
        }

        private static void ConfigureLogger()
        {
            var config = new LoggingConfiguration();

            var logfile = new FileTarget("logfile") {FileName = "SessionReport.txt"};
            var logConsole = new ConsoleTarget("logConsole");

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


        

        private static (string[] data, Vocabulary dataVocabulary) PrepareDataFromTargetGrammar(List<Rule> grammarRules, Vocabulary universalVocabulary, int maxWords = 6)
        {
            var cfGrammar = new ContextFreeGrammar(grammarRules);
            var generator = new EarleyGenerator(cfGrammar);
            var nodeList = generator.ParseSentence("", new CancellationTokenSource(), maxWords);
            return GrammarFileReader.GetSentencesOfGenerator(nodeList, universalVocabulary);
        }

        private static void PrepareRuleSpace(Vocabulary dataVocabulary, string[] data, int numberOfAllowedNonTerminals)
        {
            var posInText = dataVocabulary.POSWithPossibleWords.Keys.ToArray();
            var bigrams = ContextFreeGrammar.GetBigramsOfData(data, dataVocabulary);
            ContextSensitiveGrammar.RuleSpace = new RuleSpace(posInText, bigrams, numberOfAllowedNonTerminals);
        }
        private static bool ValidateTargetGrammar(List<Rule> grammarRules, string[] data, Vocabulary dataVocabulary)
        {
            var sentences = data.Select(x => x.Split()).ToArray();
            int maxWordsInSentence = sentences.Max(x => x.Length);

            var learner = new Learner(data, maxWordsInSentence, dataVocabulary);
            IObjectiveFunction<double> objectiveFunction = new GrammarFitnessObjectiveFunction(learner);
            var posInText = dataVocabulary.POSWithPossibleWords.Keys.ToHashSet();
            ContextFreeGrammar.RenameVariables(grammarRules, posInText);
            var targetGrammar = new ContextSensitiveGrammar(grammarRules);

            var targetProb = objectiveFunction.Compute(targetGrammar, false);

            string s = $"Target Hypothesis:\r\n{targetGrammar}\r\n. Verifying probability of target grammar (should be 1): {targetProb}\r\n";
            LogManager.GetCurrentClassLogger().Info(s);
            if (targetProb < 1)
            {
                LogManager.GetCurrentClassLogger().Fatal("probablity incorrect. exit!");
                return false;
            }

            return true;
        }

        private static void RunProgram(ProgramParams programParams)
        {
            var universalVocabulary = Vocabulary.ReadVocabularyFromFile(ContextFreeGrammar.VocabularyFile);

            ContextFreeGrammar.PartsOfSpeech = universalVocabulary.POSWithPossibleWords.Keys.Select(x => new SyntacticCategory(x)).ToHashSet();
            var grammarRules = GrammarFileReader.ReadRulesFromFile(programParams.GrammarFileName);
            var (data, dataVocabulary) = PrepareDataFromTargetGrammar(grammarRules, universalVocabulary);
            int numberOfAllowedNonTerminals = 6;
            PrepareRuleSpace(dataVocabulary, data, numberOfAllowedNonTerminals);

            if (!ValidateTargetGrammar(grammarRules, data, dataVocabulary))
                return;

            var s = "-------------------\r\n" +
                    $"Session {DateTime.Now:MM/dd/yyyy h:mm tt}\r\n" +
                    $"runs: {programParams.NumberOfRuns}, population size: {programParams.PopulationSize}, number of generations: {programParams.NumberOfGenerations}\r\n";

            LogManager.GetCurrentClassLogger().Info(s);
            var stopWatch = StartWatch();

            var probs = new List<double>();
            programParams.NumberOfRuns = 1;
            for (var i = 0; i < programParams.NumberOfRuns; i++)
            {
                LogManager.GetCurrentClassLogger().Info($"Run {i + 1}:");

                var (bestHypothesis, bestValue) = LearnGrammarFromData(data, dataVocabulary);
                probs.Add(bestValue);

                s = $"Best Hypothesis:\r\n{bestHypothesis} \r\n with probability {bestValue}";
                LogManager.GetCurrentClassLogger().Info(s);

                //the following line should be uncommented for sanity checks (i.e, it suffices to see that
                //we arrived at a possible solution), for night run / unit tests, etc.
                //if (objectiveFunction.IsMaximalValue(bestValue)) break;
            }

            var numTimesAchieveProb1 = probs.Count(x => Math.Abs(x - 1) < GeneticAlgorithm<double>.Tolerance);
            var averageProb = probs.Average();
            s = $"Average probability is: {averageProb}\r\n" +
                $"Achieved Probability=1 in {numTimesAchieveProb1} times out of {programParams.NumberOfRuns} runs";
            LogManager.GetCurrentClassLogger().Info(s);
            StopWatch(stopWatch);
        }

        public static (ContextSensitiveGrammar bestGrammar, double bestValue) LearnGrammarFromData(string[] data, Vocabulary dataVocabulary)
        {
            int numberOfAllowedNonTerminals = 6;
            PrepareRuleSpace(dataVocabulary, data, numberOfAllowedNonTerminals);

            var sentences = data.Select(x => x.Split()).ToArray();
            int maxWordsInSentence = sentences.Max(x => x.Length);

            var learner = new Learner(data, maxWordsInSentence, dataVocabulary);
            IObjectiveFunction<double> objectiveFunction = new GrammarFitnessObjectiveFunction(learner);
            var numberOfIterations = 1000;
            var coolingFactor = 0.99;
            var initialTemperature = 2000;

            var algorithm = new SimulatedAnnealing<double>(learner, numberOfIterations, coolingFactor, initialTemperature,objectiveFunction);
            var (bestHypothesis, bestValue) = algorithm.Run();
            return (bestHypothesis, bestValue);
        }

        private static ProgramParamsList ReadProgramParamsFromFile(string fileName)
        {
            ProgramParamsList programParamsList;
            using (var file = File.OpenText(fileName))
            {
                var serializer = new JsonSerializer();
                programParamsList = (ProgramParamsList) serializer.Deserialize(file, typeof(ProgramParamsList));
            }

            return programParamsList;
        }
    }
}