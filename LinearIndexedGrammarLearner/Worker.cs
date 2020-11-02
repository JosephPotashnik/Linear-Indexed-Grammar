using LinearIndexedGrammarParser;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace LinearIndexedGrammarLearner
{
    public class Worker
    {
        private readonly int _workerIndex = 0;
        private bool killSignal = false;
        private WaitHandle[] _waitHandles;
        private ManualResetEvent _finishedProcessingEvent;
        private readonly EarleyParser[] _parser;
        private int _numberOfsentences;
        private int _numberOfWorkers;

        public Worker(int workerIndex, WaitHandle[] waitHandles, WaitHandle finishedProcessing, EarleyParser[] sentencesParser, int numberOfWorkers) {

            _workerIndex = workerIndex;
            _waitHandles = waitHandles;
            _finishedProcessingEvent = (ManualResetEvent)finishedProcessing;
            _parser = sentencesParser;
            _numberOfsentences = sentencesParser.Length;
            _numberOfWorkers = numberOfWorkers;
        }
        public void Run()
        {
            while (!killSignal)
            {
                int index = WaitHandle.WaitAny(_waitHandles);
                switch (index)
                {
                    case 0: //parse from scratch
                        ParseSentence();
                        break;
                    case 1: //parse with addition
                        ReParseSentenceWithRuleAddition();
                        break;
                    case 2: //parse with deletion
                        ReParseSentenceWithRuleDeletion();
                        break;
                    case 3: //kill signal
                        killSignal = true;
                        break;
                    default:
                        throw new Exception("should not happen - worker signal switch");
                        break;
                }

                _finishedProcessingEvent.Set();
            }
        }

        private void ParseSentence()
        {
            for (int i = _workerIndex; i < _numberOfsentences; i+= _numberOfWorkers)
                _parser[i].ParseSentence();
        }

        private void ReParseSentenceWithRuleAddition()
        {
            for (int i = _workerIndex; i < _numberOfsentences; i += _numberOfWorkers)
                _parser[i].ReParseSentenceWithRuleAddition(Learner.CFGForWorkers, Learner.rulesForWorkers);
        }

        private void ReParseSentenceWithRuleDeletion()
        {
            for (int i = _workerIndex; i < _numberOfsentences; i += _numberOfWorkers)
                _parser[i].ReParseSentenceWithRuleDeletion(Learner.CFGForWorkers, Learner.rulesForWorkers, Learner.predictionSet);
        }

        
    }
}
