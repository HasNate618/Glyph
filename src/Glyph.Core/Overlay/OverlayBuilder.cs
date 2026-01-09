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

            if (!k.Continues || (!policy.ShowLookaheadForBlankIntermediates && !policy.ShowRepeatedKeyDisambiguation))
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
                var isRepeatedKeyDisambiguation = policy.ShowRepeatedKeyDisambiguation && (child.Key == k.Key && k.Completes);

                if (!shouldIncludeBlankIntermediate && !isRepeatedKeyDisambiguation) continue;

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

        if (options.Count == 0)
        {
            var seq = $"Glyph {KeyTokens.FormatInlineSequence(buffer)}".TrimEnd();
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

            return new OverlayModel(seq, new List<OverlayOption> { new("—", msg, false, false) });
        }

        return new OverlayModel($"Glyph {KeyTokens.FormatInlineSequence(buffer)}".TrimEnd(), options);
    }
}
