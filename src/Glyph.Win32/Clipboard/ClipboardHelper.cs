using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

using Glyph.Core.Logging;
using Glyph.Win32.Interop;

namespace Glyph.Win32.Clipboard;

public static class ClipboardHelper
{
    public static bool SetText(string text)
    {
        if (text is null) return false;

        var bytes = Encoding.Unicode.GetBytes(text + '\0');

        var handle = NativeMethods.GlobalAlloc(NativeMethods.GMEM_MOVEABLE, (UIntPtr)bytes.Length);
        if (handle == IntPtr.Zero)
        {
            Logger.Error($"GlobalAlloc failed (lastError={Marshal.GetLastWin32Error()})");
            return false;
        }

        var ptr = NativeMethods.GlobalLock(handle);
        if (ptr == IntPtr.Zero)
        {
            Logger.Error($"GlobalLock failed (lastError={Marshal.GetLastWin32Error()})");
            return false;
        }

        try
        {
            Marshal.Copy(bytes, 0, ptr, bytes.Length);
        }
        finally
        {
            NativeMethods.GlobalUnlock(handle);
        }

        var opened = false;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            if (NativeMethods.OpenClipboard(IntPtr.Zero))
            {
                opened = true;
                break;
            }

            Thread.Sleep(10);
        }

        if (!opened)
        {
            Logger.Error($"OpenClipboard failed (lastError={Marshal.GetLastWin32Error()})");
            return false;
        }

        try
        {
            if (!NativeMethods.EmptyClipboard())
            {
                Logger.Error($"EmptyClipboard failed (lastError={Marshal.GetLastWin32Error()})");
                return false;
            }

            var res = NativeMethods.SetClipboardData(NativeMethods.CF_UNICODETEXT, handle);
            // According to docs, system takes ownership of the memory handle if successful.
            if (res == IntPtr.Zero)
            {
                Logger.Error($"SetClipboardData failed (lastError={Marshal.GetLastWin32Error()})");
                return false;
            }

            return true;
        }
        finally
        {
            NativeMethods.CloseClipboard();
        }
    }
}
