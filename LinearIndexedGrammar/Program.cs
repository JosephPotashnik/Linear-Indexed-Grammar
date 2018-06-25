using LinearIndexedGrammarParser;
using System;
using System.Collections.Generic;
namespace LinearIndexedGrammar
{
    class Program
    {
        static void Main(string[] args)
        {
            //var n = ParseSentenceAccordingToGrammar("CFGLeftRecursion.txt", "John a the a the a the cried");
            //var n = ParseSentenceAccordingToGrammar("CFG.txt", "David knows the man kissed the woman a girl");
            //var n = ParseSentenceAccordingToGrammar("LIG.txt", "the man kissed the woman");
            //var n = ParseSentenceAccordingToGrammar("LIGMovementFromDirectObject.txt", "the woman the man kissed");
            //var n = ParseSentenceAccordingToGrammar("LIGMovementFromSubject.txt", "the man kissed the woman");
            //var n = ParseSentenceAccordingToGrammar("LIGMovementPP.txt", "to a girl the man went");
            //var n = ParseSentenceAccordingToGrammar("LIGMovementPP.txt", "a girl the man went to");
            //var n = ParseSentenceAccordingToGrammar("LIGMovementFromSubjectOrNoMovementAmbiguity.txt", "a girl the man kissed");

            //PrintTrees(n);

            //var n = GenerateSentenceAccordingToGrammar("SimpleCFG.txt", 10);
            var n = GenerateSentenceAccordingToGrammar("LIGMovementPP.txt", 10);
            PrintNonTerminals(n);

        }

        private static List<EarleyNode> GenerateSentenceAccordingToGrammar(string filename, int maxWords)
        {
            EarleyGenerator generator = new EarleyGenerator();

            var rules = GrammarFileReader.ReadRulesFromFile(filename);
            foreach (var item in rules)
                generator.AddGrammarRule(item);

            var n = generator.ParseSentence("", maxWords);
            return n;
        }

        private static List<EarleyNode>  ParseSentenceAccordingToGrammar(string filename, string sentence)
        {
            EarleyParser parser = new EarleyParser();

            var rules = GrammarFileReader.ReadRulesFromFile(filename);
            foreach (var item in rules)
                parser.AddGrammarRule(item);

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

        private static void PrintNonTerminals(List<EarleyNode> n)
        {
            foreach (var item in n)
            {
                Console.WriteLine(item.GetTerminalStringUnderNode());
            }
        }
    }
}
