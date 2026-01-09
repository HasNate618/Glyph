using System;
using System.Collections.Generic;

namespace Glyph.Core.Input;

public static class KeyTokens
{
    // Use a Private Use Area range for non-text / named keys.
    // These chars are never intended to be typed; they only exist as internal key identifiers.
    private const char Base = '\uE000';

    private static readonly Dictionary<string, char> TokenToChar = new(StringComparer.OrdinalIgnoreCase)
    {
        // Modifiers / special
        ["Win"] = (char)(Base + 1),

        // Navigation
        ["Left"] = (char)(Base + 10),
        ["Up"] = (char)(Base + 11),
        ["Right"] = (char)(Base + 12),
        ["Down"] = (char)(Base + 13),
        ["Home"] = (char)(Base + 14),
        ["End"] = (char)(Base + 15),
        ["PageUp"] = (char)(Base + 16),
        ["PageDown"] = (char)(Base + 17),
        ["Insert"] = (char)(Base + 18),
        ["Delete"] = (char)(Base + 19),

        // Editing / system
        ["Enter"] = (char)(Base + 30),
        ["Tab"] = (char)(Base + 31),
        // Esc is already represented by a real control char in the engine.
        ["Esc"] = '\u001B',
        ["Escape"] = '\u001B',
        ["Backspace"] = (char)(Base + 33),
        // Space is part of the glyph gesture; keep it as the literal space char.
        ["Space"] = ' ',
        ["CapsLock"] = (char)(Base + 35),

        // Aliases
        ["Return"] = (char)(Base + 30),
        ["PgUp"] = (char)(Base + 16),
        ["PgDn"] = (char)(Base + 17),
        ["Del"] = (char)(Base + 19),
        ["Ins"] = (char)(Base + 18),

        // Arrow aliases (string forms commonly used in configs)
        ["ArrowLeft"] = (char)(Base + 10),
        ["ArrowUp"] = (char)(Base + 11),
        ["ArrowRight"] = (char)(Base + 12),
        ["ArrowDown"] = (char)(Base + 13),
        ["LeftArrow"] = (char)(Base + 10),
        ["UpArrow"] = (char)(Base + 11),
        ["RightArrow"] = (char)(Base + 12),
        ["DownArrow"] = (char)(Base + 13),
        ["LeftArrowKey"] = (char)(Base + 10),
        ["RightArrowKey"] = (char)(Base + 12),
        ["UpArrowKey"] = (char)(Base + 11),
        ["DownArrowKey"] = (char)(Base + 13),
    };

    private static readonly Dictionary<char, string> CharToToken = BuildReverseMap();

    private static Dictionary<char, string> BuildReverseMap()
    {
        // Prefer canonical, user-friendly names.
        var map = new Dictionary<char, string>();

        void add(string token, char ch)
        {
            if (!map.ContainsKey(ch)) map[ch] = token;
        }

        add("Win", TokenToChar["Win"]);
        add("Left", TokenToChar["Left"]);
        add("Up", TokenToChar["Up"]);
        add("Right", TokenToChar["Right"]);
        add("Down", TokenToChar["Down"]);
        add("Home", TokenToChar["Home"]);
        add("End", TokenToChar["End"]);
        add("PageUp", TokenToChar["PageUp"]);
        add("PageDown", TokenToChar["PageDown"]);
        add("Insert", TokenToChar["Insert"]);
        add("Delete", TokenToChar["Delete"]);
        add("Enter", TokenToChar["Enter"]);
        add("Tab", TokenToChar["Tab"]);
        add("Esc", TokenToChar["Esc"]);
        add("Backspace", TokenToChar["Backspace"]);
        add("Space", TokenToChar["Space"]);
        add("CapsLock", TokenToChar["CapsLock"]);

        return map;
    }

    public static bool TryEncode(string? token, out char key)
    {
        key = default;
        if (string.IsNullOrWhiteSpace(token)) return false;

        token = token.Trim();

        // If user provided a single char token (e.g., "a"), keep it as-is.
        if (token.Length == 1)
        {
            key = token[0];
            return true;
        }

        // Normalize a few common variants.
        var t = token.Replace("_", string.Empty).Replace("-", string.Empty).Trim();

        // Function keys: F1-F24
        if (t.Length >= 2 && (t[0] == 'F' || t[0] == 'f') && int.TryParse(t.Substring(1), out var fn) && fn is >= 1 and <= 24)
        {
            // Keep F-keys in a nearby private use range.
            key = (char)(Base + 100 + fn);
            return true;
        }

        if (TokenToChar.TryGetValue(token, out key)) return true;

        // Case/format-insensitive fallbacks
        if (TokenToChar.TryGetValue(t, out key)) return true;

        // Arrow aliases (post-normalization)
        if (string.Equals(t, "left", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "leftarrow", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "arrowleft", StringComparison.OrdinalIgnoreCase))
        {
            key = TokenToChar["Left"];
            return true;
        }

        if (string.Equals(t, "up", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "uparrow", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "arrowup", StringComparison.OrdinalIgnoreCase))
        {
            key = TokenToChar["Up"];
            return true;
        }

        if (string.Equals(t, "right", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "rightarrow", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "arrowright", StringComparison.OrdinalIgnoreCase))
        {
            key = TokenToChar["Right"];
            return true;
        }

        if (string.Equals(t, "down", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "downarrow", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "arrowdown", StringComparison.OrdinalIgnoreCase))
        {
            key = TokenToChar["Down"];
            return true;
        }

        return false;
    }

    public static bool TryDecode(char key, out string token)
    {
        // F1-F24 token chars
        if (key is >= (char)(Base + 101) and <= (char)(Base + 124))
        {
            token = "F" + (key - (Base + 100));
            return true;
        }

        if (CharToToken.TryGetValue(key, out token!))
        {
            return true;
        }

        token = string.Empty;
        return false;
    }

    public static string FormatInlineSequence(string buffer)
    {
        if (string.IsNullOrEmpty(buffer)) return buffer;

        var result = new System.Text.StringBuilder(buffer.Length);
        foreach (var ch in buffer)
        {
            if (TryDecode(ch, out var token))
            {
                result.Append('<').Append(token).Append('>');
                continue;
            }

            // Friendly names for a couple of control-ish chars already used by the engine.
            if (ch == '\u001B')
            {
                result.Append("<Esc>");
                continue;
            }

            if (ch == ' ')
            {
                result.Append("<Space>");
                continue;
            }

            result.Append(ch);
        }

        return result.ToString();
    }
}
