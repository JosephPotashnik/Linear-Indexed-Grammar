using LinearIndexedGrammarParser;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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
            var n = GrammarFileReader.ParseSentenceAccordingToGrammar("CFGLeftRecursion.txt", "John a the a the a the cried");
            JsonSerializerSettings settings = new JsonSerializerSettings { Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore };
            string actual = JsonConvert.SerializeObject(n, settings);
            string expected = (File.ReadAllText(@"ExpectedCFGLeftRecursion.json"));
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
            var n = GrammarFileReader.ParseSentenceAccordingToGrammar("CFG.txt", "David knows the man kissed the woman a girl");
            JsonSerializerSettings settings = new JsonSerializerSettings { Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore };
            string actual = JsonConvert.SerializeObject(n, settings);
            string expected = (File.ReadAllText(@"CFG.json"));
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void LIGTest()
        {
            var n = GrammarFileReader.ParseSentenceAccordingToGrammar("LIG.txt", "the man kissed the woman");
            JsonSerializerSettings settings = new JsonSerializerSettings { Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore };
            string actual = JsonConvert.SerializeObject(n, settings);
            string expected = (File.ReadAllText(@"LIG.json"));
            Assert.Equal(expected, actual);
           
        }

        [Fact]
        public void LIGMovementFromDirectObjectTest()
        {
            var n = GrammarFileReader.ParseSentenceAccordingToGrammar("LIGMovementFromDirectObject.txt", "the woman the man kissed");
            JsonSerializerSettings settings = new JsonSerializerSettings { Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore };
            string actual = JsonConvert.SerializeObject(n, settings);
            string expected = (File.ReadAllText(@"LIGMovementFromDirectObject.json"));
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void LIGMovementFromSubjectTest()
        {
            var n = GrammarFileReader.ParseSentenceAccordingToGrammar("LIGMovementFromSubject.txt", "the man kissed the woman");
            JsonSerializerSettings settings = new JsonSerializerSettings { Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore };
            string actual = JsonConvert.SerializeObject(n, settings);
            string expected = (File.ReadAllText(@"LIGMovementFromSubject.json"));
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void LIGMovementPPTest()
        {
            var n = GrammarFileReader.ParseSentenceAccordingToGrammar("LIGMovementPP.txt", "to a girl the man went");
            JsonSerializerSettings settings = new JsonSerializerSettings { Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore };
            string actual = JsonConvert.SerializeObject(n, settings);
            string expected = (File.ReadAllText(@"LIGMovementPP.json"));
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void LIGMovementPPStrandingTest()
        {
            var n = GrammarFileReader.ParseSentenceAccordingToGrammar("LIGMovementPP.txt", "a girl the man went to");
            JsonSerializerSettings settings = new JsonSerializerSettings { Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore };
            string actual = JsonConvert.SerializeObject(n, settings);
            string expected = (File.ReadAllText(@"LIGMovementPPStranding.json"));
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void LIGMovementFromSubjectOrNoMovementAmbiguityTest()
        {
            var n = GrammarFileReader.ParseSentenceAccordingToGrammar("LIGMovementFromSubjectOrNoMovementAmbiguity.txt", "a girl the man kissed");
            JsonSerializerSettings settings = new JsonSerializerSettings { Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore };
            string actual = JsonConvert.SerializeObject(n, settings);
            string expected = (File.ReadAllText(@"LIGMovementFromSubjectOrNoMovementAmbiguity.json"));
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void GeneratCFGGrammarSentencesTest()
        {
            Vocabulary universalVocabulary = Vocabulary.ReadVocabularyFromFile(@"Vocabulary.json");
            (var nodeList, var grammar) = GrammarFileReader.GenerateSentenceAccordingToGrammar("SimpleCFG.txt", 10);
            (var data, var dataVocabulary) = GrammarFileReader.GetSentencesOfGenerator(nodeList, universalVocabulary);

            JsonSerializerSettings settings = new JsonSerializerSettings { Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore };
            string actual = JsonConvert.SerializeObject(data, settings);
            string expected = (File.ReadAllText(@"GeneratCFGGrammarSentences.json"));
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void GeneratLIGMovementPPSentencesTest()
        {
            Vocabulary universalVocabulary = Vocabulary.ReadVocabularyFromFile(@"Vocabulary.json");
            (var nodeList, var grammar) = GrammarFileReader.GenerateSentenceAccordingToGrammar("LIGMovementPP.txt", 10);
            (var data, var dataVocabulary) = GrammarFileReader.GetSentencesOfGenerator(nodeList, universalVocabulary);

            JsonSerializerSettings settings = new JsonSerializerSettings { Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore };
            string actual = JsonConvert.SerializeObject(data, settings);
            string expected = (File.ReadAllText(@"GeneratLIGMovementPPSentences.json"));
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void GeneratLIGMovementFromSubjectOrNoMovementAmbiguitySentencesTest()
        {
            Vocabulary universalVocabulary = Vocabulary.ReadVocabularyFromFile(@"Vocabulary.json");
            (var nodeList, var grammar) = GrammarFileReader.GenerateSentenceAccordingToGrammar("LIGMovementFromSubjectOrNoMovementAmbiguity.txt", 6);
            (var data, var dataVocabulary) = GrammarFileReader.GetSentencesOfGenerator(nodeList, universalVocabulary);

            JsonSerializerSettings settings = new JsonSerializerSettings { Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore };
            string actual = JsonConvert.SerializeObject(data, settings);
            string expected = (File.ReadAllText(@"GeneratLIGMovementFromSubjectOrNoMovementAmbiguitySentences.json"));
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
    }
}
