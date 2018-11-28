using System.IO;
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
            var n = GrammarFileReader.ParseSentenceAccordingToGrammar("CFGLeftRecursion.txt",
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
            //     serializer.Serialize(writer, n);
            //}
            //foreach (var item in n)
            //{
            //    output.WriteLine(item.TreeString());
            //}
        }

        [Fact]
        public void CFGTest()
        {
            var n = GrammarFileReader.ParseSentenceAccordingToGrammar("CFG.txt",
                "David knows the man kissed the woman a girl");
            var settings = new JsonSerializerSettings
                {Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore};
            var actual = JsonConvert.SerializeObject(n, settings);
            var expected = File.ReadAllText(@"CFG.json");
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void GeneratCFGGrammarSentencesTest()
        {
            var universalVocabulary = Vocabulary.ReadVocabularyFromFile(@"Vocabulary.json");
            var (nodeList, grammar) = GrammarFileReader.GenerateSentenceAccordingToGrammar("SimpleCFG.txt", 10);
            var (data, dataVocabulary) = GrammarFileReader.GetSentencesOfGenerator(nodeList, universalVocabulary);

            var settings = new JsonSerializerSettings
                {Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore};
            var actual = JsonConvert.SerializeObject(data, settings);
            var expected = File.ReadAllText(@"GeneratCFGGrammarSentences.json");
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void GeneratLIGMovementFromSubjectOrNoMovementAmbiguitySentencesTest()
        {
            var universalVocabulary = Vocabulary.ReadVocabularyFromFile(@"Vocabulary.json");
            var (nodeList, grammar) =
                GrammarFileReader.GenerateSentenceAccordingToGrammar("LIGMovementFromSubjectOrNoMovementAmbiguity.txt",
                    6);
            var (data, dataVocabulary) = GrammarFileReader.GetSentencesOfGenerator(nodeList, universalVocabulary);

            var settings = new JsonSerializerSettings
                {Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore};
            var actual = JsonConvert.SerializeObject(data, settings);
            var expected = File.ReadAllText(@"GeneratLIGMovementFromSubjectOrNoMovementAmbiguitySentences.json");
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
        public void GeneratLIGMovementPPSentencesTest()
        {
            var universalVocabulary = Vocabulary.ReadVocabularyFromFile(@"Vocabulary.json");
            var (nodeList, grammar) = GrammarFileReader.GenerateSentenceAccordingToGrammar("LIGMovementPP.txt", 10);
            var (data, dataVocabulary) = GrammarFileReader.GetSentencesOfGenerator(nodeList, universalVocabulary);

            var settings = new JsonSerializerSettings
                {Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore};
            var actual = JsonConvert.SerializeObject(data, settings);
            var expected = File.ReadAllText(@"GeneratLIGMovementPPSentences.json");
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void GeneratNullableCategoriesSentencesTest()
        {
            var universalVocabulary = Vocabulary.ReadVocabularyFromFile(@"Vocabulary.json");
            var (nodeList, grammar) =
                GrammarFileReader.GenerateSentenceAccordingToGrammar("NullableTransitiveClosureMovement2.txt", 10);
            var (data, dataVocabulary) = GrammarFileReader.GetSentencesOfGenerator(nodeList, universalVocabulary);

            var settings = new JsonSerializerSettings
                {Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore};
            var actual = JsonConvert.SerializeObject(data, settings);
            var expected = File.ReadAllText(@"GeneratNullableCategoriesSentences.json");
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void GeneratNullableCategoriesSentencesTest2()
        {
            var universalVocabulary = Vocabulary.ReadVocabularyFromFile(@"Vocabulary.json");
            var (nodeList, grammar) =
                GrammarFileReader.GenerateSentenceAccordingToGrammar("NullableTransitiveClosureMovement3.txt", 10);
            var (data, dataVocabulary) = GrammarFileReader.GetSentencesOfGenerator(nodeList, universalVocabulary);

            var settings = new JsonSerializerSettings
                {Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore};
            var actual = JsonConvert.SerializeObject(data, settings);
            var expected = File.ReadAllText(@"GeneratNullableCategoriesSentences2.json");
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void LIGMovementFromDirectObjectTest()
        {
            var n = GrammarFileReader.ParseSentenceAccordingToGrammar("LIGMovementFromDirectObject.txt",
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
            var n = GrammarFileReader.ParseSentenceAccordingToGrammar("LIGMovementFromSubjectOrNoMovementAmbiguity.txt",
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
            var n = GrammarFileReader.ParseSentenceAccordingToGrammar("LIGMovementFromSubject.txt",
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
            var n = GrammarFileReader.ParseSentenceAccordingToGrammar("LIGMovementPP.txt", "a girl the man went to");
            var settings = new JsonSerializerSettings
                {Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore};
            var actual = JsonConvert.SerializeObject(n, settings);
            var expected = File.ReadAllText(@"LIGMovementPPStranding.json");
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void LIGMovementPPTest()
        {
            var n = GrammarFileReader.ParseSentenceAccordingToGrammar("LIGMovementPP.txt", "to a girl the man went");
            var settings = new JsonSerializerSettings
                {Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore};
            var actual = JsonConvert.SerializeObject(n, settings);
            var expected = File.ReadAllText(@"LIGMovementPP.json");
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void LIGTest()
        {
            var n = GrammarFileReader.ParseSentenceAccordingToGrammar("LIG.txt", "the man kissed the woman");
            var settings = new JsonSerializerSettings
                {Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore};
            var actual = JsonConvert.SerializeObject(n, settings);
            var expected = File.ReadAllText(@"LIG.json");
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void TransitiveClosureMovement2Test()
        {
            var n = GrammarFileReader.ParseSentenceAccordingToGrammar("NullableTransitiveClosureMovement2.txt",
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
            var n = GrammarFileReader.ParseSentenceAccordingToGrammar("NullableTransitiveClosureMovement.txt",
                "the went");
            var settings = new JsonSerializerSettings
                {Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore};
            var actual = JsonConvert.SerializeObject(n, settings);
            var expected = File.ReadAllText(@"NullableTransitiveClosureMovement.json");
            Assert.Equal(expected, actual);
        }
    }
}