using System.Runtime.InteropServices;

namespace Glyph.Win32.Windowing;

public static class MonitorHelper
{
    // MONITORINFO structure
    [StructLayout(LayoutKind.Sequential)]
    public struct MonitorInfo
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    private const uint MONITOR_DEFAULTTOPRIMARY = 1;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

    /// <summary>
    /// Gets the monitor handle that contains the specified window.
    /// Falls back to primary monitor if window is invalid or not found.
    /// </summary>
    public static IntPtr GetMonitorForWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return GetPrimaryMonitor();
        }

        var hMonitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTOPRIMARY);
        return hMonitor != IntPtr.Zero ? hMonitor : GetPrimaryMonitor();
    }

    /// <summary>
    /// Gets the monitor handle that contains the current cursor position.
    /// Falls back to primary monitor if cursor position cannot be determined.
    /// </summary>
    public static IntPtr GetMonitorForCursor()
    {
        try
        {
            if (GetCursorPos(out var pt))
            {
                var hMonitor = MonitorFromPoint(pt, MONITOR_DEFAULTTOPRIMARY);
                return hMonitor != IntPtr.Zero ? hMonitor : GetPrimaryMonitor();
            }
        }
        catch
        {
        }

        return GetPrimaryMonitor();
    }

    /// <summary>
    /// Gets the primary monitor handle.
    /// </summary>
    public static IntPtr GetPrimaryMonitor()
    {
        // Get the monitor for a point on the primary screen (0,0)
        return MonitorFromWindow(IntPtr.Zero, MONITOR_DEFAULTTOPRIMARY);
    }

    /// <summary>
    /// Gets the work area (usable screen space, excluding taskbars) for the specified monitor.
    /// Returns null if monitor info cannot be retrieved.
    /// </summary>
    public static RECT? GetMonitorWorkArea(IntPtr hMonitor)
    {
        if (hMonitor == IntPtr.Zero)
        {
            return null;
        }

        var info = new MonitorInfo { cbSize = (uint)Marshal.SizeOf<MonitorInfo>() };
        if (GetMonitorInfo(hMonitor, ref info))
        {
            return info.rcWork;
        }

        return null;
    }

    /// <summary>
    /// Gets the full bounds (including taskbars) for the specified monitor.
    /// Returns null if monitor info cannot be retrieved.
    /// </summary>
    public static RECT? GetMonitorBounds(IntPtr hMonitor)
    {
        if (hMonitor == IntPtr.Zero)
        {
            return null;
        }

        var info = new MonitorInfo { cbSize = (uint)Marshal.SizeOf<MonitorInfo>() };
        if (GetMonitorInfo(hMonitor, ref info))
        {
            return info.rcMonitor;
        }

        return null;
    }
}
