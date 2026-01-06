using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Glyph.Win32.Hooks
{
    public class WinEventHook
    {
        private const int EVENT_SYSTEM_FOREGROUND = 0x0003;
        private const int EVENT_SYSTEM_DESKTOPSWITCH = 0x0020;

        private readonly WinEventDelegate _winEventDelegate;
        private IntPtr _hookHandle;

        public WinEventHook()
        {
            _winEventDelegate = new WinEventDelegate(WinEventProc);
        }

        public void Start()
        {
            _hookHandle = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, _winEventDelegate, 0, 0, 0);
        }

        public void Stop()
        {
            if (_hookHandle != IntPtr.Zero)
            {
                UnhookWinEvent(_hookHandle);
                _hookHandle = IntPtr.Zero;
            }
        }

        private void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            // Handle the event (e.g., log the foreground window change)
            Debug.WriteLine($"Event: {eventType}, Window Handle: {hwnd}");
        }

        private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);
    }
}