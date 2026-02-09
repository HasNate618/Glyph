using Glyph.Core.Actions;
using Glyph.Core.Engine;
using Glyph.Core.Input;

namespace Glyph.Core.Overlay;

public static class OverlayBuilder
{
    /// <summary>
    /// Maximum depth for recursive lookahead through transparent intermediate nodes.
    /// </summary>
    private const int MaxLookaheadDepth = 8;

    public static OverlayModel Build(
        string buffer,
        string? activeProcessName,
        IReadOnlyList<TrieNextKey> nextKeys,
        Func<string, TrieLookupResult<ActionRequest>> lookup,
        Func<string, string?> getGlobalPrefixDescription,
        Func<string, string?> getPerAppPrefixDescription,
        OverlayPolicy policy)
    {
        var byKey = new Dictionary<string, OverlayOption>(Math.Max(8, nextKeys.Count * 2), StringComparer.Ordinal);

        foreach (var k in nextKeys)
        {
            var desc = k.Description;

            if (policy.SubstituteProgramLayerLabelWithActiveProcess
                && k.Key == 'p'
                && buffer.Length == 0)
            {
                if (string.IsNullOrWhiteSpace(activeProcessName))
                {
                    desc = "No Program Focused";
                }
                else
                {
                    var per = getPerAppPrefixDescription("p");
                    var pLookup = lookup("p");
                    var isConfigured = pLookup.IsValidPrefix && (pLookup.IsComplete || pLookup.NextKeys.Count > 0);

                    desc = isConfigured
                        ? (!string.IsNullOrWhiteSpace(per) ? per : activeProcessName!)
                        : $"{activeProcessName} Not Configured";
                }
            }

            var includeSingle = k.Completes || !string.IsNullOrWhiteSpace(desc);
            if (includeSingle)
            {
                var keyString = k.Key.ToString();
                byKey[keyString] = new OverlayOption(
                    Key: keyString,
                    Description: desc,
                    IsLayer: k.Continues,
                    IsAction: k.Completes);
            }

            if (!k.Continues)
            {
                continue;
            }

            // Determine whether lookahead children should be shown at all.
            // Blank intermediate: the parent key itself isn't visible, so show children
            //   to give the user something to see.
            // Disambiguation: the parent key completes an action but also continues,
            //   so show children to help the user disambiguate.
            var shouldIncludeBlankIntermediate = policy.ShowLookaheadForBlankIntermediates && !includeSingle;
            var shouldIncludeDisambiguation = k.Completes;
            var allowLookahead = shouldIncludeBlankIntermediate || shouldIncludeDisambiguation;

            if (!allowLookahead) continue;

            var childLookup = lookup(buffer + k.Key);
            foreach (var child in childLookup.NextKeys)
            {
                var childKeyString = string.Concat(k.Key, child.Key);

                // Show completing children with descriptions (existing depth-1 behavior).
                if (child.Completes && !string.IsNullOrWhiteSpace(child.Description))
                {
                    byKey[childKeyString] = new OverlayOption(
                        Key: childKeyString,
                        Description: child.Description,
                        IsLayer: child.Continues,
                        IsAction: true);
                }

                // Recurse through "transparent" intermediates — nodes that don't
                // complete an action AND don't have a description (they're not named
                // layers). This surfaces 3+ character sequences like "rrr" without
                // leaking named sublayer children (e.g. "ra", "rb") to parent layers.
                if (child.Continues && !child.Completes
                    && string.IsNullOrWhiteSpace(child.Description))
                {
                    CollectTransparentDescendants(
                        buffer, childKeyString, lookup, byKey, MaxLookaheadDepth);
                }
            }
        }

        var options = byKey.Values
            .OrderBy(o => o.Key, StringComparer.Ordinal)
            .ToList();

        var title = BuildTitle(buffer, activeProcessName, lookup, getGlobalPrefixDescription, getPerAppPrefixDescription, policy);

        if (options.Count == 0)
        {
            string msg;
            if (buffer.StartsWith("p", StringComparison.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(activeProcessName))
                {
                    msg = "No Program Focused";
                }
                else
                {
                    msg = $"{activeProcessName} Not Configured";
                }
            }
            else
            {
                msg = "No keys defined for this layer";
            }

            return new OverlayModel(title, new List<OverlayOption> { new("—", msg, false, false) });
        }

        return new OverlayModel(title, options);
    }

    /// <summary>
    /// Recursively walk descendants through "transparent" intermediate nodes
    /// (nodes that don't complete an action and don't have a description/label).
    /// This allows deeply-nested completions like 3+ character sequences to
    /// surface in the overlay without leaking named sublayer children upward.
    /// </summary>
    private static void CollectTransparentDescendants(
        string buffer,
        string prefix,
        Func<string, TrieLookupResult<ActionRequest>> lookup,
        Dictionary<string, OverlayOption> byKey,
        int maxDepth)
    {
        if (maxDepth <= 0) return;

        var result = lookup(buffer + prefix);
        foreach (var child in result.NextKeys)
        {
            var keyString = prefix + child.Key;

            // Show any completing descendants.
            if (child.Completes && !string.IsNullOrWhiteSpace(child.Description))
            {
                byKey[keyString] = new OverlayOption(
                    Key: keyString,
                    Description: child.Description,
                    IsLayer: child.Continues,
                    IsAction: true);
            }

            // Keep recursing only through transparent intermediates.
            // Stop at nodes that complete (they're actions) or have
            // descriptions (they're named layers the user navigates into).
            if (child.Continues && !child.Completes
                && string.IsNullOrWhiteSpace(child.Description))
            {
                CollectTransparentDescendants(
                    buffer, keyString, lookup, byKey, maxDepth - 1);
            }
        }
    }

    internal static string BuildTitle(
        string buffer,
        string? activeProcessName,
        Func<string, TrieLookupResult<ActionRequest>> lookup,
        Func<string, string?> getGlobalPrefixDescription,
        Func<string, string?> getPerAppPrefixDescription,
        OverlayPolicy policy)
    {
        // Build a label breadcrumb from prefix descriptions.
        // Example: buffer=",tc" => [",", ",t", ",tc"] => "Glyph > Text > Copy"
        if (string.IsNullOrEmpty(buffer))
        {
            return "Glyph";
        }

        var parts = new List<string>(capacity: Math.Min(8, buffer.Length + 1))
        {
            "Glyph",
        };

        for (var i = 1; i <= buffer.Length; i++)
        {
            var prefix = buffer.Substring(0, i);

            // If this is an intermediate prefix (not the final entered buffer)
            // and the trie says this prefix is both a complete action and
            // has children (i.e. it's also a layer), skip showing it in the
            // breadcrumb to avoid confusing intermediate labels.
            var prefixLookup = lookup(prefix);
            if (i < buffer.Length && prefixLookup.IsValidPrefix && prefixLookup.IsComplete && prefixLookup.NextKeys.Count > 0)
            {
                continue;
            }

            string? label = null;

            // Special-case Program layer: show active process label when configured.
            if (policy.SubstituteProgramLayerLabelWithActiveProcess && string.Equals(prefix, "p", StringComparison.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(activeProcessName))
                {
                    label = "No Program Focused";
                }
                else
                {
                    var per = getPerAppPrefixDescription("p");
                    var pLookup = lookup("p");
                    var isConfigured = pLookup.IsValidPrefix && (pLookup.IsComplete || pLookup.NextKeys.Count > 0);

                    label = isConfigured
                        ? (!string.IsNullOrWhiteSpace(per) ? per : activeProcessName)
                        : $"{activeProcessName} Not Configured";
                }
            }
            else
            {
                // Prefer per-app descriptions for prefixes under the Program layer.
                if (prefix.StartsWith("p", StringComparison.Ordinal))
                {
                    label = getPerAppPrefixDescription(prefix);
                }

                label ??= getGlobalPrefixDescription(prefix);
            }

            if (string.IsNullOrWhiteSpace(label))
            {
                // Fallback: show the actual keycap for this step if no label exists.
                var step = buffer.Substring(i - 1, 1);
                label = KeyTokens.FormatInlineSequence(step);
            }

            parts.Add(label);
        }

        return string.Join(" → ", parts);
    }
}
