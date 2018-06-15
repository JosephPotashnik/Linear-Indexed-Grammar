using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

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
        // key = POS, value = words.
        [JsonProperty]
        public Dictionary<string, HashSet<string>> POSWithPossibleWords { get; set; }

        public HashSet<string> this[string word]
        {
            get
            {
                if (WordWithPossiblePOS.ContainsKey(word))
                    return WordWithPossiblePOS[word];
                return null;
            }
        }

        public static Vocabulary GetVocabularyFromFile(string jsonFileName)
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
    }
}