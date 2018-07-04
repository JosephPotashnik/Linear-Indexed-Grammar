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
        public int NumberOfDataSentences { get; set; }

        [JsonProperty]
        public bool DataWithMovement { get; set; }

        [JsonProperty]
        public int NumberOfRuns { get; set; }
    }

    class Program
    {
        static void Main(string[] args)
        {
            Tests();
            //Learn();
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


        private static void Learn()
        {

            ProgramParams programParams;
            using (var file = File.OpenText(@"ProgramParameters.json"))
            {
                var serializer = new JsonSerializer();
                programParams = (ProgramParams)serializer.Deserialize(file, typeof(ProgramParams));
            }

            var voc = Vocabulary.GetVocabularyFromFile(@"Vocabulary.json");
            var n = GenerateSentenceAccordingToGrammar("SimpleCFG.txt", 10);
            var data = GetSentencesOfGenerator(n, voc);

            using (var sw = File.AppendText("SessionReport.txt"))
            {
                sw.WriteLine("-------------------");
                sw.WriteLine("Session {0} ", DateTime.Now.ToString("MM/dd/yyyy h:mm tt"));
                sw.WriteLine("sentences: {0}, runs: {1}, movement: {2}", programParams.NumberOfDataSentences,
                    programParams.NumberOfRuns, programParams.DataWithMovement);
            }
            var stopWatch = StartWatch();

            var learner = new Learner(data);
            learner.CreateInitialGrammar(voc);

            var targetGrammarEnergy = learner.Energy(learner.originalGrammar);
            var s = string.Format("Target Hypothesis:\r\n{0} with energy: {1}\r\n", learner.originalGrammar,
                targetGrammarEnergy);

            Console.WriteLine(s);
            using (var sw = File.AppendText("SessionReport.txt"))
            {
                sw.WriteLine(s);
            }

            for (var i = 0; i < programParams.NumberOfRuns; i++)
            {
                var sa = new SimulatedAnnealing(learner, voc);
                sa.Run();
            }
            StopWatch(stopWatch);
        }

        private static void Tests()
        {
            //var n = ParseSentenceAccordingToGrammar("CFGLeftRecursion.txt", "John a the a the a the cried");
            //var n = ParseSentenceAccordingToGrammar("CFG.txt", "David knows the man kissed the woman a girl");
            //var n = ParseSentenceAccordingToGrammar("LIG.txt", "the man kissed the woman");
            //var n = ParseSentenceAccordingToGrammar("LIGMovementFromDirectObject.txt", "the woman the man kissed");
            //var n = ParseSentenceAccordingToGrammar("LIGMovementFromSubject.txt", "the man kissed the woman");
            //var n = ParseSentenceAccordingToGrammar("LIGMovementPP.txt", "to a girl the man went");
            //var n = ParseSentenceAccordingToGrammar("LIGMovementPP.txt", "a girl the man went to");
            var n = ParseSentenceAccordingToGrammar("LIGMovementFromSubjectOrNoMovementAmbiguity.txt", "a girl the man kissed");

            PrintTrees(n);

            //var n = GenerateSentenceAccordingToGrammar("SimpleCFG.txt", 10);
            //var n = GenerateSentenceAccordingToGrammar("LIGMovementPP.txt", 10);
            //var n = GenerateSentenceAccordingToGrammar("LIGMovementFromSubjectOrNoMovementAmbiguity.txt", 6);

            //PrintNonTerminals(n);
        }

        private static List<EarleyNode> GenerateSentenceAccordingToGrammar(string filename, int maxWords)
        {

            var grammar = GrammarFileReader.CreateGrammarFromFile(filename);
            EarleyGenerator generator = new EarleyGenerator(grammar);

            var n = generator.ParseSentence("", maxWords);
            return n;
        }

        private static List<EarleyNode>  ParseSentenceAccordingToGrammar(string filename, string sentence)
        {
            var grammar = GrammarFileReader.CreateGrammarFromFile(filename);
            EarleyParser parser = new EarleyParser(grammar);

            var n = parser.ParseSentence(sentence);
            return n;
        }

        private static void PrintTrees(List<EarleyNode> n)
        {
            foreach (var item in n)
            {
                item.Print(4);
                Console.WriteLine();
            }
        }

        private static List<string> GetSentencesNonTerminals(List<EarleyNode> n)
        {
            return n.Select(x => x.GetNonTerminalStringUnderNode()).ToList();
        }

        private static void PrintNonTerminals(List<EarleyNode> n)
        {
            var nonTerminalSentences = GetSentencesNonTerminals(n);
            foreach (var item in nonTerminalSentences)
                Console.WriteLine(item);
        }

        private static string[] GetSentencesOfGenerator(List<EarleyNode> n, Vocabulary voc)
        {
            var nonTerminalSentences = GetSentencesNonTerminals(n);
            List<string> sentences = new List<string>();

            foreach (var item in nonTerminalSentences)
            {
                string[] arr = item.Split();
                string[] sentence = new string[arr.Length];

                for (int i = 0; i < arr.Length; i++)
                    sentence[i] = voc.POSWithPossibleWords[arr[i]].First();

                var s = string.Join(" ", sentence);
                sentences.Add(s);
            }

            return sentences.ToArray();
        }

    }
}
