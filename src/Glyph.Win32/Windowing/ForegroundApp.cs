using System.Diagnostics;

using Glyph.Win32.Interop;

namespace Glyph.Win32.Windowing;

public static class ForegroundApp
{
    public static string? TryGetProcessName()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            return null;
        }

        NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == 0)
        {
            return null;
        }

        try
        {
            using var proc = Process.GetProcessById((int)pid);
            // ProcessName is without extension.
            return proc.ProcessName;
        }
        catch
        {
            return null;
        }
    }
}
