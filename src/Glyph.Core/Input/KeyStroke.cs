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

        // Space is used as part of the leader gesture (Ctrl+Shift+Space) in the engine.
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
