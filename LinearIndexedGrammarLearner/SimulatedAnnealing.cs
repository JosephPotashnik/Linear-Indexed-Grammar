using LinearIndexedGrammarParser;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
//using System.IO;

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
        private readonly GrammarFitnessObjectiveFunction _objectiveFunction;
        private readonly SimulatedAnnealingParameters _params;

        public SimulatedAnnealing(Learner l, SimulatedAnnealingParameters parameters,
            GrammarFitnessObjectiveFunction objectiveFunction)
        {
            _learner = l;
            _params = parameters;
            _objectiveFunction = objectiveFunction;
        }

        private (ContextSensitiveGrammar bestGrammar, double bestValue, bool bestFeasible) DownhillSlideWithGibbs(
            ContextSensitiveGrammar initialGrammar, double initialValue)
        {
            var bestValue = initialValue;
            var bestGrammar = initialGrammar;
            var foundImprovement = true;
            bool bestFeasible = false;

            while (foundImprovement)
            {
                foundImprovement = false;
                var rules = bestGrammar.StackConstantRules;

                var rowsCount = ContextSensitiveGrammar.RuleSpace.RowsCount(RuleType.CFGRules);

                foreach (var coord in rules)
                {
                    RuleCoordinates bestCoord = null;
                    var originalGrammar = new ContextSensitiveGrammar(bestGrammar);

                    for (var i = 0; i < rowsCount; i++)
                    {
                        if (coord.LHSIndex == i) continue;

                        var newGrammar = new ContextSensitiveGrammar(originalGrammar);
                        var newCoord = new RuleCoordinates
                        {
                            LHSIndex = i,
                            RHSIndex = coord.RHSIndex,
                            RuleType = coord.RuleType
                        };

                        // change RHS of existing coordinate:
                        _learner.SetOriginalGrammarBeforePermutation();

                        ChangeLHSCoordinates(newGrammar, coord, newCoord);

                        (var newValue, var feasible) = _objectiveFunction.Compute(newGrammar);

                        if (newValue > bestValue)
                        {
                            bestCoord = newCoord;
                            bestGrammar = newGrammar;
                            bestValue = newValue;
                            bestFeasible = feasible;
                            foundImprovement = true;
                        }

                        _learner.RejectChanges();

                        //for debugging purposes only.
                        //var currentCFHypothesis = new ContextFreeGrammar(originalGrammar);
                        //var allParses1 = _learner.ParseAllSentencesWithDebuggingAssertion(currentCFHypothesis, _learner._sentencesParser);
                    }


                    if (bestCoord != null)
                    {
                        _learner.SetOriginalGrammarBeforePermutation();

                        //switch now to best grammar by accepting the changes of the best coordinate.
                        var newGrammar = new ContextSensitiveGrammar(originalGrammar);
                        ChangeLHSCoordinates(newGrammar, coord, bestCoord);
                        _learner.AcceptChanges();
                        bestGrammar = newGrammar;

                        //for debugging purposes only
                        //var currentCFHypothesis = new ContextFreeGrammar(bestGrammar);
                        //var allParses1 = _learner.ParseAllSentencesWithDebuggingAssertion(currentCFHypothesis, _learner._sentencesParser);
                    }
                }
            }

            return (bestGrammar, bestValue, bestFeasible);
        }

        private void ChangeLHSCoordinates(ContextSensitiveGrammar newGrammar, RuleCoordinates coord,
            RuleCoordinates newCoord)
        {
            newGrammar.StackConstantRules.Remove(coord);
            _learner.ReparseWithDeletion(newGrammar,
                ContextSensitiveGrammar.RuleSpace[coord].NumberOfGeneratingRule);

            newGrammar.StackConstantRules.Add(newCoord);
            _learner.ReparseWithAddition(newGrammar,
                ContextSensitiveGrammar.RuleSpace[newCoord].NumberOfGeneratingRule);
        }

        private (ContextSensitiveGrammar bestGrammar, double bestValue, bool feasible) RunSingleIteration(
            ContextSensitiveGrammar initialGrammar, double initialValue)
        {
            var currentTemp = _params.InitialTemperature;
            var currentValue = initialValue;
            var currentGrammar = initialGrammar;
            var finalTemp = 0.3;
            var rejectCounter = 0;
            bool feasible = false;
            double newValue = 0.0;
            double percentageOfConsecutiveRejectionsToGiveUp = 0.1;

            var totalIterations = (int)((Math.Log(finalTemp) - Math.Log(_params.InitialTemperature)) / Math.Log(_params.CoolingFactor));
            var numberOfConsecutiveRejectionsToGiveUp = (int)(percentageOfConsecutiveRejectionsToGiveUp * totalIterations);
            //bool reparsed = false;
            //int counter = 0;
            //int historySize = 10;
            //var historyOrig = new ContextSensitiveGrammar[historySize];
            //var historyMutated = new ContextSensitiveGrammar[historySize];
            //var rulesOrig = new List<Rule>[historySize];
            //var rulesMutated = new List<Rule>[historySize];
            //var acceptance = new bool[historySize];

           // int currentHistoryIndex = 0;

            //if generating:
            //historyOrig[currentHistoryIndex] = currentGrammar;


            //if reproducing:
            //read original grammars
            //ReadHistoryGrammarsFile(historySize, historyOrig, historyMutated, acceptance);
            //currentGrammar = historyOrig[8];
            //_learner.ParseAllSentencesFromScratch(currentGrammar);
            //(newValue, feasible) = _objectiveFunction.Compute(currentGrammar);

            //int i = 8;
            //ContextSensitiveGrammar mutatedGrammar = null;
            //bool reparsed = false;
            //bool accept = false;

            while (currentTemp > finalTemp)
            {
                //var rulesOnlyInOrig = historyOrig[i].StackConstantRules.Select(x => ContextSensitiveGrammar.RuleSpace[x]).Except(historyMutated[i].StackConstantRules.Select(x => ContextSensitiveGrammar.RuleSpace[x]));
                //var rulesOnlyInMutated = historyMutated[i].StackConstantRules.Select(x => ContextSensitiveGrammar.RuleSpace[x]).Except(historyOrig[i].StackConstantRules.Select(x => ContextSensitiveGrammar.RuleSpace[x]));

                //if (rulesOnlyInMutated.Count() > 0)
                //{
                //    var rc = ContextSensitiveGrammar.RuleSpace.FindRule(rulesOnlyInMutated.First());
                //    var newGrammar = new ContextSensitiveGrammar(currentGrammar);
                //    _learner.SetOriginalGrammarBeforePermutation();
                //    //mutate the grammar.
                //    (mutatedGrammar, reparsed) = GrammarPermutations.InnerInsertStackConstantRule(newGrammar, _learner, rc);
                //}
                //else
                //{
                //    var rc = ContextSensitiveGrammar.RuleSpace.FindRule(rulesOnlyInOrig.First());
                //    var newGrammar = new ContextSensitiveGrammar(currentGrammar);
                //    _learner.SetOriginalGrammarBeforePermutation();
                //    //mutate the grammar.
                //    (mutatedGrammar, reparsed) = GrammarPermutations.InnerDeleteStackConstantRule(newGrammar, _learner, rc);
                //}

                var previousGrammar = currentGrammar;
                var (mutatedGrammar, reparsed) = _learner.GetNeighborAndReparse(currentGrammar);
                if (mutatedGrammar == null || !reparsed) continue;

                //historyMutated[currentHistoryIndex] = mutatedGrammar;

                currentTemp *= _params.CoolingFactor;
                (newValue, feasible) = _objectiveFunction.Compute(mutatedGrammar);
                var accept = _objectiveFunction.AcceptNewValue(newValue, currentValue, currentTemp);
                //if (i < historySize - 1)
                //    accept = acceptance[i];
                //else
                //    accept = true; //manually read from sessionReport for the grammar that generated the problem.
                //i++;

                if (accept)
                {
                    rejectCounter = 0;
                    //Console.WriteLine("accepted");
                    currentValue = newValue;
                    currentGrammar = mutatedGrammar;
                    _learner.AcceptChanges();
                    if (_objectiveFunction.IsMaximalValue(currentValue))
                    {
                        //currentHistoryIndex++;
                        //if (currentHistoryIndex == historySize)
                        //    currentHistoryIndex = 0;
                        //var currentCFHypothesis2 = new ContextFreeGrammar(currentGrammar);
                        //var previousHypothesis2 = new ContextFreeGrammar(previousGrammar);
                        //var allParses12 = _learner.ParseAllSentencesWithDebuggingAssertion(currentCFHypothesis2, previousHypothesis2, _learner._sentencesParser);
                        break;
                    }
                    

                }
                else
                {
                    rejectCounter++;
                    //Console.WriteLine("rejected");
                    _learner.RejectChanges();
                    //int numberOfSentenceUnParsedDiff = 0;
                    //for (int k = 0; k < _learner._sentencesParser.Length; k++)
                    //{
                    //    if (_learner._sentencesParser[k].BracketedRepresentations.Count == 0)
                    //        numberOfSentenceUnParsedDiff++;
                    //}
                    //bool newfeasible = false;
                    //double newnewValue = 0;
                    //(newnewValue, newfeasible) = _objectiveFunction.Compute(currentGrammar);

                    //_learner.ParseAllSentencesFromScratch(currentGrammar);
                    //int numberOfSentenceUnParsedFromScratch = 0;
                    //for (int k = 0; k < _learner._sentencesParser.Length; k++)
                    //{
                    //    if (_learner._sentencesParser[k].BracketedRepresentations.Count == 0)
                    //        numberOfSentenceUnParsedFromScratch++;
                    //}

                    //(newnewValue, newfeasible) = _objectiveFunction.Compute(currentGrammar);

                }


                //if generating:
                //currentHistoryIndex++;
                //if (currentHistoryIndex == historySize)
                //    currentHistoryIndex = 0;
                //historyOrig[currentHistoryIndex] = currentGrammar;



                //uncomment the following line ONLY to check that the differential parser works identically to the from-scratch parser.
                //var currentCFHypothesis = new ContextFreeGrammar(currentGrammar);
                //var previousHypothesis12 = new ContextFreeGrammar(previousGrammar);
                //var allParses1 = _learner.ParseAllSentencesWithDebuggingAssertion(currentCFHypothesis, previousHypothesis12,_learner._sentencesParser);

                //after a certain number of consecutive rejections, give up, reheat system.
                if (rejectCounter > numberOfConsecutiveRejectionsToGiveUp) break;
            }

            if (_objectiveFunction.IsMaximalValue(currentValue))
            {

                _learner.ParseAllSentencesFromScratch(currentGrammar);
                bool newfeasible = false;
                (newValue, newfeasible) = _objectiveFunction.Compute(currentGrammar);
                if (newValue != currentValue || newfeasible != feasible)
                {
                    LogManager.GetCurrentClassLogger().Info($"BEFORE PRUNING UNUSED RULES: Maximal grammar:  {currentGrammar}\r\n, probability {currentValue + 1.0}");
                    LogManager.GetCurrentClassLogger().Info($"reparsing (debugger), value  {newValue} feasible {newfeasible}");
                    //LogManager.GetCurrentClassLogger().Info($"HISTORY of last {historySize} grammars");
                    //for (int i = 0; i < historySize; i++)
                    //{
                    //    LogManager.GetCurrentClassLogger().Info($"original grammar {i}:");
                    //    LogManager.GetCurrentClassLogger().Info(historyOrig[(i+currentHistoryIndex) % historySize].ToString());
                    //    LogManager.GetCurrentClassLogger().Info($"mutated grammar {i}:");
                    //    LogManager.GetCurrentClassLogger().Info(historyMutated[(i+ currentHistoryIndex) % historySize].ToString());
                    //}

                    throw new Exception("should not happen! means your differential parses are compromised");
                }
            }

           

            PruneUnusedRules(currentGrammar);

            //Downhill slide was found to slow convergence in practice.
            //in the future: perhaps start using the slide only upon
            //burn-in period or higher lagrangian multiplier.
            //var localSearchAfterAnnealing = false;
            //if (localSearchAfterAnnealing)
            //{

            //    // do a local search - strictly downhill 
            //    if (!_objectiveFunction.IsMaximalValue(currentValue))
            //    {
            //        (currentGrammar, currentValue, feasible) = DownhillSlideWithGibbs(currentGrammar, currentValue);

            //        _learner.RefreshParses();
            //        PruneUnusedRules(currentGrammar);
            //    }

            //}

            return (currentGrammar, currentValue, feasible);
        }

        //private static void ReadHistoryGrammarsFile(int historySize, ContextSensitiveGrammar[] historyOrig, ContextSensitiveGrammar[] historyMutated, bool[] acceptance)
        //{
        //    string line;
        //    var separator = '-';

        //    var rules = new List<Rule>();
        //    int i = 0;
        //    using (var file = File.OpenText("HistoryGrammars.txt"))
        //    {
        //        while ((line = file.ReadLine()) != null)
        //        {

        //            if (line[0] == separator)
        //            {
        //                if (i % 2 == 0)
        //                    historyOrig[i / 2] = new ContextSensitiveGrammar(rules);
        //                else
        //                    historyMutated[i / 2] = new ContextSensitiveGrammar(rules);

        //                rules = new List<Rule>();
        //                i++;
        //            }
        //            var r = GrammarFileReader.CreateRule(line);
        //            if (r != null)
        //                rules.Add(r);
        //        }
        //    }
        //    for (i = 0; i < historySize; i++)
        //    {
        //        var rulesOnlyInOrig = historyOrig[i].StackConstantRules.Select(x => ContextSensitiveGrammar.RuleSpace[x]).Except(historyMutated[i].StackConstantRules.Select(x => ContextSensitiveGrammar.RuleSpace[x]));
        //        var rulesOnlyInMutated = historyMutated[i].StackConstantRules.Select(x => ContextSensitiveGrammar.RuleSpace[x]).Except(historyOrig[i].StackConstantRules.Select(x => ContextSensitiveGrammar.RuleSpace[x]));
        //        if (rulesOnlyInMutated.Count() > 1 || rulesOnlyInOrig.Count() > 1)
        //        {
        //            throw new Exception("should not occur. Assumption: Only 2 CFG mutations are Addition and Subtraction");
        //        }
        //    }

        //    for (i = 0; i < historySize - 1; i++)
        //    {
        //        if (!historyOrig[i + 1].Equals(historyOrig[i]) && !historyOrig[i + 1].Equals(historyMutated[i]))
        //        {
        //            throw new Exception("should not happen");
        //        }
        //        acceptance[i] = historyOrig[i + 1].Equals(historyMutated[i]);
        //    }
        //}
        private void PruneUnusedRules(ContextSensitiveGrammar currentGrammar)
        {
            var ruleDistribution = _learner.CollectUsages();
            currentGrammar.PruneUnusedRules(ruleDistribution);
            //after pruning unused rules, parse from scratch in order to remove
            //all resultant unused earley items (i.e, all items using those unused rules
            //that are a part of partial, unsuccessful, derivation)
            _learner.ParseAllSentencesFromScratch(currentGrammar);
        }

        (ContextSensitiveGrammar bestGrammar, double bestValue, bool feasible) Inject()
        {
            var grammarRules = GrammarFileReader.ReadRulesFromFile("DebugGrammar.txt");
            var debugGrammar = new ContextSensitiveGrammar(grammarRules);
            _learner.ParseAllSentencesFromScratch(debugGrammar);
            (var targetProb, var feasible) = _objectiveFunction.Compute(debugGrammar);
            return (debugGrammar, targetProb, feasible);
        }


        public (ContextSensitiveGrammar bestGrammar, double bestValue) Run(bool isCFGGrammar,
            ContextSensitiveGrammar initiaGrammar = null)
        {
            var currentIteration = 0;
            var currentGrammar = initiaGrammar ?? _learner.CreateInitialGrammar(isCFGGrammar);

            //set the parsers to the initial grammar.
            _learner.ParseAllSentencesFromScratch(currentGrammar);

            (var currentValue, var feasible) = _objectiveFunction.Compute(currentGrammar);
            BestGrammarsKey currentKey = new BestGrammarsKey(currentValue, feasible);
            LogManager.GetCurrentClassLogger().Info($"BEFORE iterations, objective function value {currentValue} (feasible: {feasible})");

            //if current grammar is already optimal on data, no need to learn anything,
            //return immediately.
            if (feasible && _objectiveFunction.IsMaximalValue(currentValue))
                return (currentGrammar, currentValue);

            var bestGrammars = new SortedDictionary<BestGrammarsKey, ContextSensitiveGrammar>();
            var numberOfBestGrammarsToKeep = 20;
            bestGrammars.Add(currentKey, new ContextSensitiveGrammar(currentGrammar));
            var smallestBestValue = new BestGrammarsKey(currentKey);

            var noImprovementCounter = 0;
            int randomPastBestGrammar = 0;
            double peakValue = 0;
            while (currentIteration++ < _params.NumberOfIterations)
            {
                //Inject(); //inject debug grammar to study certain grammars behavior for analysis. uncommented only during development.

                (currentGrammar, currentValue, feasible) = RunSingleIteration(currentGrammar, currentValue);
                LogManager.GetCurrentClassLogger().Info($"iteration {currentIteration}, objective function value {currentValue} (feasible: {feasible})");
                currentKey = new BestGrammarsKey(currentValue, feasible);

                if (_objectiveFunction.IsMaximalValue(currentValue))
                {
                    if (feasible)
                    {
                        _objectiveFunction.PenaltyCoefficient = 1;
                        //LogManager.GetCurrentClassLogger().Info($"Best Grammar so far {currentGrammar}\r\n, probability {currentValue + 1.0}");
                        if (!bestGrammars.ContainsKey(currentKey))
                        {
                            if (bestGrammars.Count > numberOfBestGrammarsToKeep)
                                bestGrammars.Remove(bestGrammars.First().Key);
                            
                            bestGrammars.Add(currentKey, new ContextSensitiveGrammar(currentGrammar));
                            smallestBestValue = new BestGrammarsKey(bestGrammars.First().Key);
                            //LogManager.GetCurrentClassLogger().Info($"Enqueued MAXIMAL feasible value to best grammars, size of bestGrammars: {bestGrammars.Count}, smallest Value is {smallestBestValue.Key}");

                        }
                        peakValue = currentKey.Key;
                        break;
                    }
                    else
                        _objectiveFunction.PenaltyCoefficient += 1;
                }

                if (smallestBestValue.Key < currentKey.Key)
                {
                    if (!bestGrammars.ContainsKey(currentKey))
                    {
                        noImprovementCounter = 0;

                        if (bestGrammars.Count > numberOfBestGrammarsToKeep)
                            bestGrammars.Remove(bestGrammars.First().Key);

                        bestGrammars.Add(currentKey, new ContextSensitiveGrammar(currentGrammar));
                        smallestBestValue = new BestGrammarsKey(bestGrammars.First().Key);

                        //LogManager.GetCurrentClassLogger().Info($"Enqueued currently Best (NOT MAXIMAL) to best grammars, size of bestGrammars: {bestGrammars.Count}, smallest Value is {smallestBestValue.Key}");

                    }
                    else
                        noImprovementCounter++;
                }
                else
                    noImprovementCounter++;

                if (noImprovementCounter >= 20)
                {

                    noImprovementCounter = 0;

                    var candidates1 = from i in bestGrammars
                                      select (i.Key, i.Value);
                    var candidates = candidates1.ToArray();
                    randomPastBestGrammar = Pseudorandom.NextInt(candidates.Length);

                    currentValue = candidates[randomPastBestGrammar].Key.objectiveFunctionValue;
                    currentGrammar = new ContextSensitiveGrammar(candidates[randomPastBestGrammar].Value);
                    _objectiveFunction.PenaltyCoefficient = 1;
                    //LogManager.GetCurrentClassLogger().Info($"reverting to random previous best grammar");
                    //LogManager.GetCurrentClassLogger().Info($"grammar reverted is: {currentGrammar}\r\n, probability {currentValue}");

                    //refresh parse forest from scratch, because we moved to an arbitrarily far away point
                    //in the hypotheses space.

                    _learner.ParseAllSentencesFromScratch(currentGrammar);
                    //PruneUnusedRules(currentGrammar);
                }
            }

            currentValue = bestGrammars.Last().Key.objectiveFunctionValue;
            //LogManager.GetCurrentClassLogger().Info($"Value of the objective function of the last key in best grammars: {currentValue} and its feasibility { bestGrammars.Last().Key.feasible} ");

            if (peakValue > 1.0)
            {
                if (peakValue != currentValue + 1.0)
                {
                    throw new Exception($"should not happen, current value is { currentValue } and peak value is {peakValue}");
                }
            }
            currentGrammar = new ContextSensitiveGrammar(bestGrammars[bestGrammars.Last().Key]);

            return (currentGrammar, currentValue);
        }
    }
}