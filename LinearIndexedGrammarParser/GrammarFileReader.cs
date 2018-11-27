using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace LinearIndexedGrammarParser
{
    public class GrammarFileReader
    {
        private static List<string> GetSentencesNonTerminals(List<EarleyNode> n)
        {
            return n.Select(x => x.GetNonTerminalStringUnderNode()).ToList();
        }

        public static (string[] sentences, Vocabulary textVocabulary) GetSentencesOfGenerator(List<EarleyNode> n,
            Vocabulary universalVocabulary)
        {
            var textVocabulary = new Vocabulary();
            var nonTerminalSentences = GetSentencesNonTerminals(n);
            var sentences = new List<string>();

            foreach (var item in nonTerminalSentences)
            {
                var arr = item.Split();
                var sentence = new string[arr.Length];

                var posCategories = new HashSet<string>();
                for (var i = 0; i < arr.Length; i++)
                {
                    var posCat = arr[i];

                    sentence[i] = universalVocabulary.POSWithPossibleWords[posCat].First();
                    if (!posCategories.Contains(posCat))
                        posCategories.Add(posCat);
                }

                foreach (var category in posCategories)
                    textVocabulary.AddWordsToPOSCategory(category,
                        universalVocabulary.POSWithPossibleWords[category].ToArray());

                var s = string.Join(" ", sentence);
                sentences.Add(s);
            }

            return (sentences.ToArray(), textVocabulary);
        }

        public static (List<EarleyNode> nodeList, Rule[] grammarRules) GenerateSentenceAccordingToGrammar(
            string filename, int maxWords)
        {
            //var cSgrammar = CreateGrammarFromFile(filename);
            //var cfGrammar = new ContextFreeGrammar(cSgrammar);

            var grammarRules = ReadRulesFromFile(filename).ToArray();
            var cfGrammar = new ContextFreeGrammar(grammarRules);

            var generator = new EarleyGenerator(cfGrammar);

            var nodeList = generator.ParseSentence("", maxWords);
            //return (nodeList, cSgrammar);
            return (nodeList, grammarRules);

        }

        public static List<EarleyNode> ParseSentenceAccordingToGrammar(string filename, string sentence)
        {
            var cSgrammar = CreateGrammarFromFile(filename);
            var cFgrammar = new ContextFreeGrammar(cSgrammar);

            var parser = new EarleyParser(cFgrammar);

            var n = parser.ParseSentence(sentence);
            return n;
        }



        public static ContextSensitiveGrammar CreateGrammarFromFile(string filename)
        {
            var rules = ReadRulesFromFile(filename);
            var grammar = new ContextSensitiveGrammar();
            //foreach (var item in rules)
            //    item.AddRuleToGrammar(grammar, true);

            return grammar;
        }

        private static DerivedCategory CreateDerivedCategory(string s)
        {
            var pattern = new Regex(@"(?<BaseCategory>\w*)(\[(?<Stack>[\w\*]*)\])?");
            var match = pattern.Match(s);
            return new DerivedCategory(match.Groups["BaseCategory"].Value, match.Groups["Stack"].Value);
        }

        private static List<Rule> ReadRulesFromFile(string filename)
        {
            string line;
            var comment = '#';

            var rules = new List<Rule>();
            using (var file = File.OpenText(filename))
            {
                while ((line = file.ReadLine()) != null)
                {
                    if (line[0] == comment) continue;
                    var r = CreateRule(line);
                    if (r != null)
                        rules.Add(r);
                }
            }

            return rules;
        }

        private static Rule CreateRule(string s)
        {
            var removeArrow = s.Replace("->", "");

            //string formatted incorrectly. (no "->" found).
            if (s == removeArrow) return null;

            var nonTerminals = removeArrow.Split().Select(p => p.Trim()).Where(p => !string.IsNullOrWhiteSpace(p))
                .ToArray();

            var leftHandCat = CreateDerivedCategory(nonTerminals[0]);
            var popRule = false;
            var pushRule = false;
            Rule baseRule;
            SyntacticCategory moveable = null;
            MoveableOperationsKey key = MoveableOperationsKey.NoOp;

            if (leftHandCat.Stack.Contains(ContextFreeGrammar.StarSymbol))
                if (leftHandCat.Stack.Length > 1)
                {
                    popRule = true;
                    //TODO: in future, implement pop2 stack changing operation.
                    key = MoveableOperationsKey.Pop2;
                    moveable = new SyntacticCategory(leftHandCat.Stack.Substring(1));
                }

            if (nonTerminals.Length == 1)
            {
                var epsilonCat = new DerivedCategory(ContextFreeGrammar.EpsilonSymbol) {StackSymbolsCount = -1};
                baseRule = new Rule(leftHandCat, new[] {epsilonCat});
                moveable = new SyntacticCategory(leftHandCat);
                key = MoveableOperationsKey.Pop1;
                return new StackChangingRule(baseRule, key , moveable);
            }

            var rightHandCategories = new DerivedCategory[nonTerminals.Length - 1];
            for (var i = 1; i < nonTerminals.Length; i++)
            {
                rightHandCategories[i - 1] = CreateDerivedCategory(nonTerminals[i]);
                if (rightHandCategories[i - 1].Stack.Contains(ContextFreeGrammar.StarSymbol))
                {
                    if (rightHandCategories[i - 1].Stack.Length > 1)
                    {
                        pushRule = true;
                        //push rule.
                        moveable = new SyntacticCategory(rightHandCategories[i - 1].Stack.Substring(1));
                        key = MoveableOperationsKey.Push1;
                        rightHandCategories[i - 1].StackSymbolsCount = 1;
                        if (popRule)
                            throw new Exception("illegal LIG format: can't push and pop within the same rule");
                    }
                    else
                    {
                        if (popRule)
                            rightHandCategories[i - 1].StackSymbolsCount = -1;
                    }
                }
            }

            if (!pushRule && !popRule)
                return new Rule(leftHandCat, rightHandCategories);
            else
            {
                baseRule = new Rule(leftHandCat, rightHandCategories);
                return new StackChangingRule(baseRule, key, moveable);
            }
        }
    }
}