using System;
using System.Collections.Generic;
using System.Text;

namespace LinearIndexedGrammarParser
{
    //during the syntactic derivation, the syntactic categories are generated dynamically.
    //The grammar contains grammar rules that list all static syntactic categories.
    //Additionally, the grammar contains a schema that explains how to derive new rules
    //from existing ones.

    //Here, The contents of the stack are concatenated with the syntactic category
    //thus, a rule with stack contents such as CP[..] -> NP CP[NP] is treated as
    //a rule CP -> NP CPNP.

    //Important - you must guarantee that SyntacticCategory.Symbols are prefix-free,
    //so no two categories of derived rules are ambiguous.

    public class DerivedRule : GrammarRule
    {

        public DerivedRule(GrammarRule rule) : base(rule)
        {

        }
    }
}
