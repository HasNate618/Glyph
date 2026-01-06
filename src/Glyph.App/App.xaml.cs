using System.Windows;

using Glyph.Core.Logging;

namespace Glyph.App;

public partial class App : Application
{
    private GlyphHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        Logger.Info("Glyph.App starting (background mode)");
        _host = new GlyphHost();
        _host.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _host?.Dispose();
        }
        catch
        {
            // Best-effort cleanup; logging may already be shutting down.
        }

        base.OnExit(e);
    }
}

