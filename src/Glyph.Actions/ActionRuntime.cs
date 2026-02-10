using System.Diagnostics;

using Glyph.Core.Actions;
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
        "openBrowser",
        "mediaPlayPause",
        "mediaNext",
        "mediaPrev",
        "volumeMute",
        "openSpotify",
        "mediaShuffle",
        "logForeground",
        "openLogs",
        "openConfig",
        "windowMinimize",
        "openGlyphGui",
        "openGlyphKeymapEditor",
        "quitGlyph",
        "setTheme",
        "reloadKeymaps",
        "toggleBreadcrumbsMode",
        "programSpecificLayer",
    };


    public async Task ExecuteAsync(ActionRequest request, CancellationToken cancellationToken)
    {
        // Support both built-in action ids and inline key/type requests carried by ActionRequest.
        if (request is null) return;

        // If this request is a chained sequence, execute each step in order.
        if (request.Steps is { Count: > 0 })
        {
            foreach (var step in request.Steps)
            {
                await ExecuteAsync(step, cancellationToken);
            }

            return;
        }

        if (!string.IsNullOrWhiteSpace(request.ActionId))
        {
            await ExecuteActionIdAsync(request.ActionId, cancellationToken);
            return;
        }

        // Handle TypeText first, then SendSpec (for chaining)
        if (!string.IsNullOrWhiteSpace(request.TypeText))
        {
            // Resolve built-in placeholders (e.g. {{now:yyyy-MM-dd}}) at trigger time.
            var resolved = Glyph.Actions.Builtins.ResolveBuiltins(request.TypeText);
            await TypeTextAsync(resolved, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(request.SendSpec))
        {
            try
            {
                Glyph.Win32.Input.InputSender.SendChordSpec(request.SendSpec);
            }
            catch (Exception ex)
            {
                Logger.Error("Error sending chord spec", ex);
            }
        }

        if (!string.IsNullOrWhiteSpace(request.ExecPath))
        {
            await LaunchWithCwdAsync(request.ExecPath, request.ExecArgs, request.ExecCwd, cancellationToken);
        }
    }

    private async Task ExecuteActionIdAsync(string actionId, CancellationToken cancellationToken)
    {
        // Parameterized theme selector: setTheme:<ThemeId>
        if (!string.IsNullOrWhiteSpace(actionId) && actionId.StartsWith("setTheme:", StringComparison.OrdinalIgnoreCase))
        {
            var themeId = actionId.Substring("setTheme:".Length).Trim();
            if (!string.IsNullOrWhiteSpace(themeId))
            {
                await SetBaseThemeAsync(themeId, cancellationToken);
            }
            return;
        }

        await (actionId switch
        {
            "launchChrome" => LaunchAsync(FindChrome(), null, cancellationToken),
            
            "openBrowser" => OpenDefaultBrowser(cancellationToken),

            // Media actions
            "mediaPlayPause" => MediaKeyAsync(NativeMediaKey.MEDIA_PLAY_PAUSE, cancellationToken),
            "mediaNext" => MediaKeyAsync(NativeMediaKey.MEDIA_NEXT_TRACK, cancellationToken),
            "mediaPrev" => MediaKeyAsync(NativeMediaKey.MEDIA_PREV_TRACK, cancellationToken),
            "volumeMute" => MediaKeyAsync(NativeMediaKey.VOLUME_MUTE, cancellationToken),
            "openSpotify" => LaunchAsync("spotify:", null, cancellationToken),
            "mediaShuffle" => OpenSpotifyAndAttemptShuffle(cancellationToken),

            // Window management (keep minimize only)
            "windowMinimize" => WindowManagerActionAsync(WindowAction.Minimize, cancellationToken),
            "logForeground" => LogForegroundAsync(cancellationToken),
            "openLogs" => OpenLogsFolderAsync(cancellationToken),
            "openConfig" => OpenConfigFolderAsync(cancellationToken),

            // Special layer marker - no-op
            "programSpecificLayer" => Task.CompletedTask,

            _ => Task.CompletedTask,
        });
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


    private static Task OpenDefaultBrowser(CancellationToken cancellationToken)
    {
        // Revert behavior: open the default system browser to Bing.
        // This intentionally opens a Bing tab in the default browser.
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://www.bing.com",
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Logger.Error("Error opening default browser (Bing)", ex);
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

    

    // Support for ActionRequest.TypeText and ActionRequest.SendSpec is handled below in ExecuteAsync.

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

    private static Task LogForegroundAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var proc = Glyph.Win32.Windowing.ForegroundApp.TryGetProcessName();
            Logger.Info($"Foreground process: {proc ?? "?"}");
        }
        catch (Exception ex)
        {
            Logger.Error("Error logging foreground process", ex);
        }

        return Task.CompletedTask;
    }

    private static Task OpenLogsFolderAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var dir = Glyph.Core.Logging.Logger.LogDirectory;
            Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo
            {
                FileName = dir,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Logger.Error("Error opening logs folder", ex);
        }

        return Task.CompletedTask;
    }

    private static Task OpenConfigFolderAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Glyph");
            Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo
            {
                FileName = dir,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Logger.Error("Error opening config folder", ex);
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
            // Normalize values: remove surrounding quotes and expand environment variables
            var exeNorm = NormalizeExecString(exe);
            var argsNorm = string.IsNullOrWhiteSpace(args) ? string.Empty : Environment.ExpandEnvironmentVariables(args.Trim());

            Logger.Info($"Launching: {exeNorm} {argsNorm}".Trim());

            var psi = new ProcessStartInfo
            {
                FileName = exeNorm,
                Arguments = argsNorm,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            psi.WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

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

    private static Task SetBaseThemeAsync(string baseName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            // Persist selection into config.json
            try
            {
                var cfgPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Glyph",
                    "config.json");

                var dir = Path.GetDirectoryName(cfgPath);
                if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);

                // Read existing config if present, merge BaseTheme.
                var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                Dictionary<string, object?> cfg = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                if (File.Exists(cfgPath))
                {
                    try
                    {
                        var existing = File.ReadAllText(cfgPath);
                        var parsed = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(existing);
                        if (parsed is not null)
                        {
                            cfg = parsed;
                        }
                    }
                    catch
                    {
                        // ignore parse errors and overwrite
                    }
                }

                cfg["BaseTheme"] = baseName;
                File.WriteAllText(cfgPath, System.Text.Json.JsonSerializer.Serialize(cfg, options));
                Logger.Info($"Set theme to: {baseName} (saved to config.json)");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to persist theme selection: {baseName}", ex);
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to set base theme to {baseName}", ex);
        }

        return Task.CompletedTask;
    }

    private static Task LaunchWithCwdAsync(string exe, string? args, string? workingDirectory, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            // Normalize and expand env vars; trim surrounding quotes which may be present in YAML
            var exeNorm = NormalizeExecString(exe);
            var argsNorm = string.IsNullOrWhiteSpace(args) ? string.Empty : Environment.ExpandEnvironmentVariables(args.Trim());
            var cwdNorm = string.IsNullOrWhiteSpace(workingDirectory) ? null : NormalizeExecString(workingDirectory);

            Logger.Info($"Launching: {exeNorm} {argsNorm} (cwd: {cwdNorm ?? ""})".Trim());

            var psi = new ProcessStartInfo
            {
                FileName = exeNorm,
                Arguments = argsNorm,
                UseShellExecute = true,
            };
            psi.WorkingDirectory = cwdNorm ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

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
        }

        return Task.CompletedTask;
    }

    private static string NormalizeExecString(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;

        var t = s.Trim();

        // Remove surrounding matching quotes if present
        if (t.Length >= 2)
        {
            if ((t[0] == '"' && t[^1] == '"') || (t[0] == '\'' && t[^1] == '\''))
            {
                t = t.Substring(1, t.Length - 2).Trim();
            }
        }

        try
        {
            return Environment.ExpandEnvironmentVariables(t);
        }
        catch
        {
            return t;
        }
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

                    // Don't auto-send Enter; let the user add `then: Enter` if needed
                    return;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error preparing clipboard paste fallback", ex);
            }

            _ = InputSender.SendText(text);
            // Don't auto-send Enter; let the user add `then: Enter` if needed
        }
        catch (Exception ex)
        {
            Logger.Error("Error typing text", ex);
        }
    }
}