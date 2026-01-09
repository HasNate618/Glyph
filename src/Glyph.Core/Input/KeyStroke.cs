namespace Glyph.Core.Input;

public readonly record struct KeyStroke(
    char? Key,
    bool Ctrl,
    bool Shift,
    bool Alt,
    bool Win,
    int VkCode = 0)
{
    public static KeyStroke FromVkCode(int vkCode, bool ctrl, bool shift, bool alt, bool win)
    {
        // Prototype: only map A–Z keys to lowercase chars. Everything else is treated as non-text.
        char? key = null;

        // Named/special keys (single-step tokens)
        // NOTE: we keep these as private-use chars so the existing char-based engine remains unchanged.
        if (vkCode == 0x5B || vkCode == 0x5C) // VK_LWIN / VK_RWIN
        {
            if (KeyTokens.TryEncode("Win", out var k))
            {
                key = k;
            }
        }

        // Arrow/navigation keys
        if (vkCode == 0x25 && KeyTokens.TryEncode("Left", out var left)) key = left;
        if (vkCode == 0x26 && KeyTokens.TryEncode("Up", out var up)) key = up;
        if (vkCode == 0x27 && KeyTokens.TryEncode("Right", out var right)) key = right;
        if (vkCode == 0x28 && KeyTokens.TryEncode("Down", out var down)) key = down;
        if (vkCode == 0x24 && KeyTokens.TryEncode("Home", out var home)) key = home;
        if (vkCode == 0x23 && KeyTokens.TryEncode("End", out var end)) key = end;
        if (vkCode == 0x21 && KeyTokens.TryEncode("PageUp", out var pgUp)) key = pgUp;
        if (vkCode == 0x22 && KeyTokens.TryEncode("PageDown", out var pgDn)) key = pgDn;
        if (vkCode == 0x2D && KeyTokens.TryEncode("Insert", out var ins)) key = ins;
        if (vkCode == 0x2E && KeyTokens.TryEncode("Delete", out var del)) key = del;

        // Editing/system
        if (vkCode == 0x0D && KeyTokens.TryEncode("Enter", out var enter)) key = enter;
        if (vkCode == 0x09 && KeyTokens.TryEncode("Tab", out var tab)) key = tab;
        if (vkCode == 0x08 && KeyTokens.TryEncode("Backspace", out var bs)) key = bs;
        if (vkCode == 0x14 && KeyTokens.TryEncode("CapsLock", out var caps)) key = caps;

        // Function keys F1-F12 (0x70-0x7B)
        if (vkCode is >= 0x70 and <= 0x7B)
        {
            var fn = vkCode - 0x70 + 1;
            if (KeyTokens.TryEncode($"F{fn}", out var fk))
            {
                key = fk;
            }
        }

        if (vkCode is >= 0x41 and <= 0x5A)
        {
            key = (char)('a' + (vkCode - 0x41));
        }

        // Digits 0-9
        if (vkCode is >= 0x30 and <= 0x39)
        {
            key = (char)('0' + (vkCode - 0x30));
        }

        // Numpad digits
        if (vkCode is >= 0x60 and <= 0x69)
        {
            key = (char)('0' + (vkCode - 0x60));
        }

        // Common punctuation (unshifted)
        // VK_OEM_COMMA = 0xBC, VK_OEM_PERIOD = 0xBE, VK_OEM_2 = 0xBF (slash)
        // VK_OEM_1 = 0xBA (semicolon), VK_OEM_7 = 0xDE (apostrophe)
        // VK_OEM_4 = 0xDB ([), VK_OEM_6 = 0xDD (]), VK_OEM_MINUS = 0xBD (-)
        // VK_OEM_PLUS = 0xBB (=), VK_OEM_5 = 0xDC (\), VK_OEM_3 = 0xC0 (`)
        if (vkCode == 0xBC) key = ',';
        if (vkCode == 0xBE) key = '.';
        if (vkCode == 0xBF) key = '/';
        if (vkCode == 0xBA) key = ';';
        if (vkCode == 0xDE) key = '\'';
        if (vkCode == 0xDB) key = '[';
        if (vkCode == 0xDD) key = ']';
        if (vkCode == 0xBD) key = '-';
        if (vkCode == 0xBB) key = '=';
        if (vkCode == 0xDC) key = '\\';
        if (vkCode == 0xC0) key = '`';

        // Space is used as part of the glyph gesture (Ctrl+Shift+Space) in the engine.
        if (vkCode == 0x20)
        {
            key = ' ';
        }

        // Escape cancels.
        if (vkCode == 0x1B)
        {
            key = '\u001B';
        }

        // Try Fn key codes: VK_F13-VK_F24 (0x7C-0x87)
        if (vkCode is >= 0x7C and <= 0x87)
        {
            // Map Fn keys to a special character range
            key = (char)(0xF000 + (vkCode - 0x7C));
        }

        return new KeyStroke(key, ctrl, shift, alt, win, vkCode);
    }
}
