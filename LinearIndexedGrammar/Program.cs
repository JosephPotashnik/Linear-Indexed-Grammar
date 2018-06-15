using LinearIndexedGrammarParser;
using System;

namespace LinearIndexedGrammar
{
    class Program
    {
        static void Main(string[] args)
        {

            Earleyarser parser = new Earleyarser();


            parser.AddDerivedRule(new GrammarRule( "START", new[] { "NP", "VP" }, 1, 1));
            parser.AddDerivedRule(new GrammarRule("VP", new[] { "V0" }, 0, 0));
            parser.AddDerivedRule(new GrammarRule("VP", new[] { "V1", "NP" }, 0, 1));
            parser.AddDerivedRule(new GrammarRule("VP", new[] { "V2", "PP" }, 0, 1));
            parser.AddDerivedRule(new GrammarRule("VP", new[] { "V3", "START" }, 0, 1));
            parser.AddDerivedRule(new GrammarRule("NP", new[] { "D", "N" }, 1, 0));
            parser.AddDerivedRule(new GrammarRule("PP", new[] { "P", "NP" }, 0, 1));

            var n = parser.ParseSentence("the child says a man knows David went for the bells");

            n.Print(4);



        }
    }
}
