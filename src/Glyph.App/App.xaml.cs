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

            if (!string.IsNullOrWhiteSpace(cfg.BaseTheme))
            {
                var selectedPath = Glyph.App.Overlay.Theming.ThemeManager.DefaultThemeSelectedPath;
                var legacyPath = Glyph.App.Overlay.Theming.ThemeManager.DefaultBaseThemeSelectorPath;
                System.IO.File.WriteAllText(selectedPath, cfg.BaseTheme);
                System.IO.File.WriteAllText(legacyPath, cfg.BaseTheme);
                Glyph.App.Overlay.Theming.ThemeManager.Reload();
            }
        }
        catch
        {
            // best-effort
        }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

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

