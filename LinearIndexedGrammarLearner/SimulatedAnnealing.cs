using System;
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
        private readonly SimulatedAnnealingParameters _params;

        public SimulatedAnnealing(Learner l, SimulatedAnnealingParameters parameters,
            IObjectiveFunction objectiveFunction)
        {
            _learner = l;
            _params = parameters;
            _objectiveFunction = objectiveFunction;
        }

        private (ContextSensitiveGrammar bestGrammar, double bestValue) DownhillSlideWithGibbs(
            ContextSensitiveGrammar initialGrammar, double initialValue)
        {
            var bestValue = initialValue;
            var bestGrammar = initialGrammar;
            var foundImprovement = true;

            while (foundImprovement)
            {
                foundImprovement = false;
                var rules = bestGrammar.StackConstantRules;

                int rowsCount = ContextSensitiveGrammar.RuleSpace.RowsCount(RuleType.CFGRules);
                
                foreach (var coord in rules)
                {
                    RuleCoordinates bestCoord = null; 
                    var originalGrammar = new ContextSensitiveGrammar(bestGrammar);
                    
                    for (int i = 0; i < rowsCount; i++)
                    {
                        if (coord.LHSIndex == i) continue;

                        var newGrammar = new ContextSensitiveGrammar(originalGrammar);
                        var newCoord = new RuleCoordinates()
                        {
                            LHSIndex = i,
                            RHSIndex = coord.RHSIndex,
                            RuleType = coord.RuleType
                        };

                        // change RHS of existing coordinate:
                        _learner.SetOriginalGrammarBeforePermutation();

                        ChangeRHSCoordinates(newGrammar, coord, newCoord);

                        var newValue = _objectiveFunction.Compute(newGrammar);

                        if (newValue > bestValue)
                        {
                            bestCoord = newCoord;
                            bestGrammar = newGrammar;
                            bestValue = newValue;
                            foundImprovement = true;

                        }
                        _learner.RejectChanges();

                        //for debugging purposes only.
                        //var currentCFHypothesis = new ContextFreeGrammar(originalGrammar);
                        //var allParses1 = _learner.ParseAllSentencesWithDebuggingAssertion(currentCFHypothesis, _learner._sentencesParser);

                    }


                    if (bestCoord != null)
                    {
                        //switch now to best grammar by accepting the changes of the best coordinate.
                        var newGrammar = new ContextSensitiveGrammar(originalGrammar);
                        ChangeRHSCoordinates(newGrammar, coord, bestCoord);
                        _learner.AcceptChanges();
                        bestGrammar = newGrammar;

                        //for debugging purposes only
                        //var currentCFHypothesis = new ContextFreeGrammar(bestGrammar);
                        //var allParses1 = _learner.ParseAllSentencesWithDebuggingAssertion(currentCFHypothesis, _learner._sentencesParser);

                    }
                }
            }

            return (bestGrammar, bestValue);
        }

        private void ChangeRHSCoordinates(ContextSensitiveGrammar newGrammar, RuleCoordinates coord, RuleCoordinates newCoord)
        {
            newGrammar.StackConstantRules.Remove(coord);
            _learner.ReparseWithDeletion(newGrammar,
                ContextSensitiveGrammar.RuleSpace[coord].NumberOfGeneratingRule);

            newGrammar.StackConstantRules.Add(newCoord);
            _learner.ReparseWithAddition(newGrammar,
                ContextSensitiveGrammar.RuleSpace[newCoord].NumberOfGeneratingRule);
        }

        private (ContextSensitiveGrammar bestGrammar, double bestValue) RunSingleIteration(
            ContextSensitiveGrammar initialGrammar, double initialValue)
        {
            var currentTemp = _params.InitialTemperature;
            var currentValue = initialValue;
            var currentGrammar = initialGrammar;
            var rejectCounter = 0;
            //bool reparsed = false;
            //int counter = 0;
            while (currentTemp > 0.3)
            {
                var (mutatedGrammar, reparsed) = _learner.GetNeighborAndReparse(currentGrammar);
                if (mutatedGrammar == null || !reparsed) continue;

                currentTemp *= _params.CoolingFactor;
                
                //when debugging a specific scenario, uncomment the two following
                //lines and comment the first two lines above in the while loop.

                //var mutatedGrammar = new ContextSensitiveGrammar(currentGrammar);
                //_learner.SetOriginalGrammarBeforePermutation();
                
                //if (counter == 0)
                //{
                //    counter++;
                //    var rc = new RuleCoordinates()
                //    {
                //        LHSIndex = 4,
                //        RHSIndex = 107
                //    };

                //    mutatedGrammar.StackConstantRules.Add(rc);

                //    var r = ContextSensitiveGrammar.RuleSpace[rc];
                //    Console.WriteLine($"added {r}");
                //    reparsed = _learner.ReparseWithAddition(mutatedGrammar, r.NumberOfGeneratingRule);
                //}
                //else if (counter == 1)
                //{
                //    counter++;
                //    var rc = new RuleCoordinates()
                //    {
                //        LHSIndex = 3,
                //        RHSIndex = 98
                //    };

                //    mutatedGrammar.StackConstantRules.Add(rc);

                //    var r = ContextSensitiveGrammar.RuleSpace[rc];
                //    Console.WriteLine($"added {r}");
                //    reparsed = _learner.ReparseWithAddition(mutatedGrammar, r.NumberOfGeneratingRule);
                //}
                //else if (counter == 2)
                //{
                //    counter++;
                //    var rc = new RuleCoordinates()
                //    {
                //        LHSIndex = 1,
                //        RHSIndex = 94
                //    };

                //    mutatedGrammar.StackConstantRules.Add(rc);

                //    var r = ContextSensitiveGrammar.RuleSpace[rc];
                //    Console.WriteLine($"added {r}");
                //    reparsed = _learner.ReparseWithAddition(mutatedGrammar, r.NumberOfGeneratingRule);
                //}
                //else if (counter == 3)
                //{
                //    counter++;
                //    var rc = new RuleCoordinates()
                //    {
                //        LHSIndex = 5,
                //        RHSIndex = 143
                //    };

                //    mutatedGrammar.StackConstantRules.Add(rc);

                //    var r = ContextSensitiveGrammar.RuleSpace[rc];
                //    Console.WriteLine($"added {r}");
                //    reparsed = _learner.ReparseWithAddition(mutatedGrammar, r.NumberOfGeneratingRule);
                //}
                //else if (counter == 4)
                //{
                //    counter++;
                //    var rc = new RuleCoordinates()
                //    {
                //        LHSIndex = 3,
                //        RHSIndex = 119
                //    };

                //    mutatedGrammar.StackConstantRules.Add(rc);

                //    var r = ContextSensitiveGrammar.RuleSpace[rc];
                //    Console.WriteLine($"added {r}");
                //    reparsed = _learner.ReparseWithAddition(mutatedGrammar, r.NumberOfGeneratingRule);
                //}
                //else if (counter == 5)
                //{
                //    counter++;
                //    var rc = new RuleCoordinates()
                //    {
                //        LHSIndex = 3,
                //        RHSIndex = 138
                //    };

                //    mutatedGrammar.StackConstantRules.Add(rc);

                //    var r = ContextSensitiveGrammar.RuleSpace[rc];
                //    Console.WriteLine($"added {r}");
                //    reparsed = _learner.ReparseWithAddition(mutatedGrammar, r.NumberOfGeneratingRule);
                //}
                //else if (counter == 6)
                //{
                //    counter++;
                //    var rc = new RuleCoordinates()
                //    {
                //        LHSIndex = 1,
                //        RHSIndex = 117
                //    };

                //    mutatedGrammar.StackConstantRules.Add(rc);

                //    var r = ContextSensitiveGrammar.RuleSpace[rc];
                //    Console.WriteLine($"added {r}");
                //    reparsed = _learner.ReparseWithAddition(mutatedGrammar, r.NumberOfGeneratingRule);
                //}


                //else if (counter == 7)
                //{
                //    counter++;
                //    var rc = new RuleCoordinates()
                //    {
                //        LHSIndex = 5,
                //        RHSIndex = 112
                //    };

                //    mutatedGrammar.StackConstantRules.Add(rc);

                //    var r = ContextSensitiveGrammar.RuleSpace[rc];
                //    Console.WriteLine($"added {r}");
                //    reparsed = _learner.ReparseWithAddition(mutatedGrammar, r.NumberOfGeneratingRule);
                //}
                //else if (counter == 8)
                //{
                //    counter++;
                //    var rc = new RuleCoordinates()
                //    {
                //        LHSIndex = 3,
                //        RHSIndex = 146
                //    };

                //    mutatedGrammar.StackConstantRules.Add(rc);

                //    var r = ContextSensitiveGrammar.RuleSpace[rc];
                //    Console.WriteLine($"added {r}");
                //    reparsed = _learner.ReparseWithAddition(mutatedGrammar, r.NumberOfGeneratingRule);
                //}
                //else if (counter == 9)
                //{
                //    counter++;
                //    var rc = new RuleCoordinates()
                //    {
                //        LHSIndex = 0,
                //        RHSIndex = 50
                //    };

                //    mutatedGrammar.StackConstantRules.Add(rc);

                //    var r = ContextSensitiveGrammar.RuleSpace[rc];
                //    Console.WriteLine($"added {r}");
                //    reparsed = _learner.ReparseWithAddition(mutatedGrammar, r.NumberOfGeneratingRule);
                //}
                //else if (counter == 10)//forever)
                //{
                //    counter++;
                //    var rc = new RuleCoordinates()
                //    {
                //        LHSIndex = 0,
                //        RHSIndex = 50
                //    };

                //    mutatedGrammar.StackConstantRules.Remove(rc);

                //    var r = ContextSensitiveGrammar.RuleSpace[rc];
                //    Console.WriteLine($"removed {r}");
                //    reparsed = _learner.ReparseWithDeletion(mutatedGrammar, r.NumberOfGeneratingRule);
                //}

                var newValue = _objectiveFunction.Compute(mutatedGrammar);

                var accept = _objectiveFunction.AcceptNewValue(newValue, currentValue, currentTemp);
                if (accept)
                {
                    rejectCounter = 0;
                    //Console.WriteLine("accepted");
                    currentValue = newValue;
                    currentGrammar = mutatedGrammar;
                    _learner.AcceptChanges();
                    if (_objectiveFunction.IsMaximalValue(currentValue)) break;
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

                //cooling factor 0.999 from temp 10 to temp 0.3 takes 3500 iterations
                //350 rejections consecutively (10% of total)- give up, reheat system.

                if (rejectCounter > 350) break;
            }

            _learner.RefreshParses();
            PruneUnusedRules(currentGrammar);

            var localSearchAfterAnnealing = false;
            if (localSearchAfterAnnealing)
            {

                // do a local search - downhill strictly
                if (!_objectiveFunction.IsMaximalValue(currentValue))
                {
                    (currentGrammar, currentValue) = DownhillSlideWithGibbs(currentGrammar, currentValue);

                    _learner.RefreshParses();
                    PruneUnusedRules(currentGrammar);
                }

            }
            return (currentGrammar, currentValue);
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

        public (ContextSensitiveGrammar bestGrammar, double bestValue) Run(bool isCFGGrammar,
            ContextSensitiveGrammar initiaGrammar = null)
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

            var bestGrammars = new PriorityQueue<double, ContextSensitiveGrammar>();
            var numberOfBestGrammarsToKeep = 10;
            for (var i = 0; i < numberOfBestGrammarsToKeep; i++)
                bestGrammars.Enqueue(currentValue, new ContextSensitiveGrammar(currentGrammar));

            var smallestBestValue = bestGrammars.PeekFirstKey();
            var noImprovemetCounter = 0;

            while (currentIteration++ < _params.NumberOfIterations)
            {
                LogManager.GetCurrentClassLogger().Info($"iteration {currentIteration}, probability {currentValue}");
                (currentGrammar, currentValue) = RunSingleIteration(currentGrammar, currentValue);

                if (_objectiveFunction.IsMaximalValue(currentValue))
                {
                    //SEFI
                    //LogManager.GetCurrentClassLogger().Info($"Best Grammar so far {currentGrammar}\r\n, probability {currentValue}");
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

                //TODO: doesn't work, find out why.
                //if (noImprovemetCounter == 20)
                //{
                //    var rand = new Random();
                //    var randomPastBestGrammar = rand.Next(numberOfBestGrammarsToKeep);
                //    var candidates = bestGrammars.KeyValuePairs.ToArray();
                //    noImprovemetCounter = 0;
                //    currentValue = candidates[randomPastBestGrammar].Item1;
                //    currentGrammar = candidates[randomPastBestGrammar].Item2;
                //    LogManager.GetCurrentClassLogger()
                //        .Info($"reverting to random previous best grammar that has probability {currentValue}");
                //}
            }

            currentValue = bestGrammars.Last().Key;
            currentGrammar = bestGrammars.Last().Value.First();
            return (currentGrammar, currentValue);
        }
    }
}