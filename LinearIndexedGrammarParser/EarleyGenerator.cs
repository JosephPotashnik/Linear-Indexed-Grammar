using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LinearIndexedGrammarParser
{
    public class EarleyGenerator : EarleyParser
    {
        public EarleyGenerator(ContextFreeGrammar g) : base(g) { }

        protected override (EarleyColumn[], int[])  PrepareEarleyTable(string text, int maxWords)
        {
            var table = new EarleyColumn[maxWords + 1];

            for (var i = 1; i < table.Length; i++)
                table[i] = new EarleyColumn(i, "generator");

            table[0] = new EarleyColumn(0, "");
            int[] gammaColumns = Enumerable.Range(1, maxWords).ToArray();
            return (table, gammaColumns);
        }

        protected override HashSet<string> GetPossibleSyntacticCategoriesForToken(string nextScannableTerm)
        {
            return voc.POSWithPossibleWords.Keys.ToHashSet();
        }

    }
}
