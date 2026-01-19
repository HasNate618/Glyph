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
                    Glyph = null,
                    BaseTheme = null,
                    BreadcrumbsMode = false,
                    StartWithWindows = false
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
