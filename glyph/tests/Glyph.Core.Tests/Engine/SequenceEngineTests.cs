using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Glyph.Core.Tests.Engine
{
    [TestClass]
    public class SequenceEngineTests
    {
        private SequenceEngine _sequenceEngine;

        [TestInitialize]
        public void Setup()
        {
            _sequenceEngine = new SequenceEngine();
        }

        [TestMethod]
        public void Test_InitialState_IsInactive()
        {
            Assert.IsFalse(_sequenceEngine.IsActive);
        }

        [TestMethod]
        public void Test_LeaderKey_EntersSession()
        {
            _sequenceEngine.OnKeyPress(Key.Leader);
            Assert.IsTrue(_sequenceEngine.IsActive);
        }

        [TestMethod]
        public void Test_ValidSequence_ExecutesAction()
        {
            _sequenceEngine.OnKeyPress(Key.Leader);
            _sequenceEngine.OnKeyPress(Key.R);
            _sequenceEngine.OnKeyPress(Key.C);

            Assert.IsTrue(_sequenceEngine.LastExecutedAction is RunChromeAction);
        }

        [TestMethod]
        public void Test_InvalidSequence_CancelsSession()
        {
            _sequenceEngine.OnKeyPress(Key.Leader);
            _sequenceEngine.OnKeyPress(Key.Invalid);

            Assert.IsFalse(_sequenceEngine.IsActive);
        }

        [TestMethod]
        public void Test_Timeout_EndsSession()
        {
            _sequenceEngine.OnKeyPress(Key.Leader);
            System.Threading.Thread.Sleep(3000); // Simulate timeout duration
            Assert.IsFalse(_sequenceEngine.IsActive);
        }

        [TestMethod]
        public void Test_OverlayUpdates_WithValidNextKeys()
        {
            _sequenceEngine.OnKeyPress(Key.Leader);
            var nextKeys = _sequenceEngine.GetValidNextKeys();

            Assert.IsTrue(nextKeys.Contains(Key.R));
            Assert.IsTrue(nextKeys.Contains(Key.Q));
        }
    }
}