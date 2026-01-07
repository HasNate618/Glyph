using System.Collections.Immutable;

namespace Glyph.Core.Engine;

public sealed class Trie<TValue>
    where TValue : class
{
    private sealed class Node
    {
        public readonly Dictionary<char, Node> Children = new();
        public TValue? Value;
        public bool HasValue;
        public string? Description;
    }

    private readonly Node _root = new();

    public void Add(string sequence, TValue value, string description)
    {
        if (sequence.Length == 0)
        {
            throw new ArgumentException("Sequence must be non-empty", nameof(sequence));
        }

        var node = _root;
        foreach (var ch in sequence)
        {
            if (!node.Children.TryGetValue(ch, out var next))
            {
                next = new Node();
                node.Children[ch] = next;
            }
            node = next;
        }

        node.Value = value;
        node.HasValue = true;
        node.Description = description;
    }

    public void SetDescription(string prefix, string description)
    {
        if (prefix.Length == 0)
        {
            throw new ArgumentException("Prefix must be non-empty", nameof(prefix));
        }

        var node = _root;
        foreach (var ch in prefix)
        {
            if (!node.Children.TryGetValue(ch, out var next))
            {
                next = new Node();
                node.Children[ch] = next;
            }
            node = next;
        }

        node.Description = description;
    }

    public TrieLookupResult<TValue> Lookup(string prefix)
    {
        var node = _root;
        foreach (var ch in prefix)
        {
            if (!node.Children.TryGetValue(ch, out var next))
            {
                return TrieLookupResult<TValue>.Invalid;
            }
            node = next;
        }

        var nextKeys = node.Children
            .OrderBy(kvp => kvp.Key)
            .Select(kvp => new TrieNextKey(kvp.Key, kvp.Value.Description ?? string.Empty))
            .ToImmutableArray();

        return new TrieLookupResult<TValue>(
            IsValidPrefix: true,
            IsComplete: node.HasValue,
            Value: node.HasValue ? node.Value : null,
            NextKeys: nextKeys);
    }

    public string? GetDescription(string prefix)
    {
        if (prefix.Length == 0) return null;

        var node = _root;
        foreach (var ch in prefix)
        {
            if (!node.Children.TryGetValue(ch, out var next))
            {
                return null;
            }
            node = next;
        }

        return node.Description;
    }
}

public readonly record struct TrieNextKey(char Key, string Description);

public readonly record struct TrieLookupResult<TValue>(
    bool IsValidPrefix,
    bool IsComplete,
    TValue? Value,
    IReadOnlyList<TrieNextKey> NextKeys)
    where TValue : class
{
    public static TrieLookupResult<TValue> Invalid => new(false, false, null, Array.Empty<TrieNextKey>());
}
