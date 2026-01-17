using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media.Imaging;

using Glyph.Actions;
using Glyph.Core.Actions;
using Glyph.Core.Engine;
using Glyph.Core.Input;
using Glyph.Core.Logging;
using Glyph.Core.Overlay;
using Glyph.Win32.Hooks;
using Glyph.Win32.Interop;
using Glyph.Win32.Windowing;

namespace Glyph.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly KeyboardHook _keyboardHook;
    private readonly SequenceEngine _engine;
    private readonly ActionRuntime _actionRuntime;
    private readonly OverlayWindow _overlay;

    public MainWindow()
    {
        InitializeComponent();

        var icon = LoadWindowIcon();
        if (icon is not null)
        {
            Icon = icon;
        }

        Logger.Info("Glyph starting...");
        Logger.Info($"Log file: {Logger.LogFile}");

        _engine = SequenceEngine.CreatePrototype();
        _actionRuntime = new ActionRuntime();
        _overlay = new OverlayWindow();

        _keyboardHook = new KeyboardHook();
        _keyboardHook.KeyDown += OnGlobalKeyDown;
        _keyboardHook.Start();
        
        Logger.Info("Keyboard hook started. Press Ctrl+Shift+NumPad * to activate.");
    }

    private static System.Windows.Media.ImageSource? LoadWindowIcon()
    {
        try
        {
            var exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppDomain.CurrentDomain.BaseDirectory;
            var iconPath = Path.Combine(exeDir, WindowsThemeHelper.GetIconFileName());

            if (!File.Exists(iconPath))
            {
                iconPath = Path.Combine(exeDir, "Logo.ico");
            }

            if (File.Exists(iconPath))
            {
                return BitmapFrame.Create(new Uri(iconPath, UriKind.Absolute));
            }
        }
        catch
        {
            // Ignore and fall back to default icon.
        }

        return null;
    }

    protected override void OnClosed(EventArgs e)
    {
        _keyboardHook.KeyDown -= OnGlobalKeyDown;
        _keyboardHook.Dispose();
        base.OnClosed(e);
    }

    private void OnGlobalKeyDown(object? sender, KeyboardHookEventArgs e)
    {
        var stroke = KeyStroke.FromVkCode(
            e.VkCode,
            ctrl: NativeMethods.IsKeyDown(VirtualKey.VK_CONTROL),
            shift: NativeMethods.IsKeyDown(VirtualKey.VK_SHIFT),
            alt: NativeMethods.IsKeyDown(VirtualKey.VK_MENU),
            win: NativeMethods.IsKeyDown(VirtualKey.VK_LWIN) || NativeMethods.IsKeyDown(VirtualKey.VK_RWIN));

        Logger.Info($"Key: VK={e.VkCode} Key={stroke.Key} (char code: {(int?)(stroke.Key)}) Ctrl={stroke.Ctrl} Shift={stroke.Shift}");

        var activeProcess = ForegroundApp.TryGetProcessName();
        var result = _engine.Handle(stroke, DateTimeOffset.UtcNow, activeProcess);

        if (result.Consumed)
        {
            e.Suppress = true;
        }

        if (result.Overlay is not null)
        {
            Dispatcher.Invoke(() =>
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
            Dispatcher.Invoke(() =>
            {
                if (_overlay.IsVisible)
                {
                    _overlay.Hide();
                }
            });
        }

        if (result.Action is not null)
        {
            Logger.Info($"Action triggered: {result.Action.ActionId}");
            _ = _actionRuntime.ExecuteAsync(result.Action, CancellationToken.None);
        }
    }
}
