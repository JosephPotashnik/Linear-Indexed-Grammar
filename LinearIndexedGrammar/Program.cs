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
using System.Threading.Tasks;
using NLog.LayoutRenderers.Wrappers;

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
    public class InputParams
    {
        [JsonProperty] public string GrammarFileName { get; set; }
        [JsonProperty] public string VocabularyFileName { get; set; }
        [JsonProperty] public string DataFileName { get; set; }
        [JsonProperty] public bool IsCFG { get; set; }
        [JsonProperty] public DistributionType DistributionType { get; set; }
        [JsonProperty] public int MaxSentenceLength { get; set; }

    }
    [JsonObject(MemberSerialization.OptIn)]
    public class SimulatedAnnealingParams
    {
        [JsonProperty] public int NumberOfNonImprovingIterationsBeforeRestart { get; set; }
        [JsonProperty] public int NumberOfRestarts { get; set; }
        [JsonProperty] public double InitialTemperature { get; set; }
        [JsonProperty] public double CoolingFactor { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class SearchSpaceParams
    {
        [JsonProperty] public int NumberOfRuns { get; set; }
        [JsonProperty] public int MinNumberOfNonTerminals { get; set; }
        [JsonProperty] public int MaxNumberOfNonTerminals { get; set; }
        [JsonProperty] public double MinNoise { get; set; }
        [JsonProperty] public double MaxNoise { get; set; }
        [JsonProperty] public double NoiseStepSize { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class ProgramParams
    {
        [JsonProperty] public InputParams InputParams { get; set; }
        [JsonProperty] public SearchSpaceParams SearchSpaceParams { get; set; }
        [JsonProperty] public SimulatedAnnealingParams SimulatedAnnealingParams { get; set; }
    }


    public class Program
    {
        private static void Learn(string fileName)
        {
            var programParamsList = ReadProgramParamsFromFile(fileName);
            foreach (var programParams in programParamsList.ProgramsToRun)
                RunProgram(programParams);
        }


        private static List<string> ReadChildesCSVFile(string filename)
        {
            var sentences = new List<string>();
            string line;
            using var file = File.OpenText(filename);
            while ((line = file.ReadLine()) != null)
            {
                var row = line.Split(',');
                sentences.Add(row[0]); //row[0] = sentence, row[1] = POS sequence.

            }

            return sentences;
        }



        private static void Main(string[] args)
        {
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
                    default:
                        throw new Exception("unrecognized argument. Please use the following format: NightRun: True/False MaxNonTerminals: some integer (try 6)");
                }
            }

            ConfigureLogger();
            Process p = Process.GetCurrentProcess();
            p.PriorityClass = ProcessPriorityClass.High;
            Learn(fileName);
        }

        
        //note - this method should be not be normally called. It is used only to generate the vocabulary file once.
        //it can be called when wishing to re-generate the json file from scratch. see comments below.
/*
        private static void ReadChildesVocabulary()
        {
            var vocab = ReadChildesCSVFile("childesVocabulary.csv");
            Vocabulary v = new Vocabulary();
            for (int i = 0; i < vocab.Count(); i++)
            {
                var pos = vocab[i][1];
                var word = vocab[i][0];
                if (pos.Length > 0)
                    v.AddWordsToPOSCategory(pos, new[] { word });
            }

            JsonSerializer serializer = new JsonSerializer();
            serializer.NullValueHandling = NullValueHandling.Ignore;
            serializer.Formatting = Formatting.Indented;

            using (StreamWriter sw = new StreamWriter(@"ChildesVocabulary.json"))
            using (JsonWriter writer = new JsonTextWriter(sw))
            {
                serializer.Serialize(writer, v);
            }
            //in the vocabulary read of the file , some parts-of-speech are either a mistake in transcribing or very peripherial
            //etc,    "wplay": [    "sarbaby", "kitto" .. ] 
            //    "meta": [      "take",      "spider",     "cranberry",      "no" .. ]
            //    "bab": [      "na",      "ba",      "wowheel".. ]
            //    "n:pt": [      "pliers",      "pants",      "clothes",      "measles" ] 
            // "n:adj": [      "franks"    ],
            // "v pro:per": [      "do you"    ],
            // "adv:int mod": [      "why don't"    ],
            //"part prep": [      "trying to"    ],
            // "part inf": [      "trying to"    ],
            // "coord mod": [      "and do"    ],
            // "mod v": [      "don't know"    ],
            //"neo": [      "turn_arounder",      "crayoned"       ]

            //I manually removed such part-of-speech groups from the ChildesVocabulary.json .file
        }
*/

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

            //1. get sentences from file
            var allData = ReadChildesCSVFile("childes.csv");

            //2. leave only sentences with words recognized by the universal vocabulary.
            var sentencesInVocabulary = universalVocabulary.LeaveOnlySentencesWithWordsInVocabulary(allData).ToArray();

            //3. leave only sentences in word length range from minWords to maxWords in a sentence.
            var sentences =
                GrammarFileReader.GetSentencesInWordLengthRange(sentencesInVocabulary, minWords,
                    maxWords);

            //for (int i = 0; i < 10; i++)
            //{
            //    Console.WriteLine(string.Join(" ", sentences[i]));
            //    string s = "";
            //    foreach (var word in sentences[i])
            //        s += string.Join(" ", universalVocabulary.WordWithPossiblePOS[word]) + " , ";
            //    Console.WriteLine(s);

            //}
            //var dic = new Dictionary<string, int>();
            //for (int i = 0; i < sentences.Length; i++)
            //{
            //    for (int j = 0; j < sentences[i].Length; j++)
            //    {
            //        if (dic.ContainsKey(sentences[i][j]))
            //            dic[sentences[i][j]]++;
            //        else
            //            dic[sentences[i][j]] = 1;
            //    }
            //}

            //var reverseList = dic.Select(x => (x.Value, x.Key)).ToList();
            //reverseList.Sort( (x, y) =>
            //{
            //    if (x.Value > y.Value) return -1;
            //    if (x.Value < y.Value) return 1;
            //    return 0;
            //});

            var res = sentences;
            //4. leave only sentences that can be parsed according to an ideal, oracular grammar that is supposed to parse the input
            //if no oracle is supplied, try to learn the entire input. 
            if (grammarRules != null)
            {
                var learner = new Learner(sentences, maxWords, minWords, universalVocabulary);

                var filteredSen = new List<string[]>();

                learner.ParseAllSentencesFromScratch(new ContextSensitiveGrammar(grammarRules));

                List<SentenceParsingResults> parsableData = new List<SentenceParsingResults>();
                //does not work - adapt .

                //for (int i = 0; i < learner.SentencesParser.Length; i++)
                //{
                //    if (learner.SentencesParser[i].BracketedRepresentations.Count > 0)
                //        parsableData.Add(learner.Parses[i]);

                //}

                foreach (var sen in parsableData)
                    for (var i = 0; i < sen.Count; i++)
                        filteredSen.Add(sen.Sentence);

                res = filteredSen.ToArray();
            }

            return res;
        }

        private static List<string[]> POSSequencesOfSentences(Span<string> sentence, Vocabulary voc)
        {
            if (sentence.Length == 0)
                return new List<string[]> { new string[] { } };

            var l = new List<string[]>();

            var firstWord = sentence[0];
            var poses = voc.WordWithPossiblePOS[firstWord];
            var restOfSentencePOSSequences = POSSequencesOfSentences(sentence.Slice(1), voc);

            foreach (var pos in poses)
            {
                foreach (var sequence in restOfSentencePOSSequences)
                {
                    var posSequences = new string[sentence.Length];
                    posSequences[0] = pos;
                    if (sequence.Length > 0)
                        sequence.CopyTo(posSequences, 1);
                    l.Add(posSequences);
                }
            }
            return l;
        }


        private static void RunProgram(ProgramParams programParams)
        {
            var s = "------------------------------------------------------------\r\n" +
                    $"Session {DateTime.Now:MM/dd/yyyy h:mm tt}\r\n" +
                    $"runs: {programParams.SearchSpaceParams.NumberOfRuns}, grammar file name: {programParams.InputParams.GrammarFileName}, vocabulary file name: {programParams.InputParams.VocabularyFileName}";
            if (programParams.InputParams.DataFileName == null)
                s += $", Distribution: {programParams.InputParams.DistributionType}";
            LogManager.GetCurrentClassLogger().Info(s);


            int maxWordsInSentence = programParams.InputParams.MaxSentenceLength;
            var universalVocabulary = Vocabulary.ReadVocabularyFromFile(programParams.InputParams.VocabularyFileName);
            ContextFreeGrammar.PartsOfSpeech = universalVocabulary.POSWithPossibleWords.Keys
                .Select(x => new SyntacticCategory(x)).ToHashSet();


            string[][] data;
            Vocabulary dataVocabulary;
            List<Rule> grammarRules = null;

            if (programParams.InputParams.DataFileName == null)
            {
                grammarRules = GrammarFileReader.ReadRulesFromFile(programParams.InputParams.GrammarFileName);
                (data, dataVocabulary) =
                    SampleGenerator.PrepareDataFromTargetGrammar(grammarRules, universalVocabulary, maxWordsInSentence, programParams.InputParams.DistributionType);
                LogManager.GetCurrentClassLogger().Info($"POS contained in data: {string.Join(" ", dataVocabulary.POSWithPossibleWords.Keys)}");
            }
            else
            {

                //leave only sentences in range[minWordsInSentence, maxWordsInSentence]
                var minWordsInSentence = 3;
                var sentences = FilterDataAccordingToTargetGrammar(null, programParams.InputParams.DataFileName,
                    minWordsInSentence, maxWordsInSentence, universalVocabulary);
                (data, dataVocabulary) = (sentences, universalVocabulary);
            }

            var maxSentenceLength = data.Max(x => x.Length);
            var minWordsInSentences = data.Min(x => x.Length);

            LogManager.GetCurrentClassLogger().Info($"Data samples:");
            for (int p = minWordsInSentences; p < maxSentenceLength + 1; p++)
            {
                int count = data.Where(x => x.Length == p).Count();
                if (count > 0)
                    LogManager.GetCurrentClassLogger().Info($"{count} sentences of length {p}");
            }

            data = ReduceDataToUniquePOSTypes(data, dataVocabulary);

            LogManager.GetCurrentClassLogger().Info($"Unique sentences types (POS sequences) from data samples:");
            for (int p = minWordsInSentences; p < maxSentenceLength + 1; p++)
            {
                int count = data.Where(x => x.Length == p).Count();
                if (count > 0)
                    LogManager.GetCurrentClassLogger().Info($"{count} unique sentences types of length {p}");
            }
            var stopWatch = StartWatch();

            for (var k = 0; k < programParams.SearchSpaceParams.NumberOfRuns; k++)
            {
                LogManager.GetCurrentClassLogger().Info($"Run {k + 1}:");
                int nonTerminalsSpaceSize = programParams.SearchSpaceParams.MaxNumberOfNonTerminals - programParams.SearchSpaceParams.MinNumberOfNonTerminals + 1;
                int noiseToleranceSpaceSize = (int)(Math.Round((programParams.SearchSpaceParams.MaxNoise - programParams.SearchSpaceParams.MinNoise) / programParams.SearchSpaceParams.NoiseStepSize)) + 1;
                var results = new Tuple<ContextSensitiveGrammar, double, bool>[nonTerminalsSpaceSize, noiseToleranceSpaceSize];

                var saParams = new LinearIndexedGrammarLearner.SimulatedAnnealingParams
                {
                    NumberOfNonImprovingIterationsBeforeRestart = programParams.SimulatedAnnealingParams.NumberOfNonImprovingIterationsBeforeRestart,
                    NumberOfRestarts = programParams.SimulatedAnnealingParams.NumberOfRestarts,
                    CoolingFactor = programParams.SimulatedAnnealingParams.CoolingFactor,
                    InitialTemperature = programParams.SimulatedAnnealingParams.InitialTemperature
                };

                // initialWordLength is the sentence length from which you would like to start learning
                //it does not have to be the length of the shortest sentences
                //for instance, you have sentences in range [1,12] words, and you start learning from initial word length 6,
                //i.e, all sentences [1,6], then [1,7],... up to [1,12].


               

                int i = 0;
                const double roundingError = 0.001;
                ContextSensitiveGrammar initialGrammar = null;
                //var injectInitial = false;
                //if (injectInitial)
                //{
                //    PrepareLearningUpToSentenceLengthN(data, universalVocabulary, minWordsInSentences, maxWordsInSentence, 5, 0.0, out var objectiveFunction);
                //    var grammarRules1 = GrammarFileReader.ReadRulesFromFile("InitialGrammar.txt");
                //    var partOfSpeechCategories = universalVocabulary.POSWithPossibleWords.Keys.ToHashSet();
                //    initialGrammar = new ContextSensitiveGrammar(grammarRules1);
                //}

                for (int numberOfNonTerminals = programParams.SearchSpaceParams.MinNumberOfNonTerminals; numberOfNonTerminals <= programParams.SearchSpaceParams.MaxNumberOfNonTerminals; numberOfNonTerminals++)
                {
                    int j = 0;
                    bool continueSearching = false;
                    for (double noiseTolerance = programParams.SearchSpaceParams.MaxNoise; noiseTolerance >= (programParams.SearchSpaceParams.MinNoise - roundingError); noiseTolerance -= programParams.SearchSpaceParams.NoiseStepSize)
                    {
                        s = $"\t\t\t\tHyper-parameters : NumberOfNonTerminals: {numberOfNonTerminals} NoiseTolerance: {noiseTolerance:0.00}\r\n";
                        LogManager.GetCurrentClassLogger().Info(s);


                        if (j > 0 && results[i, j - 1] != null)
                            initialGrammar = results[i, j - 1].Item1;
                        else if (i > 0)
                        {
                            for (int p = noiseToleranceSpaceSize - 1; p >= 0; p--)
                            {
                                if (results[i - 1, p] != null)
                                {
                                    initialGrammar = results[i - 1, p].Item1;
                                    break;
                                }
                            }

                        }

                        var (bestHypothesis, bestValue, feasible) = LearnGrammarFromData(data, dataVocabulary, saParams, programParams.InputParams.IsCFG, numberOfNonTerminals, noiseTolerance, initialGrammar);

                        s = $"Best Hypothesis:\r\n{bestHypothesis} \r\n with objective function value {bestValue:0.000}";
                        LogManager.GetCurrentClassLogger().Info(s);

                        results[i, j] = Tuple.Create(bestHypothesis, bestValue, feasible);
                        j++;


                        var task = Task.Run(() => Statistics(bestHypothesis, grammarRules, universalVocabulary, maxWordsInSentence));
                        TimeSpan ts = TimeSpan.FromSeconds(10);
                        double f1Score = 0;
                        if (!task.Wait(ts))
                            LogManager.GetCurrentClassLogger().Info("Statistics were not computed, hypothesis grammar underfits poorly, generating intractable number of sentences (POS sequences)");
                        else
                            f1Score = task.Result;

                        continueSearching = true;
                        if (f1Score > 0.95)
                        {
                            continueSearching = false;
                            break;
                        }


                    }
                    if (!continueSearching)
                    {
                        LogManager.GetCurrentClassLogger().Info($"f1_score sufficiently high for latest hypothesis.");
                        break;
                    }
                    i++;
                }

            }

            LogManager.GetCurrentClassLogger().Info(s);
            StopWatch(stopWatch);
        }


        private static List<string> FilterUnrecognizedPOS(List<string[]> dataWithPOSTags)
        {
            List<string> filteredData = new List<string>();
            for (int i = 0; i < dataWithPOSTags.Count; i++)
            {
                //do not analyze not (negative) POS at the moment.
                if (dataWithPOSTags[i][0].Contains("n't") || dataWithPOSTags[i][0].Contains("not"))
                    continue;

                var POSSequence = dataWithPOSTags[i][1].Split();
                var sentence = dataWithPOSTags[i][0].Split();

                if (POSSequence.Length != sentence.Length)
                    continue;

                var unrecognizedPOS = false;
                for (int j = 0; j < POSSequence.Length; j++)
                {
                    if (!ContextFreeGrammar.PartsOfSpeech.Contains(new SyntacticCategory(POSSequence[j])))
                    {
                        unrecognizedPOS = true;
                        break;
                    }
                }

                if (!unrecognizedPOS)
                    filteredData.Add(dataWithPOSTags[i][0]);
            }

            return filteredData;
        }

        private static string[][] ReduceDataToUniquePOSTypes(string[][] data, Vocabulary dataVocabulary)
        {
            List<string[]> uniqueData = new List<string[]>();
            //Leave in data only unique sets of sequences of POS
            HashSet<List<string[]>> uniquePOSSequences = new HashSet<List<string[]>>(new ListStringArrayCompare());

            foreach (var sentence in data)
            {
                var posSequence = POSSequencesOfSentences(sentence, dataVocabulary);

                if (!uniquePOSSequences.Contains(posSequence))
                {
                    uniquePOSSequences.Add(posSequence);
                    uniqueData.Add(sentence);
                }
            }

            data = uniqueData.ToArray();
            return data;
        }

        private static double Statistics(ContextSensitiveGrammar bestHypothesis, List<Rule> grammarRules, Vocabulary universalVocabulary, int maxWords)
        {

            //note - in real data you don't have access to target hypothesis grammarRules (i.e. = null).
            if (grammarRules == null)
            {
                LogManager.GetCurrentClassLogger().Info("precision and recall must be computed by hand for actual grammar. Or use test set. For starters look at the quality of the hypothesis");
                return 0;
            }
            //get all distinct sentences of target grammar:
            var targetGrammar = new ContextFreeGrammar(grammarRules);
            var targetSentences = GetAllNonTerminalSentencesOfGrammar(targetGrammar, universalVocabulary, maxWords).Distinct().ToArray();

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
            var f1Score = 2 * precision * recall / (precision + recall);
            var s = $"Precision: {precision:0.0000} Recall: {recall:0.0000} F1-Score: {f1Score:0.0000}";
            LogManager.GetCurrentClassLogger().Info(s);

            return f1Score;
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

        public static (ContextSensitiveGrammar bestGrammar, double bestValue, bool feasible) LearnGrammarFromDataUpToLengthN(
            string[][] data, Vocabulary dataVocabulary, int n, int minWordsInSentence, LinearIndexedGrammarLearner.SimulatedAnnealingParams saParams,
            bool isCFG, int numberOfNonterminals, double noiseTolerance, ContextSensitiveGrammar initialGrammar)
        {
            List<Rule> rules = null;
            //1. transform initial grammar to rule list
            if (initialGrammar != null)
            {
                rules = ContextFreeGrammar.ExtractRules(initialGrammar).Select(x => new Rule(x)).ToList();
                //LogManager.GetCurrentClassLogger().Info($"rules of the initial grammar BEFORE preparing rule space\r\n:  {string.Join(" ", rules)}");

            }

            var learner = PrepareLearningUpToSentenceLengthN(data, dataVocabulary, minWordsInSentence, n, numberOfNonterminals, noiseTolerance, out var objectiveFunction);
            //3. re-place rule list inside new rule space (the coordinates of the old rules need not be the same
            //coordinates in the new rule space, for example in the case when the number of nonterminals have changed).
            if (initialGrammar != null)
            {
                initialGrammar = new ContextSensitiveGrammar(rules);
                //var rules1 = ContextFreeGrammar.ExtractRules(initialGrammar);
                //LogManager.GetCurrentClassLogger().Info($"rules of the initial grammar AFTER preparing rule space\r\n:  {string.Join(" ", rules1)}");
            }

            var algorithm = new SimulatedAnnealing(learner, saParams, objectiveFunction);
            var (bestHypothesis, bestValue, feasible) = algorithm.Run(isCFG, initialGrammar);
            return (bestHypothesis, bestValue, feasible);
        }

        private static Learner PrepareLearningUpToSentenceLengthN(string[][] data, Vocabulary dataVocabulary,
            int minWords, int maxWords, int numberOfNonterminals, double noiseTolerance,out GrammarFitnessObjectiveFunction objectiveFunction)
        {
            //1. get sentences up to length n and the relevant POS categories in them.
            var sentences =
                GrammarFileReader.GetSentencesInWordLengthRange(data, minWords, maxWords);

            //2. prepare the rule space
            PrepareRuleSpace(dataVocabulary, sentences, numberOfNonterminals);

            //3. prepare the learner
            var learner = new Learner(sentences, minWords, maxWords, dataVocabulary);
            objectiveFunction = new GrammarFitnessObjectiveFunction(learner, noiseTolerance);
            return learner;
        }

        private static void PrepareRuleSpace(Vocabulary dataVocabulary, string[][] sentences, int numberOfNonterminals)
        {
            var posInText = dataVocabulary.POSWithPossibleWords.Keys.ToHashSet();
            var bigrams = ContextFreeGrammar.GetBigramsOfData(sentences, dataVocabulary);
            ContextSensitiveGrammar.RuleSpace = new RuleSpace(posInText, bigrams, numberOfNonterminals);
        }


        public static (ContextSensitiveGrammar bestGrammar, double bestValue, bool feasible) LearnGrammarFromData(string[][] data,
            Vocabulary dataVocabulary, LinearIndexedGrammarLearner.SimulatedAnnealingParams saParams, bool isCFG, int numberOfNonterminals, double noiseTolerance, ContextSensitiveGrammar initialGrammar)
        {

                var initialWordLength = 6;
                var currentWordLength = initialWordLength;
                var maxSentenceLength = data.Max(x => x.Length);
                var minWordsInSentences = data.Min(x => x.Length);


            var initialGrammars = new ContextSensitiveGrammar[maxSentenceLength + 1];
                ContextSensitiveGrammar currentGrammar = null;
                initialGrammars[currentWordLength] = initialGrammar;
                double currentValue = 0;
                bool feasible = false;
                while (currentWordLength <= maxSentenceLength)
                {
                    LogManager.GetCurrentClassLogger().Info($"learning from sentences up to word length  {currentWordLength}");

  
                    (currentGrammar, currentValue, feasible) = LearnGrammarFromDataUpToLengthN(data, dataVocabulary,
                        currentWordLength, minWordsInSentences, saParams, isCFG, numberOfNonterminals, noiseTolerance, initialGrammars[currentWordLength]);
                    currentWordLength++;

                    //LogManager.GetCurrentClassLogger().Info($"best grammar so far:\r\n  {currentGrammar}");

                if (currentWordLength <= maxSentenceLength)
                        initialGrammars[currentWordLength] = new ContextSensitiveGrammar(currentGrammar);
                }

            return (currentGrammar, currentValue, feasible);
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