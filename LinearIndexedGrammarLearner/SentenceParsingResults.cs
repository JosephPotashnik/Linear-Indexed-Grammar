using System.Collections.Generic;
using LinearIndexedGrammarParser;

namespace LinearIndexedGrammarLearner
{
    public class SentenceParsingResults
    {
        //the sentence being parsed
        public string[] Sentence { get; set; }

        //the set of trees corresponding to the sentence, given a grammar.
        public List<EarleyNode> Trees { get; set; }

        public List<EarleyState> GammaStates { get; set; }
        //the number of times the sentence was encountered in the corpus.
        public int Count  { get; set; }

        //the length of the sentence (number of words)
        public int Length { get; set; }
    }
}