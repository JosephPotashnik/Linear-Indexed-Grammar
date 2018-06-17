using LinearIndexedGrammarParser;
using System;
using System.Text.RegularExpressions;

namespace LinearIndexedGrammar
{
    class Program
    {

       
        static void Main(string[] args)
        {
            //var grammarRule = new Rule(new Rule("CP*", new[] { "NP", "IP*NP" }, 0, 1));
            //var cat = new SyntacticCategory("CP");

            //var newRule = GenerateRule(grammarRule, cat);
            Earleyarser parser = new Earleyarser();

            var startCat = new DerivedCategory("START", "*");
            var VPCat = new DerivedCategory("VP", "*");
            var VPCat1 = new DerivedCategory("VP", "*NP");

            var NPCat = new DerivedCategory("NP", "*");
            var NCat = new DerivedCategory("N");
            var DCat = new DerivedCategory("D");
            var V0Cat = new DerivedCategory("V0");
            var V1Cat = new DerivedCategory("V1");
            var CPCat = new DerivedCategory("CP", "*");
            var NPCat1 = new DerivedCategory("NP");
            var CPCat1 = new DerivedCategory("CP", "*NP");
            var NPCat2 = new DerivedCategory("NP", "*NP");


            //parser.AddGrammarRule(new Rule(CPCat, new[] { NPCat1, CPCat1 }));

            parser.AddGrammarRule(new Rule(startCat, new[] { NPCat1, VPCat }));
            parser.AddGrammarRule(new Rule(startCat, new[] { NPCat1, VPCat1 }));

            parser.AddGrammarRule(new Rule(VPCat, new[] { V0Cat }));
            parser.AddGrammarRule(new Rule(VPCat, new[] { V1Cat, NPCat }));
            parser.AddGrammarRule(new Rule(VPCat, new[] { V1Cat, NPCat2 }));

            parser.AddGrammarRule(new Rule(NPCat1, new[] { DCat, NCat }));


            var n = parser.ParseSentence("the man kissed the woman");


            //parser.AddRule(new Rule("START", new[] { "NP", "VP" }));
            //parser.AddRule(new Rule("VP", new[] { "V0" }));
            //parser.AddRule(new Rule("VP", new[] { "V1", "NP" }));
            //parser.AddRule(new Rule("VP", new[] { "V2", "PP" }));
            //parser.AddRule(new Rule("VP", new[] { "V3", "START" }));
            //parser.AddRule(new Rule("NP", new[] { "D", "N" }));
            //parser.AddRule(new Rule("PP", new[] { "P", "NP" }));
            //parser.AddRule(new Rule("NP", new[] { "NP", "ADJP" }));
            //parser.AddRule(new Rule("VP", new[] { "VP", "ADJP" }));
            //parser.AddRule(new Rule("ADJP", new[] { "NP"  }));

            //var n = parser.ParseSentence("David knows the man kissed the woman a girl");

            foreach (var item in n)
            {
                item.Print(4);
                Console.WriteLine();
            }
            



        }
    }
}
