using System;
using System.IO;
using System.Text.Json;

namespace Glyph.App.Config;

public sealed class GlyphKeyConfig
{
    public bool Ctrl { get; set; }
    public bool Shift { get; set; }
    public bool Alt { get; set; }
    public bool Win { get; set; }
    public int VkCode { get; set; }
}

public sealed class AppConfig
{
    public GlyphKeyConfig? Glyph { get; set; }
    public List<GlyphKeyConfig>? GlyphSequence { get; set; }
    public string? BaseTheme { get; set; }
    public bool BreadcrumbsMode { get; set; }
    public bool StartWithWindows { get; set; }
        // Internal flag to indicate whether the initial GUI has already been shown to the user.
        // Defaults to false so the Settings window can be shown on first startup.
        public bool HasShownInitialGui { get; set; }

    public static string ConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Glyph",
        "config.json");

    public static AppConfig Load()
    {
        try
        {
            var path = ConfigPath;
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);

            if (!File.Exists(path))
            {
                var cfg = new AppConfig
                {
                    // Default Glyph: Right Alt (VK_RMENU = 0xA5)
                    Glyph = new GlyphKeyConfig { VkCode = 0xA5 },
                    // Default visual theme
                    BaseTheme = "System",
                    BreadcrumbsMode = false,
                    StartWithWindows = false,
                    HasShownInitialGui = false
                };
                Save(cfg);
                return cfg;
            }

            var text = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppConfig>(text) ?? new AppConfig();
        }
        catch
        {
            return new AppConfig();
        }
    }

    public static void Save(AppConfig cfg)
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(cfg, options));
        }
        catch
        {
            // Best-effort
        }
    }
}
