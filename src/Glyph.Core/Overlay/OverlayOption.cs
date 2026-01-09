using System;
using System.Collections.Generic;
using System.Linq;

using Glyph.Core.Input;

namespace Glyph.Core.Overlay;

public sealed record OverlayOption(string Key, string Description, bool IsLayer, bool IsAction)
{
    public IReadOnlyList<string> KeyCaps { get; } = BuildKeyCaps(Key);

    private static IReadOnlyList<string> BuildKeyCaps(string key)
    {
        if (string.IsNullOrEmpty(key)) return Array.Empty<string>();

        var caps = new List<string>(key.Length);
        foreach (var ch in key)
        {
            if (KeyTokens.TryDecode(ch, out var token))
            {
                caps.Add(token);
                continue;
            }

            if (ch == '\u001B')
            {
                caps.Add("Esc");
                continue;
            }

            if (ch == ' ')
            {
                caps.Add("Space");
                continue;
            }

            caps.Add(ch.ToString());
        }

        return caps;
    }
}
