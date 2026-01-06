using System.Windows;

using Glyph.Actions;
using Glyph.Core.Engine;
using Glyph.Core.Input;
using Glyph.Core.Logging;
using Glyph.Win32.Hooks;
using Glyph.Win32.Interop;
using Glyph.Win32.Windowing;

namespace Glyph.App;

public sealed class GlyphHost : IDisposable
{
    private readonly KeyboardHook _keyboardHook;
    private readonly SequenceEngine _engine;
    private readonly ActionRuntime _actionRuntime;
    private readonly OverlayWindow _overlay;

    public GlyphHost()
    {
        _engine = SequenceEngine.CreatePrototype();
        _actionRuntime = new ActionRuntime();
        _overlay = new OverlayWindow();

        _keyboardHook = new KeyboardHook();
        _keyboardHook.KeyDown += OnGlobalKeyDown;
    }

    public void Start()
    {
        Logger.Info("GlyphHost starting...");
        _keyboardHook.Start();
        Logger.Info("Keyboard hook started (background). Press Ctrl+Shift+NumPad * to activate.");
    }

    public void Dispose()
    {
        _keyboardHook.KeyDown -= OnGlobalKeyDown;
        _keyboardHook.Dispose();
        _overlay.Dispatcher.Invoke(() =>
        {
            if (_overlay.IsVisible)
            {
                _overlay.Hide();
            }
        });
    }

    private void OnGlobalKeyDown(object? sender, KeyboardHookEventArgs e)
    {
        var stroke = KeyStroke.FromVkCode(
            e.VkCode,
            ctrl: NativeMethods.IsKeyDown(VirtualKey.VK_CONTROL),
            shift: NativeMethods.IsKeyDown(VirtualKey.VK_SHIFT),
            alt: NativeMethods.IsKeyDown(VirtualKey.VK_MENU),
            win: NativeMethods.IsKeyDown(VirtualKey.VK_LWIN) || NativeMethods.IsKeyDown(VirtualKey.VK_RWIN));

        var activeProcess = ForegroundApp.TryGetProcessName();

        var result = _engine.Handle(stroke, DateTimeOffset.UtcNow, activeProcess);

        if (result.Consumed)
        {
            e.Suppress = true;
        }

        if (result.Overlay is not null)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _overlay.Update(result.Overlay);
                if (!_overlay.IsVisible)
                {
                    _overlay.Show();
                }
            });
        }
        else
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_overlay.IsVisible)
                {
                    _overlay.Hide();
                }
            });
        }

        if (result.Action is not null)
        {
            Logger.Info($"Action triggered: {result.Action.ActionId} (app={activeProcess ?? "?"})");
            if (string.Equals(result.Action.ActionId, "quitGlyph", StringComparison.OrdinalIgnoreCase))
            {
                Application.Current.Dispatcher.Invoke(Application.Current.Shutdown);
                return;
            }

            _ = _actionRuntime.ExecuteAsync(result.Action, CancellationToken.None);
        }
    }
}
