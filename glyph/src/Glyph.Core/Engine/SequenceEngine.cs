using System;
using System.Collections.Generic;

namespace Glyph.Core.Engine
{
    public class SequenceEngine
    {
        private SessionState _sessionState;
        private Trie _trie;
        private List<string> _validNextKeys;

        public SequenceEngine()
        {
            _sessionState = new SessionState();
            _trie = new Trie();
            _validNextKeys = new List<string>();
        }

        public void StartSession()
        {
            _sessionState.Start();
            UpdateValidNextKeys();
        }

        public void EndSession()
        {
            _sessionState.End();
        }

        public void ProcessKey(string key)
        {
            if (_sessionState.IsActive)
            {
                if (_trie.TryGetNextKeys(_sessionState.CurrentPrefix, out var nextKeys))
                {
                    if (nextKeys.Contains(key))
                    {
                        _sessionState.AddToBuffer(key);
                        if (_trie.IsCompleteSequence(_sessionState.CurrentPrefix + key))
                        {
                            ExecuteAction(_sessionState.CurrentPrefix + key);
                            EndSession();
                        }
                        else
                        {
                            UpdateValidNextKeys();
                        }
                    }
                    else
                    {
                        // Handle invalid key
                        EndSession();
                    }
                }
            }
        }

        private void UpdateValidNextKeys()
        {
            _validNextKeys = _trie.GetValidNextKeys(_sessionState.CurrentPrefix);
        }

        private void ExecuteAction(string actionKey)
        {
            // Logic to execute the action associated with the completed key sequence
        }
    }
}