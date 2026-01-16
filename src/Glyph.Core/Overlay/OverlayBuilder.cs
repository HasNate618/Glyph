using Glyph.Core.Actions;
using Glyph.Core.Engine;
using Glyph.Core.Input;

namespace Glyph.Core.Overlay;

public static class OverlayBuilder
{
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

            var childLookup = lookup(buffer + k.Key);
            foreach (var child in childLookup.NextKeys)
            {
                if (!child.Completes) continue;

                var childDesc = child.Description;
                if (string.IsNullOrWhiteSpace(childDesc)) continue;

                var shouldIncludeBlankIntermediate = policy.ShowLookaheadForBlankIntermediates && !includeSingle;
                var shouldIncludeDisambiguation = k.Completes;

                if (!shouldIncludeBlankIntermediate && !shouldIncludeDisambiguation)
                {
                    continue;
                }

                var keyString = string.Concat(k.Key, child.Key);
                byKey[keyString] = new OverlayOption(
                    Key: keyString,
                    Description: childDesc,
                    IsLayer: child.Continues,
                    IsAction: true);
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

    private static string BuildTitle(
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
