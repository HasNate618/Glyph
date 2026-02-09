using System.Linq;
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

        // Check if the foreground app is Notepad - it needs slower, batched input
        var isNotepad = false;
        try
        {
            var active = Glyph.Win32.Windowing.ForegroundApp.TryGetProcessName();
            isNotepad = string.Equals(active, "notepad", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(active, "notepad.exe", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            // If we can't detect, assume not Notepad and use fast path
        }

        if (isNotepad)
        {
            // Notepad is slow - send in small batches with delays
            const int batchSize = 10; // Send 10 characters at a time
            var allInputs = new List<NativeMethods.INPUT>(text.Length * 2);

            foreach (var ch in text)
            {
                // key down
                allInputs.Add(new NativeMethods.INPUT
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
                allInputs.Add(new NativeMethods.INPUT
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

            // Send in batches with small delays
            var success = true;
            for (int i = 0; i < allInputs.Count; i += batchSize * 2) // *2 because each char has down+up
            {
                var batch = allInputs.Skip(i).Take(batchSize * 2).ToList();
                if (batch.Count > 0)
                {
                    success &= SendInputs(batch);
                    if (i + batchSize * 2 < allInputs.Count)
                    {
                        // Small delay between batches to let Notepad catch up
                        System.Threading.Thread.Sleep(5);
                    }
                }
            }

            return success;
        }
        else
        {
            // Fast path for other apps - send all at once
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

        // Support multi-step send sequences using spaces, matching common chord notation:
        //   "Ctrl+K S" -> send Ctrl+K, then S
        // If there are no spaces, this is treated as a single chord.
        var steps = spec.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (steps.Length > 1)
        {
            var ok = true;
            foreach (var step in steps)
            {
                ok &= SendChordSpec(step);
                // Small delay helps some apps reliably receive multi-step chords.
                System.Threading.Thread.Sleep(10);
            }

            return ok;
        }

        var parts = spec.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return false;

        var keyToken = parts[^1];
        var mods = parts.Length > 1 ? parts.Take(parts.Length - 1).ToArray() : Array.Empty<string>();

        var (vk, impliedShift) = GetVkForToken(keyToken);
        Glyph.Core.Logging.Logger.Info($"SendChordSpec: spec='{spec}' keyToken='{keyToken}' vk=0x{vk:X}");
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

        if (impliedShift && !modVks.Contains((ushort)VirtualKey.VK_SHIFT))
        {
            modVks.Add((ushort)VirtualKey.VK_SHIFT);
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

    private static (ushort Vk, bool ImpliedShift) GetVkForToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return (0, false);
        token = token.Trim();

        // Single-character tokens
        if (token.Length == 1)
        {
            var ch = token[0];
            var up = char.ToUpperInvariant(ch);
            if (up >= 'A' && up <= 'Z') return ((ushort)up, false);
            if (ch >= '0' && ch <= '9') return ((ushort)ch, false);

            // OEM punctuation (US layout virtual keys)
            // See: https://learn.microsoft.com/windows/win32/inputdev/virtual-key-codes
            return ch switch
            {
                ',' => (0xBC, false), // VK_OEM_COMMA
                '.' => (0xBE, false), // VK_OEM_PERIOD
                '/' => (0xBF, false), // VK_OEM_2
                ';' => (0xBA, false), // VK_OEM_1
                '\'' => (0xDE, false), // VK_OEM_7
                '[' => (0xDB, false), // VK_OEM_4
                ']' => (0xDD, false), // VK_OEM_6
                '-' => (0xBD, false), // VK_OEM_MINUS
                '=' => (0xBB, false), // VK_OEM_PLUS
                '\\' => (0xDC, false), // VK_OEM_5
                '`' => (0xC0, false), // VK_OEM_3

                // Common shifted punctuation: map to base VK + implied Shift
                '!' => ((ushort)'1', true),
                '@' => ((ushort)'2', true),
                '#' => ((ushort)'3', true),
                '$' => ((ushort)'4', true),
                '%' => ((ushort)'5', true),
                '^' => ((ushort)'6', true),
                '&' => ((ushort)'7', true),
                '*' => ((ushort)'8', true),
                '(' => ((ushort)'9', true),
                ')' => ((ushort)'0', true),
                '_' => (0xBD, true),
                '+' => (0xBB, true),
                '{' => (0xDB, true),
                '}' => (0xDD, true),
                '|' => (0xDC, true),
                ':' => (0xBA, true),
                '"' => (0xDE, true),
                '<' => (0xBC, true),
                '>' => (0xBE, true),
                '?' => (0xBF, true),
                '~' => (0xC0, true),

                _ => (0, false)
            };
        }

        // Function keys F1-F12
        if (token.StartsWith("F", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(token.Substring(1), out var n) && n >= 1 && n <= 24)
            {
                return ((ushort)(0x70 + (n - 1)), false);
            }
        }

        // Common named keys
        var t = token.ToLowerInvariant();
        if (t == "enter" || t == "return") return (NativeMethods.VK_RETURN, false);
        if (t == "tab") return (0x09, false);
        if (t == "space" || t == " ") return (0x20, false);
        if (t == "capslock" || t == "caps" || t == "capital") return (0x14, false);

        if (t == "left" || t == "arrowleft" || t == "arrow-left" || t == "leftarrow" || t == "left-arrow" || t == "left_arrow") return (0x25, false);
        if (t == "up" || t == "arrowup" || t == "arrow-up" || t == "uparrow" || t == "up-arrow" || t == "up_arrow") return (0x26, false);
        if (t == "right" || t == "arrowright" || t == "arrow-right" || t == "rightarrow" || t == "right-arrow" || t == "right_arrow") return (0x27, false);
        if (t == "down" || t == "arrowdown" || t == "arrow-down" || t == "downarrow" || t == "down-arrow" || t == "down_arrow") return (0x28, false);

        if (t == "home") return (0x24, false);
        if (t == "end") return (0x23, false);
        if (t == "delete" || t == "del") return (0x2E, false);
        if (t == "insert" || t == "ins") return (0x2D, false);
        if (t == "pageup" || t == "page-up" || t == "pgup") return (0x21, false);
        if (t == "pagedown" || t == "page-down" || t == "pgdn") return (0x22, false);
        if (t == "backspace" || t == "bs" || t == "back") return (0x08, false);
        if (t == "esc" || t == "escape") return (0x1B, false);

        // Win as a primary key
        if (t == "win" || t == "lwin" || t == "rwin") return ((ushort)VirtualKey.VK_LWIN, false);

        // OEM tokens / aliases
        if (t == "comma" || t == "oemcomma" || t == "oem-comma") return (0xBC, false);
        if (t == "period" || t == "dot" || t == "oemperiod" || t == "oem-period") return (0xBE, false);
        if (t == "slash" || t == "forwardslash" || t == "oem2" || t == "oem/" || t == "oemslash") return (0xBF, false);
        if (t == "semicolon" || t == "oem1" || t == "oem;") return (0xBA, false);
        if (t == "quote" || t == "apostrophe" || t == "oem7" || t == "oem'") return (0xDE, false);
        if (t == "lbracket" || t == "leftbracket" || t == "oem4" || t == "oem[") return (0xDB, false);
        if (t == "rbracket" || t == "rightbracket" || t == "oem6" || t == "oem]") return (0xDD, false);
        if (t == "minus" || t == "dash" || t == "oemminus" || t == "oem-") return (0xBD, false);
        if (t == "equals" || t == "equal" || t == "oemplus" || t == "oem=") return (0xBB, false);
        if (t == "backslash" || t == "oem5" || t == "oem\\") return (0xDC, false);
        if (t == "backtick" || t == "grave" || t == "oem3" || t == "oem`") return (0xC0, false);
        if (t == "plus") return (0xBB, true);

        return (0, false);
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
