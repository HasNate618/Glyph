using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Glyph.Core.Context
{
    public class ContextService
    {
        public event EventHandler<ContextChangedEvent> ContextChanged;

        private string _activeProcessName;
        private string _activeWindowTitle;

        public void UpdateContext()
        {
            var foregroundWindow = GetForegroundWindow();
            GetWindowThreadProcessId(foregroundWindow, out uint processId);
            var process = Process.GetProcessById((int)processId);

            if (process != null)
            {
                var newProcessName = process.ProcessName;
                var newWindowTitle = process.MainWindowTitle;

                if (newProcessName != _activeProcessName || newWindowTitle != _activeWindowTitle)
                {
                    _activeProcessName = newProcessName;
                    _activeWindowTitle = newWindowTitle;

                    OnContextChanged(new ContextChangedEvent(_activeProcessName, _activeWindowTitle));
                }
            }
        }

        protected virtual void OnContextChanged(ContextChangedEvent e)
        {
            ContextChanged?.Invoke(this, e);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    }
}