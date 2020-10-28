using LinearIndexedGrammarParser;
using Newtonsoft.Json;
using System.IO;
using System.Linq;
using Xunit;

namespace LinearIndexedGrammarParserTests
{
    public class ParserTests
    {

        //public ParserTests(ITestOutputHelper output)
        //{
        //    this.output = output;
        //}

        [Fact]
        public void Test()
        {
        }
            //private readonly ITestOutputHelper output;
            /*
            [Fact]
            public void CFGLeftRecursionTest()
            {
                var n = GrammarFileReader.ParseSentenceAccordingToGrammar("CFGLeftRecursion.txt", "Vocabulary.json",
                    "John a the a the a the cried");
                var settings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    NullValueHandling = NullValueHandling.Ignore,
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                };
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
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,

                };
                var actual = JsonConvert.SerializeObject(n, Formatting.Indented, settings);
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
                var pos = universalVocabulary.POSWithPossibleWords.Keys.ToHashSet();

                var (nodeList, grammar) =
                    GrammarFileReader.GenerateSentenceAccordingToGrammar("SimpleCFG.txt", "Vocabulary.json", 10);

                //var (data, dataVocabulary) =
                //    GrammarFileReader.GetSentencesOfGenerator(nodeList, universalVocabulary, pos, false);
                //replace with new DrawSamples()

                string[][] data = null;
                //Vocabulary dataVocabulary = null;

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

                var posInText = PrepareRuleSpace(out var bigrams);
                ContextSensitiveGrammar.RuleSpace = new RuleSpace(posInText, bigrams, 5);

                var rule = new Rule("X3", new[] { "D", "N" });

                var cfGrammar = new ContextFreeGrammar(new ContextSensitiveGrammar(grammarRules));

                var parser = new EarleyParser(cfGrammar, universalVocabulary);
                var sentence = "the man kissed the woman";
                parser.ParseSentence(sentence.Split(), new CancellationTokenSource());
                grammarRules.Add(rule);
                cfGrammar = new ContextFreeGrammar(new ContextSensitiveGrammar(grammarRules));
                var n = parser.ReParseSentenceWithRuleAddition(cfGrammar, rule);
                var settings = new JsonSerializerSettings
                    { Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore };
                var actual = JsonConvert.SerializeObject(n.nodes, settings);
                var expected = File.ReadAllText(@"ExpectedReparseWithRuleAddition1.json");
                //Assert.Equal(expected, actual);
            }

            [Fact]
            public void ReparseWithRuleAddition2()
            {
                var universalVocabulary = Vocabulary.ReadVocabularyFromFile("Vocabulary.json");
                ContextFreeGrammar.PartsOfSpeech = universalVocabulary.POSWithPossibleWords.Keys.Select(x => new SyntacticCategory(x)).ToHashSet();
                var grammarRules = GrammarFileReader.ReadRulesFromFile("CFGMissingVPRule.txt");

                var posInText = PrepareRuleSpace(out var bigrams);
                ContextSensitiveGrammar.RuleSpace = new RuleSpace(posInText, bigrams, 6);

                var rule = new Rule("X3", new[] { "V1", "X2" });

                var cfGrammar = new ContextFreeGrammar(new ContextSensitiveGrammar(grammarRules));

                var parser = new EarleyParser(cfGrammar, universalVocabulary);
                var sentence = "the man kissed the woman";
                parser.ParseSentence(sentence.Split(), new CancellationTokenSource());
                grammarRules.Add(rule);
                cfGrammar = new ContextFreeGrammar(new ContextSensitiveGrammar(grammarRules));

                var n = parser.ReParseSentenceWithRuleAddition(cfGrammar, rule);
                var settings = new JsonSerializerSettings
                    { Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore };
                var actual = JsonConvert.SerializeObject(n.nodes, settings);
                var expected = File.ReadAllText(@"ExpectedReparseWithRuleAddition2.json");
                //Assert.Equal(expected, actual);
            }

            [Fact]
            public void ReparseWithRuleAddition3()
            {
                var universalVocabulary = Vocabulary.ReadVocabularyFromFile("Vocabulary.json");
                ContextFreeGrammar.PartsOfSpeech = universalVocabulary.POSWithPossibleWords.Keys.Select(x => new SyntacticCategory(x)).ToHashSet();
                var grammarRules = GrammarFileReader.ReadRulesFromFile("CFGMissingVPRule.txt");

                var posInText = PrepareRuleSpace(out var bigrams);
                ContextSensitiveGrammar.RuleSpace = new RuleSpace(posInText, bigrams, 6);


                var rule = new Rule("X3", new[] { "X4", "X6" });

                var cfGrammar = new ContextFreeGrammar(new ContextSensitiveGrammar(grammarRules));

                var parser = new EarleyParser(cfGrammar, universalVocabulary);
                var sentence = "the man kissed the woman the woman";
                parser.ParseSentence(sentence.Split(), new CancellationTokenSource());
                grammarRules.Add(rule);
                cfGrammar = new ContextFreeGrammar(new ContextSensitiveGrammar(grammarRules));

                var n = parser.ReParseSentenceWithRuleAddition(cfGrammar, rule);
                var settings = new JsonSerializerSettings
                    { Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore };
                var actual = JsonConvert.SerializeObject(n.nodes, settings);
                var expected = File.ReadAllText(@"ExpectedReparseWithRuleAddition3.json");
                //Assert.Equal(expected, actual);
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
            /*
            [Fact]
            public void ReparseWithRuleDeletion1()
            {
                var universalVocabulary = Vocabulary.ReadVocabularyFromFile("Vocabulary.json");
                ContextFreeGrammar.PartsOfSpeech = universalVocabulary.POSWithPossibleWords.Keys.Select(x => new SyntacticCategory(x)).ToHashSet();
                var grammarRules = GrammarFileReader.ReadRulesFromFile("CFGMissingNPRule.txt");

                var posInText = PrepareRuleSpace(out var bigrams);
                ContextSensitiveGrammar.RuleSpace = new RuleSpace(posInText, bigrams, 6);

                var csg = new ContextSensitiveGrammar(grammarRules);
                var cfGrammar = new ContextFreeGrammar(csg);

                var parser = new EarleyParser(cfGrammar, universalVocabulary);
                var sentence = "the man kissed the woman";
                parser.ParseSentence(sentence.Split(), new CancellationTokenSource());

                var deletedRule = new Rule("X2", new[] { "D", "N" });
                var rc = ContextSensitiveGrammar.RuleSpace.FindRule(deletedRule);
                deletedRule.NumberOfGeneratingRule = ContextSensitiveGrammar.RuleSpace[rc].Number;

                csg.StackConstantRules.Remove(rc);

                cfGrammar = new ContextFreeGrammar(csg);

                parser.ReParseSentenceWithRuleDeletion(cfGrammar, deletedRule);
                var actual = parser.ToString();
                //File.WriteAllText(@"ExpectedRuleDeletion1.txt", str);
                var expected = File.ReadAllText(@"ExpectedRuleDeletion1.txt");
                //Assert.Equal(expected, actual);
            }

            [Fact]
            public void ReparseWithRuleDeletion2()
            {
                var universalVocabulary = Vocabulary.ReadVocabularyFromFile("Vocabulary.json");
                ContextFreeGrammar.PartsOfSpeech = universalVocabulary.POSWithPossibleWords.Keys.Select(x => new SyntacticCategory(x)).ToHashSet();
                var grammarRules = GrammarFileReader.ReadRulesFromFile("CFGMissingVPRule.txt");

                var cfGrammar = new ContextFreeGrammar(grammarRules);

                var parser = new EarleyParser(cfGrammar, universalVocabulary);
                var sentence = "the man kissed the woman";
                parser.ParseSentence(sentence.Split(), new CancellationTokenSource());
                var grules = cfGrammar.Rules;
                var rule = grules[5]; //VP -> V1 NP
                grules.RemoveAt(5);
                var lhs = new SyntacticCategory(rule.LeftHandSide);
                var r = ContextFreeGrammar.GenerateStaticRuleFromDynamicRule(rule, new DerivedCategory(lhs.ToString()));

                parser.ReParseSentenceWithRuleDeletion(grules, r);
                var actual = parser.ToString();
                //File.WriteAllText(@"ExpectedRuleDeletion2.txt", actual);
                var expected = File.ReadAllText(@"ExpectedRuleDeletion2.txt");
                Assert.Equal(expected, actual);
            }

            [Fact]
            public void ReparseWithRuleDeletion3()
            {
                var universalVocabulary = Vocabulary.ReadVocabularyFromFile("Vocabulary.json");
                ContextFreeGrammar.PartsOfSpeech = universalVocabulary.POSWithPossibleWords.Keys.Select(x => new SyntacticCategory(x)).ToHashSet();
                var grammarRules = GrammarFileReader.ReadRulesFromFile("CFGMissingVPRule.txt");

                var rule1 = new Rule("YP", new[] { "VP", "ADJP" });
                rule1.Number = -5;
                var cfGrammar = new ContextFreeGrammar(grammarRules);

                var parser = new EarleyParser(cfGrammar, universalVocabulary);
                var sentence = "the man kissed the woman the woman";
                parser.ParseSentence(sentence.Split(), new CancellationTokenSource());
                grammarRules.Add(rule1);
                var n = parser.ReParseSentenceWithRuleAddition(grammarRules, rule1);
                var s = parser.ToString();
                var grules = parser._grammar.Rules;
                var rule = grules[7]; //YP -> VP ADJP
                grules.RemoveAt(7);
                rule.Number = -5;

                var lhs = new SyntacticCategory(rule.LeftHandSide);
                var r = ContextFreeGrammar.GenerateStaticRuleFromDynamicRule(rule, new DerivedCategory(lhs.ToString()));

                var n1 = parser.ReParseSentenceWithRuleDeletion(grules, r);
                var actual = parser.ToString();
                //File.WriteAllText(@"ExpectedRuleDeletion3.txt", actual);
                var expected = File.ReadAllText(@"ExpectedRuleDeletion3.txt");
                Assert.Equal(expected, actual);
            }

            [Fact]
            public void ReparseWithRuleDeletion4()
            {
                var universalVocabulary = Vocabulary.ReadVocabularyFromFile("Vocabulary.json");
                ContextFreeGrammar.PartsOfSpeech = universalVocabulary.POSWithPossibleWords.Keys.Select(x => new SyntacticCategory(x)).ToHashSet();
                var grammarRules = GrammarFileReader.ReadRulesFromFile("CFGMissingVPRule.txt");

                var cfGrammar = new ContextFreeGrammar(grammarRules);

                var parser = new EarleyParser(cfGrammar, universalVocabulary);
                var sentence = "the man kissed the woman";
                var n = parser.ParseSentence(sentence.Split(), new CancellationTokenSource());
                var grules = cfGrammar.Rules;


                var rule = grules[1]; // START -> NP VP
                grules.RemoveAt(1);
                var lhs = new SyntacticCategory(rule.LeftHandSide);
                var r = ContextFreeGrammar.GenerateStaticRuleFromDynamicRule(rule, new DerivedCategory(lhs.ToString()));
                r.Number = 2;
                var n2 = parser.ReParseSentenceWithRuleDeletion(grules, r);
                var actual = parser.ToString();
                //File.WriteAllText(@"ExpectedRuleDeletion4.txt", actual);
                var expected = File.ReadAllText(@"ExpectedRuleDeletion4.txt");
                Assert.Equal(expected, actual);
            }


            [Fact]
            public void ReparseWithRuleAdditionAndDeletion1()
            {
                var universalVocabulary = Vocabulary.ReadVocabularyFromFile("Vocabulary.json");
                ContextFreeGrammar.PartsOfSpeech = universalVocabulary.POSWithPossibleWords.Keys.Select(x => new SyntacticCategory(x)).ToHashSet();
                var grammarRules = GrammarFileReader.ReadRulesFromFile("CFGMissingNPRule.txt");

                var rule1 = new Rule("ZP", new[] { "D", "N" });
                rule1.Number = -5;
                var cfGrammar = new ContextFreeGrammar(grammarRules);

                var parser = new EarleyParser(cfGrammar, universalVocabulary);
                var sentence = "the man kissed the woman";
                var n1 = parser.ParseSentence(sentence.Split(), new CancellationTokenSource());
                var settings = new JsonSerializerSettings
                    { Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore };
                var origTree = JsonConvert.SerializeObject(n1, settings);

                var originalTable = parser.ToString();
                grammarRules.Add(rule1);
                var n = parser.ReParseSentenceWithRuleAddition(grammarRules, rule1);
                var grules = parser._grammar.Rules;
                var rule = grules[7]; //ZP -> D N
                grules.RemoveAt(7);
                rule.Number = -5;

                var lhs = new SyntacticCategory(rule.LeftHandSide);
                var r = ContextFreeGrammar.GenerateStaticRuleFromDynamicRule(rule, new DerivedCategory(lhs.ToString()));

                var n2 = parser.ReParseSentenceWithRuleDeletion(grules, r);
                var restoredTree = JsonConvert.SerializeObject(n2, settings);

                var tableAfterAdditionAndDeletion = parser.ToString();
                Assert.Equal(tableAfterAdditionAndDeletion, originalTable);
                Assert.Equal(restoredTree, origTree);

            }


            [Fact]
            public void ReparseWithRuleAdditionAndDeletion2()
            {
                var universalVocabulary = Vocabulary.ReadVocabularyFromFile("Vocabulary.json");
                ContextFreeGrammar.PartsOfSpeech = universalVocabulary.POSWithPossibleWords.Keys.Select(x => new SyntacticCategory(x)).ToHashSet();
                var grammarRules = GrammarFileReader.ReadRulesFromFile("CFGMissingVPRule.txt");

                var rule1 = new Rule("YP", new[] { "V1", "NP" });
                rule1.Number = -5;
                var cfGrammar = new ContextFreeGrammar(grammarRules);

                var parser = new EarleyParser(cfGrammar, universalVocabulary);
                var sentence = "the man kissed the woman";
                var n1 = parser.ParseSentence(sentence.Split(), new CancellationTokenSource());
                var settings = new JsonSerializerSettings
                    { Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore };
                var origTree = JsonConvert.SerializeObject(n1, settings);

                var originalTable = parser.ToString();
                grammarRules.Add(rule1);
                var n = parser.ReParseSentenceWithRuleAddition(grammarRules, rule1);
                var grules = parser._grammar.Rules;
                var rule = grules[7]; //YP -> V1 NP
                grules.RemoveAt(7);
                rule.Number = -5;

                var lhs = new SyntacticCategory(rule.LeftHandSide);
                var r = ContextFreeGrammar.GenerateStaticRuleFromDynamicRule(rule, new DerivedCategory(lhs.ToString()));

                var n2 = parser.ReParseSentenceWithRuleDeletion(grules, r);
                var restoredTree = JsonConvert.SerializeObject(n2, settings);

                var tableAfterAdditionAndDeletion = parser.ToString();
                Assert.Equal(tableAfterAdditionAndDeletion, originalTable);
                Assert.Equal(restoredTree, origTree);

            }


            [Fact]
            public void ReparseWithRuleAdditionAndDeletion3()
            {
                var universalVocabulary = Vocabulary.ReadVocabularyFromFile("Vocabulary.json");
                ContextFreeGrammar.PartsOfSpeech = universalVocabulary.POSWithPossibleWords.Keys.Select(x => new SyntacticCategory(x)).ToHashSet();
                var grammarRules = GrammarFileReader.ReadRulesFromFile("CFGMissingVPRule.txt");

                var rule1 = new Rule("YP", new[] { "VP", "ADJP" });
                rule1.Number = -5;
                var cfGrammar = new ContextFreeGrammar(grammarRules);

                var parser = new EarleyParser(cfGrammar, universalVocabulary);
                var sentence = "the man kissed the woman the woman";
                var n1 = parser.ParseSentence(sentence.Split(), new CancellationTokenSource());
                var settings = new JsonSerializerSettings
                    { Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore };
                var origTree = JsonConvert.SerializeObject(n1, settings);

                grammarRules.Add(rule1);
                var n = parser.ReParseSentenceWithRuleAddition(grammarRules, rule1);
                var grules = parser._grammar.Rules;
                var rule = grules[7]; //YP -> V1 NP
                grules.RemoveAt(7);
                rule.Number = -5;

                var lhs = new SyntacticCategory(rule.LeftHandSide);
                var r = ContextFreeGrammar.GenerateStaticRuleFromDynamicRule(rule, new DerivedCategory(lhs.ToString()));

                var n2 = parser.ReParseSentenceWithRuleDeletion(grules, r);
                var restoredTree = JsonConvert.SerializeObject(n2, settings);

                Assert.Equal(restoredTree, origTree);

            }

            [Fact]
            public void ReparseWithRuleAdditionAndDeletion4()
            {
                var universalVocabulary = Vocabulary.ReadVocabularyFromFile("Vocabulary.json");
                ContextFreeGrammar.PartsOfSpeech = universalVocabulary.POSWithPossibleWords.Keys.Select(x => new SyntacticCategory(x)).ToHashSet();
                var grammarRules = GrammarFileReader.ReadRulesFromFile("CFGMissingVPRule.txt");

                var rule1 = new Rule("YP", new[] { "V1", "NP" });

                var cfGrammar = new ContextFreeGrammar(grammarRules);

                var parser = new EarleyParser(cfGrammar, universalVocabulary);
                var sentence = "the man kissed the woman";
                var n = parser.ParseSentence(sentence.Split(), new CancellationTokenSource());
                grammarRules.Add(rule1);
                var n1 = parser.ReParseSentenceWithRuleAddition(grammarRules, rule1);
                var s0 = parser.ToString();
                var grules = parser._grammar.Rules;

                var rule = grules[5]; 
                grules.RemoveAt(5);
                rule.Number = 6;

                var lhs = new SyntacticCategory(rule.LeftHandSide);
                var r = ContextFreeGrammar.GenerateStaticRuleFromDynamicRule(rule, new DerivedCategory(lhs.ToString()));

                var n2 = parser.ReParseSentenceWithRuleDeletion(grules, r);
                var s = parser.ToString();

                var settings = new JsonSerializerSettings
                { Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore };
                var actual = JsonConvert.SerializeObject(n2, settings);
                var expected = File.ReadAllText(@"ExpectedReparseWithRuleAdditionAndDeletion4.json");
                Assert.Equal(expected, actual);
            }

            [Fact]
            public void LeftRecursionRuleAddition()
            {

                var universalVocabulary = Vocabulary.ReadVocabularyFromFile("Vocabulary.json");
                ContextFreeGrammar.PartsOfSpeech = universalVocabulary.POSWithPossibleWords.Keys.Select(x => new SyntacticCategory(x)).ToHashSet();
                var grammarRules = GrammarFileReader.ReadRulesFromFile("CFGLeftRecursionMissingNPRule.txt");

                var rule = new Rule("NP", new[] { "NP", "D" });

                var cfGrammar = new ContextFreeGrammar(grammarRules);

                var parser = new EarleyParser(cfGrammar, universalVocabulary);
                var sentence = "John a the a the a the cried";
                var n = parser.ParseSentence(sentence.Split(), new CancellationTokenSource());
                grammarRules.Add(rule);
                var n1 = parser.ReParseSentenceWithRuleAddition(grammarRules, rule);
                var settings = new JsonSerializerSettings
                    { Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore };
                var actual = JsonConvert.SerializeObject(n1, settings);
                var expected = File.ReadAllText(@"ExpectedCFGLeftRecursion.json");
                Assert.Equal(expected, actual);

                var s = parser.ToString();
            }

            [Fact]
            public void ReparseWithRuleDeletion5()
            {
                var universalVocabulary = Vocabulary.ReadVocabularyFromFile("Vocabulary.json");
                ContextFreeGrammar.PartsOfSpeech = universalVocabulary.POSWithPossibleWords.Keys.Select(x => new SyntacticCategory(x)).ToHashSet();
                var grammarRules = GrammarFileReader.ReadRulesFromFile("CFGLeftRecursion.txt");

                var cfGrammar = new ContextFreeGrammar(grammarRules);

                var parser = new EarleyParser(cfGrammar, universalVocabulary);
                var sentence = "John a the a the a the cried";
                var n = parser.ParseSentence(sentence.Split(), new CancellationTokenSource());
                var grules = cfGrammar.Rules;


                var rule = grules[4]; // NP -> NP D
                grules.RemoveAt(4);
                var lhs = new SyntacticCategory(rule.LeftHandSide);
                var r = ContextFreeGrammar.GenerateStaticRuleFromDynamicRule(rule, new DerivedCategory(lhs.ToString()));

                var n1  = parser.ReParseSentenceWithRuleDeletion(grules, r);
                var actual = parser.ToString();
                //File.WriteAllText(@"ExpectedRuleDeletion5.txt", actual);
                var expected = File.ReadAllText(@"ExpectedRuleDeletion5.txt");
                Assert.Equal(expected, actual);
            }

            [Fact]
            public void CFGLeftRecursionDeletionAndAdditionTest()
            {
                var universalVocabulary = Vocabulary.ReadVocabularyFromFile("Vocabulary.json");
                ContextFreeGrammar.PartsOfSpeech = universalVocabulary.POSWithPossibleWords.Keys.Select(x => new SyntacticCategory(x)).ToHashSet();
                var grammarRules = GrammarFileReader.ReadRulesFromFile("CFGLeftRecursion.txt");

                var cfGrammar = new ContextFreeGrammar(grammarRules);

                var parser = new EarleyParser(cfGrammar, universalVocabulary);
                var sentence = "John a the a the a the cried";
                var n = parser.ParseSentence(sentence.Split(), new CancellationTokenSource());
                var s = parser.ToString();
                var settings = new JsonSerializerSettings
                    { Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore };
                var origTree = JsonConvert.SerializeObject(n, settings);

                var grules = cfGrammar.Rules;


                var rule1 = grules[4]; // NP -> NP D
                grules.RemoveAt(4);
                var lhs = new SyntacticCategory(rule1.LeftHandSide);
                var r = ContextFreeGrammar.GenerateStaticRuleFromDynamicRule(rule1, new DerivedCategory(lhs.ToString()));

                var n1 = parser.ReParseSentenceWithRuleDeletion(grules, r);

                var rule = new Rule("NP", new[] { "NP", "D" });
                var grammarRules1 = parser._grammar.Rules;
                grammarRules1.Add(rule);
                var n2 = parser.ReParseSentenceWithRuleAddition(grammarRules1, rule);
                var restoredTree = JsonConvert.SerializeObject(n2, settings);
                var s2 = parser.ToString();

                Assert.Equal(restoredTree, origTree);

            }*/
        }
}