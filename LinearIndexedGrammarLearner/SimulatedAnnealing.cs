using LinearIndexedGrammarParser;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LinearIndexedGrammarLearner
{
    public class SimulatedAnnealingParams
    {
        public int NumberOfRestarts { get; set; }
        public int NumberOfNonImprovingIterationsBeforeRestart { get; set; }
        public double InitialTemperature { get; set; }
        public double CoolingFactor { get; set; }
    }


    public class SimulatedAnnealing
    {
        private readonly Learner _learner;
        private readonly GrammarFitnessObjectiveFunction _objectiveFunction;
        private readonly SimulatedAnnealingParams _params;

        public SimulatedAnnealing(Learner l, SimulatedAnnealingParams parameters,
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

            while (currentTemp > finalTemp)
            {
                var previousGrammar = currentGrammar;
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
                    {
                        //uncomment the following line ONLY to check that the differential parser works identically to the from-scratch parser.
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

                }

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


        public (ContextSensitiveGrammar bestGrammar, double bestValue, bool feasible) Run(bool isCFGGrammar,
            ContextSensitiveGrammar initiaGrammar = null)
        {
            var restartCounter = 0;
            double currentValue = 0.0;
            bool feasible = false;
            ContextSensitiveGrammar currentGrammar = null;

            if (_learner._gp == null)
                _learner._gp = new GrammarPermutations(isCFGGrammar);
            
            var bestGrammars = new BestGrammarsQueue();
            var promiscuousGrammar = _learner.CreatePromiscuousGrammar(isCFGGrammar);
            _learner.ParseAllSentencesFromScratch(promiscuousGrammar);
            (var promiscuousValue, var promiscuousFeasible) = _objectiveFunction.Compute(promiscuousGrammar);
            BestGrammarsKey promiscuousKey = new BestGrammarsKey(promiscuousValue, promiscuousFeasible);
            bestGrammars.Insert((promiscuousKey, new ContextSensitiveGrammar(promiscuousGrammar)));

            if (initiaGrammar != null)
            {
                currentGrammar = initiaGrammar;
                _learner.ParseAllSentencesFromScratch(currentGrammar);
                (currentValue, feasible) = _objectiveFunction.Compute(currentGrammar);
                if (feasible)
                {
                    BestGrammarsKey currentKey = new BestGrammarsKey(currentValue, feasible);
                    bestGrammars.Insert((currentKey, new ContextSensitiveGrammar(currentGrammar)));
                }
   
            }
            else
            {
                currentGrammar = promiscuousGrammar;
                currentValue = promiscuousValue;
                feasible = promiscuousFeasible;
            }


            //LogManager.GetCurrentClassLogger().Info($"BEFORE iterations, objective function value {currentValue} (feasible: {feasible})");

            //if current grammar is already optimal on data, no need to learn anything,
            //return immediately.
            if (feasible && _objectiveFunction.IsMaximalValue(currentValue))
                return (currentGrammar, currentValue, feasible);

            var numberOfBestGrammarsToKeep = 20;

            var noImprovementCounter = 0;

            while (restartCounter < _params.NumberOfRestarts)
            {

                (currentGrammar, currentValue, feasible) = RunSingleIteration(currentGrammar, currentValue);
                //LogManager.GetCurrentClassLogger().Info($"iteration {currentIteration}, objective function value {currentValue} (feasible: {feasible})");
                var currentKey = new BestGrammarsKey(currentValue, feasible);

                if (_objectiveFunction.IsMaximalValue(currentValue))
                {
                    LogManager.GetCurrentClassLogger().Info($"Maximual value reached");

                    if (feasible)
                    {
                        _objectiveFunction.PenaltyCoefficient = 1;
                        //LogManager.GetCurrentClassLogger().Info($"Best Grammar so far {currentGrammar}\r\n, probability {currentValue + 1.0}");
                        if (!bestGrammars.ContainsKey(currentKey))
                        {
                            if (bestGrammars.Count > numberOfBestGrammarsToKeep)
                                bestGrammars.RemoveMin();

                            bestGrammars.Insert((currentKey, new ContextSensitiveGrammar(currentGrammar)));
                            //LogManager.GetCurrentClassLogger().Info($"Enqueued MAXIMAL feasible value to best grammars, with key {currentKey}");

                        }
                        break;
                    }
                    else
                        _objectiveFunction.PenaltyCoefficient += 1;
                }

                var smallestBestValue = bestGrammars.FindMinKey();
                if (smallestBestValue.Key < currentKey.Key)
                {
                    if (!bestGrammars.ContainsKey(currentKey))
                    {
                        noImprovementCounter = 0;

                        if (bestGrammars.Count > numberOfBestGrammarsToKeep)
                            bestGrammars.RemoveMin();

                        bestGrammars.Insert((currentKey, new ContextSensitiveGrammar(currentGrammar)));
                        //LogManager.GetCurrentClassLogger().Info($"Enqueued currently Best (NOT MAXIMAL) to best grammars, with key {currentKey}");

                    }
                    else
                        noImprovementCounter++;
                }
                else
                    noImprovementCounter++;

                if (noImprovementCounter >= _params.NumberOfNonImprovingIterationsBeforeRestart)
                {
                    restartCounter++;
                    noImprovementCounter = 0;
                    var item = bestGrammars.Next();
                    currentGrammar = new ContextSensitiveGrammar(item.Item2);
                    currentValue = item.Item1.objectiveFunctionValue;
                    _objectiveFunction.PenaltyCoefficient = 1;

                    //LogManager.GetCurrentClassLogger().Info($"reverting to random previous best grammar");
                    //LogManager.GetCurrentClassLogger().Info($"grammar reverted is: {currentGrammar}\r\n, probability {currentValue}");

                    //refresh parse forest from scratch, because we moved to an arbitrarily far away point
                    //in the hypotheses space.

                    _learner.ParseAllSentencesFromScratch(currentGrammar);
                    //PruneUnusedRules(currentGrammar);
                }
            }

            var max = bestGrammars.FindMax();
            currentValue = max.Item1.objectiveFunctionValue;
            feasible = max.Item1.feasible;

            //LogManager.GetCurrentClassLogger().Info($"Value of the objective function of the last key in best grammars: {currentValue} and its feasibility { bestGrammars.Last().Key.feasible} ");
            currentGrammar = new ContextSensitiveGrammar(max.Item2);

            return (currentGrammar, currentValue, feasible);
        }
    }
}