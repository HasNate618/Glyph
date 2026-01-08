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
