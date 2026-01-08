using Xunit;
using Glyph.Core.Engine;

namespace Glyph.Core.Tests;

public class TrieTests
{
    [Fact]
    public void Lookup_ReturnsNextKeysAndCompletion()
    {
        var trie = new Trie<string>();
        trie.Add("a", "A", "LabelA");
        trie.Add("ab", "AB", "LabelAB");

        var aLookup = trie.Lookup("a");
        Assert.True(aLookup.IsValidPrefix);
        Assert.True(aLookup.IsComplete);
        Assert.Equal("A", aLookup.Value);
        Assert.Contains(aLookup.NextKeys, k => k.Key == 'b' && k.Completes);
    }
}
