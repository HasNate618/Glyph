using System.Runtime.InteropServices;

using Glyph.Core.Logging;
using Glyph.Win32.Interop;

namespace Glyph.Win32.Input;

public static class InputSender
{
    public static bool SendText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return true;
        }

        var inputs = new List<NativeMethods.INPUT>(text.Length * 2);

        foreach (var ch in text)
        {
            // key down
            inputs.Add(new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                U = new NativeMethods.InputUnion
                {
                    ki = new NativeMethods.KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = ch,
                        dwFlags = NativeMethods.KEYEVENTF_UNICODE,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero,
                    },
                },
            });

            // key up
            inputs.Add(new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                U = new NativeMethods.InputUnion
                {
                    ki = new NativeMethods.KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = ch,
                        dwFlags = NativeMethods.KEYEVENTF_UNICODE | NativeMethods.KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero,
                    },
                },
            });
        }

        return SendInputs(inputs);
    }

    public static bool SendEnter()
    {
        var inputs = new[]
        {
            new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                U = new NativeMethods.InputUnion
                {
                    ki = new NativeMethods.KEYBDINPUT
                    {
                        wVk = NativeMethods.VK_RETURN,
                        wScan = 0,
                        dwFlags = 0,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero,
                    },
                },
            },
            new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                U = new NativeMethods.InputUnion
                {
                    ki = new NativeMethods.KEYBDINPUT
                    {
                        wVk = NativeMethods.VK_RETURN,
                        wScan = 0,
                        dwFlags = NativeMethods.KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero,
                    },
                },
            },
        };

        return SendInputs(inputs);
    }

    public static bool SendCtrlShiftV()
    {
        var inputs = new List<NativeMethods.INPUT>
        {
            // Ctrl down
            new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                U = new NativeMethods.InputUnion
                {
                    ki = new NativeMethods.KEYBDINPUT
                    {
                        wVk = (ushort)VirtualKey.VK_CONTROL,
                        wScan = 0,
                        dwFlags = 0,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero,
                    },
                },
            },
            // Shift down
            new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                U = new NativeMethods.InputUnion
                {
                    ki = new NativeMethods.KEYBDINPUT
                    {
                        wVk = (ushort)VirtualKey.VK_SHIFT,
                        wScan = 0,
                        dwFlags = 0,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero,
                    },
                },
            },
            // V down
            new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                U = new NativeMethods.InputUnion
                {
                    ki = new NativeMethods.KEYBDINPUT
                    {
                        wVk = 0x56,
                        wScan = 0,
                        dwFlags = 0,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero,
                    },
                },
            },
            // V up
            new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                U = new NativeMethods.InputUnion
                {
                    ki = new NativeMethods.KEYBDINPUT
                    {
                        wVk = 0x56,
                        wScan = 0,
                        dwFlags = NativeMethods.KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero,
                    },
                },
            },
            // Shift up
            new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                U = new NativeMethods.InputUnion
                {
                    ki = new NativeMethods.KEYBDINPUT
                    {
                        wVk = (ushort)VirtualKey.VK_SHIFT,
                        wScan = 0,
                        dwFlags = NativeMethods.KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero,
                    },
                },
            },
            // Ctrl up
            new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                U = new NativeMethods.InputUnion
                {
                    ki = new NativeMethods.KEYBDINPUT
                    {
                        wVk = (ushort)VirtualKey.VK_CONTROL,
                        wScan = 0,
                        dwFlags = NativeMethods.KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero,
                    },
                },
            },
        };

        return SendInputs(inputs);
    }

    public static bool SendShiftInsert()
    {
        const ushort VK_INSERT = 0x2D;

        var inputs = new List<NativeMethods.INPUT>
        {
            // Shift down
            new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                U = new NativeMethods.InputUnion
                {
                    ki = new NativeMethods.KEYBDINPUT
                    {
                        wVk = (ushort)VirtualKey.VK_SHIFT,
                        wScan = 0,
                        dwFlags = 0,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero,
                    },
                },
            },
            // Insert down
            new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                U = new NativeMethods.InputUnion
                {
                    ki = new NativeMethods.KEYBDINPUT
                    {
                        wVk = VK_INSERT,
                        wScan = 0,
                        dwFlags = 0,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero,
                    },
                },
            },
            // Insert up
            new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                U = new NativeMethods.InputUnion
                {
                    ki = new NativeMethods.KEYBDINPUT
                    {
                        wVk = VK_INSERT,
                        wScan = 0,
                        dwFlags = NativeMethods.KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero,
                    },
                },
            },
            // Shift up
            new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                U = new NativeMethods.InputUnion
                {
                    ki = new NativeMethods.KEYBDINPUT
                    {
                        wVk = (ushort)VirtualKey.VK_SHIFT,
                        wScan = 0,
                        dwFlags = NativeMethods.KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero,
                    },
                },
            },
        };

        return SendInputs(inputs);
    }

    public static bool SendCtrlV()
    {
        var inputs = new List<NativeMethods.INPUT>
        {
            // Ctrl down
            new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                U = new NativeMethods.InputUnion
                {
                    ki = new NativeMethods.KEYBDINPUT
                    {
                        wVk = (ushort)VirtualKey.VK_CONTROL,
                        wScan = 0,
                        dwFlags = 0,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero,
                    },
                },
            },
            // V down
            new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                U = new NativeMethods.InputUnion
                {
                    ki = new NativeMethods.KEYBDINPUT
                    {
                        wVk = 0x56,
                        wScan = 0,
                        dwFlags = 0,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero,
                    },
                },
            },
            // V up
            new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                U = new NativeMethods.InputUnion
                {
                    ki = new NativeMethods.KEYBDINPUT
                    {
                        wVk = 0x56,
                        wScan = 0,
                        dwFlags = NativeMethods.KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero,
                    },
                },
            },
            // Ctrl up
            new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                U = new NativeMethods.InputUnion
                {
                    ki = new NativeMethods.KEYBDINPUT
                    {
                        wVk = (ushort)VirtualKey.VK_CONTROL,
                        wScan = 0,
                        dwFlags = NativeMethods.KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero,
                    },
                },
            },
        };

        return SendInputs(inputs);
    }

    public static bool SendCtrlKey(ushort vk)
    {
        var inputs = new List<NativeMethods.INPUT>
        {
            // Ctrl down
            new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                U = new NativeMethods.InputUnion
                {
                    ki = new NativeMethods.KEYBDINPUT
                    {
                        wVk = (ushort)VirtualKey.VK_CONTROL,
                        wScan = 0,
                        dwFlags = 0,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero,
                    },
                },
            },
            // Key down
            new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                U = new NativeMethods.InputUnion
                {
                    ki = new NativeMethods.KEYBDINPUT
                    {
                        wVk = vk,
                        wScan = 0,
                        dwFlags = 0,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero,
                    },
                },
            },
            // Key up
            new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                U = new NativeMethods.InputUnion
                {
                    ki = new NativeMethods.KEYBDINPUT
                    {
                        wVk = vk,
                        wScan = 0,
                        dwFlags = NativeMethods.KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero,
                    },
                },
            },
            // Ctrl up
            new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                U = new NativeMethods.InputUnion
                {
                    ki = new NativeMethods.KEYBDINPUT
                    {
                        wVk = (ushort)VirtualKey.VK_CONTROL,
                        wScan = 0,
                        dwFlags = NativeMethods.KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero,
                    },
                },
            },
        };

        return SendInputs(inputs);
    }

    public static bool SendChordSpec(string spec)
    {
        if (string.IsNullOrWhiteSpace(spec)) return false;

        var parts = spec.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return false;

        var keyToken = parts[^1];
        var mods = parts.Length > 1 ? parts.Take(parts.Length - 1).ToArray() : Array.Empty<string>();

        ushort vk = GetVkForToken(keyToken);
        if (vk == 0) return false;

        var modVks = new List<ushort>();
        foreach (var m in mods)
        {
            var mm = m.Trim().ToLowerInvariant();
            if (mm == "ctrl" || mm == "control") modVks.Add((ushort)VirtualKey.VK_CONTROL);
            else if (mm == "shift") modVks.Add((ushort)VirtualKey.VK_SHIFT);
            else if (mm == "alt" || mm == "menu") modVks.Add((ushort)VirtualKey.VK_MENU);
            else if (mm == "win" || mm == "lwin" || mm == "rwin") modVks.Add((ushort)VirtualKey.VK_LWIN);
        }

        var inputs = new List<NativeMethods.INPUT>();

        // modifiers down
        foreach (var mv in modVks)
        {
            inputs.Add(new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                U = new NativeMethods.InputUnion
                {
                    ki = new NativeMethods.KEYBDINPUT
                    {
                        wVk = mv,
                        wScan = 0,
                        dwFlags = 0,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero,
                    },
                },
            });
        }

        // key down
        inputs.Add(new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            U = new NativeMethods.InputUnion
            {
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = vk,
                    wScan = 0,
                    dwFlags = 0,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero,
                },
            },
        });

        // key up
        inputs.Add(new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            U = new NativeMethods.InputUnion
            {
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = vk,
                    wScan = 0,
                    dwFlags = NativeMethods.KEYEVENTF_KEYUP,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero,
                },
            },
        });

        // modifiers up (reverse order)
        for (int i = modVks.Count - 1; i >= 0; i--)
        {
            var mv = modVks[i];
            inputs.Add(new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                U = new NativeMethods.InputUnion
                {
                    ki = new NativeMethods.KEYBDINPUT
                    {
                        wVk = mv,
                        wScan = 0,
                        dwFlags = NativeMethods.KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero,
                    },
                },
            });
        }

        return SendInputs(inputs);
    }

    private static ushort GetVkForToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return 0;
        token = token.Trim();
        if (token.Length == 1)
        {
            var ch = char.ToUpperInvariant(token[0]);
            if (ch >= 'A' && ch <= 'Z') return (ushort)ch;
            if (ch >= '0' && ch <= '9') return (ushort)ch;
        }

        // Function keys F1-F12
        if (token.StartsWith("F", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(token.Substring(1), out var n) && n >= 1 && n <= 12)
            {
                return (ushort)(0x70 + (n - 1));
            }
        }

        // Common named keys
        var t = token.ToLowerInvariant();
        if (t == "enter" || t == "return") return NativeMethods.VK_RETURN;
        if (t == "tab") return 0x09;
        if (t == "space" || t == " ") return 0x20;
        if (t == "left" || t == "arrowleft" || t == "arrow-left") return 0x25;
        if (t == "up" || t == "arrowup" || t == "arrow-up") return 0x26;
        if (t == "right" || t == "arrowright" || t == "arrow-right") return 0x27;
        if (t == "down" || t == "arrowdown" || t == "arrow-down") return 0x28;
        return 0;
    }

    public static bool SendMediaKey(ushort vk)
    {
        var inputs = new[]
        {
            new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                U = new NativeMethods.InputUnion
                {
                    ki = new NativeMethods.KEYBDINPUT
                    {
                        wVk = vk,
                        wScan = 0,
                        dwFlags = 0,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero,
                    },
                },
            },
            new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                U = new NativeMethods.InputUnion
                {
                    ki = new NativeMethods.KEYBDINPUT
                    {
                        wVk = vk,
                        wScan = 0,
                        dwFlags = NativeMethods.KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero,
                    },
                },
            },
        };

        return SendInputs(inputs);
    }

    private static bool SendInputs(IReadOnlyList<NativeMethods.INPUT> inputs)
    {
        if (inputs.Count == 0)
        {
            return true;
        }

        var sent = NativeMethods.SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<NativeMethods.INPUT>());
        if (sent != (uint)inputs.Count)
        {
            var err = Marshal.GetLastWin32Error();
            Logger.Error($"SendInput incomplete: sent {sent}/{inputs.Count} (lastError={err})");
            return false;
        }

        return true;
    }
}
