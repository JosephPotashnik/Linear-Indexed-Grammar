using System.Collections.Generic;

namespace LinearIndexedGrammarParser
{
    public class Grammar
    {
        public Grammar() { }
        internal const string GammaRule = "Gamma";
        internal const string StartRule = "START";
        internal const string EpsislonSymbol = "Epsilon";

        internal readonly Dictionary<DerivedCategory, List<Rule>> staticRules = new Dictionary<DerivedCategory, List<Rule>>();
        internal readonly Dictionary<SyntacticCategory, List<Rule>> dynamicRules = new Dictionary<SyntacticCategory, List<Rule>>();
        internal readonly HashSet<DerivedCategory> staticRulesGeneratedForCategory = new HashSet<DerivedCategory>();
        internal readonly HashSet<DerivedCategory> nullableCategories = new HashSet<DerivedCategory>();
        internal static int ruleCounter = 0;
    }
}