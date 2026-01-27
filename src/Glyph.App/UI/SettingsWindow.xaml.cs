using System.Diagnostics;
using System.Windows;

using Glyph.App.Overlay.Theming;
using Glyph.Core.Logging;
using Glyph.App.Config;
using System.Linq;
using System.Windows.Input;
using System.Windows.Threading;

namespace Glyph.App.UI;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();

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

        Loaded += (_, _) => LoadToUi();

        // Live-apply when the user changes theme or breadcrumbs
        ThemeCombo.SelectionChanged += ThemeCombo_SelectionChanged;
        BreadcrumbsModeCheckBox.Checked += BreadcrumbsModeCheckChanged;
        BreadcrumbsModeCheckBox.Unchecked += BreadcrumbsModeCheckChanged;
    }

    private bool _suppressUiEvents = false;

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
            RecordedGlyphText.Text = DescribeGlyphSequence(leaderSeq);

            LoadThemesIntoCombo(cfg.BaseTheme);
            BreadcrumbsModeCheckBox.IsChecked = cfg.BreadcrumbsMode;
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

            AppConfig.Save(cfg);
            // Apply immediately
            ThemeManager.Reload();

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
            RecordedGlyphText.Text = "Recording: 0";
            this.PreviewKeyDown += SettingsWindow_PreviewKeyDown;
            this.Focus();
        }
        else
        {
            RecordButton.Content = "Record";
            this.PreviewKeyDown -= SettingsWindow_PreviewKeyDown;
            if (_recordedGlyphSequence.Count > 0)
            {
                RecordedGlyphText.Text = DescribeGlyphSequence(_recordedGlyphSequence);
            }
            else
            {
                RecordedGlyphText.Text = "(not recording)";
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
                RecordedGlyphText.Text = $"Recording: {_recordedGlyphSequence.Count}";
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
            new GlyphKeyConfig { Ctrl = false, Shift = false, Alt = false, Win = false, VkCode = 0x7B }
        };
    }

    private static string DescribeGlyphSequence(IReadOnlyList<GlyphKeyConfig> seq)
    {
        if (seq.Count == 0)
        {
            return "Glyph: Default (Ctrl+Shift+NumPad *)";
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
        return $"Glyph: {rendered}";
    }
}
