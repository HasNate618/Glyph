using System.Runtime.InteropServices;

namespace Glyph.Win32.Windowing;

public static class WindowEffects
{
    // Best-effort native backdrop:
    // - On Windows 11: try DWMWA_SYSTEMBACKDROP_TYPE (mica-like)
    // - Fallback: SetWindowCompositionAttribute blur/acrylic
    public static void ApplyBestEffortBackdrop(IntPtr hwnd)
    {
        // Try Windows 11 system backdrop first.
        try
        {
            if (TryApplyDwmSystemBackdrop(hwnd))
            {
                Glyph.Core.Logging.Logger.Info("Applied DWM system backdrop (mica)");
                return;
            }

            if (TryApplyAccentBlur(hwnd))
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

    private static bool TryApplyDwmSystemBackdrop(IntPtr hwnd)
    {
        try
        {
            // DWMWA_SYSTEMBACKDROP_TYPE = 38
            // Values: 0=Auto, 1=None, 2=MainWindow, 3=TransientWindow, 4=TabbedWindow
            const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
            const int DWMSBT_TRANSIENTWINDOW = 3;

            int value = DWMSBT_TRANSIENTWINDOW;
            var hr = DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref value, Marshal.SizeOf<int>());
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

    private static bool TryApplyAccentBlur(IntPtr hwnd)
    {
        try
        {
            var accent = new ACCENT_POLICY
            {
                AccentState = ACCENT_STATE.ACCENT_ENABLE_ACRYLICBLURBEHIND,
                AccentFlags = 0,
                // Use a semi-transparent gradient color (ARGB). 0x99 = ~60% alpha.
                GradientColor = unchecked((int)0x991B1B1B),
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

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WINDOWCOMPOSITIONATTRIBDATA data);

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
