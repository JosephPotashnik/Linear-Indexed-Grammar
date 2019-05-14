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

        private (ContextSensitiveGrammar bestGrammar, double bestValue) RunSingleIteration(
            ContextSensitiveGrammar initialGrammar, double initialValue)
        {
            var currentTemp = _params.InitialTemperature;
            var currentValue = initialValue;
            var currentGrammar = initialGrammar;
            var counter = 1;
            while (currentTemp > 0.3)
            {
                var (mutatedGrammar, reparsed) = _learner.GetNeighborAndReparse(currentGrammar);
                if (mutatedGrammar == null || !reparsed) continue;

                currentTemp *= _params.CoolingFactor;

                var newValue = _objectiveFunction.Compute(mutatedGrammar);

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
                }

                //uncomment the following line ONLY to check that the differential parser works identically to the from-scratch parser.
                //var currentCFHypothesis = new ContextFreeGrammar(currentGrammar);
                //var allParses1 = _learner.ParseAllSentencesWithDebuggingAssertion(currentCFHypothesis, _learner._sentencesParser);
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