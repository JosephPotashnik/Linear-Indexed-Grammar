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
        [JsonProperty] public string DataFileName { get; set; }
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

        private static void Main(string[] args)
        {
            bool nightRun = false;
            if (args.Length > 0)
            {
                if (args[0] != "-NightRun" || args.Length > 1)
                    throw new Exception("the single (optional) argument expected is \"-NightRun\"");
                nightRun = true;
            }

            ConfigureLogger();
            Learn(nightRun);
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

        private static (string[][] data, Vocabulary dataVocabulary) PrepareDataFromTargetGrammar(
            List<Rule> grammarRules, Vocabulary universalVocabulary, int maxWords)
        {
            var numberOfSentencesPerTree = 10;
            var pos = universalVocabulary.POSWithPossibleWords.Keys.ToHashSet();

            var cfGrammar = new ContextFreeGrammar(grammarRules);
            var generator = new EarleyGenerator(cfGrammar, universalVocabulary);
            var statesList = generator.ParseSentence(null, new CancellationTokenSource(), maxWords);
            return GrammarFileReader.GetSentencesOfGenerator(statesList, universalVocabulary, numberOfSentencesPerTree,
                pos);
        }

        private static IEnumerable<string> GetSentenceFromDataFile(string dataFileName)
        {
            //read sentences. (unused delimiters for sentence level: ' ' and '-')
            var sentenceDelimiters = new[]
                {'(', ')', '?', ',', '*', '.', ';', '!', '\\', '/', ':', '"', '“', '—', '”'};
            var filestring = File.ReadAllText(dataFileName);
            var sentences1 = filestring.Split(sentenceDelimiters, StringSplitOptions.RemoveEmptyEntries);
            //discard empty spaces, lowercase. I will not be concerned with capitalization for now.
            var sentences = sentences1.Select(sentence => sentence.TrimStart()).Select(sentence => sentence.ToLower());
            sentences = sentences.Select(sentence => sentence.Replace('\r', ' '));
            sentences = sentences.Select(sentence => sentence.Replace('\n', ' '));
            return sentences;
        }

        private static string[][] FilterDataAccordingToTargetGrammar(List<Rule> grammarRules, string dataFileName,
            int minWords, int maxWords, Vocabulary universalVocabulary)
        {
            var cfGrammar = new ContextFreeGrammar(grammarRules);

            //1. get sentences from file
            var allData = GetSentenceFromDataFile(dataFileName);

            //2. leave only sentences with words recognized by the universal vocabulary.
            var sentencesInVocabulary = universalVocabulary.LeaveOnlySentencesWithWordsInVocabulary(allData).ToArray();

            //3. leave only sentences in word length range from minWords to maxWords in a sentence.
            var (sentences, posInText) =
                GrammarFileReader.GetSentencesInWordLengthRange(sentencesInVocabulary, universalVocabulary, minWords,
                    maxWords);

            var learner = new Learner(sentences, maxWords, minWords, posInText, universalVocabulary);

            //4. leave only sentences that can be parsed according to an ideal, oracular grammar that is supposed to parse the input
            //if no oracle is supplied, try to learn the entire input. 
            var filteredSen = new List<string[]>();

            var allParses = learner.ParseAllSentences(cfGrammar);
            var parsableData = allParses.Where(x => x.GammaStates.Count > 0);

            foreach (var sen in parsableData)
                for (var i = 0; i < sen.Count; i++)
                    filteredSen.Add(sen.Sentence);

            return filteredSen.ToArray();
        }

        private static bool ValidateTargetGrammar(List<Rule> grammarRules, string[][] data,
            Vocabulary universalVocabulary)
        {
            var maxWordsInSentence = data.Max(x => x.Length);
            var minWordsInSentences = data.Min(x => x.Length);

            PrepareLearningUpToSentenceLengthN(data, universalVocabulary, minWordsInSentences, maxWordsInSentence,
                out var objectiveFunction);
            var partOfSpeechCategories = universalVocabulary.POSWithPossibleWords.Keys.ToHashSet();
            ContextFreeGrammar.RenameVariables(grammarRules, partOfSpeechCategories);
            var targetGrammar = new ContextSensitiveGrammar(grammarRules);

            Learner.ParsingTimeOut = int.MaxValue;

            var stopWatch = new Stopwatch();
            stopWatch.Start();
            objectiveFunction.GetLearner().ParseAllSentencesFromScratch(targetGrammar);

            var targetProb = objectiveFunction.Compute(targetGrammar);
            stopWatch.Stop();
            var ts = stopWatch.Elapsed;
            var parsingTimeout = Math.Max((ts.Seconds * 1000 + ts.Milliseconds) * 3, Learner.InitialTimeOut);
            Learner.ParsingTimeOut = int.MaxValue;

            //trying to learn data from incomplete source leads to p < 1
            //so set the maximum value to the target probability, which is the maximal support
            //given to the grammar from the data..
            objectiveFunction.SetMaximalValue(targetProb);

            var s =
                $"Target Hypothesis:\r\n{targetGrammar}\r\n. Verifying probability of target grammar given the data: {targetProb} \r\n parsing timeout is {Learner.ParsingTimeOut}: \r\n";
            LogManager.GetCurrentClassLogger().Info(s);
            if (!objectiveFunction.IsMaximalValue(targetProb))
            {
                LogManager.GetCurrentClassLogger().Fatal("probablity incorrect. exit!");
                return false;
            }

            return true;
        }

        private static void RunProgram(ProgramParams programParams)
        {
            var universalVocabulary = Vocabulary.ReadVocabularyFromFile(programParams.VocabularyFileName);
            ContextFreeGrammar.PartsOfSpeech = universalVocabulary.POSWithPossibleWords.Keys
                .Select(x => new SyntacticCategory(x)).ToHashSet();
            var grammarRules = GrammarFileReader.ReadRulesFromFile(programParams.GrammarFileName);

            string[][] data;
            Vocabulary dataVocabulary;

            if (programParams.DataFileName == null)
            {
                var maxWordsInSentence = 12;
                (data, dataVocabulary) =
                    PrepareDataFromTargetGrammar(grammarRules, universalVocabulary, maxWordsInSentence);
            }
            else
            {
                //leave only sentences in range [minWordsInSentence,maxWordsInSentence]
                var minWordsInSentence = 1;
                var maxWordsInSentence = 6;
                var sentences = FilterDataAccordingToTargetGrammar(grammarRules, programParams.DataFileName,
                    minWordsInSentence, maxWordsInSentence, universalVocabulary);
                (data, dataVocabulary) = (sentences, universalVocabulary);
            }

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
            string[][] data, Vocabulary universalVocabulary, int n, int minWordsInSentence, bool isCFGGrammar,
            ContextSensitiveGrammar initialGrammar)
        {
            IEnumerable<Rule> rules = null;
            //1. transform initial grammar to rule list
            if (initialGrammar != null)
                rules = ContextFreeGrammar.ExtractRules(initialGrammar);

            //2. prepare new rule space
            var learner = PrepareLearningUpToSentenceLengthN(data, universalVocabulary, minWordsInSentence, n,
                out var objectiveFunction);

            //3. re-place rule list inside new rule space (the coordinates of the old rules need not be the same
            //coordinates in the new rule space, for example in the case when the number of nonterminals have changed).
            if (initialGrammar != null)
                initialGrammar = new ContextSensitiveGrammar(rules.ToList());


            var parameters = new SimulatedAnnealingParameters
            {
                CoolingFactor = 0.999,
                InitialTemperature = 10,
                NumberOfIterations = 400
            };

            //run
            var algorithm = new SimulatedAnnealing(learner, parameters, objectiveFunction);
            var (bestHypothesis, bestValue) = algorithm.Run(isCFGGrammar, initialGrammar);
            return (bestHypothesis, bestValue);
        }

        private static Learner PrepareLearningUpToSentenceLengthN(string[][] data, Vocabulary universalVocabulary,
            int minWords, int maxWords,
            out IObjectiveFunction objectiveFunction)
        {
            //1. get sentences up to length n and the relevant POS categories in them.
            var (sentences, posInText) =
                GrammarFileReader.GetSentencesInWordLengthRange(data, universalVocabulary, minWords, maxWords);

            //2. prepare the rule space
            PrepareRuleSpace(universalVocabulary, posInText, sentences);

            //3. prepare the learner
            var learner = new Learner(sentences, minWords, maxWords, posInText, universalVocabulary);
            objectiveFunction = new GrammarFitnessObjectiveFunction(learner);
            return learner;
        }

        private static void PrepareRuleSpace(Vocabulary universalVocabulary, HashSet<string> posInText,
            string[][] sentences)
        {
            //int numberOfAllowedNonTerminals = posInText.Count + 1;
            var numberOfAllowedNonTerminals = 6;

            var bigrams = ContextFreeGrammar.GetBigramsOfData(sentences, universalVocabulary);
            ContextSensitiveGrammar.RuleSpace = new RuleSpace(posInText, bigrams, numberOfAllowedNonTerminals);
        }


        public static (ContextSensitiveGrammar bestGrammar, double bestValue) LearnGrammarFromData(string[][] data,
            Vocabulary universalVocabulary, bool isCFGGrammar)
        {
            // initialWordLength is the sentence length from which you would like to start learning
            //it does not have to be the length of the shortest sentences
            //for instance, you have sentences in range [1,12] words, and you start learning from initial word length 6,
            //i.e, all sentences [1,6], then [1,7],... up to [1,12].
            var initialWordLength = 6;
            var currentWordLength = initialWordLength;
            var maxSentenceLength = data.Max(x => x.Length);
            var minWordsInSentences = data.Min(x => x.Length);

            var initialGrammars = new ContextSensitiveGrammar[maxSentenceLength + 1];

            ContextSensitiveGrammar currentGrammar = null;
            var successfulGrammars = new Queue<ContextSensitiveGrammar>();

            double currentValue = 0;
            while (currentWordLength <= maxSentenceLength)
            {
                LogManager.GetCurrentClassLogger().Info($"learning word length  {currentWordLength}");

                (currentGrammar, currentValue) = LearnGrammarFromDataUpToLengthN(data, universalVocabulary,
                    currentWordLength, minWordsInSentences, isCFGGrammar, initialGrammars[currentWordLength]);
                //SEFI
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