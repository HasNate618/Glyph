using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Windows;

using Glyph.App.Overlay.Theming;
using Glyph.App.UI;
using Glyph.Core.Logging;

namespace Glyph.App.Tray;

public sealed class TrayIconService : IDisposable
{
    private readonly System.Windows.Forms.NotifyIcon _notifyIcon;

    public TrayIconService()
    {
        // Must be created on the WPF UI thread.
        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Text = "Glyph",
            Visible = false,
            Icon = SystemIcons.Application,
            ContextMenuStrip = BuildMenu()
        };

        _notifyIcon.DoubleClick += (_, _) => OpenGui();
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
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    private System.Windows.Forms.ContextMenuStrip BuildMenu()
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();

        var openGui = new System.Windows.Forms.ToolStripMenuItem("Open GUI");
        openGui.Click += (_, _) => OpenGui();

        var openConfig = new System.Windows.Forms.ToolStripMenuItem("Open Config Folder");
        openConfig.Click += (_, _) => OpenConfigFolder();

        var openLogs = new System.Windows.Forms.ToolStripMenuItem("Open Logs Folder");
        openLogs.Click += (_, _) => OpenLogsFolder();

        var reloadTheme = new System.Windows.Forms.ToolStripMenuItem("Reload Theme");
        reloadTheme.Click += (_, _) => ReloadTheme();

        var about = new System.Windows.Forms.ToolStripMenuItem("About");
        about.Click += (_, _) => About();

        var exit = new System.Windows.Forms.ToolStripMenuItem("Exit");
        exit.Click += (_, _) => Exit();

        menu.Items.Add(openGui);
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add(openConfig);
        menu.Items.Add(openLogs);
        menu.Items.Add(reloadTheme);
        menu.Items.Add(about);
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add(exit);

        return menu;
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

    private static void ReloadTheme()
    {
        try
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                ThemeManager.Reload();
            });
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to reload theme", ex);
        }
    }

    private static void About()
    {
        try
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "?";
                System.Windows.MessageBox.Show(
                    $"Glyph\nVersion: {version}\n\nRight-click the tray icon for actions.",
                    "About Glyph",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            });
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to show About dialog", ex);
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
