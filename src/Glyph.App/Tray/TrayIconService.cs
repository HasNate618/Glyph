using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows;

using Glyph.App.Overlay.Theming;
using Glyph.App.UI;
using Glyph.App.Startup;
using Glyph.App.Config;
using Glyph.Core.Logging;
using Glyph.App;

namespace Glyph.App.Tray;

public sealed class TrayIconService : IDisposable
{
    private readonly System.Windows.Forms.NotifyIcon _notifyIcon;
    private TrayMenuWindow? _menuWindow;

    public TrayIconService()
    {
        // Must be created on the WPF UI thread.
        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Text = "Glyph",
            Visible = false,
            Icon = LoadAppIcon() ?? SystemIcons.Application,
            ContextMenuStrip = null
        };

        _notifyIcon.DoubleClick += (_, _) => OpenGui();
        _notifyIcon.MouseUp += (_, e) =>
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Right)
            {
                ShowTrayMenu();
            }
        };
    }

    public void Start()
    {
        _notifyIcon.Visible = true;
        Logger.Info("Tray icon started");
    }

    public void Dispose()
    {
        try
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _menuWindow?.ForceClose();
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    private void ShowTrayMenu()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            if (_menuWindow == null)
            {
                _menuWindow = new TrayMenuWindow
                {
                    OpenGuiAction = OpenGui,
                    OpenConfigAction = OpenConfigFolder,
                    OpenLogsAction = OpenLogsFolder,
                    ExitAction = Exit,
                    ToggleStartupAction = ToggleStartup
                };
            }

            try
            {
                _menuWindow.SetStartupChecked(AppConfig.Load().StartWithWindows);
            }
            catch
            {
            }

            var cursor = System.Windows.Forms.Cursor.Position;
            _menuWindow.ShowAt(new System.Windows.Point(cursor.X, cursor.Y));
        });
    }

    private static void ToggleStartup(bool enabled)
    {
        try
        {
            StartupManager.SetEnabled(enabled);
            var cfg = AppConfig.Load();
            cfg.StartWithWindows = enabled;
            AppConfig.Save(cfg);
        }
        catch
        {
        }
    }

    private static Icon? LoadAppIcon()
    {
        try
        {
            // The build copies the icons to the output directory (see project file).
            var exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppDomain.CurrentDomain.BaseDirectory;
            var iconPath = Path.Combine(exeDir, WindowsThemeHelper.GetIconFileName());

            if (File.Exists(iconPath))
            {
                return new Icon(iconPath);
            }

            var fallbackPath = Path.Combine(exeDir, "Logo.ico");
            if (File.Exists(fallbackPath))
            {
                return new Icon(fallbackPath);
            }
        }
        catch
        {
            // Ignore and fall back to SystemIcons.Application
        }

        return null;
    }

    private static void OpenGui()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            // Open a lightweight settings window (avoid starting duplicate hooks).
            var window = System.Windows.Application.Current.Windows.OfType<SettingsWindow>().FirstOrDefault() ?? new SettingsWindow();

            if (!window.IsVisible)
            {
                window.Show();
            }

            if (window.WindowState == WindowState.Minimized)
            {
                window.WindowState = WindowState.Normal;
            }

            window.Activate();
            window.Topmost = true; // bring to front reliably
            window.Topmost = false;
            window.Focus();
        });
    }

    private static void OpenConfigFolder()
    {
        try
        {
            var dir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Glyph");

            System.IO.Directory.CreateDirectory(dir);

            Process.Start(new ProcessStartInfo
            {
                FileName = dir,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to open config folder", ex);
        }
    }

    private static void OpenLogsFolder()
    {
        try
        {
            var dir = Logger.LogDirectory;
            System.IO.Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo
            {
                FileName = dir,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to open logs folder", ex);
        }
    }

    private static void Exit()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            try
            {
                System.Windows.Application.Current.Shutdown();
            }
            catch
            {
            }
        });
    }
}
