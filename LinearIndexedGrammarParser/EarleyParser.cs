using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace LinearIndexedGrammarParser
{
    public class LogException : Exception
    {
        public LogException(string str) : base(str)
        {
        }
    }

    public class GenerateException : Exception
    {
    }

    public class Earleyarser
    {
        private const string GammaRule = "Gamma";
        private const float ChanceToGenerateConstituent = 0.4f; //for running the parser as a generator.
        private readonly Random rand = new Random(); //for running the parser as a generator.
        private readonly Dictionary<DerivedCategory, List<Rule>> rules = new Dictionary<DerivedCategory, List<Rule>>();
        private readonly Dictionary<SyntacticCategory, List<Rule>> grammarRules = new Dictionary<SyntacticCategory, List<Rule>>();

        static int ruleCounter = 0;
        private Vocabulary voc;

        private static Rule GenerateRule(Rule grammarRule, DerivedCategory leftHandSide)
        {
            string patternStringLeftHandSide = grammarRule.LeftHandSide.Stack;
            if (!patternStringLeftHandSide.Contains("*")) return null;

            //1. make the pattern be your Syntactic Category
            //2. then find the stack contents - anything by "*" (the first group)
            var newRule = new Rule(grammarRule);
            string patternString = patternStringLeftHandSide.Replace("*", "(.*)");
            Regex pattern = new Regex(patternString);

            string textToMatch = leftHandSide.Stack;
            Match match = pattern.Match(textToMatch);
            var stackContents = match.Groups[1].Value;
            newRule.LeftHandSide = leftHandSide;


            //3. replace the contents of the stack * in the right hand side productions.
            for (int i = 0; i < newRule.RightHandSide.Length; i++)
            {
                string patternRightHandSide = newRule.RightHandSide[i].Stack;
                string res = patternRightHandSide.Replace("*", stackContents);

                newRule.RightHandSide[i].Stack = res;
            }

            return newRule;

        }

        public Earleyarser()
        {
            voc = Vocabulary.GetVocabularyFromFile(@"Vocabulary.json");
        }

        public void AddRule(Rule r)
        {
            if (r == null) return;

            ruleCounter++;
            var newRule = new Rule(r);
            newRule.Number = ruleCounter;

            if (!rules.ContainsKey(newRule.LeftHandSide))
                rules[newRule.LeftHandSide] = new List<Rule>();

            rules[newRule.LeftHandSide].Add(newRule);
        }

        public void AddGrammarRule(Rule r)
        {
            var newRule = new Rule(r);
            var newSynCat = new SyntacticCategory(newRule.LeftHandSide);

            if (!grammarRules.ContainsKey(newSynCat))
                grammarRules[newSynCat] = new List<Rule>();

            grammarRules[newSynCat].Add(newRule);

            var emptyStackRule = new DerivedCategory(newSynCat.ToString());

            //generate base form of the rule with the empty stack
            //as a starting point of the grammar (= equal to context free case)
            var derivedRule = GenerateRule(newRule, emptyStackRule);

            var rule = derivedRule ?? newRule;
            //if there is no derived rule to be generated (i.e, the rule does not
            //come with a stack specified, it is a strictly context-free rule, then just add it)
            AddRule(rule);

        }
        public bool Debug { get; set; }

        public Grammar Grammar { get; set; }

        private void Predict(EarleyColumn col, List<Rule> ruleList)
        {
            foreach (var rule in ruleList)
            {
                var newState = new EarleyState(rule, 0, col, null) { LogProbability = 0 };

                if (newState.LogProbability < 0)
                    throw new Exception("wrong probability");

                col.AddState(newState);

                if (Debug)
                    Console.WriteLine("{0} & {1} & {2} & Predicted from syntactic category {3}\\\\", newState.StateNumber,
                        newState,
                        col.Index, rule.RightHandSide);
            }
        }

        private void Scan(EarleyColumn col, EarleyState state, SyntacticCategory term, string token)
        {
            var v = new EarleyNode(term.ToString(), col.Index - 1, col.Index)
            {
                AssociatedTerminal = token,
                LogProbability = 0.0f,
                Bits = 1
            };
            var y = EarleyState.MakeNode(state, col.Index, v);
            var newState = new EarleyState(state.Rule, state.DotIndex + 1, state.StartColumn, y);

            col.AddState(newState);
            if (Debug)
                Console.WriteLine("{0} & {1} & {2} & Scanned from State {3}, word: {4}\\\\", newState.StateNumber,
                    newState, col.Index,
                    state.StateNumber, token);

            if (newState.Node.LogProbability < 0)
            {
                throw new LogException(string.Format("scanarrrr! NODE log probability lower than 0: {0}, state: {1}",
                    newState.Node.LogProbability, newState));
            }
        }

        private void Complete(EarleyColumn col, EarleyState state)
        {
            if (state.Rule.LeftHandSide.ToString() == GammaRule)
            {
                col.GammaStates.Add(state);
                return;
            }

            var startColumn = state.StartColumn;
            var completedSyntacticCategory = state.Rule.LeftHandSide;

            var predecessorStates = startColumn.StatesWithNextSyntacticCategory[completedSyntacticCategory];

            foreach (var st in predecessorStates)
            {
                if (state.Node.LogProbability < 0)
                {
                    throw new LogException(
                        string.Format(
                            "trrrr! NODE log probability lower than 0: {0}, reductor state: {1}, predecessor state {2}",
                            state.Node.LogProbability, state, st));
                }

                var y = EarleyState.MakeNode(st, state.EndColumn.Index, state.Node);
                var newState = new EarleyState(st.Rule, st.DotIndex + 1, st.StartColumn, y);

                col.AddState(newState);
                if (Debug)
                    Console.WriteLine("{0} & {1} & {2} & Completed from States {3} and {4}\\\\",
                        newState.StateNumber,
                        newState, col.Index, st.StateNumber, state.StateNumber);
                
            }
        }

        private void TestForTooManyStatesInColumn(int count, bool debug)
        {
            if (count > 10000 && !debug)
            {
                Console.WriteLine("More than 10000 states in a single column. Suspicious. Grammar is : {0}",
                    Grammar);
                Debug = true;
                throw new Exception("Grammar with infinite parse. abort this grammar..");
            }
        }

        public string[] GenerateSentences(int numberOfSentences)
        {
            var sentences = new string[numberOfSentences];
            for (var i = 0; i < numberOfSentences; i++)
            {
                EarleyNode n = null;
                while (n == null)
                {
                    try
                    {
                        n = ParseSentence(null)[0];
                    }
                    catch (Exception e)
                    {
                        n = null;
                    }
                }

                sentences[i] = n.GetTerminalStringUnderNode();
            }

            return sentences;
        }
        
        public List<EarleyNode> ParseSentence(string text)
        {
            string[] arr;
            arr = text.Split();

            //check below that the text appears in the vocabulary
            if (arr.Any(str => !voc.ContainsWord(str)))
                throw new Exception("word in text does not appear in the vocabulary.");

            var table = new EarleyColumn[arr.Length + 1];

            for (var i = 1; i < table.Length; i++)
                table[i] = new EarleyColumn(i, arr[i - 1]);

            table[0] = new EarleyColumn(0, "");

            EarleyState.stateCounter = 0;

            var startGrammarRule = new Rule(GammaRule, new[] { "START" });
            var startRule = new Rule(startGrammarRule);
            AddRule(startGrammarRule);

            var startState = new EarleyState(startRule, 0, table[0], null);
            startState.LogProbability = 0.0f;
            table[0].AddState(startState);
            var finalColumn = table[table.Length - 1];
            try
            {
                foreach (var col in table)
                {
                    var count = 0;

                    //1. complete
                    count = TraverseCompletedStates(col, count);

                    //2. predict after complete:
                    count = TraversePredictableStates(col, count);

                    //3. scan after predict.
                    TraverseScannableStates(table, col);
                }

                var nodes = finalColumn.GammaStates.Select(x => x.Node.Children[0]).ToList();
                return nodes;

            }
            catch (LogException e)
            {
                var s = e.ToString();
                Console.WriteLine(s);
                Console.WriteLine(string.Format("sentence: {0}, grammar: {1}", text, Grammar));
            }

            catch (Exception e)
            {
                var s = e.ToString();
                Console.WriteLine(s);
            }


            throw new Exception("Parsing Failed!");

        }

        private void TraverseScannableStates(EarleyColumn[] table, EarleyColumn col)
        {
            if (col.Index + 1 >= table.Length) return;


            var nextScannableTerm = table[col.Index + 1].Token;

            var possibleSyntacticCategories = voc[nextScannableTerm];

            foreach (var item in possibleSyntacticCategories)
            {
                var currentCategory = new DerivedCategory(item);

                if (col.StatesWithNextSyntacticCategory.ContainsKey(currentCategory))
                {
                    foreach (var state in col.StatesWithNextSyntacticCategory[currentCategory])
                    {
                        Scan(table[col.Index + 1], state, currentCategory, nextScannableTerm);
                    }
                }
            }
                
        }

        private int TraversePredictableStates(EarleyColumn col, int count)
        {

            while (col.CategoriesToPredict.Any())
            {

                var nextTerm = col.CategoriesToPredict.Dequeue();

                if (col.ActionableCompleteStates.Any())
                    throw new Exception(
                        "completed states queue should always be empty while processing predicted states.");
                count++;
                TestForTooManyStatesInColumn(count, Debug);

                if (!rules.ContainsKey(nextTerm))
                {
                    var newSynCat = new SyntacticCategory(nextTerm);

                    if (!grammarRules.ContainsKey(newSynCat)) return count;

                    var grammarRuleList = grammarRules[newSynCat];

                    if (grammarRuleList != null)
                    {
                        foreach (var item in grammarRuleList)
                        {
                            var derivedRule = GenerateRule(item, nextTerm);
                            AddRule(derivedRule);
                        }
                    }

                }

                if (!rules.ContainsKey(nextTerm)) return count;

                var ruleList = rules[nextTerm];
                Predict(col, ruleList);


            }

            return count;
        }

        private int TraverseCompletedStates(EarleyColumn col, int count)
        {
            while (col.ActionableCompleteStates.Any())
            {
                count++;
                TestForTooManyStatesInColumn(count, Debug);

                var completedStatesQueueKey = col.ActionableCompleteStates.First().Key;
                var completedStatesQueue = col.ActionableCompleteStates.First().Value;

                var state = completedStatesQueue.Dequeue();

                if (!completedStatesQueue.Any())
                    col.ActionableCompleteStates.Remove(completedStatesQueueKey);

                Complete(col, state);
            }

            return count;
        }
    }
}