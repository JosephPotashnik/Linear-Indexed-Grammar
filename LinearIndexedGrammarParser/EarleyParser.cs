using System;
using System.Collections.Generic;
using System.Linq;

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
        private readonly Dictionary<int, double> ruleLogProbabilities;
        private readonly Dictionary<SyntacticCategory, List<DerivedRule>> rules = new Dictionary<SyntacticCategory, List<DerivedRule>>();
        private bool generator;
        static int ruleCounter = 0;
        private Vocabulary voc;

        public Earleyarser(bool debug = false)
        {
            Debug = debug;
            voc = Vocabulary.GetVocabularyFromFile(@"Vocabulary.json");

            //ruleLogProbabilities = Grammar.GetLogProbabilitiesOfRules();
        }


        public void AddDerivedRule(GrammarRule r)
        {
            ruleCounter++;
            var newRule = new DerivedRule(r);
            newRule.Number = ruleCounter;

            if (!rules.ContainsKey(newRule.LeftHandSide))
                rules[newRule.LeftHandSide] = new List<DerivedRule>();

            rules[newRule.LeftHandSide].Add(newRule);
        }
        public bool Debug { get; set; }

        public Grammar Grammar { get; set; }

        private void Predict(EarleyColumn col, List<DerivedRule> ruleList)
        {

            if (generator)
            {
                //Rule rule = null;

                //var l = Grammar.Rules[currObject.NonTerminal];
                //var feasibleRules = new List<Rule>();

                ////when generating, predict only rules that terminate the derivation successfully.
                //foreach (var candidate in l)
                //{
                //    var c = IsPredictedRuleConsistentWithCurrentDerivation(currObject, candidate);
                //    if (c == RuleConsistentWithDerivation.SkipGeneration) return;

                //    //temporary: do not push another symbol if the current stack already contains a symbol
                //    //in other words: allow only one symbol. 
                //    var complementPositionObject = candidate.Production[candidate.ComplementPosition];
                //    var isPushRule = !complementPositionObject.IsStackEmpty() &&
                //                     complementPositionObject.Stack.Top != Grammar.Epsilon;


                //    if (!currObject.IsStackEmpty() && isPushRule) continue;

                //    if (c == RuleConsistentWithDerivation.RuleConsistent)
                //        feasibleRules.Add(candidate);
                //}

                //if (feasibleRules.Count == 0) //no feasible rules to predict
                //    return;

                //rule = Grammar.GetRandomRuleForAGivenLHS(currObject.NonTerminal, feasibleRules);
                //ruleList = new List<Rule> { rule };
            }

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
            var v = new EarleyNode(term.Symbol, col.Index - 1, col.Index)
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
            if (state.Rule.LeftHandSide.Symbol == GammaRule)
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
                        n = ParseSentence(null);
                    }
                    catch (Exception e)
                    {
                        n = null;
                    }
                }

                sentences[i] = n.GetTerminalStringUnderNode();
            }
            //var y = (float)sentences.Select(x => x.Length).Sum() / numberOfSentences;
            //Console.WriteLine($"average characters in generated sentence: {y}");
            return sentences;
        }

        //private HashSet<NonTerminalObject> GenerateSetOfPredictions(int maxElementsInStack = 2)
        //{
        //    Queue<NonTerminalObject> queue = new Queue<NonTerminalObject>();
        //    HashSet<NonTerminalObject> visitedNonTerminalObjects = new HashSet<NonTerminalObject>(new NonTerminalObjectStackComparer());

        //    NonTerminalObject o = null;

        //    foreach (var moveable in Grammar.Moveables)
        //    {
        //        o = new NonTerminalObject(moveable);
        //        o.Stack = new NonTerminalStack(moveable);
        //        queue.Enqueue(o);
        //    }

        //    while (queue.Any())
        //    {
        //        var rhs = queue.Dequeue();
        //        visitedNonTerminalObjects.Add(rhs);

        //        var rulesforRHS = Grammar.RHSDictionary[rhs.NonTerminal].ToList();
        //        foreach (var item in rulesforRHS)
        //        {
        //            o = null;
        //            var rule = Grammar.ruleNumberDictionary[item.Item1];
        //            int RHSPosition = item.Item2;
        //            if (rule.Name.NonTerminal == Grammar.StartSymbol) continue;

        //            if (rule.IsInitialRule())
        //            {
        //                if (rule.ComplementPosition == RHSPosition)
        //                {
        //                    o = new NonTerminalObject(rule.Name.NonTerminal);
        //                    o.Stack = new NonTerminalStack(rhs.Stack);
        //                    if (!visitedNonTerminalObjects.Contains(o))
        //                        queue.Enqueue(o);
        //                }
        //            }
        //            else
        //            {
        //                NonTerminalStack contentOfDot;

        //                var s = rule.Production[RHSPosition].Stack;
        //                if (s != null)
        //                {
        //                    if (s.Peek() == ".")
        //                        contentOfDot = rhs.Stack;
        //                    else if (s.PrefixList != null)
        //                        contentOfDot = rhs.Stack.GetPrefixListStackObjectOfGivenTop(rhs.Stack.Peek());
        //                    else
        //                        continue;
        //                    //assumption: if this is a secondary constituent (i.e, s.PrefixList == null), does not participate in the sharing of stacks,
        //                    //the LHS will be handled through the primary constituent (the distinguished descendant).


        //                    o = new NonTerminalObject(rule.Name.NonTerminal);
        //                    if (rule.Name.Stack.Peek() == ".")
        //                    {
        //                        if (contentOfDot != null)
        //                            o.Stack = new NonTerminalStack(contentOfDot);
        //                    }
        //                    else
        //                    {
        //                        if (contentOfDot == null || contentOfDot.Depth() < maxElementsInStack)
        //                            o.Stack = new NonTerminalStack(rule.Name.Stack.Peek(), contentOfDot);
        //                    }
        //                    if (o.Stack != null && !visitedNonTerminalObjects.Contains(o))
        //                        queue.Enqueue(o);
        //                }

        //            }

        //        }
        //    }

        //    return visitedNonTerminalObjects;
        //}

        public EarleyNode ParseSentence(string text)
        {
            string[] arr;
            if (text == null)
            {
                generator = true;
                arr = Enumerable.Repeat("", 100).ToArray();
            }
            else
                arr = text.Split();


            //check below that the text appears in the vocabulary
            if (!generator && arr.Any(str => !voc.ContainsWord(str)))
                throw new Exception("word in text does not appear in the vocabulary.");

            var table = new EarleyColumn[arr.Length + 1];

            for (var i = 1; i < table.Length; i++)
                table[i] = new EarleyColumn(i, arr[i - 1]);

            table[0] = new EarleyColumn(0, "");

            EarleyState.stateCounter = 0;

            var startGrammarRule = new GrammarRule(GammaRule, new[] { "START" }, 0, 0);
            var startRule = new DerivedRule(startGrammarRule);
            AddDerivedRule(startGrammarRule);

            var startState = new EarleyState(startRule, 0, table[0], null);
            startState.LogProbability = 0.0f;
            table[0].AddState(startState);
            var finalColumn = table[table.Length - 1];
            try
            {
                foreach (var col in table)
                {
                    var count = 0;
                    if (generator && !col.StatesWithNextSyntacticCategory.Any())
                    {
                        finalColumn = table[col.Index - 1];
                        break;
                    }

                    //1. complete
                    count = TraverseCompletedStates(col, count);

                    //2. predict after complete:
                    count = TraversePredictableStates(col, count);

                    //3. scan after predict.
                    TraverseScannableStates(table, col);
                }

                foreach (var state in finalColumn.GammaStates)
                    return state.Node.Children[0];
            }
            catch (LogException e)
            {
                var s = e.ToString();
                Console.WriteLine(s);
                Console.WriteLine(string.Format("sentence: {0}, grammar: {1}", text, Grammar));
            }
            catch (GenerateException e)
            {
            }
            catch (Exception e)
            {
                var s = e.ToString();
                Console.WriteLine(s);
            }

            if (!generator)
                throw new Exception("Parsing Failed!");
            throw new Exception("Generating Failed!");
        }

        private void TraverseScannableStates(EarleyColumn[] table, EarleyColumn col)
        {
            if (col.Index + 1 >= table.Length && !generator) return;

            if (!generator)
            {
                var nextScannableTerm = table[col.Index + 1].Token;

                var possibleSyntacticCategories = voc[nextScannableTerm];

                foreach (var item in possibleSyntacticCategories)
                {
                    var currentCategory = new SyntacticCategory(item);

                    if (col.StatesWithNextSyntacticCategory.ContainsKey(currentCategory))
                    {
                        foreach (var state in col.StatesWithNextSyntacticCategory[currentCategory])
                        {
                            Scan(table[col.Index + 1], state, currentCategory, nextScannableTerm);
                        }
                    }
                }
                
            }
            else
            {
                //if (Grammar.Vocabulary.POSWithPossibleWords.ContainsKey(term))
                //{
                //    var ruleList = Grammar[term];
                //    //if the term is a constituent, generate it given some probability. otherwise continue.
                //    if (ruleList != null)
                //    {
                //        if (rand.NextDouble() > ChanceToGenerateConstituent) continue;
                //        if (ruleList[0].IsEpsilonRule())
                //            continue;
                //        //if we generated a predicted epsilon rule for that constituent, don't scan.
                //    }

                //    //selecting random word from vocabulary: (uncomment the next line)
                //    //var index = rand.Next(Vocabulary.POSWithPossibleWords[currentNode.Name].Count);

                //    //always selecting the same word from vocabulary is considerably faster because I do not re-parse the same sentence
                //    //but keep the counts of appearances of the sentence.
                //    //the parse of two sentences with identical sequence of POS is the same - regardless of the actual word selected.

                //    if (table[col.Index + 1].Token == "")
                //    //if the token was already written by a previous scan 
                //    //(for instance NP -> John, NP -> D N, D -> the, "John" was already written before "the")
                //    {
                //        var index = 0;
                //        table[col.Index + 1].Token =
                //            Grammar.Vocabulary.POSWithPossibleWords[term].ElementAt(index);
                //        Scan(table[col.Index + 1], state, term, table[col.Index + 1].Token);
                //    }
                //}
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

                if (!rules.ContainsKey(nextTerm)) return count;

                var ruleList = rules[nextTerm];

                if (ruleList != null)
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

                if (generator)
                    state.LogProbability = 0;

                Complete(col, state);
            }

            return count;
        }
    }
}