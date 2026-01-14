using System.Runtime.InteropServices;

namespace Glyph.Win32.Windowing;

public static class WindowEffects
{
    private static readonly int DefaultAcrylicColorAbgr = ToAbgr(0x99, 0x1B, 0x1B, 0x1B);

    // Best-effort native backdrop:
    // - On Windows 11: try DWMWA_SYSTEMBACKDROP_TYPE (mica-like)
    // - Fallback: SetWindowCompositionAttribute blur/acrylic
    public static void ApplyBestEffortBackdrop(IntPtr hwnd)
    {
        // Try backdrops in a "most likely to be visible" order.
        try
        {
            if (TryApplyAccentHostBackdrop(hwnd))
            {
                Glyph.Core.Logging.Logger.Info("Applied accent host backdrop");
                return;
            }

            if (TryApplyDwmSystemBackdrop(hwnd, 3 /* Transient */))
            {
                Glyph.Core.Logging.Logger.Info("Applied DWM system backdrop (mica)");
                return;
            }

            if (TryApplyAccentAcrylic(hwnd, DefaultAcrylicColorAbgr))
            {
                Glyph.Core.Logging.Logger.Info("Applied accent acrylic blur");
                return;
            }

            Glyph.Core.Logging.Logger.Info("No backdrop effect applied; falling back to plain window");
        }
        catch (Exception ex)
        {
            try { Glyph.Core.Logging.Logger.Error("Error applying backdrop", ex); } catch {}
        }
    }

    // Theme-driven backdrop selection.
    // mode values (case-insensitive):
    // - Auto (default): same as ApplyBestEffortBackdrop
    // - None: do nothing
    // - DwmMain | DwmTransient | DwmTabbed: DWMWA_SYSTEMBACKDROP_TYPE = 2/3/4
    // - Acrylic: accent acrylic blur behind (color from acrylicColor)
    // - Blur: classic blur behind
    // - HostBackdrop: host backdrop (best effort)
    public static void ApplyBackdrop(IntPtr hwnd, string mode, string? acrylicColor)
    {
        try
        {
            var m = (mode ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(m) || string.Equals(m, "Auto", StringComparison.OrdinalIgnoreCase))
            {
                try { Glyph.Core.Logging.Logger.Info("Backdrop mode: Auto"); } catch {}
                ApplyBestEffortBackdrop(hwnd);
                return;
            }

            if (string.Equals(m, "None", StringComparison.OrdinalIgnoreCase))
            {
                try { Glyph.Core.Logging.Logger.Info("Backdrop mode: None"); } catch {}
                return;
            }

            if (string.Equals(m, "DwmMain", StringComparison.OrdinalIgnoreCase))
            {
                var ok = TryApplyDwmSystemBackdrop(hwnd, 2);
                try { Glyph.Core.Logging.Logger.Info($"Backdrop mode: DwmMain (ok={ok})"); } catch {}
                if (!ok)
                {
                    ApplyBestEffortBackdrop(hwnd);
                }
                return;
            }

            if (string.Equals(m, "DwmTransient", StringComparison.OrdinalIgnoreCase))
            {
                var ok = TryApplyDwmSystemBackdrop(hwnd, 3);
                try { Glyph.Core.Logging.Logger.Info($"Backdrop mode: DwmTransient (ok={ok})"); } catch {}
                if (!ok)
                {
                    ApplyBestEffortBackdrop(hwnd);
                }
                return;
            }

            if (string.Equals(m, "DwmTabbed", StringComparison.OrdinalIgnoreCase))
            {
                var ok = TryApplyDwmSystemBackdrop(hwnd, 4);
                try { Glyph.Core.Logging.Logger.Info($"Backdrop mode: DwmTabbed (ok={ok})"); } catch {}
                if (!ok)
                {
                    ApplyBestEffortBackdrop(hwnd);
                }
                return;
            }

            if (string.Equals(m, "Acrylic", StringComparison.OrdinalIgnoreCase))
            {
                var color = ParseAcrylicColor(acrylicColor) ?? DefaultAcrylicColorAbgr;
                var ok = TryApplyAccentAcrylic(hwnd, color);
                try { Glyph.Core.Logging.Logger.Info($"Backdrop mode: Acrylic (ok={ok})"); } catch {}
                if (!ok)
                {
                    ApplyBestEffortBackdrop(hwnd);
                }
                return;
            }

            if (string.Equals(m, "Blur", StringComparison.OrdinalIgnoreCase))
            {
                var ok = TryApplyAccentBlur(hwnd);
                try { Glyph.Core.Logging.Logger.Info($"Backdrop mode: Blur (ok={ok})"); } catch {}
                if (!ok)
                {
                    ApplyBestEffortBackdrop(hwnd);
                }
                return;
            }

            if (string.Equals(m, "HostBackdrop", StringComparison.OrdinalIgnoreCase))
            {
                var ok = TryApplyAccentHostBackdrop(hwnd);
                try { Glyph.Core.Logging.Logger.Info($"Backdrop mode: HostBackdrop (ok={ok})"); } catch {}
                if (!ok)
                {
                    ApplyBestEffortBackdrop(hwnd);
                }
                return;
            }

            // Unknown => best effort.
            ApplyBestEffortBackdrop(hwnd);
        }
        catch
        {
            // best-effort
        }
    }

    private static bool TryApplyDwmSystemBackdrop(IntPtr hwnd, int systemBackdropType)
    {
        try
        {
            LogLayeredStatus(hwnd);
            TryExtendFrameIntoClientArea(hwnd);

            // DWMWA_SYSTEMBACKDROP_TYPE = 38
            // Values: 0=Auto, 1=None, 2=MainWindow, 3=TransientWindow, 4=TabbedWindow
            const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

            int value = systemBackdropType;
            var hr = DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref value, Marshal.SizeOf<int>());
            try { Glyph.Core.Logging.Logger.Info($"DwmSetWindowAttribute(DWMWA_SYSTEMBACKDROP_TYPE={systemBackdropType}) hr=0x{hr:X8}"); } catch {}
            return hr == 0;
        }
        catch
        {
            return false;
        }
    }
    public static void TrySetRoundedCorners(IntPtr hwnd)
    {
        try
        {
            // DWMWA_WINDOW_CORNER_PREFERENCE = 33
            // DWMWCP_ROUND = 2
            const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
            int preference = 2;
            var hr = DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, Marshal.SizeOf<int>());
            try
            {
                Glyph.Core.Logging.Logger.Info($"Set rounded corners result: {hr}");
            }
            catch
            {
            }
        }
        catch
        {
            // ignore
        }
    }

    private static bool TryApplyAccentAcrylic(IntPtr hwnd, int gradientColor)
    {
        try
        {
            TryExtendFrameIntoClientArea(hwnd);
            var accent = new ACCENT_POLICY
            {
                AccentState = ACCENT_STATE.ACCENT_ENABLE_ACRYLICBLURBEHIND,
                // Many implementations use 2 here; it can improve consistency.
                AccentFlags = 2,
                // Use a semi-transparent gradient color (ARGB). 0x99 = ~60% alpha.
                GradientColor = gradientColor,
                AnimationId = 0
            };

            var size = Marshal.SizeOf<ACCENT_POLICY>();
            var accentPtr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(accent, accentPtr, false);

                var data = new WINDOWCOMPOSITIONATTRIBDATA
                {
                    Attribute = WINDOWCOMPOSITIONATTRIB.WCA_ACCENT_POLICY,
                    Data = accentPtr,
                    SizeOfData = size
                };

                return SetWindowCompositionAttribute(hwnd, ref data) != 0;
            }
            finally
            {
                Marshal.FreeHGlobal(accentPtr);
            }
        }
        catch
        {
            return false;
        }
    }

    private static bool TryApplyAccentBlur(IntPtr hwnd)
    {
        try
        {
            TryExtendFrameIntoClientArea(hwnd);
            var accent = new ACCENT_POLICY
            {
                AccentState = ACCENT_STATE.ACCENT_ENABLE_BLURBEHIND,
                AccentFlags = 0,
                GradientColor = 0,
                AnimationId = 0
            };

            return ApplyAccent(hwnd, accent);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryApplyAccentHostBackdrop(IntPtr hwnd)
    {
        try
        {
            TryExtendFrameIntoClientArea(hwnd);
            var accent = new ACCENT_POLICY
            {
                AccentState = ACCENT_STATE.ACCENT_ENABLE_HOSTBACKDROP,
                AccentFlags = 0,
                GradientColor = 0,
                AnimationId = 0
            };

            return ApplyAccent(hwnd, accent);
        }
        catch
        {
            return false;
        }
    }

    private static bool ApplyAccent(IntPtr hwnd, ACCENT_POLICY accent)
    {
        LogLayeredStatus(hwnd);
        var size = Marshal.SizeOf<ACCENT_POLICY>();
        var accentPtr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(accent, accentPtr, false);

            var data = new WINDOWCOMPOSITIONATTRIBDATA
            {
                Attribute = WINDOWCOMPOSITIONATTRIB.WCA_ACCENT_POLICY,
                Data = accentPtr,
                SizeOfData = size
            };

            var ok = SetWindowCompositionAttribute(hwnd, ref data) != 0;
            try { Glyph.Core.Logging.Logger.Info($"SetWindowCompositionAttribute ok={ok} state={accent.AccentState}"); } catch {}
            return ok;
        }
        finally
        {
            Marshal.FreeHGlobal(accentPtr);
        }
    }

    private static void TryExtendFrameIntoClientArea(IntPtr hwnd)
    {
        try
        {
            // Mica/system backdrop often requires the frame to be extended into the client area.
            var margins = new MARGINS { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
            var hr = DwmExtendFrameIntoClientArea(hwnd, ref margins);
            try { Glyph.Core.Logging.Logger.Info($"DwmExtendFrameIntoClientArea hr=0x{hr:X8}"); } catch {}
        }
        catch
        {
            // ignore
        }
    }

    private static void LogLayeredStatus(IntPtr hwnd)
    {
        try
        {
            const int GWL_EXSTYLE = -20;
            const int WS_EX_LAYERED = 0x00080000;

            var exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE);
            var isLayered = (((long)exStyle) & WS_EX_LAYERED) != 0;
            try { Glyph.Core.Logging.Logger.Info($"Window exstyle=0x{((long)exStyle):X} layered={isLayered}"); } catch {}
        }
        catch
        {
        }
    }

    private static int? ParseAcrylicColor(string? s)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            var text = s.Trim();
            if (text.StartsWith("#", StringComparison.Ordinal))
            {
                text = text.Substring(1);
            }

            // Accept AARRGGBB or RRGGBB (assumes FF alpha).
            if (text.Length == 6)
            {
                text = "FF" + text;
            }
            if (text.Length != 8)
            {
                return null;
            }

            var a = Convert.ToByte(text.Substring(0, 2), 16);
            var r = Convert.ToByte(text.Substring(2, 2), 16);
            var g = Convert.ToByte(text.Substring(4, 2), 16);
            var b = Convert.ToByte(text.Substring(6, 2), 16);

            // ACCENT_POLICY expects ABGR in a 32-bit int.
            return ToAbgr(a, r, g, b);
        }
        catch
        {
            return null;
        }
    }

    private static int ToAbgr(byte a, byte r, byte g, byte b)
    {
        // ACCENT_POLICY GradientColor expects 0xAABBGGRR.
        return (a << 24) | (b << 16) | (g << 8) | r;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS pMarInset);

    [StructLayout(LayoutKind.Sequential)]
    private struct MARGINS
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;
    }

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WINDOWCOMPOSITIONATTRIBDATA data);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    private enum WINDOWCOMPOSITIONATTRIB
    {
        WCA_ACCENT_POLICY = 19
    }

    private enum ACCENT_STATE
    {
        ACCENT_DISABLED = 0,
        ACCENT_ENABLE_GRADIENT = 1,
        ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
        ACCENT_ENABLE_BLURBEHIND = 3,
        ACCENT_ENABLE_ACRYLICBLURBEHIND = 4,
        ACCENT_ENABLE_HOSTBACKDROP = 5
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WINDOWCOMPOSITIONATTRIBDATA
    {
        public WINDOWCOMPOSITIONATTRIB Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ACCENT_POLICY
    {
        public ACCENT_STATE AccentState;
        public int AccentFlags;
        public int GradientColor;
        public int AnimationId;
    }
}
