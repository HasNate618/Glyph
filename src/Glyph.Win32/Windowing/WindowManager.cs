using System;
using System.Runtime.InteropServices;

namespace Glyph.Win32.Windowing;

public static class WindowManager
{
    public static void MinimizeForeground()
    {
        var hwnd = Glyph.Win32.Interop.NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return;
        // SC_MINIMIZE = 0xF020
        PostMessage(hwnd, WM_SYSCOMMAND, new IntPtr(SC_MINIMIZE), IntPtr.Zero);
    }

    public static void MaximizeForeground()
    {
        var hwnd = Glyph.Win32.Interop.NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return;
        // SC_MAXIMIZE = 0xF030
        PostMessage(hwnd, WM_SYSCOMMAND, new IntPtr(SC_MAXIMIZE), IntPtr.Zero);
    }

    public static void RestoreForeground()
    {
        var hwnd = Glyph.Win32.Interop.NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return;
        // SC_RESTORE = 0xF120
        PostMessage(hwnd, WM_SYSCOMMAND, new IntPtr(SC_RESTORE), IntPtr.Zero);
    }

    public static void CloseForeground()
    {
        var hwnd = Glyph.Win32.Interop.NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return;
        const int WM_CLOSE = 0x0010;
        PostMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
    }

    public static void ToggleTopmostForeground()
    {
        var hwnd = Glyph.Win32.Interop.NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return;

        const uint SWP_NOSIZE = 0x0001;
        const uint SWP_NOMOVE = 0x0002;
        const uint SWP_NOACTIVATE = 0x0010;
        const int HWND_TOPMOST = -1;
        const int HWND_NOTOPMOST = -2;

        // For prototype we simply set topmost; toggling detection is more involved.
        SetWindowPos(hwnd, new IntPtr(HWND_TOPMOST), 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    private const int WM_SYSCOMMAND = 0x0112;
    private const int SC_MINIMIZE = 0xF020;
    private const int SC_MAXIMIZE = 0xF030;
    private const int SC_RESTORE = 0xF120;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
}
