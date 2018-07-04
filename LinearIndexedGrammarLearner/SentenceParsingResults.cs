using LinearIndexedGrammarParser;
using System;
using System.Collections.Generic;
using System.Text;

namespace LinearIndexedGrammarLearner
{
    public class SentenceParsingResults
    {
        //the sentence being parsed
        public string Sentence { get; set; }

        //the set of trees coresponding to the sentence, given a grammar.
        public List<EarleyNode> Trees { get; set; }

        //the number of times the sentence was encountered in the corpus.
        public int Count { get; set; }

    }
}
