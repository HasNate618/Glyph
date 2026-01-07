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
