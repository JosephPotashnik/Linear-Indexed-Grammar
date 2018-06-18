using LinearIndexedGrammarParser;
using System;

namespace LinearIndexedGrammar
{
    class Program
    {
        static void Main(string[] args)
        {
            System.Collections.Generic.List<EarleyNode> n = PPMovement();
            foreach (var item in n)
            {
                item.Print(4);
                Console.WriteLine();
            }
        }
        private static System.Collections.Generic.List<EarleyNode> CFGTestLeftRecursion()
        {
            EarleyParser parser = new EarleyParser();

            parser.AddGrammarRule(new Rule("START", new[] { "NP", "VP" }));
            parser.AddGrammarRule(new Rule("VP", new[] { "V0" }));
            parser.AddGrammarRule(new Rule("NP", new[] { "D", "N" }));
            parser.AddGrammarRule(new Rule("NP", new[] { "NP", "D" }));


            parser.AddGrammarRule(new Rule("PP", new[] { "P", "NP" }));
            parser.AddGrammarRule(new Rule("NP", new[] { "NP", "ADJP" }));
            parser.AddGrammarRule(new Rule("VP", new[] { "VP", "ADJP" }));
            parser.AddGrammarRule(new Rule("ADJP", new[] { "NP" }));

            var n = parser.ParseSentence("John a the a the a the cried");

            return n;
        }

        private static System.Collections.Generic.List<EarleyNode> CFGTest()
        {
            EarleyParser parser = new EarleyParser();

            parser.AddGrammarRule(new Rule("START", new[] { "NP", "VP" }));
            parser.AddGrammarRule(new Rule("VP", new[] { "V0" }));
            parser.AddGrammarRule(new Rule("VP", new[] { "V1", "NP" }));
            parser.AddGrammarRule(new Rule("VP", new[] { "V2", "PP" }));
            parser.AddGrammarRule(new Rule("VP", new[] { "V3", "START" }));
            parser.AddGrammarRule(new Rule("NP", new[] { "D", "N" }));

            parser.AddGrammarRule(new Rule("PP", new[] { "P", "NP" }));
            parser.AddGrammarRule(new Rule("NP", new[] { "NP", "ADJP" }));
            parser.AddGrammarRule(new Rule("VP", new[] { "VP", "ADJP" }));
            parser.AddGrammarRule(new Rule("ADJP", new[] { "NP" }));

            var n = parser.ParseSentence("David knows the man kissed the woman a girl");
            return n;
        }

        private static System.Collections.Generic.List<EarleyNode> LIGTest1()
        {
            EarleyParser parser = new EarleyParser();

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
            var NPCat3 = new DerivedCategory("NP", "*");
            var NPCat4 = new DerivedCategory("NP", "NP");
            var epsiloncat = new DerivedCategory("Epsilon");

            parser.AddGrammarRule(new Rule(NPCat4, new[] { epsiloncat }));


            parser.AddGrammarRule(new Rule(VPCat1, new[] { V0Cat, NPCat3 }));

            parser.AddGrammarRule(new Rule(startCat, new[] { NPCat1, VPCat }));
            parser.AddGrammarRule(new Rule(startCat, new[] { NPCat1, VPCat1 }));
            parser.AddGrammarRule(new Rule(VPCat, new[] { V0Cat }));
            parser.AddGrammarRule(new Rule(VPCat, new[] { V1Cat, NPCat }));
            parser.AddGrammarRule(new Rule(VPCat, new[] { V1Cat, NPCat2 }));
            parser.AddGrammarRule(new Rule(NPCat1, new[] { DCat, NCat }));

            var n = parser.ParseSentence("the man kissed the woman");
            return n;
        }

        private static System.Collections.Generic.List<EarleyNode> MovemetFromDirectObjectPositionTest()
        {
            EarleyParser parser = new EarleyParser();
            var startCat = new DerivedCategory("START", "*");

            var CPCat = new DerivedCategory("CP", "*");
            var CPCatPushNP = new DerivedCategory("CP", "*NP");
            var NPCat = new DerivedCategory("NP");

            var IPCat = new DerivedCategory("IP", "*");
            var VPCat = new DerivedCategory("VP", "*");
            var V1Cat = new DerivedCategory("V1");
            var DCat = new DerivedCategory("D");
            var NCat = new DerivedCategory("N");

            var NPCatComplement = new DerivedCategory("NP", "*");

            var NPNPCat = new DerivedCategory("NP", "NP");
            var epsiloncat = new DerivedCategory("Epsilon");

            parser.AddGrammarRule(new Rule(startCat, new[] { CPCat }));


            parser.AddGrammarRule(new Rule(NPNPCat, new[] { epsiloncat }));
            parser.AddGrammarRule(new Rule(CPCat, new[] { NPCat, CPCatPushNP }));

            parser.AddGrammarRule(new Rule(CPCat, new[] { IPCat }));
            parser.AddGrammarRule(new Rule(IPCat, new[] { NPCat, VPCat }));
            parser.AddGrammarRule(new Rule(VPCat, new[] { V1Cat, NPCatComplement }));
            parser.AddGrammarRule(new Rule(NPCat, new[] { DCat, NCat }));

            var n = parser.ParseSentence("the woman the man kissed");
            return n;
        }

        private static System.Collections.Generic.List<EarleyNode> MovemetFromSubjectPositionTest()
        {
            EarleyParser parser = new EarleyParser();
            var startCat = new DerivedCategory("START", "*");

            var CPCat = new DerivedCategory("CP", "*");
            var CPCatPushNP = new DerivedCategory("CP", "*NP");
            var NPCat = new DerivedCategory("NP");

            var IPCat = new DerivedCategory("IP", "*");
            var IPCatPop = new DerivedCategory("IP", "*NP");

            var VPCat = new DerivedCategory("VP", "*");
            var V1Cat = new DerivedCategory("V1");
            var DCat = new DerivedCategory("D");
            var NCat = new DerivedCategory("N");

            var NPCatComplement = new DerivedCategory("NP", "*");

            var NPNPCat = new DerivedCategory("NP", "NP");
            var epsiloncat = new DerivedCategory("Epsilon");

            parser.AddGrammarRule(new Rule(startCat, new[] { CPCat }));

            parser.AddGrammarRule(new Rule(NPNPCat, new[] { epsiloncat }));
            parser.AddGrammarRule(new Rule(CPCat, new[] { NPCat, CPCatPushNP }));

            parser.AddGrammarRule(new Rule(CPCat, new[] { IPCat }));
            //parser.AddGrammarRule(new Rule(IPCat, new[] { NPCat, VPCat }));

            parser.AddGrammarRule(new Rule(IPCatPop, new[] { NPNPCat, VPCat }));
            parser.AddGrammarRule(new Rule(VPCat, new[] { V1Cat, NPCatComplement }));
            parser.AddGrammarRule(new Rule(NPCat, new[] { DCat, NCat }));

            var n = parser.ParseSentence("the man kissed the woman");
            return n;
        }

        private static System.Collections.Generic.List<EarleyNode> PPMovement()
        {
            EarleyParser parser = new EarleyParser();
            var startCat = new DerivedCategory("START", "*");

            var CPCat = new DerivedCategory("CP", "*");
            var CPCatPushNP = new DerivedCategory("CP", "*NP");
            var CPCatPushPP = new DerivedCategory("CP", "*PP");

            var NPCat = new DerivedCategory("NP");
            var PPCat = new DerivedCategory("PP");

            var IPCat = new DerivedCategory("IP", "*");
            var IPCatPop = new DerivedCategory("IP", "*NP");

            var VPCat = new DerivedCategory("VP", "*");
            var V1Cat = new DerivedCategory("V1");
            var V2Cat = new DerivedCategory("V2");

            var DCat = new DerivedCategory("D");
            var NCat = new DerivedCategory("N");
            var PCat = new DerivedCategory("P");

            var NPCatComplement = new DerivedCategory("NP", "*");
            var PPCatComplement = new DerivedCategory("PP", "*");

            var NPNPCat = new DerivedCategory("NP", "NP");
            var PPPPCat = new DerivedCategory("PP", "PP");

            var epsiloncat = new DerivedCategory("Epsilon");

            parser.AddGrammarRule(new Rule(startCat, new[] { CPCat }));
            parser.AddGrammarRule(new Rule(PPPPCat, new[] { epsiloncat }));
            parser.AddGrammarRule(new Rule(NPNPCat, new[] { epsiloncat }));
            parser.AddGrammarRule(new Rule(CPCat, new[] { NPCat, CPCatPushNP }));
            parser.AddGrammarRule(new Rule(CPCat, new[] { PPCat, CPCatPushPP }));

            parser.AddGrammarRule(new Rule(CPCat, new[] { IPCat }));
            parser.AddGrammarRule(new Rule(IPCat, new[] { NPCat, VPCat }));

            parser.AddGrammarRule(new Rule(IPCatPop, new[] { NPNPCat, VPCat }));
            parser.AddGrammarRule(new Rule(VPCat, new[] { V1Cat, NPCatComplement }));
            parser.AddGrammarRule(new Rule(NPCat, new[] { DCat, NCat }));

            parser.AddGrammarRule(new Rule(VPCat, new[] { V2Cat, PPCatComplement }));
            parser.AddGrammarRule(new Rule(PPCatComplement, new[] { PCat, NPCatComplement }));


            var n = parser.ParseSentence("to a girl the man went");
            return n;
        }
        private static System.Collections.Generic.List<EarleyNode> movementOutOfPP()
        {
            EarleyParser parser = new EarleyParser();
            var startCat = new DerivedCategory("START", "*");

            var CPCat = new DerivedCategory("CP", "*");
            var CPCatPushNP = new DerivedCategory("CP", "*NP");
            var NPCat = new DerivedCategory("NP");

            var IPCat = new DerivedCategory("IP", "*");
            var IPCatPop = new DerivedCategory("IP", "*NP");

            var VPCat = new DerivedCategory("VP", "*");
            var V1Cat = new DerivedCategory("V1");
            var V2Cat = new DerivedCategory("V2");

            var DCat = new DerivedCategory("D");
            var NCat = new DerivedCategory("N");
            var PCat = new DerivedCategory("P");

            var NPCatComplement = new DerivedCategory("NP", "*");
            var PPCatComplement = new DerivedCategory("PP", "*");

            var NPNPCat = new DerivedCategory("NP", "NP");
            var epsiloncat = new DerivedCategory("Epsilon");

            parser.AddGrammarRule(new Rule(startCat, new[] { CPCat }));

            parser.AddGrammarRule(new Rule(NPNPCat, new[] { epsiloncat }));
            parser.AddGrammarRule(new Rule(CPCat, new[] { NPCat, CPCatPushNP }));

            parser.AddGrammarRule(new Rule(CPCat, new[] { IPCat }));
            parser.AddGrammarRule(new Rule(IPCat, new[] { NPCat, VPCat }));

            parser.AddGrammarRule(new Rule(IPCatPop, new[] { NPNPCat, VPCat }));
            parser.AddGrammarRule(new Rule(VPCat, new[] { V1Cat, NPCatComplement }));
            parser.AddGrammarRule(new Rule(NPCat, new[] { DCat, NCat }));

            parser.AddGrammarRule(new Rule(VPCat, new[] { V2Cat, PPCatComplement }));
            parser.AddGrammarRule(new Rule(PPCatComplement, new[] { PCat, NPCatComplement }));


            var n = parser.ParseSentence("a girl the man went to");
            return n;
        }

        private static System.Collections.Generic.List<EarleyNode> MovementFromSubjectPositionTestOrNoMovementAmbiguity()
        {
            EarleyParser parser = new EarleyParser();
            var startCat = new DerivedCategory("START", "*");

            var CPCat = new DerivedCategory("CP", "*");
            var CPCatPushNP = new DerivedCategory("CP", "*NP");
            var NPCat = new DerivedCategory("NP");

            var IPCat = new DerivedCategory("IP", "*");
            var IPCatPop = new DerivedCategory("IP", "*NP");

            var VPCat = new DerivedCategory("VP", "*");
            var V1Cat = new DerivedCategory("V1");
            var DCat = new DerivedCategory("D");
            var NCat = new DerivedCategory("N");

            var NPCatComplement = new DerivedCategory("NP", "*");

            var NPNPCat = new DerivedCategory("NP", "NP");
            var epsiloncat = new DerivedCategory("Epsilon");

            parser.AddGrammarRule(new Rule(startCat, new[] { CPCat }));

            parser.AddGrammarRule(new Rule(NPNPCat, new[] { epsiloncat }));
            parser.AddGrammarRule(new Rule(CPCat, new[] { NPCat, CPCatPushNP }));

            parser.AddGrammarRule(new Rule(CPCat, new[] { IPCat }));
            parser.AddGrammarRule(new Rule(IPCat, new[] { NPCat, VPCat }));

            parser.AddGrammarRule(new Rule(IPCatPop, new[] { NPNPCat, VPCat }));
            parser.AddGrammarRule(new Rule(VPCat, new[] { V1Cat, NPCatComplement }));
            parser.AddGrammarRule(new Rule(NPCat, new[] { DCat, NCat }));

            var n = parser.ParseSentence("a girl the man kissed");
            return n;
        }
    }
}
