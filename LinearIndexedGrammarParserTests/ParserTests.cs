using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using LinearIndexedGrammarParser;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace LinearIndexedGrammarParserTests
{
    public class ParserTests
    {
        public ParserTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        private readonly ITestOutputHelper output;

        [Fact]
        public void CFGLeftRecursionTest()
        {
            var n = GrammarFileReader.ParseSentenceAccordingToGrammar("CFGLeftRecursion.txt", "Vocabulary.json", 
                "John a the a the a the cried");
            var settings = new JsonSerializerSettings
                {Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore};
            var actual = JsonConvert.SerializeObject(n, settings);
            var expected = File.ReadAllText(@"ExpectedCFGLeftRecursion.json");
            Assert.Equal(expected, actual);
            //JsonSerializer serializer = new JsonSerializer();
            //serializer.NullValueHandling = NullValueHandling.Ignore;
            //serializer.Formatting = Formatting.Indented;

            //using (StreamWriter sw = new StreamWriter(@"ExpectedCFGLeftRecursion.json"))
            //using (JsonWriter writer = new JsonTextWriter(sw))
            //{
            //    serializer.Serialize(writer, n);
            //}
            //foreach (var item in n)
            //{
            //    output.WriteLine(item.TreeString());
            //}
        }

        [Fact]
        public void CFGTest()
        {
            var n = GrammarFileReader.ParseSentenceAccordingToGrammar("CFG.txt", "Vocabulary.json",
                "David knows the man kissed the woman a girl");
            var settings = new JsonSerializerSettings
                {Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore};
            var actual = JsonConvert.SerializeObject(n, settings);
            var expected = File.ReadAllText(@"ExpectedCFG.json");
            Assert.Equal(expected, actual);

            
        }

        [Fact]
        public void LIGMovementFromDirectObjectTest()
        {
            var n = GrammarFileReader.ParseSentenceAccordingToGrammar("LIGMovementFromDirectObject.txt", "Vocabulary.json",
                "the woman the man kissed");
            var settings = new JsonSerializerSettings
                {Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore};
            var actual = JsonConvert.SerializeObject(n, settings);
            var expected = File.ReadAllText(@"LIGMovementFromDirectObject.json");
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void LIGMovementFromSubjectOrNoMovementAmbiguityTest()
        {
            var n = GrammarFileReader.ParseSentenceAccordingToGrammar("LIGMovementFromSubjectOrNoMovementAmbiguity.txt", "Vocabulary.json",
                "a girl the man kissed");
            var settings = new JsonSerializerSettings
                {Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore};
            var actual = JsonConvert.SerializeObject(n, settings);
            var expected = File.ReadAllText(@"LIGMovementFromSubjectOrNoMovementAmbiguity.json");
            Assert.Equal(expected, actual);

        }

        [Fact]
        public void LIGMovementFromSubjectTest()
        {
            var n = GrammarFileReader.ParseSentenceAccordingToGrammar("LIGMovementFromSubject.txt", "Vocabulary.json",
                "the man kissed the woman");
            var settings = new JsonSerializerSettings
                {Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore};
            var actual = JsonConvert.SerializeObject(n, settings);
            var expected = File.ReadAllText(@"LIGMovementFromSubject.json");
            Assert.Equal(expected, actual);

        }

        [Fact]
        public void LIGMovementPPStrandingTest()
        {
            var n = GrammarFileReader.ParseSentenceAccordingToGrammar("LIGMovementPP.txt", "Vocabulary.json", "a girl the man went to");
            var settings = new JsonSerializerSettings
                {Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore};
            var actual = JsonConvert.SerializeObject(n, settings);
            var expected = File.ReadAllText(@"LIGMovementPPStranding.json");
            Assert.Equal(expected, actual);

        }

        [Fact]
        public void LIGMovementPPTest()
        {
            var n = GrammarFileReader.ParseSentenceAccordingToGrammar("LIGMovementPP.txt", "Vocabulary.json", "to a girl the man went");
            var settings = new JsonSerializerSettings
                {Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore};
            var actual = JsonConvert.SerializeObject(n, settings);
            var expected = File.ReadAllText(@"LIGMovementPP.json");
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void LIGTest()
        {
            var n = GrammarFileReader.ParseSentenceAccordingToGrammar("LIG.txt", "Vocabulary.json", "the man kissed the woman");
            var settings = new JsonSerializerSettings
                {Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore};
            var actual = JsonConvert.SerializeObject(n, settings);
            var expected = File.ReadAllText(@"LIG.json");
            Assert.Equal(expected, actual);
        }


        [Fact]
        public void TransitiveClosureMovement2Test()
        {
            var n = GrammarFileReader.ParseSentenceAccordingToGrammar("NullableTransitiveClosureMovement2.txt", "Vocabulary.json",
                "the kissed went");
            var settings = new JsonSerializerSettings
                {Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore};
            var actual = JsonConvert.SerializeObject(n, settings);
            var expected = File.ReadAllText(@"NullableTransitiveClosureMovement2.json");
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void TransitiveClosureMovementTest()
        {
            var n = GrammarFileReader.ParseSentenceAccordingToGrammar("NullableTransitiveClosureMovement.txt", "Vocabulary.json",
                "the went");
            var settings = new JsonSerializerSettings
                {Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore};
            var actual = JsonConvert.SerializeObject(n, settings);
            var expected = File.ReadAllText(@"NullableTransitiveClosureMovement.json");
            Assert.Equal(expected, actual);
        }


        [Fact]
        public void GenerateLIGMovementFromSubjectOrNoMovementAmbiguitySentencesTest()
        {
            var universalVocabulary = Vocabulary.ReadVocabularyFromFile(@"Vocabulary.json");

            var (nodeList, grammar) =
                GrammarFileReader.GenerateSentenceAccordingToGrammar("LIGMovementFromSubjectOrNoMovementAmbiguity.txt", "Vocabulary.json",
                    6);
            var (data, dataVocabulary) = GrammarFileReader.GetSentencesOfGenerator(nodeList, universalVocabulary, 1, false);

            var sentences = data.Select(x => string.Join(" ", x));
            var settings = new JsonSerializerSettings
            { Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore };
            var actual = JsonConvert.SerializeObject(sentences, settings);
            var expected = File.ReadAllText(@"GenerateLIGMovementFromSubjectOrNoMovementAmbiguitySentences.json");
            Assert.Equal(expected, actual);

            //JsonSerializer serializer = new JsonSerializer();
            //serializer.NullValueHandling = NullValueHandling.Ignore;
            //serializer.Formatting = Formatting.Indented;

            //using (StreamWriter sw = new StreamWriter(@"GeneratLIGMovementFromSubjectOrNoMovementAmbiguitySentences.json"))
            //using (JsonWriter writer = new JsonTextWriter(sw))
            //{
            //    serializer.Serialize(writer, data);
            //}
            //foreach (var item in data)
            //{
            //    output.WriteLine(item);
            //}
        }

        [Fact]
        public void GenerateCFGGrammarSentencesTest()
        {
            var universalVocabulary = Vocabulary.ReadVocabularyFromFile(@"Vocabulary.json");
            var (nodeList, grammar) = GrammarFileReader.GenerateSentenceAccordingToGrammar("SimpleCFG.txt", "Vocabulary.json",10);
            var (data, dataVocabulary) = GrammarFileReader.GetSentencesOfGenerator(nodeList, universalVocabulary, 1, false);
            var sentences = data.Select(x => string.Join(" ", x));

            var settings = new JsonSerializerSettings
                { Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore };
            var actual = JsonConvert.SerializeObject(sentences, settings);
            var expected = File.ReadAllText(@"GenerateCFGGrammarSentences.json");
            Assert.Equal(expected, actual);


            //JsonSerializer serializer = new JsonSerializer
            //{
            //    NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented
            //};

            //using (StreamWriter sw = new StreamWriter(@"GenerateCFGGrammarSentences.json"))
            //using (JsonWriter writer = new JsonTextWriter(sw))
            //{
            //    serializer.Serialize(writer, data);
            //}
            //foreach (var item in data)
            //{
            //    output.WriteLine(string.Join(" ", item));
            //}
        }

        [Fact]
        public void GenerateLIGMovementPPSentencesTest()
        {
            var universalVocabulary = Vocabulary.ReadVocabularyFromFile(@"Vocabulary.json");
            var (nodeList, grammar) = GrammarFileReader.GenerateSentenceAccordingToGrammar("LIGMovementPP.txt", "Vocabulary.json", 10);
            var (data, dataVocabulary) = GrammarFileReader.GetSentencesOfGenerator(nodeList, universalVocabulary, 1, false);

            var sentences = data.Select(x => string.Join(" ", x));

            var settings = new JsonSerializerSettings
            { Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore };
            var actual = JsonConvert.SerializeObject(sentences, settings);
            var expected = File.ReadAllText(@"GenerateLIGMovementPPSentences.json");
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void GenerateNullableCategoriesSentencesTest()
        {
            var universalVocabulary = Vocabulary.ReadVocabularyFromFile(@"Vocabulary.json");
            var (nodeList, grammar) =
                GrammarFileReader.GenerateSentenceAccordingToGrammar("NullableTransitiveClosureMovement2.txt", "Vocabulary.json",10);
            var (data, dataVocabulary) = GrammarFileReader.GetSentencesOfGenerator(nodeList, universalVocabulary, 1, false);
            var sentences = data.Select(x => string.Join(" ", x));

            var settings = new JsonSerializerSettings
            { Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore };
            var actual = JsonConvert.SerializeObject(sentences, settings);
            var expected = File.ReadAllText(@"GenerateNullableCategoriesSentences.json");
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void GenerateNullableCategoriesSentencesTest2()
        {
            var universalVocabulary = Vocabulary.ReadVocabularyFromFile(@"Vocabulary.json");
            var (nodeList, grammar) =
                GrammarFileReader.GenerateSentenceAccordingToGrammar("NullableTransitiveClosureMovement3.txt", "Vocabulary.json",10);
            var (data, dataVocabulary) = GrammarFileReader.GetSentencesOfGenerator(nodeList, universalVocabulary, 1, false);
            var sentences = data.Select(x => string.Join(" ", x));

            var settings = new JsonSerializerSettings
            { Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore };
            var actual = JsonConvert.SerializeObject(sentences, settings);
            var expected = File.ReadAllText(@"GenerateNullableCategoriesSentences2.json");
            Assert.Equal(expected, actual);
        }

        private static HashSet<string> PrepareRuleSpace(out HashSet<(string rhs1, string rhs2)> bigrams)
        {
            var universalVocabulary = Vocabulary.ReadVocabularyFromFile(@"Vocabulary.json");
            var posInText = universalVocabulary.POSWithPossibleWords.Keys.ToHashSet();
            bigrams = new HashSet<(string rhs1, string rhs2)>();
            foreach (var pos1 in posInText)
            foreach (var pos2 in posInText)
                bigrams.Add((pos1, pos2));
            return posInText;
        }


        [Fact]
        public void FindRHSIndexText1()
        {
            var posInText = PrepareRuleSpace(out var bigrams);

            ContextSensitiveGrammar.RuleSpace = new RuleSpace(posInText, bigrams, 5);
            int actual = ContextSensitiveGrammar.RuleSpace.FindRHSIndex(new[] { "X2", "X4" });
            int expected = 191;
            Assert.Equal(expected, actual);
        }

  
        [Fact]
        public void FindRHSIndexText2()
        {
            var posInText = PrepareRuleSpace(out var bigrams);

            ContextSensitiveGrammar.RuleSpace = new RuleSpace(posInText, bigrams, 5);
            int actual = ContextSensitiveGrammar.RuleSpace.FindRHSIndex(new[] { "D", "N" });
            int expected = 3;
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void FindLHSIndexText()
        {
            var posInText = PrepareRuleSpace(out var bigrams);

            ContextSensitiveGrammar.RuleSpace = new RuleSpace(posInText, bigrams, 5);
            int actual = ContextSensitiveGrammar.RuleSpace.FindLHSIndex("X3");
            int expected = 2;
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void FindCFGRuleTest()
        {
            var posInText = PrepareRuleSpace(out var bigrams);
            ContextSensitiveGrammar.RuleSpace = new RuleSpace(posInText, bigrams, 5);

            var r = new Rule("X2", new[] { "V2", "X3" });
            var actual = ContextSensitiveGrammar.RuleSpace.FindRule(r);
            var expected = new RuleCoordinates()
            {
                LHSIndex = 1,
                RHSIndex = 94,
                RuleType = 0
            };

            Assert.Equal(actual, expected);
        }

        [Fact]
        public void CyclicUnitProductionsTest()
        {
            var n = GrammarFileReader.ParseSentenceAccordingToGrammar("CFGCyclicUnitProduction.txt", "Vocabulary.json",
                "the man kissed the woman");
            var settings = new JsonSerializerSettings
                { Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore };
            var actual = JsonConvert.SerializeObject(n, settings);
            var expected = File.ReadAllText(@"ExpectedCyclicUnitProduction.json");
            Assert.Equal(expected, actual); 
        }

        [Fact]
        public void ReparseWithRuleAddition1()
        {
            var universalVocabulary = Vocabulary.ReadVocabularyFromFile("Vocabulary.json");
            ContextFreeGrammar.PartsOfSpeech = universalVocabulary.POSWithPossibleWords.Keys.Select(x => new SyntacticCategory(x)).ToHashSet();
            var grammarRules = GrammarFileReader.ReadRulesFromFile("CFGMissingNPRule.txt");

            var rule = new Rule("ZP", new[] { "D", "N" });

            var cfGrammar = new ContextFreeGrammar(grammarRules);

            var parser = new EarleyParser(cfGrammar, universalVocabulary);
            var sentence = "the man kissed the woman";
            parser.ParseSentence(sentence.Split(), new CancellationTokenSource());
            grammarRules.Add(rule);
            var n = parser.ReParseSentenceWithRuleAddition(grammarRules, rule);
            var settings = new JsonSerializerSettings
                { Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore };
            var actual = JsonConvert.SerializeObject(n, settings);
            var expected = File.ReadAllText(@"ExpectedReparseWithRuleAddition1.json");
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ReparseWithRuleAddition2()
        {
            var universalVocabulary = Vocabulary.ReadVocabularyFromFile("Vocabulary.json");
            ContextFreeGrammar.PartsOfSpeech = universalVocabulary.POSWithPossibleWords.Keys.Select(x => new SyntacticCategory(x)).ToHashSet();
            var grammarRules = GrammarFileReader.ReadRulesFromFile("CFGMissingVPRule.txt");

            var rule = new Rule("YP", new[] { "V1", "NP" });

            var cfGrammar = new ContextFreeGrammar(grammarRules);

            var parser = new EarleyParser(cfGrammar, universalVocabulary);
            var sentence = "the man kissed the woman";
            parser.ParseSentence(sentence.Split(), new CancellationTokenSource());
            grammarRules.Add(rule);
            var n = parser.ReParseSentenceWithRuleAddition(grammarRules, rule);
            var settings = new JsonSerializerSettings
                { Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore };
            var actual = JsonConvert.SerializeObject(n, settings);
            var expected = File.ReadAllText(@"ExpectedReparseWithRuleAddition2.json");
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ReparseWithRuleAddition3()
        {
            var universalVocabulary = Vocabulary.ReadVocabularyFromFile("Vocabulary.json");
            ContextFreeGrammar.PartsOfSpeech = universalVocabulary.POSWithPossibleWords.Keys.Select(x => new SyntacticCategory(x)).ToHashSet();
            var grammarRules = GrammarFileReader.ReadRulesFromFile("CFGMissingVPRule.txt");

            var rule = new Rule("YP", new[] { "VP", "ADJP" });

            var cfGrammar = new ContextFreeGrammar(grammarRules);

            var parser = new EarleyParser(cfGrammar, universalVocabulary);
            var sentence = "the man kissed the woman the woman";
            parser.ParseSentence(sentence.Split(), new CancellationTokenSource());
            grammarRules.Add(rule);
            var n = parser.ReParseSentenceWithRuleAddition(grammarRules, rule);
            var settings = new JsonSerializerSettings
                { Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore };
            var actual = JsonConvert.SerializeObject(n, settings);
            var expected = File.ReadAllText(@"ExpectedReparseWithRuleAddition3.json");
            Assert.Equal(expected, actual);
            //JsonSerializer serializer = new JsonSerializer();
            //serializer.NullValueHandling = NullValueHandling.Ignore;
            //serializer.Formatting = Formatting.Indented;

            //using (StreamWriter sw = new StreamWriter(@"ExpectedReparseWithRuleAddition3.json"))
            //using (JsonWriter writer = new JsonTextWriter(sw))
            //{
            //    serializer.Serialize(writer, n);
            //}
            //foreach (var item in n)
            //{
            //    output.WriteLine(item.TreeString());
            //}
        }

    }
}