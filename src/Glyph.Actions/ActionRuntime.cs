using System.Diagnostics;

using Glyph.Core.Engine;
using Glyph.Core.Logging;
using Glyph.Win32.Input;

namespace Glyph.Actions;

public sealed class ActionRuntime
{
    public Task ExecuteAsync(ActionRequest request, CancellationToken cancellationToken)
    {
        // Prototype: hardcoded actions.
        return request.ActionId switch
        {
            "launchChrome" => LaunchAsync(FindChrome(), null, cancellationToken),
            "openTerminal" => LaunchAsync("wt.exe", "-d \"%USERPROFILE%\"", cancellationToken, fallbackExe: "cmd.exe"),
            "openExplorer" => LaunchAsync("explorer.exe", null, cancellationToken),
            "openTaskManager" => LaunchAsync("taskmgr.exe", null, cancellationToken),

            "typeNvimDot" => TypeTextAsync("nvim .", cancellationToken),
            _ => Task.CompletedTask,
        };
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
