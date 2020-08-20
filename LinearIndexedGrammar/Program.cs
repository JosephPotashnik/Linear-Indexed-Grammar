using LinearIndexedGrammarLearner;
using LinearIndexedGrammarParser;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NLog;
using NLog.Config;
using NLog.Targets;
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
        [JsonProperty] public ProgramParams[] ProgramsToRun { get; set; }
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum DistributionType
    {
        Uniform = 0,
        Normal,
        PowerLaw
    }
    [JsonObject(MemberSerialization.OptIn)]
    public class ProgramParams
    {
        [JsonProperty] public int NumberOfRuns { get; set; }
        [JsonProperty] public string GrammarFileName { get; set; }
        [JsonProperty] public string VocabularyFileName { get; set; }
        [JsonProperty] public string DataFileName { get; set; }
        [JsonProperty] public bool IsCFG { get; set; }
        [JsonProperty] public DistributionType DistributionType { get; set; }
        [JsonProperty] public float CoolingFactor { get; set; }
    }


    public class Program
    {
        private static int maxNonTerminals = 6;
        private static void Learn(string fileName, int _maxNonTerminals = 6)
        {
            maxNonTerminals = _maxNonTerminals;
            var programParamsList = ReadProgramParamsFromFile(fileName);
            foreach (var programParams in programParamsList.ProgramsToRun)
                RunProgram(programParams);
        }

        private static void Main(string[] args)
        {
            var maxNonTerminals = 6;
            string fileName = null;
            for (int i = 0; i < args.Length / 2; i++)
            {
                switch (args[i * 2])
                {
                    case @"FileName:":
                        {
                            fileName = args[i * 2 + 1];
                            break;
                        }
                    case @"MaxNonTerminals:":
                        {
                            maxNonTerminals = Int16.Parse(args[i * 2 + 1]);
                            break;
                        }
                    default:
                        throw new Exception("unrecognized argument. Please use the following format: NightRun: True/False MaxNonTerminals: some integer (try 6)");
                }
            }

            ConfigureLogger();
            Learn(fileName, maxNonTerminals);
        }

        private static void ConfigureLogger()
        {
            var config = new LoggingConfiguration();

            var logfile = new FileTarget("logfile") { FileName = "SessionReport.txt" };
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
            var sentences =
                GrammarFileReader.GetSentencesInWordLengthRange(sentencesInVocabulary, minWords,
                    maxWords);

            var learner = new Learner(sentences, maxWords, minWords, universalVocabulary);

            //4. leave only sentences that can be parsed according to an ideal, oracular grammar that is supposed to parse the input
            //if no oracle is supplied, try to learn the entire input. 
            var filteredSen = new List<string[]>();

            var allParses = learner.ParseAllSentences(cfGrammar);
            var parsableData = allParses.Where(x => x.BracketedTreeRepresentations.Count > 0);

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

            PrepareLearningUpToSentenceLengthN(data, universalVocabulary, minWordsInSentences, maxWordsInSentence, out var objectiveFunction);
            var partOfSpeechCategories = universalVocabulary.POSWithPossibleWords.Keys.ToHashSet();
            ContextFreeGrammar.RenameVariables(grammarRules, partOfSpeechCategories);
            var targetGrammar = new ContextSensitiveGrammar(grammarRules);


            objectiveFunction.GetLearner().ParseAllSentencesFromScratch(targetGrammar);
            (var targetProb, var feasible) = objectiveFunction.Compute(targetGrammar);


            //trying to learn data from incomplete source leads to p < 1
            //so set the maximum value to the target probability, which is the maximal support
            //given to the grammar from the data..


            objectiveFunction.SetMaximalValue(targetProb);

            var s =
                $"Target Hypothesis:\r\n{targetGrammar}\r\n. Verifying probability of target grammar given the data: {targetProb} \r\n";
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
            int maxWordsInSentence = 11;
            var universalVocabulary = Vocabulary.ReadVocabularyFromFile(programParams.VocabularyFileName);
            ContextFreeGrammar.PartsOfSpeech = universalVocabulary.POSWithPossibleWords.Keys
                .Select(x => new SyntacticCategory(x)).ToHashSet();
            var grammarRules = GrammarFileReader.ReadRulesFromFile(programParams.GrammarFileName);

            string[][] data;
            Vocabulary dataVocabulary;

            if (programParams.DataFileName == null)
            {
                (data, dataVocabulary) =
                    SampleGenerator.PrepareDataFromTargetGrammar(grammarRules, universalVocabulary, maxWordsInSentence, programParams.DistributionType);
            }
            else
            {
                //leave only sentences in range [minWordsInSentence,maxWordsInSentence]
                var minWordsInSentence = 1;
                var sentences = FilterDataAccordingToTargetGrammar(grammarRules, programParams.DataFileName,
                    minWordsInSentence, maxWordsInSentence, universalVocabulary);
                (data, dataVocabulary) = (sentences, universalVocabulary);
            }

            if (!ValidateTargetGrammar(grammarRules, data, universalVocabulary))
                return;

            var s = "------------------------------------------------------------\r\n" +
                    $"Session {DateTime.Now:MM/dd/yyyy h:mm tt}\r\n" +
                    $"runs: {programParams.NumberOfRuns}, grammar file name: {programParams.GrammarFileName}, vocabulary file name: {programParams.VocabularyFileName}";
            if (programParams.DataFileName == null)
                s += $", Distribution: {programParams.DistributionType}";
            s += "\r\n";
            LogManager.GetCurrentClassLogger().Info(s);

            var stopWatch = StartWatch();

            var probs = new List<double>();
            for (var i = 0; i < programParams.NumberOfRuns; i++)
            {
                LogManager.GetCurrentClassLogger().Info($"Run {i + 1}:");

                var (bestHypothesis, bestValue) = LearnGrammarFromData(data, dataVocabulary, programParams);
                probs.Add(bestValue);

                s = $"Best Hypothesis:\r\n{bestHypothesis} \r\n with probability {bestValue}";
                LogManager.GetCurrentClassLogger().Info(s);

                Statistics(bestHypothesis, grammarRules, universalVocabulary, maxWordsInSentence);
                
            }

            var numTimesAchieveProb1 = probs.Count(x => Math.Abs(x - 1) < 0.00001);
            var averageProb = probs.Average();
            s = $"Average probability is: {averageProb}\r\n" +
                $"Achieved Probability=1 in {numTimesAchieveProb1} times out of {programParams.NumberOfRuns} runs";
            LogManager.GetCurrentClassLogger().Info(s);
            StopWatch(stopWatch);
        }

        private static void Statistics(ContextSensitiveGrammar bestHypothesis, List<Rule> grammarRules, Vocabulary universalVocabulary, int maxWords)
        {
            //get all distinct sentences of target grammar:
            var targetGrammar = new ContextFreeGrammar(grammarRules);
            var targetSentences  = GetAllNonTerminalSentencesOfGrammar(targetGrammar, universalVocabulary, maxWords).Distinct().ToArray();

            //get all distinct sentences of best hypothesis
            var learnedGrammar = new ContextFreeGrammar(bestHypothesis);
            var learnedSentences = GetAllNonTerminalSentencesOfGrammar(learnedGrammar, universalVocabulary, maxWords).Distinct().ToArray();

            //note : if we remove distinctness, then precision counts repetitions of sentences, thus ambiguous grammars
            //may receive lower precision score. But since they generate the same set of sentences, it is not a-priori
            //clear whether they should be punished for it (weakly equivalent).
            //I choose here not to introduce bias for unambiguous grammars - the target grammar may itself be ambiguous.

            var truePositives = targetSentences.Intersect(learnedSentences).Count();

            var precision = truePositives / (double)(learnedSentences.Count());
            var recall = truePositives / (double)(targetSentences.Count());
            var f1_score = 2 * precision * recall / (precision + recall);
            var s = $"Precision: {precision} Recall: {recall} F1-Score: {f1_score}";
            LogManager.GetCurrentClassLogger().Info(s);
        }

        static private string[] GetAllNonTerminalSentencesOfGrammar(ContextFreeGrammar g, Vocabulary universalVocabulary, int maxWords)
        {
            var pos = universalVocabulary.POSWithPossibleWords.Keys.ToHashSet();
            var generator = new EarleyGenerator(g, universalVocabulary);
            var statesList = generator.GenerateSentence(null, maxWords);
            var nonterminalSentences = new string[statesList.Count];
            for (int i = 0; i < statesList.Count; i++)
                nonterminalSentences[i] = statesList[i].GetNonTerminalStringUnderNode(pos);

            return nonterminalSentences;
        }

        public static (ContextSensitiveGrammar bestGrammar, double bestValue) LearnGrammarFromDataUpToLengthN(
            string[][] data, Vocabulary dataVocabulary, int n, int minWordsInSentence, ProgramParams progParams,
            ContextSensitiveGrammar initialGrammar)
        {
            IEnumerable<Rule> rules = null;
            //1. transform initial grammar to rule list
            if (initialGrammar != null)
                rules = ContextFreeGrammar.ExtractRules(initialGrammar);

            //2. prepare new rule space
            var learner = PrepareLearningUpToSentenceLengthN(data, dataVocabulary, minWordsInSentence, n, out var objectiveFunction);

            //3. re-place rule list inside new rule space (the coordinates of the old rules need not be the same
            //coordinates in the new rule space, for example in the case when the number of nonterminals have changed).
            if (initialGrammar != null)
                initialGrammar = new ContextSensitiveGrammar(rules.ToList());


            var parameters = new SimulatedAnnealingParameters
            {
                CoolingFactor = progParams.CoolingFactor,
                InitialTemperature = 10,
                NumberOfIterations = 1400
            };

            //run
            var algorithm = new SimulatedAnnealing(learner, parameters, objectiveFunction);
            var (bestHypothesis, bestValue) = algorithm.Run(progParams.IsCFG, initialGrammar);
            return (bestHypothesis, bestValue);
        }

        private static Learner PrepareLearningUpToSentenceLengthN(string[][] data, Vocabulary dataVocabulary,
            int minWords, int maxWords, out GrammarFitnessObjectiveFunction objectiveFunction)
        {
            //1. get sentences up to length n and the relevant POS categories in them.
            var sentences =
                GrammarFileReader.GetSentencesInWordLengthRange(data, minWords, maxWords);

            //2. prepare the rule space
            PrepareRuleSpace(dataVocabulary, sentences);

            //3. prepare the learner
            var learner = new Learner(sentences, minWords, maxWords, dataVocabulary);
            double noiseTolerance = 0.0;
            objectiveFunction = new GrammarFitnessObjectiveFunction(learner, noiseTolerance);
            return learner;
        }

        private static void PrepareRuleSpace(Vocabulary dataVocabulary, string[][] sentences)
        {
            //int numberOfAllowedNonTerminals = posInText.Count + 1;
            var numberOfAllowedNonTerminals = Program.maxNonTerminals;
            var posInText = dataVocabulary.POSWithPossibleWords.Keys.ToHashSet();
            var bigrams = ContextFreeGrammar.GetBigramsOfData(sentences, dataVocabulary);
            ContextSensitiveGrammar.RuleSpace = new RuleSpace(posInText, bigrams, numberOfAllowedNonTerminals);
        }


        public static (ContextSensitiveGrammar bestGrammar, double bestValue) LearnGrammarFromData(string[][] data,
            Vocabulary dataVocabulary, ProgramParams progParams)
        {
            // initialWordLength is the sentence length from which you would like to start learning
            //it does not have to be the length of the shortest sentences
            //for instance, you have sentences in range [1,12] words, and you start learning from initial word length 6,
            //i.e, all sentences [1,6], then [1,7],... up to [1,12].
            var initialWordLength = 6;
            var currentWordLength = initialWordLength;
            var maxSentenceLength = data.Max(x => x.Length);
            var minWordsInSentences = data.Min(x => x.Length);

            LogManager.GetCurrentClassLogger().Info($"Data samples:");
            for (int i = minWordsInSentences; i < maxSentenceLength + 1; i++)
            {
                int count = data.Where(x => x.Length == i).Count();
                if (count > 0)
                    LogManager.GetCurrentClassLogger().Info($"{count} sentences of length {i}");
            }
            var initialGrammars = new ContextSensitiveGrammar[maxSentenceLength + 1];

            ContextSensitiveGrammar currentGrammar = null;
            var successfulGrammars = new Queue<ContextSensitiveGrammar>();

            double currentValue = 0;
            while (currentWordLength <= maxSentenceLength)
            {
                LogManager.GetCurrentClassLogger().Info($"learning word length  {currentWordLength}");

                (currentGrammar, currentValue) = LearnGrammarFromDataUpToLengthN(data, dataVocabulary,
                    currentWordLength, minWordsInSentences, progParams, initialGrammars[currentWordLength]);
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
                programParamsList = (ProgramParamsList)serializer.Deserialize(file, typeof(ProgramParamsList));
            }

            return programParamsList;
        }
    }
}