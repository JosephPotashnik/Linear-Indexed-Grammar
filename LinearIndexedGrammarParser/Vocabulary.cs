using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace LinearIndexedGrammarParser
{
    [JsonObject(MemberSerialization.OptIn)]
    public class Vocabulary
    {
        public Vocabulary()
        {
            WordWithPossiblePOS = new Dictionary<string, HashSet<string>>();
            POSWithPossibleWords = new Dictionary<string, HashSet<string>>();
        }

        // key = word, value = possible POS
        public Dictionary<string, HashSet<string>> WordWithPossiblePOS { get; set; }

        // key = POS, value = words having the same POS.
        [JsonProperty] public Dictionary<string, HashSet<string>> POSWithPossibleWords { get; set; }

        public HashSet<string> this[string word]
        {
            get
            {
                if (WordWithPossiblePOS.ContainsKey(word))
                    return WordWithPossiblePOS[word];
                return null;
            }
        }

        public static Vocabulary ReadVocabularyFromFile(string jsonFileName)
        {
            Vocabulary voc;

            //deserialize JSON directly from a file
            using (var file = File.OpenText(jsonFileName))
            {
                var serializer = new JsonSerializer();
                voc = (Vocabulary)serializer.Deserialize(file, typeof(Vocabulary));
            }

            voc.PopulateDependentJsonPropertys();
            return voc;
        }

        public bool ContainsWord(string word)
        {
            return WordWithPossiblePOS.ContainsKey(word);
        }

        public bool ContainsPOS(string pos)
        {
            return POSWithPossibleWords.ContainsKey(pos);
        }

        public void AddWordsToPOSCategory(string posCat, string[] words)
        {
            foreach (var word in words)
            {
                if (!WordWithPossiblePOS.ContainsKey(word))
                    WordWithPossiblePOS[word] = new HashSet<string>();
                WordWithPossiblePOS[word].Add(posCat);
            }

            if (!POSWithPossibleWords.ContainsKey(posCat))
                POSWithPossibleWords[posCat] = new HashSet<string>();

            foreach (var word in words)
                POSWithPossibleWords[posCat].Add(word);
        }

        //the function initializes WordWithPossiblePOS field after POSWithPossibleWords has been read from a json file.
        private void PopulateDependentJsonPropertys()
        {
            foreach (var kvp in POSWithPossibleWords)
            {
                var words = kvp.Value;
                foreach (var word in words)
                {
                    if (!WordWithPossiblePOS.ContainsKey(word))
                        WordWithPossiblePOS[word] = new HashSet<string>();
                    WordWithPossiblePOS[word].Add(kvp.Key);
                }
            }
        }

        //filter sentences with words that are not recognized by vocabulary
        //note: this function also adds inflected nouns and conjugated verbs in text to
        //the vocabulary they are not present (i.e. remembered (-> remember), gates -> gate)
        public string[][] LeaveOnlySentencesWithWordsInVocabulary(IEnumerable<string> sentences)
        {
            var sentencesToLearn = new List<string[]>();
            var wordsNotInVocabulary = new HashSet<string>();
            var encounteredWords = new HashSet<string>();
            foreach (var sentence1 in sentences)
            {
                //first stage of preprocessing =
                //replace contractions with full words.
                var sentence = ReplaceContractions(sentence1);
                var unableToResolveWord = false;

                //split to words
                var sentenceWords = sentence.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var newWords = new Dictionary<string, List<string>>
                {
                    ["N"] = new List<string>(),
                    ["V"] = new List<string>(),
                    ["ADJ"] = new List<string>(),
                    ["ADV"] = new List<string>()
                };


                //second stage of preprocessing
                //go over each word in the sentence
                //if it does not appear in dictionary,
                //infer its base form and part of speech from known conjugations
                //(i.e ing/ed/s , er/est, etc)
                var s = new List<string>();
                foreach (var wordOrig in sentenceWords)
                {
                    //trim leading or trailing single apostrophe
                    //(sometimes text come with single quotation marks: 'did you see him?'
                    //this case needs to be differentiated from single apostrophe used
                    //for contractions: I'd, can't, isn't...
                    var word1 = wordOrig.TrimStart('\'');
                    var word = word1.TrimEnd('\'');

                    if (wordsNotInVocabulary.Contains(word))
                    {
                        unableToResolveWord = true;
                        break;
                    }

                    if (encounteredWords.Contains(word))
                    {
                        s.Add(word);
                        continue;
                    }

                    encounteredWords.Add(word);
                    unableToResolveWord = true;

                    if (word.EndsWith("ed"))
                    {
                        newWords["V"].Add(word);
                        newWords["ADJ"].Add(word);
                        unableToResolveWord = false;
                    }
                    else if (word.EndsWith("ing"))
                    {
                        newWords["V"].Add(word);
                        newWords["ADJ"].Add(word);
                        unableToResolveWord = false;
                    }
                    else if (word.EndsWith("s") || word.EndsWith("es") || word.EndsWith("ies"))
                    {
                        var baseWords = new List<string>();
                        //"s"
                        baseWords.Add(word.Substring(0, word.Length - 1));

                        if (word.EndsWith("ies"))
                            baseWords.Add(word.Substring(0, word.Length - 3) + "y");
                        else if (word.EndsWith("es"))
                            baseWords.Add(word.Substring(0, word.Length - 2));

                        foreach (var baseWord in baseWords)
                            if (ContainsWord(baseWord))
                            {
                                var possiblePOS = WordWithPossiblePOS[baseWord];

                                if (possiblePOS.Contains("N"))
                                {
                                    newWords["N"].Add(word);
                                    unableToResolveWord = false;
                                }

                                if (possiblePOS.Contains("V"))
                                {
                                    newWords["V"].Add(word);
                                    unableToResolveWord = false;
                                }

                                //adverbs and adjectives in English do not conjugate.
                            }
                    }
                    //superlative (more)
                    else if (word.EndsWith("er") || word.EndsWith("ier"))
                    {
                        var baseWords = new List<string>();
                        //"er"
                        baseWords.Add(word.Substring(0, word.Length - 1)); //safer -> safe
                        baseWords.Add(word.Substring(0, word.Length - 2)); //thicker -> thick

                        if (word.EndsWith("ier"))
                            baseWords.Add(word.Substring(0, word.Length - 3) + "y");

                        foreach (var baseWord in baseWords)
                            if (ContainsWord(baseWord))
                            {
                                var possiblePOS = WordWithPossiblePOS[baseWord];

                                if (possiblePOS.Contains("ADJ"))
                                {
                                    unableToResolveWord = false;
                                    newWords["ADJ"].Add(word);
                                }
                            }
                    }
                    //superlatives (most)
                    else if (word.EndsWith("est") || word.EndsWith("iest"))
                    {
                        var baseWords = new List<string>();
                        //"est"
                        baseWords.Add(word.Substring(0, word.Length - 3)); //thickest -> thick
                        baseWords.Add(word.Substring(0, word.Length - 2)); //safest -> safe

                        if (word.EndsWith("iest"))
                            baseWords.Add(word.Substring(0, word.Length - 4) + "y");

                        foreach (var baseWord in baseWords)
                            if (ContainsWord(baseWord))
                            {
                                var possiblePOS = WordWithPossiblePOS[baseWord];

                                if (possiblePOS.Contains("ADJ"))
                                {
                                    newWords["ADJ"].Add(word);
                                    unableToResolveWord = false;
                                }
                            }
                    }

                    if (!ContainsWord(word))
                        if (unableToResolveWord)
                        {
                            wordsNotInVocabulary.Add(word);
                            break;
                        }

                    unableToResolveWord = false;
                    s.Add(word);
                }

                foreach (var pos in newWords.Keys)
                    AddWordsToPOSCategory(pos, newWords[pos].ToArray());

                if (unableToResolveWord == false && s.Count > 0)
                    sentencesToLearn.Add(s.ToArray());
            }

            return sentencesToLearn.ToArray();
        }

        //first step = outrule WH questions, see if you succeed in learning CFG grammar.
        //private HashSet<string> outruledWords = new HashSet<string>()
        //    {"who", "what", "why", "where", "how", "whom", "whose", "when", "which"};

        private string ReplaceContractions(string sentence)
        {
            var s = sentence.Replace("'ll", " will"); //ambiguous: I'll = I will / I shall
            var s1 = s.Replace("'ve", " have");
            var s2 = s1.Replace("'m", " am");
            var s3 = s2.Replace("'d", " had"); //ambiguous: I'd = I had / I would, how'd = how did / how would.
            var s4 = s3.Replace("n't", " not");
            var s5 = s4.Replace("'re", " are");

            //
            //string s6 = s5.Replace("'s", " is"); //ambiguous: it's = it is / it has
            //another problem: 's could be also the possessive:
            //[john's father] -> not! [john is/has father].
            return s5;
        }

        //private bool RuleOutSentence(string prevWord, string word)
        //{

        //    //rule out (for now) forms "to V" (to cry, to do..)
        //    if (prevWord != null && prevWord == "to" && WordWithPossiblePOS[word].Contains("V"))
        //        return true;

        //    if (prevWord == "of" && word == "course")
        //        return true;

        //    if (prevWord == "a" && word == "few")
        //        return true;

        //    if (word == "let")
        //        return true;

        //    if (word == "please")
        //        return true;

        //    if (word == "pray")
        //        return true;

        //    if (word == "random")
        //        return true;

        //    if (WordWithPossiblePOS[word].Contains("CONJ"))
        //        return true;

        //    if (WordWithPossiblePOS[word].Contains("ADV"))
        //        return true;

        //    //rule out (for now)
        //    //1. yes/no questions: "did you see that man?"
        //    //2. sentences starting with conjugations: "I squeezed my eyes shut, and opened them"

        //    if (prevWord == null)
        //    {
        //        if (word == "did" || word == "will" || word == "do" || word == "does"
        //            || word == "have" || word == "has" || word == "would"
        //            || word == "had" || word == "were" || word == "was"
        //            || word == "are" || word == "is" || word == "am"
        //            || word == "can" || word == "could" || word == "may" || word == "might"
        //            || word == "shall" || word == "should")

        //            return true;


        //    }
        //    else
        //    {
        //        //outrule two consecutive pronouns. - this is evidence of movement (cleft or a relative):
        //        //"he it was , I was certain, that.."
        //        // "whoever he was".. 
        //        if 
        //            (WordWithPossiblePOS.ContainsKey(word) && WordWithPossiblePOS.ContainsKey(prevWord) && 
        //            WordWithPossiblePOS[word].Contains("PRON") && WordWithPossiblePOS[prevWord].Contains("PRON"))
        //        return true;
        //    }

        //    //rule out (for now) sentences with wh-words.
        //    return (outruledWords.Contains(word));

        //}
    }
}