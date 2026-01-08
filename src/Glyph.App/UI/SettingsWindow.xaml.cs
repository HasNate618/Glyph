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
        ApplyButton.Click += (_, _) => ApplyConfig();

        RecordButton.Click += (_, _) => ToggleRecording();

        Loaded += (_, _) => LoadToUi();
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

    private void LoadToUi()
    {
        try
        {
            var cfg = AppConfig.Load();

            var leaderSeq = NormalizeGlyphSequence(cfg);
            CurrentGlyphText.Text = DescribeGlyphSequence(leaderSeq);
            RecordedGlyphText.Text = DescribeGlyphSequence(leaderSeq);

            if (!string.IsNullOrWhiteSpace(cfg.BaseTheme))
            {
                foreach (var item in ThemeCombo.Items.OfType<System.Windows.Controls.ComboBoxItem>())
                {
                    if ((item.Tag?.ToString() ?? string.Empty).Equals(cfg.BaseTheme, StringComparison.OrdinalIgnoreCase))
                    {
                        ThemeCombo.SelectedItem = item;
                        break;
                    }
                }
            }
            else
            {
                // default selection
                ThemeCombo.SelectedIndex = 0;
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to load settings to UI", ex);
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
                var path = ThemeManager.DefaultBaseThemeSelectorPath;
                System.IO.File.WriteAllText(path, cfg.BaseTheme ?? string.Empty);
            }

            AppConfig.Save(cfg);

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
