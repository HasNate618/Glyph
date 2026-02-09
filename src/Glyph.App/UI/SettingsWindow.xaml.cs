using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

using Glyph.App.Overlay.Theming;
using Glyph.Core.Logging;
using Glyph.App.Config;
using Glyph.App.Startup;
using System.Linq;
using System.Windows.Input;
using System.Windows.Navigation;
using System.Windows.Threading;

namespace Glyph.App.UI;

public partial class SettingsWindow : Wpf.Ui.Controls.FluentWindow
{
    public SettingsWindow()
    {
        InitializeComponent();

        // Apply system theme to this window
        Wpf.Ui.Appearance.SystemThemeWatcher.Watch(this);

        OpenThemesFolderButton.Click += (_, _) =>
        {
            try
            {
                OpenFolder(ThemeManager.DefaultThemesDirectory);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to open themes folder", ex);
            }
        };

        RecordButton.Click += (_, _) => ToggleRecording();

        Loaded += (_, _) =>
        {
            LoadToUi();
            UpdateHeaderLogoSource();
        };

        // Live-apply when the user changes theme or breadcrumbs
        ThemeCombo.SelectionChanged += ThemeCombo_SelectionChanged;
        BreadcrumbsModeCheckBox.Checked += BreadcrumbsModeCheckChanged;
        BreadcrumbsModeCheckBox.Unchecked += BreadcrumbsModeCheckChanged;
        StartWithWindowsCheckBox.Checked += StartWithWindowsCheckChanged;
        StartWithWindowsCheckBox.Unchecked += StartWithWindowsCheckChanged;
    }

    private KeymapEditorPage? _keymapEditorPage;
    private ScrollViewer? _generalSettingsView;

    private void OpenKeymapEditorButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            NavigateToKeymapEditor();
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to open keymap editor", ex);
            System.Windows.MessageBox.Show($"Failed to open keymap editor:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BackButton_Click(object? sender, RoutedEventArgs e)
    {
        NavigateToGeneralSettings();
    }

    private void NavigateToKeymapEditor()
    {
        // Store reference to general settings view if not already stored
        if (_generalSettingsView == null)
        {
            _generalSettingsView = ContentArea.Content as ScrollViewer;
        }

        if (_keymapEditorPage == null)
        {
            _keymapEditorPage = new KeymapEditorPage();
        }
        ContentArea.Content = _keymapEditorPage;
        BackButton.Visibility = Visibility.Visible;
        Title = "Keymap Editor";
    }

    private void NavigateToGeneralSettings()
    {
        if (_generalSettingsView != null)
        {
            ContentArea.Content = _generalSettingsView;
        }
        BackButton.Visibility = Visibility.Collapsed;
        Title = "Glyph Settings";
    }

    private bool _suppressUiEvents = false;

    private void UpdateHeaderLogoSource()
    {
        try
        {
            if (HeaderLogo == null) return;
            var logo = WindowsThemeHelper.IsLightTheme()
                ? "pack://application:,,,/Glyph.App;component/Assets/LogoTextBlack.svg"
                : "pack://application:,,,/Glyph.App;component/Assets/LogoTextWhite.svg";
            HeaderLogo.Source = new Uri(logo, UriKind.Absolute);
        }
        catch
        {
            // best-effort only
        }
    }

    private static string GetConfigDir()
    {
        var dir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Glyph");

        System.IO.Directory.CreateDirectory(dir);
        return dir;
    }

    private static void OpenFolder(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to open folder: {path}", ex);
        }
    }

    private void Hyperlink_RequestNavigate(object? sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to launch help link", ex);
        }
        e.Handled = true;
    }

    private void LoadToUi()
    {
        try
        {
            _suppressUiEvents = true;
            // Display version
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            VersionText.Text = $"Version {version?.Major}.{version?.Minor}.{version?.Build}";

            var cfg = AppConfig.Load();

            var leaderSeq = NormalizeGlyphSequence(cfg);
            CurrentGlyphText.Text = DescribeGlyphSequence(leaderSeq);
            // By default the recorded-glyph UI shows recording state, not the effective glyph.

            LoadThemesIntoCombo(cfg.BaseTheme);
            BreadcrumbsModeCheckBox.IsChecked = cfg.BreadcrumbsMode;
            StartWithWindowsCheckBox.IsChecked = cfg.StartWithWindows;
            _suppressUiEvents = false;
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to load settings to UI", ex);
        }
    }

    private void ThemeCombo_SelectionChanged(object? sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_suppressUiEvents) return;
        try
        {
            // Save selection and immediately apply to running instance
            SaveConfigInternal(userInitiated: false);
            var cfg = AppConfig.Load();
            if (System.Windows.Application.Current is Glyph.App.App app)
            {
                app.ApplyConfig(cfg);
            }
            UpdateHeaderLogoSource();
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to apply theme change live", ex);
        }
    }

    private void BreadcrumbsModeCheckChanged(object? sender, RoutedEventArgs e)
    {
        if (_suppressUiEvents) return;
        try
        {
            SaveConfigInternal(userInitiated: false);
            var cfg = AppConfig.Load();
            if (System.Windows.Application.Current is Glyph.App.App app)
            {
                app.ApplyConfig(cfg);
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to apply breadcrumbs change live", ex);
        }
    }

    private void StartWithWindowsCheckChanged(object? sender, RoutedEventArgs e)
    {
        if (_suppressUiEvents) return;
        try
        {
            SaveConfigInternal(userInitiated: false);
            var cfg = AppConfig.Load();
            // StartupManager already called inside SaveConfigInternal, but ensure app config applied if needed
            if (System.Windows.Application.Current is Glyph.App.App app)
            {
                app.ApplyConfig(cfg);
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to apply start-with-windows change live", ex);
        }
    }

    private void LoadThemesIntoCombo(string? selectedThemeId)
    {
        try
        {
            ThemeCombo.Items.Clear();

            var themes = ThemeManager.ListAvailableThemes();
            if (themes.Count == 0)
            {
                // Fallback list (shouldn't happen if extraction ran).
                themes = new List<(string Id, string Name)>
                {
                    ("Fluent", "Fluent"),
                    ("CatppuccinMocha", "Catppuccin Mocha"),
                    ("Light", "Light"),
                    ("Nord", "Nord"),
                    ("Darcula", "Darcula"),
                    ("RosePine", "Rose Pine"),
                };
            }

            System.Windows.Controls.ComboBoxItem? toSelect = null;
            foreach (var (id, name) in themes)
            {
                var item = new System.Windows.Controls.ComboBoxItem
                {
                    Tag = id,
                    Content = name
                };

                ThemeCombo.Items.Add(item);

                if (!string.IsNullOrWhiteSpace(selectedThemeId) &&
                    id.Equals(selectedThemeId, StringComparison.OrdinalIgnoreCase))
                {
                    toSelect = item;
                }
            }

            ThemeCombo.SelectedItem = toSelect ?? (ThemeCombo.Items.Count > 0 ? ThemeCombo.Items[0] : null);
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to populate theme list", ex);
        }
    }

    private void SaveConfig()
    {
        SaveConfigInternal(userInitiated: true);
    }

    private void SaveConfigInternal(bool userInitiated)
    {
        try
        {
            var cfg = AppConfig.Load();

            // Glyph: always driven by recorded sequence in this UI.
            // If nothing recorded, keep existing (or default) and do NOT write invalid empty glyph.
            if (_recordedGlyphSequence is { Count: > 0 })
            {
                // Reject invalid / accidental glyph steps such as Space (VK_SPACE = 0x20).
                var sanitized = _recordedGlyphSequence.Where(s => s.VkCode != 0 && s.VkCode != 0x20).ToList();
                if (sanitized.Count > 0)
                {
                    cfg.GlyphSequence = sanitized;
                    cfg.Glyph = null;
                }
            }

            if (ThemeCombo.SelectedItem is System.Windows.Controls.ComboBoxItem themeItem)
            {
                cfg.BaseTheme = themeItem.Tag?.ToString();
            }

            cfg.BreadcrumbsMode = BreadcrumbsModeCheckBox.IsChecked == true;
            cfg.StartWithWindows = StartWithWindowsCheckBox.IsChecked == true;

            AppConfig.Save(cfg);
            // Apply immediately
            ThemeManager.Reload();

            // Apply startup registration if needed (best-effort)
            try
            {
                StartupManager.SetEnabled(cfg.StartWithWindows);
            }
            catch
            {
                // Best-effort
            }

            var leaderSeq = NormalizeGlyphSequence(cfg);
            CurrentGlyphText.Text = DescribeGlyphSequence(leaderSeq);
            if (userInitiated)
            {
                Logger.Info("Settings saved");
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to save settings", ex);
        }
    }

    private void ApplyConfig()
    {
        try
        {
            SaveConfigInternal(userInitiated: true);
            var cfg = AppConfig.Load();
            if (System.Windows.Application.Current is Glyph.App.App app)
            {
                app.ApplyConfig(cfg);
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to apply settings", ex);
        }
    }

    private bool _isRecording = false;
    private readonly List<GlyphKeyConfig> _recordedGlyphSequence = new();

    private void ToggleRecording()
    {
        _isRecording = !_isRecording;
            if (_isRecording)
        {
            RecordButton.Content = "Stop Recording";
            _recordedGlyphSequence.Clear();
            this.PreviewKeyDown += SettingsWindow_PreviewKeyDown;
            this.Focus();
        }
        else
        {
            RecordButton.Content = "Record";
            this.PreviewKeyDown -= SettingsWindow_PreviewKeyDown;
            if (_recordedGlyphSequence.Count > 0)
            {
                CurrentGlyphText.Text = DescribeGlyphSequence(_recordedGlyphSequence);
            }
            // Persist and apply the new glyph sequence immediately when recording stops.
            try
            {
                SaveConfigInternal(userInitiated: true);
                var cfg = AppConfig.Load();
                if (System.Windows.Application.Current is Glyph.App.App app)
                {
                    app.ApplyConfig(cfg);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to save/apply glyph after recording", ex);
            }
        }
    }

    private void SettingsWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        try
        {
            e.Handled = true;
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            var vk = KeyInterop.VirtualKeyFromKey(key);

            var step = new GlyphKeyConfig
            {
                VkCode = vk,
                Ctrl = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl),
                Shift = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift),
                Alt = Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt),
                Win = Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin)
            };

            if (step.VkCode != 0)
            {
                _recordedGlyphSequence.Add(step);
                CurrentGlyphText.Text = DescribeGlyphSequence(_recordedGlyphSequence);
            }
        }
        catch
        {
        }
    }

    // Autosave removed; Apply persists settings.

    private static List<GlyphKeyConfig> NormalizeGlyphSequence(AppConfig cfg)
    {
        if (cfg.GlyphSequence is { Count: > 0 })
        {
            var cleaned = cfg.GlyphSequence.Where(s => s.VkCode != 0).ToList();
            if (cleaned.Count > 0) return cleaned;
        }

        if (cfg.Glyph is not null && cfg.Glyph.VkCode != 0)
        {
            return new List<GlyphKeyConfig> { cfg.Glyph };
        }

        return new List<GlyphKeyConfig>
        {
            // Default glyph: Right Alt (VK_RMENU = 0xA5)
            new GlyphKeyConfig { Ctrl = false, Shift = false, Alt = false, Win = false, VkCode = 0xA5 }
        };
    }

    private static string DescribeGlyphSequence(IReadOnlyList<GlyphKeyConfig> seq)
    {
        if (seq.Count == 0)
        {
            return "Glyph Key: Default (Ctrl+Shift+NumPad *)";
        }

        static string StepToString(GlyphKeyConfig s)
        {
            var parts = new List<string>();
            if (s.Ctrl) parts.Add("Ctrl");
            if (s.Shift) parts.Add("Shift");
            if (s.Alt) parts.Add("Alt");
            if (s.Win) parts.Add("Win");

            var keyName = "?";
            try
            {
                keyName = KeyInterop.KeyFromVirtualKey(s.VkCode).ToString();
            }
            catch
            {
                keyName = s.VkCode.ToString();
            }

            // If the recorded key is itself a direction-specific modifier (e.g. LeftCtrl),
            // avoid rendering a redundant generic modifier prefix (e.g. "Ctrl+LeftCtrl").
            // Map common direction modifier names and remove the generic counterpart.
            var keyLower = keyName.ToLowerInvariant();
            if (keyLower.Contains("left") || keyLower.Contains("right"))
            {
                if (keyLower.Contains("ctrl") && parts.Contains("Ctrl")) parts.Remove("Ctrl");
                if (keyLower.Contains("shift") && parts.Contains("Shift")) parts.Remove("Shift");
                if ((keyLower.Contains("menu") || keyLower.Contains("alt")) && parts.Contains("Alt")) parts.Remove("Alt");
            }

            if (parts.Count == 0) return keyName;
            return $"{string.Join("+", parts)}+{keyName}";
        }

        var rendered = string.Join(", ", seq.Select(StepToString));
        return $"Glyph Key: {rendered}";
    }
}
