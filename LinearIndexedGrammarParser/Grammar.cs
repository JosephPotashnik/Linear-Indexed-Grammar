using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace LinearIndexedGrammarParser
{

    [JsonObject(MemberSerialization.OptIn)]
    public class Grammar
    {
        public const string Epsilon = "#epsilon#";
        public const int ScanRuleNumber = 0;
        private int numberOfRules;

        public Grammar()
        {
            StartSymbol = "START";
            POSTypes = new HashSet<string>();
            numberOfRules = 0;
            Rules = new Dictionary<SyntacticCategory, List<Rule>>();
        }

        public Grammar(Vocabulary voc) : this()
        {
            Vocabulary = voc;
        }

        
        [JsonProperty]
        public string StartSymbol { get; set; }

        public Vocabulary Vocabulary { get; set; }

        [JsonProperty]
        public Dictionary<SyntacticCategory, List<Rule>> Rules { get; set; }
    
        [JsonProperty]
        public HashSet<string> POSTypes { get; set; }

        public List<Rule> this[SyntacticCategory lhs]
        {
            get { return Rules.ContainsKey(lhs) ? Rules[lhs] : null; }
        }

        public static Grammar GetGrammarFromFile(string jsonFileName, Vocabulary voc)
        {
            var grammar = JsonConvert.DeserializeObject<Grammar>(File.ReadAllText(jsonFileName));
            grammar.Vocabulary = voc;
            grammar.PopulateDependentJsonPropertys();
            return grammar;
        }

        private void PopulateDependentJsonPropertys()
        {
            var rules = Rules.Values.SelectMany(l => l).ToArray();
            Rules = new Dictionary<SyntacticCategory, List<Rule>>();

            //foreach (var r in rules)
            //    AddRule(r);
        }

        
        //return log probab
        public static double GetProbabilitySumOfTwoLogProbabilities(double logProb1, double logProb2)
        {
            var prob1 = GetProbabilityFromLogProbability(logProb1);
            var prob2 = GetProbabilityFromLogProbability(logProb2);

            return GetLogProbabilityFromProbability(prob1 + prob2);
        }


        public static double GetProbabilityFromLogProbability(double logProb)
        {
            return Math.Pow(2, -logProb);
        }

        public static double GetLogProbabilityFromProbability(double prob)
        {
            return -Math.Log(prob, 2);
        }
        
        //public override string ToString()
        //{
        //    var rules = ruleNumberDictionary.Values;

        //    var sortedRules = rules.OrderBy(x => x.Number);
        //    var ruleTable = string.Join("\r\n", sortedRules);

        //    var landingSites = "Landing Sites: " + string.Join(" ", LandingSites) + "\r\n";
        //    var moveables = "Moveables: " + string.Join(" ", Moveables) + "\r\n";

        //    return ruleTable + "\r\n" + landingSites + moveables;
        //}
        

    }
}