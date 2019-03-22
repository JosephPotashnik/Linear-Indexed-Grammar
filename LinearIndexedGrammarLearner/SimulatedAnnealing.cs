using System;
using System.Collections.Generic;
using System.Linq;
using LinearIndexedGrammarParser;
using Newtonsoft.Json;
using NLog;

namespace LinearIndexedGrammarLearner
{
    [JsonObject(MemberSerialization.OptIn)]
    public class SimulatedAnnealingParameters
    {
        [JsonProperty] public int NumberOfIterations { get; set; }
        [JsonProperty] public double InitialTemperature { get; set; }
        [JsonProperty] public double CoolingFactor { get; set; }
    }

    public class SimulatedAnnealing
    {
        private readonly Learner _learner;
        private readonly IObjectiveFunction _objectiveFunction;
        private SimulatedAnnealingParameters _params;

        public SimulatedAnnealing(Learner l, SimulatedAnnealingParameters parameters,
            IObjectiveFunction objectiveFunction)
        {
            _learner = l;
            _params = parameters;
            _objectiveFunction = objectiveFunction;
        }

        private (ContextSensitiveGrammar bestGrammar, double bestValue) RunSingleIteration(
            ContextSensitiveGrammar initialGrammar, double initialValue)
        {
            var currentTemp = _params.InitialTemperature;
            var currentValue = initialValue;
            var currentGrammar = initialGrammar;

            while (currentTemp > 0.3)
            {
            
                var mutatedGrammar = _learner.GetNeighbor(currentGrammar);
                currentTemp *= _params.CoolingFactor;
                if (mutatedGrammar == null) continue;

                var addedRule = mutatedGrammar.StackConstantRules.Except(currentGrammar.StackConstantRules);
                var removedRule = currentGrammar.StackConstantRules.Except(mutatedGrammar.StackConstantRules);
                bool reparsed = false;
                //string parstr = string.Empty;
                if (addedRule.Any())
                {
                    var rr = ContextSensitiveGrammar.RuleSpace[addedRule.First()];

                    var lhs = new SyntacticCategory(rr.LeftHandSide);
                    var r = ContextFreeGrammar.GenerateStaticRuleFromDynamicRule(rr,
                        new DerivedCategory(lhs.ToString()));
                    //Console.WriteLine($" added {r}");
                    r.NumberOfGeneratingRule = rr.Number;
                    //for (int i = 0; i < _learner._sentencesParser.Length; i++)
                    //    parstr += _learner._sentencesParser[i].ToString();
                    reparsed = _learner.ReparseWithAddition(mutatedGrammar, r);

                }
                else
                {
                    var rr = ContextSensitiveGrammar.RuleSpace[removedRule.First()];
                    var lhs = new SyntacticCategory(rr.LeftHandSide);
                    var r = ContextFreeGrammar.GenerateStaticRuleFromDynamicRule(rr,
                        new DerivedCategory(lhs.ToString()));
                    //Console.WriteLine($" removed {r}");
                    r.NumberOfGeneratingRule = rr.Number;
                    //for (int i = 0; i < _learner._sentencesParser.Length; i++)
                    //    parstr += _learner._sentencesParser[i].ToString();
                    reparsed = _learner.ReparseWithDeletion(mutatedGrammar, r);

                }

                if (reparsed == false) continue;

                var newValue = _objectiveFunction.Compute(mutatedGrammar);
                //if (counter++ % 100 == 0)
                //    LogManager.GetCurrentClassLogger().Info($"currentTemp {currentTemp}, probability {newValue}");
            
                var accept = _objectiveFunction.AcceptNewValue(newValue, currentValue, currentTemp);
                if (accept)
                {
                    //Console.WriteLine("accepted");
                    currentValue = newValue;
                    currentGrammar = mutatedGrammar;
                    _learner.AcceptChanges();

                    if (_objectiveFunction.IsMaximalValue(currentValue)) break;
                }
                else
                {
                    //Console.WriteLine("rejected");

                    _learner.RejectChanges();

                    //var parstr1 = string.Empty;

                    //for (int i = 0; i < _learner._sentencesParser.Length; i++)
                    //    parstr1 += _learner._sentencesParser[i].ToString();

                    //if (parstr != parstr1)
                    //{
                    //    throw new Exception("parser representation after rejected hyptohesis is not the same as original");
                    //}

                }
            }
            _learner.RefreshParses();
            var ruleDistribution = _learner.CollectUsages();
            currentGrammar.PruneUnusedRules(ruleDistribution);
            //after pruning unused rules, parse from scratch in order to remove
            //all resultant unused earley items (i.e, all items using those unused rules
            //that are a part of partial, unsuccessful, derivation)
            _learner.ParseAllSentencesFromScratch(currentGrammar);
            return (currentGrammar, currentValue);
        }

        public (ContextSensitiveGrammar bestGrammar, double bestValue) Run(bool isCFGGrammar, ContextSensitiveGrammar initiaGrammar = null)
        {
            var currentIteration = 0;
            var currentGrammar = initiaGrammar ?? _learner.CreateInitialGrammar(isCFGGrammar);

            //set the parsers to the initial grammar.
            _learner.ParseAllSentencesFromScratch(currentGrammar);


            var currentValue = _objectiveFunction.Compute(currentGrammar);

            //if current grammar is already optimal on data, no need to learn anything,
            //return immediately.
            if (_objectiveFunction.IsMaximalValue(currentValue))
                return (currentGrammar, currentValue);

            PriorityQueue<double, ContextSensitiveGrammar> bestGrammars = new PriorityQueue<double, ContextSensitiveGrammar>();
            int numberOfBestGrammarsToKeep = 10;
            for (int i = 0; i < numberOfBestGrammarsToKeep; i++)
                bestGrammars.Enqueue(currentValue, new ContextSensitiveGrammar(currentGrammar));

            double smallestBestValue = bestGrammars.PeekFirstKey();
            int noImprovemetCounter = 0;

            while (currentIteration++ < _params.NumberOfIterations)
            {

                LogManager.GetCurrentClassLogger().Info($"iteration {currentIteration}, probability {currentValue}");
                (currentGrammar, currentValue) = RunSingleIteration(currentGrammar, currentValue);

                if (_objectiveFunction.IsMaximalValue(currentValue))
                {
                    bestGrammars.Enqueue(currentValue, currentGrammar);
                    break;
                }

                if (smallestBestValue < currentValue)
                {
                    noImprovemetCounter = 0;
                    bestGrammars.Dequeue();
                    bestGrammars.Enqueue(currentValue, currentGrammar);
                    smallestBestValue = bestGrammars.PeekFirstKey();

                }
                else
                {
                    noImprovemetCounter++;
                }

                if (noImprovemetCounter == 50)
                {
                    var rand = new Random();
                    int randomPastBestGrammar = rand.Next(numberOfBestGrammarsToKeep);
                    var candidates = bestGrammars.KeyValuePairs.ToArray();
                    noImprovemetCounter = 0;
                    currentValue = candidates[randomPastBestGrammar].Item1;
                    currentGrammar = candidates[randomPastBestGrammar].Item2;
                    LogManager.GetCurrentClassLogger().Info($"reverting to random previous best grammar that has probability {currentValue}");

                }
            }

            currentValue = bestGrammars.Last().Key;
            currentGrammar = bestGrammars.Last().Value.First();
            return (currentGrammar, currentValue);
        }
    }
}