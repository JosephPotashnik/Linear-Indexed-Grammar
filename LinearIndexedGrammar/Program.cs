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

    [JsonObject(MemberSerialization.OptIn)]
    public class ProgramParams
    {
        [JsonProperty] public int NumberOfRuns { get; set; }
        [JsonProperty] public string GrammarFileName { get; set; }
        [JsonProperty] public string VocabularyFileName { get; set; }
        [JsonProperty] public bool IsCFG { get; set; }
    }

    public class Program
    {

        private static void Learn(bool nightRun = true)
        {
            var fileName = nightRun ? @"NightRunFull.json" : @"ProgramsToRun.json";

            var programParamsList = ReadProgramParamsFromFile(fileName);
            foreach (var programParams in programParamsList.ProgramsToRun)
                RunProgram(programParams);
        }

        private static void Main()
        {
            ConfigureLogger();
            Learn();
            //LearnGrammarFromFile("CorwinBooks.txt", true);
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

        private static (string[] data, Vocabulary dataVocabulary) PrepareDataFromTargetGrammar(List<Rule> grammarRules, Vocabulary universalVocabulary, int maxWords)
        {
            int numberOfSentencesPerTree = 10;
            var cfGrammar = new ContextFreeGrammar(grammarRules);
            var generator = new EarleyGenerator(cfGrammar, universalVocabulary);
            var nodeList = generator.ParseSentence("", new CancellationTokenSource(), maxWords);
            return GrammarFileReader.GetSentencesOfGenerator(nodeList, universalVocabulary, numberOfSentencesPerTree);
        }

        private static IEnumerable<string> GetSentenceFromDataFile(string dataFileName)
        {
            //read sentences. (unused delimiters for sentence level: ' ' and '-')
            var sentenceDelimiters = new[]
                {'(', ')', '?', ',', '*', '.', ';', '!', '\\', '/', ':', '"', '“', '—', '”'};
            var filestring = System.IO.File.ReadAllText(dataFileName);
            var sentences1 = filestring.Split(sentenceDelimiters, StringSplitOptions.RemoveEmptyEntries);
            //discard empty spaces, lowercase. I will not be concerned with capitalization for now.
            var sentences = sentences1.Select(sentence => sentence.TrimStart()).Select(sentence => sentence.ToLower());
            sentences = sentences.Select(sentence => sentence.Replace('\r', ' '));
            sentences = sentences.Select(sentence => sentence.Replace('\n', ' '));
            return sentences;
        }
        private static (IEnumerable<string> data, Vocabulary vocabulary) FilterDataAccordingToTargetGrammar(string grammarFileName, string dataFileName,  int minWords, int maxWords, string universalVocabularyFileName = @"UniversalVocabulary.json")
        {
            var universalVocabulary = Vocabulary.ReadVocabularyFromFile(universalVocabularyFileName);
            ContextFreeGrammar.PartsOfSpeech = universalVocabulary.POSWithPossibleWords.Keys.Select(x => new SyntacticCategory(x)).ToHashSet();
            var grammarRules = GrammarFileReader.ReadRulesFromFile(grammarFileName);
            var cfGrammar = new ContextFreeGrammar(grammarRules);

            //1. get sentences from file
            var allData = GetSentenceFromDataFile(dataFileName);

            //2. leave only sentences with words recognized by the universal vocabulary.
            var sentencesInVocabulary = universalVocabulary.LeaveOnlySentencesWithWordsInVocabulary(allData).ToArray();

            //3. leave only sentences in word length range from minWords to maxWords in a sentence.
            (var sentences, var posInText) =
                GrammarFileReader.GetDataAndVocabularyForSentencesUpToLengthN(sentencesInVocabulary, universalVocabulary, minWords, maxWords);

            var learner = new Learner(sentences, maxWords, minWords, posInText, universalVocabulary);

            //4. leave only sentences that can be parsed according to an ideal, oracular grammar that is supposed to parse the input
            //if no oracle is supplied, try to learn the entire input. 
            List<string> filteredSen = new List<string>();

            if (grammarFileName != null)
            {
                SentenceParsingResults[] allParses = learner.ParseAllSentences(cfGrammar);
                var parsableData = allParses.Where(x => x.Trees.Count > 0);

                foreach (var sen in parsableData)
                {
                    for (int i = 0; i < sen.Count; i++)
                        filteredSen.Add(sen.Sentence);
                }
            }
            else
            {
                filteredSen = sentences.ToList();
            }

            if (!ValidateTargetGrammar(grammarRules, filteredSen.ToArray(), universalVocabulary))
                throw new Exception();

            return (filteredSen,universalVocabulary);

        }

        private static bool ValidateTargetGrammar(List<Rule> grammarRules, string[] data, Vocabulary universalVocabulary)
        {
            var sentences = data.Select(x => x.Split()).ToArray();
            int maxWordsInSentence = sentences.Max(x => x.Length);
            int minWordsInSentences = sentences.Min(x => x.Length);

            PrepareLearningUpToSentenceLengthN(data, universalVocabulary, minWordsInSentences, maxWordsInSentence, out var objectiveFunction);
            var partOfSpeechCategories = universalVocabulary.POSWithPossibleWords.Keys.ToHashSet();
            ContextFreeGrammar.RenameVariables(grammarRules, partOfSpeechCategories);
            var targetGrammar = new ContextSensitiveGrammar(grammarRules);

            var targetProb = objectiveFunction.Compute(targetGrammar);

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
            var universalVocabulary = Vocabulary.ReadVocabularyFromFile(programParams.VocabularyFileName);
            ContextFreeGrammar.PartsOfSpeech = universalVocabulary.POSWithPossibleWords.Keys.Select(x => new SyntacticCategory(x)).ToHashSet();
            var grammarRules = GrammarFileReader.ReadRulesFromFile(programParams.GrammarFileName);

            int maxSentenceLength = 12;
            var (data, dataVocabulary) = PrepareDataFromTargetGrammar(grammarRules, universalVocabulary, maxSentenceLength);

            if (!ValidateTargetGrammar(grammarRules, data, dataVocabulary))
                return;

            var s = "------------------------------------------------------------\r\n" +
                    $"Session {DateTime.Now:MM/dd/yyyy h:mm tt}\r\n" +
                    $"runs: {programParams.NumberOfRuns}, grammar file name: {programParams.GrammarFileName}, vocabulary file name: {programParams.VocabularyFileName}\r\n";

            LogManager.GetCurrentClassLogger().Info(s);
            var stopWatch = StartWatch();

            var probs = new List<double>();
            for (var i = 0; i < programParams.NumberOfRuns; i++)
            {
                LogManager.GetCurrentClassLogger().Info($"Run {i + 1}:");

                var (bestHypothesis, bestValue) = LearnGrammarFromData(data, dataVocabulary, programParams.IsCFG);
                probs.Add(bestValue);

                s = $"Best Hypothesis:\r\n{bestHypothesis} \r\n with probability {bestValue}";
                LogManager.GetCurrentClassLogger().Info(s);
            }

            var numTimesAchieveProb1 = probs.Count(x => Math.Abs(x - 1) < 0.00001);
            var averageProb = probs.Average();
            s = $"Average probability is: {averageProb}\r\n" +
                $"Achieved Probability=1 in {numTimesAchieveProb1} times out of {programParams.NumberOfRuns} runs";
            LogManager.GetCurrentClassLogger().Info(s);
            StopWatch(stopWatch);
        }

        public static (ContextSensitiveGrammar bestGrammar, double bestValue) LearnGrammarFromDataUpToLengthN(
            string[] data, Vocabulary universalVocabulary, int n, int minWordsInSentence, bool isCFGGrammar, ContextSensitiveGrammar initialGrammar)
        {
            IEnumerable<Rule> rules = null;
            //1. transform initial grammar to rule list
            if (initialGrammar != null)
                rules = ContextFreeGrammar.ExtractRules(initialGrammar);
            
            //2. prepare new rule space
            var learner = PrepareLearningUpToSentenceLengthN(data, universalVocabulary, minWordsInSentence, n, out var objectiveFunction);

            //3. re-place rule list inside new rule space (the coordinates of the old rules need not be the same
            //coordinates in the new rule space, for example in the case when the number of nonterminals have changed).
            if (initialGrammar != null)
                initialGrammar = new ContextSensitiveGrammar(rules.ToList());


            SimulatedAnnealingParameters parameters = new SimulatedAnnealingParameters()
            {
                CoolingFactor = 0.999,
                InitialTemperature = 10,
                NumberOfIterations = 2000
            };

            //run
            var algorithm = new SimulatedAnnealing(learner, parameters, objectiveFunction);
            var (bestHypothesis, bestValue) = algorithm.Run(isCFGGrammar, initialGrammar);
            return (bestHypothesis, bestValue);
        }

        private static Learner PrepareLearningUpToSentenceLengthN(string[] data, Vocabulary universalVocabulary, int minWords, int maxWords,
            out IObjectiveFunction objectiveFunction)
        {
            //1. get sentences up to length n and the relevant POS categories in them.
            (var sentences, var posInText) =
                GrammarFileReader.GetDataAndVocabularyForSentencesUpToLengthN(data, universalVocabulary, minWords, maxWords);

            //2. prepare the rule space
            PrepareRuleSpace(universalVocabulary, posInText, sentences);

            //3. prepare the learner
            var learner = new Learner(sentences, minWords, maxWords, posInText, universalVocabulary);
            objectiveFunction = new GrammarFitnessObjectiveFunction(learner);
            return learner;
        }

        private static void PrepareRuleSpace(Vocabulary universalVocabulary, HashSet<string> posInText, string[] sentences)
        {
            //int numberOfAllowedNonTerminals = posInText.Count + 1;
            int numberOfAllowedNonTerminals = 6;

            var bigrams = ContextFreeGrammar.GetBigramsOfData(sentences, universalVocabulary);
            ContextSensitiveGrammar.RuleSpace = new RuleSpace(posInText, bigrams, numberOfAllowedNonTerminals);
        }

        public static void LearnGrammarFromFile(string fileName, bool isCFG)
        {
            int minWordsInSentence = 4;
            int maxWordsInSentence = 4;
            (var sentences, var voc) = FilterDataAccordingToTargetGrammar("CorwinIdealGrammar.txt", "CorwinBooks.txt", minWordsInSentence, maxWordsInSentence);
            (ContextSensitiveGrammar bestGrammar, double bestValue) = LearnGrammarFromData(sentences.ToArray(), voc, isCFG);
            var s = $"Best Hypothesis:\r\n{bestGrammar} \r\n with probability {bestValue}";
            LogManager.GetCurrentClassLogger().Info(s);
        }
        public static (ContextSensitiveGrammar bestGrammar, double bestValue) LearnGrammarFromData(string[] data, Vocabulary universalVocabulary, bool isCFGGrammar)
        {
            var initialWordLength = 6;
            var currentWordLength = initialWordLength;
            int maxSentenceLength = data.Max(x => x.Split().Length);
            int minWordsInSentences = data.Min(x => x.Split().Length);

            ContextSensitiveGrammar[] initialGrammars = new ContextSensitiveGrammar[maxSentenceLength+1];

            ContextSensitiveGrammar currentGrammar = null;
            Queue<ContextSensitiveGrammar> successfulGrammars = new Queue<ContextSensitiveGrammar>();

            double currentValue = 0;
            while (currentWordLength <= maxSentenceLength)
            {
                LogManager.GetCurrentClassLogger().Info($"learning word length  {currentWordLength}");
                (currentGrammar, currentValue) = LearnGrammarFromDataUpToLengthN(data, universalVocabulary, currentWordLength, minWordsInSentences, isCFGGrammar, initialGrammars[currentWordLength]);
                //LogManager.GetCurrentClassLogger().Info($"End of learning word Length { currentWordLength}, \r\n Current Grammar {currentGrammar} \r\n CurrentValue { currentValue}");
               
                /*
                //what if you did not converge on a grammar that generates the data precisely?
                if (currentValue < 1)
                {
                    //backtrack to initial word length and retry?
                    if (successfulGrammars.Any())
                    {
                        LogManager.GetCurrentClassLogger().Info("retrying from previous successful grammar");
                        currentGrammar = successfulGrammars.Dequeue();
                        LogManager.GetCurrentClassLogger().Info($"the initial grammar is: {currentGrammar}");
                        currentWordLength--;

                    }
                    else
                    {
                        LogManager.GetCurrentClassLogger().Info("retrying from scratch");
                        currentWordLength = initialWordLength - 1;
                    }
                }
                else
                    successfulGrammars.Enqueue(currentGrammar);
                */
                currentWordLength++;
                if (currentWordLength <= maxSentenceLength)
                    initialGrammars[currentWordLength] = currentGrammar;
            }

            return (currentGrammar, currentValue);
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