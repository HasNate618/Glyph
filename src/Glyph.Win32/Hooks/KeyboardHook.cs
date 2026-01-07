using System.Runtime.InteropServices;

using Glyph.Win32.Interop;

namespace Glyph.Win32.Hooks;

public sealed class KeyboardHook : IDisposable
{
    private readonly NativeMethods.LowLevelKeyboardProc _proc;
    private IntPtr _hookHandle;

    public event EventHandler<KeyboardHookEventArgs>? KeyDown;
    public event EventHandler<KeyboardHookEventArgs>? KeyUp;

    public KeyboardHook()
    {
        _proc = HookCallback;
    }

    public void Start()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            return;
        }

        var module = NativeMethods.GetModuleHandle(null);
        _hookHandle = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _proc, module, 0);
        if (_hookHandle == IntPtr.Zero)
        {
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    public void Stop()
    {
        if (_hookHandle == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.UnhookWindowsHookEx(_hookHandle);
        _hookHandle = IntPtr.Zero;
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var message = wParam.ToInt32();
            if (message == NativeMethods.WM_KEYDOWN || message == NativeMethods.WM_SYSKEYDOWN)
            {
                var data = Marshal.PtrToStructure<NativeMethods.KbdLlHookStruct>(lParam);

                // Don't process injected input (e.g., our own SendInput typing).
                if ((data.flags & (NativeMethods.LLKHF_INJECTED | NativeMethods.LLKHF_LOWER_IL_INJECTED)) != 0)
                {
                    return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
                }

                var args = new KeyboardHookEventArgs(data.vkCode, data.scanCode, data.flags);
                KeyDown?.Invoke(this, args);

                if (args.Suppress)
                {
                    return (IntPtr)1;
                }
            }
            else if (message == NativeMethods.WM_KEYUP || message == NativeMethods.WM_SYSKEYUP)
            {
                var data = Marshal.PtrToStructure<NativeMethods.KbdLlHookStruct>(lParam);

                // Don't process injected input.
                if ((data.flags & (NativeMethods.LLKHF_INJECTED | NativeMethods.LLKHF_LOWER_IL_INJECTED)) != 0)
                {
                    return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
                }

                var args = new KeyboardHookEventArgs(data.vkCode, data.scanCode, data.flags);
                KeyUp?.Invoke(this, args);

                if (args.Suppress)
                {
                    return (IntPtr)1;
                }
            }
        }

        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }
}

public sealed class KeyboardHookEventArgs : EventArgs
{
    public KeyboardHookEventArgs(int vkCode, int scanCode, int flags)
    {
        VkCode = vkCode;
        ScanCode = scanCode;
        Flags = flags;
    }

    public int VkCode { get; }
    public int ScanCode { get; }
    public int Flags { get; }
    public bool Suppress { get; set; }
}
