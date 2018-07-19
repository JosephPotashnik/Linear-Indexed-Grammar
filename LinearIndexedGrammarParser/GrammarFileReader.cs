using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace LinearIndexedGrammarParser
{
    public class GrammarFileReader
    {
        private static List<string> GetSentencesNonTerminals(List<EarleyNode> n)
        {
            return n.Select(x => x.GetNonTerminalStringUnderNode()).ToList();
        }

        public static (string[] sentences, Vocabulary textVocabulary) GetSentencesOfGenerator(List<EarleyNode> n, Vocabulary universalVocabulary)
        {
            Vocabulary textVocabulary = new Vocabulary();
            var nonTerminalSentences = GetSentencesNonTerminals(n);
            List<string> sentences = new List<string>();

            foreach (var item in nonTerminalSentences)
            {
                string[] arr = item.Split();
                string[] sentence = new string[arr.Length];

                var posCategories = new HashSet<string>();
                for (int i = 0; i < arr.Length; i++)
                {
                    string posCat = arr[i];

                    sentence[i] = universalVocabulary.POSWithPossibleWords[posCat].First();
                    if (!posCategories.Contains(posCat))
                        posCategories.Add(posCat);

                }
                foreach (var category in posCategories)
                    textVocabulary.AddWordsToPOSCategory(category, universalVocabulary.POSWithPossibleWords[category].ToArray());

                var s = string.Join(" ", sentence);
                sentences.Add(s);
            }

            return (sentences.ToArray(), textVocabulary);
        }
        
        public static (List<EarleyNode> n, Grammar g) GenerateSentenceAccordingToGrammar(string filename, int maxWords)
        {

            var grammar = CreateGrammarFromFile(filename);
            EarleyGenerator generator = new EarleyGenerator(grammar);

            var n = generator.ParseSentence("", maxWords);
            return (n, grammar);
        }

        public static List<EarleyNode> ParseSentenceAccordingToGrammar(string filename, string sentence)
        {
            var grammar = CreateGrammarFromFile(filename);
            EarleyParser parser = new EarleyParser(grammar);

            var n = parser.ParseSentence(sentence);
            return n;
        }
        public static Grammar CreateGrammarFromFile(string filename)
        {
            var rules = ReadRulesFromFile(filename);
            Grammar grammar = new Grammar();
            foreach (var item in rules)
                grammar.AddGrammarRule(item);

            return grammar;
        }
        private static DerivedCategory CreateDerivedCategory(string s)
        {
            Regex pattern = new Regex(@"(?<BaseCategory>\w*)(\[(?<Stack>[\w\*]*)\])?");
            Match match = pattern.Match(s);
            return new DerivedCategory(match.Groups["BaseCategory"].Value, match.Groups["Stack"].Value);
        }
        private static List<Rule> ReadRulesFromFile(string filename)
        {
            string line;
            char comment = '#';

            List<Rule> rules = new List<Rule>();
            using (var file = File.OpenText(filename))
            {
                while ((line = file.ReadLine()) != null)
                {
                    if (line[0] == comment) continue;
                    Rule r = CreateRule(line);
                    if (r != null)
                        rules.Add(r);
                }

            }
            return rules;
        }
        private static Rule CreateRule(string s)
        {
            string removeArrow = s.Replace("->", "");

            //string formatted incorrectly. (no "->" found).
            if (s == removeArrow) return null;

            string[] nonTerminals = removeArrow.Split().Select(p => p.Trim()).Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();

            var leftHandCat = CreateDerivedCategory(nonTerminals[0]);

            if (nonTerminals.Length == 1)
            {
                var epsiloncat = new DerivedCategory("Epsilon");
                return new Rule(leftHandCat, new[] { epsiloncat });
            }

            var rightHandCategories = new DerivedCategory[nonTerminals.Length - 1];
            for (int i = 1; i < nonTerminals.Length; i++)
                rightHandCategories[i - 1] = CreateDerivedCategory(nonTerminals[i]);

            return new Rule(leftHandCat, rightHandCategories);
        }
    }
}
