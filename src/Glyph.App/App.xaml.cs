using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;

using Glyph.App.Overlay.Theming;
using Glyph.App.Tray;
using Glyph.Core.Logging;

namespace Glyph.App;

public partial class App : System.Windows.Application
{
    private GlyphHost? _host;
    private TrayIconService? _tray;

    public void ApplyConfig(Glyph.App.Config.AppConfig cfg)
    {
        try
        {
            if (cfg is null) return;

            if (_host is not null)
            {
                var seq = (cfg.GlyphSequence is { Count: > 0 })
                    ? cfg.GlyphSequence
                    : (cfg.Glyph is null ? null : new List<Glyph.App.Config.GlyphKeyConfig> { cfg.Glyph });

                _host.UpdateGlyphSequence(seq);
            }

            // Persist config and reload ThemeManager (theme and breadcrumbs mode)
            try
            {
                Glyph.App.Config.AppConfig.Save(cfg);
            }
            catch
            {
                // best-effort
            }

            Glyph.App.Overlay.Theming.ThemeManager.Reload();
        }
        catch
        {
            // best-effort
        }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        TrySetWorkingDirectoryToExecutableDirectory();

        ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;

        Logger.Info("Glyph.App starting (background mode)");

        // Load user theme overrides (if present) and start hot-reload watcher.
        ThemeManager.Initialize();

        _host = new GlyphHost();
        _host.Start();

        // Tray icon for background UX
        _tray = new TrayIconService();
        _tray.Start();
    }

    private static void TrySetWorkingDirectoryToExecutableDirectory()
    {
        try
        {
            var processPath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(processPath) || !File.Exists(processPath))
            {
                return;
            }

            var resolvedPath = GetFinalPathName(processPath) ?? processPath;
            var dir = Path.GetDirectoryName(resolvedPath);
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
            {
                return;
            }

            Directory.SetCurrentDirectory(dir);
            Logger.Info($"Working directory set to: {dir}");
        }
        catch
        {
            // Best-effort; never fail app startup due to cwd.
        }
    }

    private static string? GetFinalPathName(string path)
    {
        try
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var handle = stream.SafeFileHandle;
            if (handle.IsInvalid)
            {
                return null;
            }

            // 4096 should be plenty for a Windows path.
            Span<char> buffer = stackalloc char[4096];
            var len = GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Length, 0);
            if (len == 0 || len >= buffer.Length)
            {
                return null;
            }

            var full = new string(buffer[..(int)len]);
            // Strip Win32 extended-length prefix if present.
            return full.StartsWith("\\\\?\\", StringComparison.Ordinal) ? full[4..] : full;
        }
        catch
        {
            return null;
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandleW(
        Microsoft.Win32.SafeHandles.SafeFileHandle hFile,
        Span<char> lpszFilePath,
        uint cchFilePath,
        uint dwFlags);

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _tray?.Dispose();
            _host?.Dispose();
        }
        catch
        {
            // Best-effort cleanup; logging may already be shutting down.
        }

        base.OnExit(e);
    }
}

