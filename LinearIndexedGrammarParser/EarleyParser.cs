using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace LinearIndexedGrammarParser
{
    public class LogException : Exception
    {
        public LogException(string str) : base(str) { }
    }

    public class GenerateException : Exception { }
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
    public class EarleyParser
    {
        private Vocabulary voc;
        private Grammar grammar;

        public EarleyParser()
        {
            voc = Vocabulary.GetVocabularyFromFile(@"Vocabulary.json");
            grammar = new Grammar();
        }


        private Rule GenerateRule(Rule grammarRule, DerivedCategory leftHandSide)
        {
            if (grammarRule.LeftHandSide.Stack == null || grammarRule.LeftHandSide.Stack == string.Empty)
                return null;

            string patternStringLeftHandSide = grammarRule.LeftHandSide.Stack;

            //1. make the pattern be your Syntactic Category
            //2. then find the stack contents - anything by "*" (the first group)
            var newRule = new Rule(grammarRule);
            string patternString = patternStringLeftHandSide.Replace("*", "(.*)");

            Regex pattern = new Regex(patternString);

            string textToMatch = leftHandSide.Stack;
            Match match = pattern.Match(textToMatch);
            if (!match.Success) return null;

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

        public void AddStaticRule(Rule r)
        {
            if (r == null) return;

            Grammar.ruleCounter++;
            var newRule = new Rule(r);
            newRule.Number = Grammar.ruleCounter;

            if (!grammar.staticRules.ContainsKey(newRule.LeftHandSide))
                grammar.staticRules[newRule.LeftHandSide] = new List<Rule>();

            grammar.staticRules[newRule.LeftHandSide].Add(newRule);

            //TODO: calculate the transitive closure of all nullable symbols.
            //at the moment you calculate only the rules that directly lead to epsilon.
            //For instance. C -> D E, D -> epsilon, E-> epsilon. C is not in itself an epsilon rule
            //yet it is a nullable production.
            if (newRule.RightHandSide[0].IsEpsilon())
                grammar.nullableCategories.Add(newRule.LeftHandSide);
        }

        public void AddGrammarRule(Rule r)
        {
            var newRule = new Rule(r);

            //if non-empty stack
            if (newRule.LeftHandSide.Stack != null)
            {
                var stackContents = newRule.LeftHandSide.Stack;
                //and if the left hand side allows manipulating the stack (has the wildcard)
                //insert into the stackManipulationRules dictionary.
                if (stackContents.Contains("*"))
                {

                    var newSynCat = new SyntacticCategory(newRule.LeftHandSide);
                    if (!grammar.dynamicRules.ContainsKey(newSynCat))
                        grammar.dynamicRules[newSynCat] = new List<Rule>();

                    grammar.dynamicRules[newSynCat].Add(newRule);

                    var emptyStackRule = new DerivedCategory(newSynCat.ToString());
                    //generate base form of the rule with the empty stack
                    //as a starting point of the grammar (= equal to context free case)
                    grammar.staticRulesGeneratedForCategory.Add(emptyStackRule);
                    var derivedRule = GenerateRule(newRule, emptyStackRule);
                    if (derivedRule != null)
                        AddStaticRule(derivedRule);


                }
                else
                {
                    grammar.staticRulesGeneratedForCategory.Add(newRule.LeftHandSide);
                    AddStaticRule(newRule);
                }
            }
            else
            {
                grammar.staticRulesGeneratedForCategory.Add(newRule.LeftHandSide);
                AddStaticRule(newRule);
            }
        }

        private void Predict(EarleyColumn col, List<Rule> ruleList)
        {
            foreach (var rule in ruleList)
            {
                var newState = new EarleyState(rule, 0, col, null);
                col.AddState(newState, grammar);
            }
        }

        private void Scan(EarleyColumn col, EarleyState state, SyntacticCategory term, string token)
        {
            var v = new EarleyNode(term.ToString(), col.Index - 1, col.Index)
            {
                AssociatedTerminal = token
            };
            var y = EarleyState.MakeNode(state, col.Index, v);
            var newState = new EarleyState(state.Rule, state.DotIndex + 1, state.StartColumn, y);

            col.AddState(newState, grammar);
        }

        private void Complete(EarleyColumn col, EarleyState state)
        {
            if (state.Rule.LeftHandSide.ToString() == Grammar.GammaRule)
            {
                col.GammaStates.Add(state);
                return;
            }

            var startColumn = state.StartColumn;
            var completedSyntacticCategory = state.Rule.LeftHandSide;
            var predecessorStates = startColumn.StatesWithNextSyntacticCategory[completedSyntacticCategory];

            foreach (var st in predecessorStates)
            {
                var y = EarleyState.MakeNode(st, state.EndColumn.Index, state.Node);
                var newState = new EarleyState(st.Rule, st.DotIndex + 1, st.StartColumn, y);
                col.AddState(newState, grammar);       
            }
        }

        private void TestForTooManyStatesInColumn(int count)
        {
            if (count > 10000)
            {
                Console.WriteLine("More than 10000 states in a single column. Suspicious. Grammar is : {0}",
                    grammar);
                throw new Exception("Grammar with infinite parse. abort this grammar..");
            }
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

            var startGrammarRule = new Rule(Grammar.GammaRule, new[] { Grammar.StartRule });
            var startRule = new Rule(startGrammarRule);
            AddStaticRule(startGrammarRule);

            var startState = new EarleyState(startRule, 0, table[0], null);
            table[0].AddState(startState, grammar);
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
                Console.WriteLine(string.Format("sentence: {0}, grammar: {1}", text, grammar));
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
                        Scan(table[col.Index + 1], state, currentCategory, nextScannableTerm);
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
                TestForTooManyStatesInColumn(count);

                //if static rules have not been generated for this term yet
                //compute them from dynamaic rules dictionary
                if (!grammar.staticRulesGeneratedForCategory.Contains(nextTerm))
                {
                    grammar.staticRulesGeneratedForCategory.Add(nextTerm);
                    var baseSyntacticCategory = new SyntacticCategory(nextTerm);

                    if (grammar.dynamicRules.ContainsKey(baseSyntacticCategory))
                    {
                        var grammarRuleList = grammar.dynamicRules[baseSyntacticCategory];
                        foreach (var item in grammarRuleList)
                        {
                            var derivedRule = GenerateRule(item, nextTerm);
                            AddStaticRule(derivedRule);
                        }
                    }
                }

                if (!grammar.staticRules.ContainsKey(nextTerm)) continue;

                var ruleList = grammar.staticRules[nextTerm];
                Predict(col, ruleList);
            }

            return count;
        }

        private int TraverseCompletedStates(EarleyColumn col, int count)
        {
            while (col.ActionableCompleteStates.Any())
            {
                count++;
                TestForTooManyStatesInColumn(count);

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