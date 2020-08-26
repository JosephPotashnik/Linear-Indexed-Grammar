﻿using LinearIndexedGrammarParser;
using Newtonsoft.Json;
using NLog;
using System;
using System.Linq;

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
            while (currentTemp > finalTemp)
            {
                var (mutatedGrammar, reparsed) = _learner.GetNeighborAndReparse(currentGrammar);
                if (mutatedGrammar == null || !reparsed) continue;

                currentTemp *= _params.CoolingFactor;
                (newValue, feasible) = _objectiveFunction.Compute(mutatedGrammar);

                var accept = _objectiveFunction.AcceptNewValue(newValue, currentValue, currentTemp);
                if (accept)
                {
                    rejectCounter = 0;
                    //Console.WriteLine("accepted");
                    currentValue = newValue;
                    currentGrammar = mutatedGrammar;
                    _learner.AcceptChanges();
                    if (_objectiveFunction.IsMaximalValue(currentValue))
                            break;
                    
                }
                else
                {
                    rejectCounter++;
                    //Console.WriteLine("rejected");
                    _learner.RejectChanges();
                }

                //uncomment the following line ONLY to check that the differential parser works identically to the from-scratch parser.
                //var currentCFHypothesis = new ContextFreeGrammar(currentGrammar);
                //var allParses1 = _learner.ParseAllSentencesWithDebuggingAssertion(currentCFHypothesis, _learner._sentencesParser);

                //after a certain number of consecutive rejections, give up, reheat system.
                if (rejectCounter > numberOfConsecutiveRejectionsToGiveUp) break;
            }

            _learner.RefreshParses();
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

        private void PruneUnusedRules(ContextSensitiveGrammar currentGrammar)
        {
            var ruleDistribution = _learner.CollectUsages();
            currentGrammar.PruneUnusedRules(ruleDistribution);
            //after pruning unused rules, parse from scratch in order to remove
            //all resultant unused earley items (i.e, all items using those unused rules
            //that are a part of partial, unsuccessful, derivation)
            _learner.ParseAllSentencesFromScratch(currentGrammar);
        }

        //(ContextSensitiveGrammar bestGrammar, double bestValue, bool feasible) Inject()
        //{
        //                var grammarRules = GrammarFileReader.ReadRulesFromFile("DebugGrammar.txt");
        //var debugGrammar = new ContextSensitiveGrammar(grammarRules);
        //_learner.ParseAllSentencesFromScratch(debugGrammar);
        //                (var targetProb, var feasible) = _objectiveFunction.Compute(debugGrammar);
        //                return (debugGrammar, targetProb, feasible);
        // }


    public (ContextSensitiveGrammar bestGrammar, double bestValue) Run(bool isCFGGrammar,
            ContextSensitiveGrammar initiaGrammar = null)
        {
            var currentIteration = 0;
            var currentGrammar = initiaGrammar ?? _learner.CreateInitialGrammar(isCFGGrammar);

            //set the parsers to the initial grammar.
            _learner.ParseAllSentencesFromScratch(currentGrammar);

            (var currentValue, var feasible) = _objectiveFunction.Compute(currentGrammar);

            //if current grammar is already optimal on data, no need to learn anything,
            //return immediately.
            if (feasible && _objectiveFunction.IsMaximalValue(currentValue))
                return (currentGrammar, currentValue);

            var bestGrammars = new PriorityQueue<double, ContextSensitiveGrammar>();
            var numberOfBestGrammarsToKeep = 20;
            for (var i = 0; i < numberOfBestGrammarsToKeep; i++)
                bestGrammars.Enqueue(currentValue, new ContextSensitiveGrammar(currentGrammar));

            var smallestBestValue = bestGrammars.PeekFirstKey();
            var noImprovemetCounter = 0;
            int randomPastBestGrammar = 0;
            while (currentIteration++ < _params.NumberOfIterations)
            {
                (currentGrammar, currentValue, feasible) = RunSingleIteration(currentGrammar, currentValue);
                LogManager.GetCurrentClassLogger().Info($"iteration {currentIteration}, probability {currentValue} (feasible: {feasible})");

                if (_objectiveFunction.IsMaximalValue(currentValue))
                {
                    if (feasible)
                    {
                        _objectiveFunction.PenaltyCoefficient = 1;
                        //LogManager.GetCurrentClassLogger().Info($"Best Grammar so far {currentGrammar}\r\n, probability {currentValue}");
                        bestGrammars.Enqueue(currentValue, new ContextSensitiveGrammar(currentGrammar));
                        break;
                    }
                    else
                        _objectiveFunction.PenaltyCoefficient += 1;

                }

                if (smallestBestValue < currentValue)
                {
                    noImprovemetCounter = 0;
                    //LogManager.GetCurrentClassLogger().Info($"Enqueuing to best grammars {currentGrammar}\r\n, probability {currentValue}");

                    bestGrammars.Dequeue();
                    bestGrammars.Enqueue(currentValue, new ContextSensitiveGrammar(currentGrammar));
                    smallestBestValue = bestGrammars.PeekFirstKey();
                }
                else
                {
                    noImprovemetCounter++;
                }

                if (noImprovemetCounter == 20)
                { 
                    randomPastBestGrammar = Pseudorandom.NextInt(numberOfBestGrammarsToKeep);
                    var candidates = bestGrammars.KeyValuePairs.ToArray();
                    noImprovemetCounter = 0;
                    currentValue = candidates[randomPastBestGrammar].Item1;
                    currentGrammar = candidates[randomPastBestGrammar].Item2;
                    _objectiveFunction.PenaltyCoefficient = 1;
                    //LogManager.GetCurrentClassLogger()
                    //    .Info($"reverting to random previous best grammar");
                    //LogManager.GetCurrentClassLogger().Info($"grammar reverted is: {currentGrammar}\r\n, probability {currentValue}");

                    //refresh parse forest from scratch, because we moved to an arbitrarily far away point
                    //in the hypotheses space.
                    _learner.ParseAllSentencesFromScratch(currentGrammar);
                }
            }

            currentValue = bestGrammars.Last().Key;
            currentGrammar = bestGrammars.Last().Value.First();
            return (currentGrammar, currentValue);
        }
    }
}