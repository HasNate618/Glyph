using System;
using System.Collections.Generic;

namespace Glyph.Core.Engine
{
    public class TrieNode
    {
        public Dictionary<string, TrieNode> Children { get; } = new Dictionary<string, TrieNode>();
        public bool IsEndOfSequence { get; set; }
        public string ActionName { get; set; }
    }

    public class Trie
    {
        private readonly TrieNode root;

        public Trie()
        {
            root = new TrieNode();
        }

        public void Insert(string sequence, string actionName)
        {
            var currentNode = root;
            var keys = sequence.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var key in keys)
            {
                if (!currentNode.Children.ContainsKey(key))
                {
                    currentNode.Children[key] = new TrieNode();
                }
                currentNode = currentNode.Children[key];
            }

            currentNode.IsEndOfSequence = true;
            currentNode.ActionName = actionName;
        }

        public List<string> GetValidNextKeys(string prefix)
        {
            var currentNode = root;
            var keys = prefix.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var key in keys)
            {
                if (!currentNode.Children.ContainsKey(key))
                {
                    return new List<string>();
                }
                currentNode = currentNode.Children[key];
            }

            return new List<string>(currentNode.Children.Keys);
        }

        public string GetActionForSequence(string sequence)
        {
            var currentNode = root;
            var keys = sequence.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var key in keys)
            {
                if (!currentNode.Children.ContainsKey(key))
                {
                    return null;
                }
                currentNode = currentNode.Children[key];
            }

            return currentNode.IsEndOfSequence ? currentNode.ActionName : null;
        }
    }
}