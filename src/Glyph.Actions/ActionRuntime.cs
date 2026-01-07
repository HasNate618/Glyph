using System.Diagnostics;

using Glyph.Core.Engine;
using Glyph.Core.Logging;
using Glyph.Win32.Input;
using Glyph.Win32.Windowing;
using Glyph.Win32.Audio;
using System.IO;
using Microsoft.Win32;

namespace Glyph.Actions;

public sealed class ActionRuntime
{
    public static readonly IReadOnlySet<string> KnownActionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "launchChrome",
        "openTerminal",
        "openExplorer",
        "openTaskManager",
            "openBrowser",

        "mediaPlayPause",
        "mediaNext",
        "mediaPrev",
        "volumeMute",
        "openSpotify",
        "muteMic",
        "mediaShuffle",

        "windowMinimize",
        "windowMaximize",
        "windowRestore",
        "windowClose",
        "windowTopmost",

        "typeNvimDot",
        "quitGlyph",
    };

    public Task ExecuteAsync(ActionRequest request, CancellationToken cancellationToken)
    {
        // Prototype: hardcoded actions.
        return request.ActionId switch
        {
            "launchChrome" => LaunchAsync(FindChrome(), null, cancellationToken),
            "openTerminal" => OpenDefaultTerminalAsync(cancellationToken),
            "openExplorer" => LaunchAsync("explorer.exe", null, cancellationToken),
            "openTaskManager" => LaunchAsync("taskmgr.exe", null, cancellationToken),
            "openBrowser" => OpenDefaultBrowser(cancellationToken),

            // Media actions
            "mediaPlayPause" => MediaKeyAsync(NativeMediaKey.MEDIA_PLAY_PAUSE, cancellationToken),
            "mediaNext" => MediaKeyAsync(NativeMediaKey.MEDIA_NEXT_TRACK, cancellationToken),
            "mediaPrev" => MediaKeyAsync(NativeMediaKey.MEDIA_PREV_TRACK, cancellationToken),
            "volumeMute" => MediaKeyAsync(NativeMediaKey.VOLUME_MUTE, cancellationToken),
            "openSpotify" => LaunchAsync("spotify:", null, cancellationToken),
            "muteMic" => ToggleMicAsync(cancellationToken),
            "mediaShuffle" => OpenSpotifyAndAttemptShuffle(cancellationToken),

            // Window management
            "windowMinimize" => WindowManagerActionAsync(WindowAction.Minimize, cancellationToken),
            "windowMaximize" => WindowManagerActionAsync(WindowAction.Maximize, cancellationToken),
            "windowRestore" => WindowManagerActionAsync(WindowAction.Restore, cancellationToken),
            "windowClose" => WindowManagerActionAsync(WindowAction.Close, cancellationToken),
            "windowTopmost" => WindowManagerActionAsync(WindowAction.ToggleTopmost, cancellationToken),

            "typeNvimDot" => TypeTextAsync("nvim .", cancellationToken),
            _ => Task.CompletedTask,
        };
    }

    private static Task MediaKeyAsync(NativeMediaKey key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            Glyph.Win32.Input.InputSender.SendMediaKey((ushort)key);
        }
        catch (Exception ex)
        {
            Logger.Error("Error sending media key", ex);
        }

        return Task.CompletedTask;
    }

    private static Task ToggleMicAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            MicrophoneToggle.ToggleDefaultCaptureMute();
        }
        catch (Exception ex)
        {
            Logger.Error("Error toggling microphone mute", ex);
        }

        return Task.CompletedTask;
    }

    private static Task OpenDefaultBrowser(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            // Try to detect the system default browser executable from the registry.
            string? defaultExe = GetDefaultBrowserExe();

            // If default browser exe detected, try to focus a running instance.
            if (!string.IsNullOrWhiteSpace(defaultExe))
            {
                try
                {
                    var exeName = Path.GetFileNameWithoutExtension(defaultExe);
                    if (!string.IsNullOrWhiteSpace(exeName))
                    {
                        var procs = System.Diagnostics.Process.GetProcessesByName(exeName);
                        foreach (var p in procs)
                        {
                            try
                            {
                                var h = p.MainWindowHandle;
                                if (h != IntPtr.Zero)
                                {
                                    SetForegroundWindow(h);
                                    return Task.CompletedTask;
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch { }

                // Not running: open a safe HTTPS URL using the default handler
                // rather than launching a specific browser executable. This
                // ensures the user's system default browser is used.
                var psiFallback = new ProcessStartInfo
                {
                    FileName = "https://www.bing.com",
                    UseShellExecute = true,
                };
                Process.Start(psiFallback);
                return Task.CompletedTask;
            }

            // As a last resort, open a safe HTTPS URL using the system default handler.
            var psi = new ProcessStartInfo
            {
                FileName = "https://www.bing.com",
                UseShellExecute = true,
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            Logger.Error("Error opening default browser", ex);
        }

        return Task.CompletedTask;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private static string? GetDefaultBrowserExe()
    {
        try
        {
            // HKEY_CLASSES_ROOT\http\shell\open\command often contains the default command.
            var val = Registry.GetValue("HKEY_CLASSES_ROOT\\http\\shell\\open\\command", null, null) as string;
            if (string.IsNullOrWhiteSpace(val))
            {
                // Try HKCU user choice fallback
                val = Registry.GetValue("HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\Shell\\Associations\\UrlAssociations\\http\\UserChoice", "Progid", null) as string;
                if (!string.IsNullOrWhiteSpace(val))
                {
                    // Resolve ProgId to command
                    var key = $"HKEY_CLASSES_ROOT\\{val}\\shell\\open\\command";
                    val = Registry.GetValue(key, null, null) as string;
                }
            }

            if (string.IsNullOrWhiteSpace(val)) return null;

            // Extract the exe path (naive but practical): find first .exe and expand left/right.
            var idx = val.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;

            // include up to .exe
            var end = idx + 4;

            // find start: quoted or whitespace
            var start = val.LastIndexOf('"', idx);
            string path;
            if (start >= 0)
            {
                // quoted path
                path = val.Substring(start + 1, end - start - 1);
            }
            else
            {
                // unquoted: backtrack to space
                start = val.LastIndexOf(' ', idx);
                var s = (start >= 0) ? start + 1 : 0;
                path = val.Substring(s, end - s);
            }

            if (File.Exists(path)) return path;
            return null;
        }
        catch
        {
            return null;
        }
    }

    private static Task OpenDefaultTerminalAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Prefer Windows Terminal when available, then PowerShell (pwsh/powershell),
        // then COMSPEC (usually cmd.exe), finally fallback to cmd.exe.
        var candidates = new[]
        {
            (exe: "wt.exe", args: string.Empty),
            (exe: "pwsh.exe", args: string.Empty),
            (exe: "powershell.exe", args: string.Empty),
            (exe: Environment.GetEnvironmentVariable("COMSPEC") ?? string.Empty, args: string.Empty),
            (exe: "cmd.exe", args: string.Empty),
        };

        foreach (var c in candidates)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(c.exe)) continue;
                var psi = new ProcessStartInfo
                {
                    FileName = c.exe,
                    Arguments = c.args,
                    UseShellExecute = true,
                };
                Process.Start(psi);
                return Task.CompletedTask;
            }
            catch
            {
                // Try next candidate
            }
        }

        return Task.CompletedTask;
    }

    private static Task OpenSpotifyAndAttemptShuffle(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            // Best-effort: bring Spotify to foreground (via URI) and rely on user to toggle shuffle.
            // Advanced integration (remote control) is out of scope for prototype.
            LaunchAsync("spotify:", null, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.Error("Error opening Spotify for shuffle", ex);
        }

        return Task.CompletedTask;
    }

    private enum NativeMediaKey : ushort
    {
        MEDIA_NEXT_TRACK = 0xB0,
        MEDIA_PREV_TRACK = 0xB1,
        MEDIA_PLAY_PAUSE = 0xB3,
        VOLUME_MUTE = 0xAD,
        VOLUME_DOWN = 0xAE,
        VOLUME_UP = 0xAF,
    }

    private enum WindowAction
    {
        Minimize,
        Maximize,
        Restore,
        Close,
        ToggleTopmost,
    }

    private static Task WindowManagerActionAsync(WindowAction action, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            switch (action)
            {
                case WindowAction.Minimize:
                    WindowManager.MinimizeForeground();
                    break;
                case WindowAction.Maximize:
                    WindowManager.MaximizeForeground();
                    break;
                case WindowAction.Restore:
                    WindowManager.RestoreForeground();
                    break;
                case WindowAction.Close:
                    WindowManager.CloseForeground();
                    break;
                case WindowAction.ToggleTopmost:
                    WindowManager.ToggleTopmostForeground();
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Error performing window manager action", ex);
        }

        return Task.CompletedTask;
    }

    private static string FindChrome()
    {
        var candidates = new[]
        {
            @"C:\Program Files\Google\Chrome\Application\chrome.exe",
            @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
            @"C:\Users\natee\AppData\Local\Google\Chrome\Application\chrome.exe",
        };
        
        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }
        
        return "chrome";
    }

    private static Task LaunchAsync(string exe, string? args, CancellationToken cancellationToken, string? fallbackExe = null)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            Logger.Info($"Launching: {exe} {args ?? string.Empty}".Trim());
            
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args ?? string.Empty,
                UseShellExecute = true,
            };

            var proc = Process.Start(psi);
            if (proc is null)
            {
                Logger.Error($"Failed to start process: {exe}");
            }
            else
            {
                Logger.Info($"Process started (PID: {proc.Id}): {exe}");
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Error launching {exe}", ex);
            if (!string.IsNullOrWhiteSpace(fallbackExe))
            {
                try
                {
                    Logger.Info($"Falling back to: {fallbackExe}");
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = fallbackExe,
                        UseShellExecute = true,
                    });
                }
                catch (Exception ex2)
                {
                    Logger.Error($"Fallback failed: {fallbackExe}", ex2);
                }
            }
        }
        
        return Task.CompletedTask;
    }

    private static async Task TypeTextAsync(string text, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            Logger.Info($"Typing text: {text}");

            // Give the UI thread a moment to hide the overlay and let the target app
            // reliably remain the foreground receiver.
            // Removed delay to send input instantly.

            try
            {
                var active = Glyph.Win32.Windowing.ForegroundApp.TryGetProcessName();
                if (string.Equals(active, "WindowsTerminal", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(active, "wt", StringComparison.OrdinalIgnoreCase))
                {
                    // Use clipboard paste for Windows Terminal which may not accept
                    // direct unicode SendInput reliably in some environments.
                    var setOk = Glyph.Win32.Clipboard.ClipboardHelper.SetText(text);
                    Logger.Info($"Clipboard set for paste: {(setOk ? "ok" : "failed")}");
                    // Removed delay to send input instantly.

                    // IMPORTANT: only send one paste gesture, otherwise text can be pasted multiple times.
                    // Prefer Ctrl+Shift+V (Windows Terminal default), fall back if SendInput fails.
                    var pasteOk = InputSender.SendCtrlShiftV();
                    Logger.Info($"Paste keystroke sent (Ctrl+Shift+V): {(pasteOk ? "ok" : "failed")}");

                    if (!pasteOk)
                    {
                        pasteOk = InputSender.SendCtrlV();
                        Logger.Info($"Paste keystroke sent (Ctrl+V): {(pasteOk ? "ok" : "failed")}");
                    }

                    if (!pasteOk)
                    {
                        pasteOk = InputSender.SendShiftInsert();
                        Logger.Info($"Paste keystroke sent (Shift+Insert): {(pasteOk ? "ok" : "failed")}");
                    }

                    var enterOk = InputSender.SendEnter();
                    Logger.Info($"Enter sent: {(enterOk ? "ok" : "failed")}");
                    return;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error preparing clipboard paste fallback", ex);
            }

            _ = InputSender.SendText(text);
            _ = InputSender.SendEnter();
        }
        catch (Exception ex)
        {
            Logger.Error("Error typing text", ex);
        }
    }
}
