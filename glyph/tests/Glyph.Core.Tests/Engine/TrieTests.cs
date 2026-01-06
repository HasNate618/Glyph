using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Glyph.Core.Tests.Engine
{
    [TestClass]
    public class TrieTests
    {
        private Trie _trie;

        [TestInitialize]
        public void Setup()
        {
            _trie = new Trie();
        }

        [TestMethod]
        public void InsertAndSearch_ValidKey_ReturnsTrue()
        {
            _trie.Insert("test");
            Assert.IsTrue(_trie.Search("test"));
        }

        [TestMethod]
        public void InsertAndSearch_InvalidKey_ReturnsFalse()
        {
            _trie.Insert("test");
            Assert.IsFalse(_trie.Search("invalid"));
        }

        [TestMethod]
        public void InsertAndSearch_Prefix_ReturnsTrue()
        {
            _trie.Insert("test");
            Assert.IsTrue(_trie.StartsWith("te"));
        }

        [TestMethod]
        public void InsertAndSearch_NonExistentPrefix_ReturnsFalse()
        {
            _trie.Insert("test");
            Assert.IsFalse(_trie.StartsWith("invalid"));
        }

        [TestMethod]
        public void InsertAndSearch_EmptyString_ReturnsFalse()
        {
            Assert.IsFalse(_trie.Search(""));
        }

        [TestMethod]
        public void InsertAndSearch_NullKey_ThrowsArgumentNullException()
        {
            Assert.ThrowsException<ArgumentNullException>(() => _trie.Insert(null));
        }

        [TestMethod]
        public void InsertAndSearch_EmptyTrie_ReturnsFalse()
        {
            Assert.IsFalse(_trie.Search("test"));
        }

        [TestMethod]
        public void InsertAndSearch_MultipleKeys_ReturnsCorrectResults()
        {
            _trie.Insert("apple");
            _trie.Insert("app");
            _trie.Insert("banana");

            Assert.IsTrue(_trie.Search("apple"));
            Assert.IsTrue(_trie.Search("app"));
            Assert.IsFalse(_trie.Search("appl"));
            Assert.IsTrue(_trie.StartsWith("app"));
            Assert.IsFalse(_trie.StartsWith("banan"));
        }
    }
}