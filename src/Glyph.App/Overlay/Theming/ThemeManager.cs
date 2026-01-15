using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;

using Glyph.Core.Logging;

namespace Glyph.App.Overlay.Theming;

public static class ThemeManager
{
    private static readonly ResourceDictionary _defaultsDictionary = BuildOverlayDefaults();

    // Preferred theme selection file (contains a theme id, e.g. "Fluent").
    // Path: %APPDATA%\Glyph\theme.selected
    public static readonly string DefaultThemeSelectedPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Glyph",
        "theme.selected");

    // Legacy base theme selector file (contains a built-in theme name).
    // Path: %APPDATA%\Glyph\theme.base
    public static readonly string DefaultBaseThemeSelectorPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Glyph",
        "theme.base");

    // Theme directory containing user + built-in themes (JSON).
    // Path: %APPDATA%\Glyph\themes
    public static readonly string DefaultThemesDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Glyph",
        "themes");

    // Users can still drop a XAML ResourceDictionary here to override theme keys.
    // Example: %APPDATA%\Glyph\theme.xaml
    public static readonly string DefaultUserThemePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Glyph",
        "theme.xaml");

    private static readonly List<FileSystemWatcher> _watchers = new();
    private static ResourceDictionary? _userDictionary;
    private static ResourceDictionary? _baseDictionary;

    private sealed class ThemeJson
    {
        public int SchemaVersion { get; set; } = 1;
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Inherits { get; set; }

        public Dictionary<string, string>? Brushes { get; set; }
        public Dictionary<string, string>? Fonts { get; set; }
        public Dictionary<string, double>? CornerRadii { get; set; }

        // Expanded theme variables:
        // - numbers: doubles (sizes, font sizes, offsets)
        // - thickness: WPF Thickness strings (e.g. "20,14,20,14")
        // - strings: string values (anchors, backdrop modes, etc.)
        public Dictionary<string, double>? Numbers { get; set; }
        public Dictionary<string, string>? Thickness { get; set; }
        public Dictionary<string, string>? Strings { get; set; }
    }

    public static void Initialize(string? userThemePath = null)
    {
        var path = userThemePath ?? DefaultUserThemePath;

        EnsureBuiltInThemesExtracted();

        EnsureDefaultsInserted();
        ApplyThemeFromSelector();

        TryLoadUserTheme(path);

        StartWatcher(path);
        StartWatcher(DefaultThemeSelectedPath);
        StartWatcher(DefaultBaseThemeSelectorPath);
        StartThemesDirectoryWatcher(DefaultThemesDirectory);
    }

    public static void Reload()
    {
        // Re-read selected theme + reload overrides.
        try
        {
            EnsureDefaultsInserted();
            ApplyThemeFromSelector();
            TryLoadUserTheme(DefaultUserThemePath);
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to reload theme", ex);
        }
    }

    private static void EnsureDefaultsInserted()
    {
        try
        {
            if (System.Windows.Application.Current is null)
            {
                return;
            }

            var merged = System.Windows.Application.Current.Resources.MergedDictionaries;
            if (!merged.Contains(_defaultsDictionary))
            {
                merged.Insert(0, _defaultsDictionary);
            }
        }
        catch
        {
            // best-effort
        }
    }

    private static ResourceDictionary BuildOverlayDefaults()
    {
        var d = new ResourceDictionary();

        // Layout defaults (match current hard-coded overlay XAML values)
        d["Glyph.Overlay.PanelPadding"] = new Thickness(20, 14, 20, 14);
        d["Glyph.Overlay.PanelMinWidth"] = 300.0;
        d["Glyph.Overlay.PanelMaxWidth"] = 420.0;

        d["Glyph.Overlay.SequenceFontSize"] = 14.0;
        d["Glyph.Overlay.SequenceMargin"] = new Thickness(0, 0, 0, 8);
        d["Glyph.Overlay.OptionsMargin"] = new Thickness(0, 2, 0, 0);

        d["Glyph.Overlay.OptionRowMargin"] = new Thickness(0, 6, 0, 6);
        d["Glyph.Overlay.OptionKeycapsMargin"] = new Thickness(0, 0, 8, 0);
        d["Glyph.Overlay.OptionDescriptionFontSize"] = 13.0;

        d["Glyph.Overlay.KeycapMinWidth"] = 28.0;
        d["Glyph.Overlay.KeycapHeight"] = 28.0;
        d["Glyph.Overlay.KeycapPadding"] = new Thickness(6, 0, 6, 0);
        d["Glyph.Overlay.KeycapMargin"] = new Thickness(0, 0, 4, 0);
        d["Glyph.Overlay.KeycapFontSize"] = 13.0;
        d["Glyph.Overlay.KeycapBorderThickness"] = new Thickness(2);

        // Placement defaults (match current bottom-right logic)
        d["Glyph.Overlay.ScreenAnchor"] = "BottomRight";
        d["Glyph.Overlay.ScreenPadding"] = 8.0;
        d["Glyph.Overlay.OffsetX"] = 0.0;
        d["Glyph.Overlay.OffsetY"] = 0.0;

        // Backdrop defaults
        // Supported values: Auto, None, DwmMain, DwmTransient, DwmTabbed, Acrylic, Blur, HostBackdrop
        d["Glyph.Overlay.WindowBackdrop"] = "Auto";
        d["Glyph.Overlay.WindowAcrylicColor"] = "#991B1B1B";
        d["Glyph.Overlay.WindowCorners"] = "Round";

        return d;
    }

    public static IReadOnlyList<(string Id, string Name)> ListAvailableThemes()
    {
        try
        {
            Directory.CreateDirectory(DefaultThemesDirectory);
            var files = Directory.EnumerateFiles(DefaultThemesDirectory, "*.json", SearchOption.TopDirectoryOnly);
            var items = new List<(string Id, string Name)>();

            foreach (var file in files)
            {
                var id = Path.GetFileNameWithoutExtension(file);
                if (string.IsNullOrWhiteSpace(id)) continue;

                var name = id;
                try
                {
                    var jsonText = File.ReadAllText(file);
                    var theme = JsonSerializer.Deserialize<ThemeJson>(jsonText, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (!string.IsNullOrWhiteSpace(theme?.Name))
                    {
                        name = theme.Name.Trim();
                    }
                }
                catch
                {
                }

                items.Add((id, name));
            }

            return items
                .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return Array.Empty<(string Id, string Name)>();
        }
    }

    private static void ApplyThemeFromSelector()
    {
        try
        {
            var themeId = "Fluent";

            var dir = Path.GetDirectoryName(DefaultThemeSelectedPath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            if (File.Exists(DefaultThemeSelectedPath))
            {
                themeId = (File.ReadAllText(DefaultThemeSelectedPath) ?? string.Empty).Trim();
            }
            else if (File.Exists(DefaultBaseThemeSelectorPath))
            {
                // Back-compat.
                themeId = (File.ReadAllText(DefaultBaseThemeSelectorPath) ?? string.Empty).Trim();
            }

            if (string.IsNullOrWhiteSpace(themeId))
            {
                themeId = "Fluent";
            }

            ApplyTheme(themeId);
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to apply theme selector", ex);
            ApplyTheme("Fluent");
        }
    }

    private static void ApplyTheme(string themeId)
    {
        if (System.Windows.Application.Current is null)
        {
            return;
        }

        EnsureDefaultsInserted();

        var merged = System.Windows.Application.Current.Resources.MergedDictionaries;
        if (_baseDictionary is not null)
        {
            merged.Remove(_baseDictionary);
            _baseDictionary = null;
        }

        var insertIndex = merged.IndexOf(_defaultsDictionary);
        insertIndex = insertIndex < 0 ? 0 : insertIndex + 1;

        // Prefer JSON themes in %APPDATA%\Glyph\themes
        if (TryLoadThemeJson(themeId, out var jsonDict))
        {
            merged.Insert(insertIndex, jsonDict);
            _baseDictionary = jsonDict;
            Logger.Info($"Theme applied (JSON): {themeId}");
            return;
        }

        // No legacy built-in XAML themes anymore; fall back to Fluent JSON.
        if (!string.Equals(themeId, "Fluent", StringComparison.OrdinalIgnoreCase)
            && TryLoadThemeJson("Fluent", out var fluentDict))
        {
            merged.Insert(insertIndex, fluentDict);
            _baseDictionary = fluentDict;
            Logger.Info("Theme applied (JSON fallback): Fluent");
            return;
        }

        Logger.Info($"Theme not found: {themeId}");
    }

    private static void EnsureBuiltInThemesExtracted()
    {
        try
        {
            Directory.CreateDirectory(DefaultThemesDirectory);

            var asm = Assembly.GetExecutingAssembly();
            var marker = ".Overlay.ThemesJson.";
            foreach (var resourceName in asm.GetManifestResourceNames())
            {
                if (!resourceName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var idx = resourceName.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (idx < 0)
                {
                    continue;
                }

                var fileName = resourceName.Substring(idx + marker.Length); // e.g. Fluent.json
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    continue;
                }

                var outPath = Path.Combine(DefaultThemesDirectory, fileName);
                if (File.Exists(outPath))
                {
                    continue; // user-owned; never overwrite
                }

                using var stream = asm.GetManifestResourceStream(resourceName);
                if (stream is null)
                {
                    continue;
                }

                using var reader = new StreamReader(stream);
                File.WriteAllText(outPath, reader.ReadToEnd());
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to extract built-in themes", ex);
        }
    }

    private static bool TryLoadThemeJson(string themeId, out ResourceDictionary dictionary)
    {
        dictionary = new ResourceDictionary();

        try
        {
            Directory.CreateDirectory(DefaultThemesDirectory);
            var path = Path.Combine(DefaultThemesDirectory, themeId + ".json");
            if (!File.Exists(path))
            {
                return false;
            }

            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var mergedTheme = LoadThemeJsonRecursive(path, visited);
            if (mergedTheme is null)
            {
                return false;
            }

            dictionary = BuildResourceDictionary(mergedTheme);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to load theme JSON: {themeId}", ex);
            return false;
        }
    }

    private static ThemeJson? LoadThemeJsonRecursive(string path, HashSet<string> visited)
    {
        var id = Path.GetFileNameWithoutExtension(path);
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        if (!visited.Add(id))
        {
            Logger.Error($"Theme inheritance cycle detected at '{id}'");
            return null;
        }

        var jsonText = ReadAllTextShared(path);
        var theme = JsonSerializer.Deserialize<ThemeJson>(jsonText, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (theme is null)
        {
            return null;
        }

        theme.Id ??= id;

        if (string.IsNullOrWhiteSpace(theme.Inherits))
        {
            return theme;
        }

        var parentPath = Path.Combine(Path.GetDirectoryName(path) ?? DefaultThemesDirectory, theme.Inherits + ".json");
        if (!File.Exists(parentPath))
        {
            Logger.Info($"Theme '{id}' inherits from missing theme '{theme.Inherits}'");
            return theme;
        }

        var parent = LoadThemeJsonRecursive(parentPath, visited);
        if (parent is null)
        {
            return theme;
        }

        return MergeThemes(parent, theme);
    }

    private static ThemeJson MergeThemes(ThemeJson parent, ThemeJson child)
    {
        var merged = new ThemeJson
        {
            SchemaVersion = child.SchemaVersion,
            Id = child.Id,
            Name = string.IsNullOrWhiteSpace(child.Name) ? parent.Name : child.Name,
            Description = string.IsNullOrWhiteSpace(child.Description) ? parent.Description : child.Description,
            Inherits = child.Inherits,
            Brushes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            Fonts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            CornerRadii = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase),
            Numbers = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase),
            Thickness = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            Strings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        };

        if (parent.Brushes is not null)
        {
            foreach (var kvp in parent.Brushes) merged.Brushes[kvp.Key] = kvp.Value;
        }
        if (child.Brushes is not null)
        {
            foreach (var kvp in child.Brushes) merged.Brushes[kvp.Key] = kvp.Value;
        }

        if (parent.Fonts is not null)
        {
            foreach (var kvp in parent.Fonts) merged.Fonts[kvp.Key] = kvp.Value;
        }
        if (child.Fonts is not null)
        {
            foreach (var kvp in child.Fonts) merged.Fonts[kvp.Key] = kvp.Value;
        }

        if (parent.CornerRadii is not null)
        {
            foreach (var kvp in parent.CornerRadii) merged.CornerRadii[kvp.Key] = kvp.Value;
        }
        if (child.CornerRadii is not null)
        {
            foreach (var kvp in child.CornerRadii) merged.CornerRadii[kvp.Key] = kvp.Value;
        }

        if (parent.Numbers is not null)
        {
            foreach (var kvp in parent.Numbers) merged.Numbers[kvp.Key] = kvp.Value;
        }
        if (child.Numbers is not null)
        {
            foreach (var kvp in child.Numbers) merged.Numbers[kvp.Key] = kvp.Value;
        }

        if (parent.Thickness is not null)
        {
            foreach (var kvp in parent.Thickness) merged.Thickness[kvp.Key] = kvp.Value;
        }
        if (child.Thickness is not null)
        {
            foreach (var kvp in child.Thickness) merged.Thickness[kvp.Key] = kvp.Value;
        }

        if (parent.Strings is not null)
        {
            foreach (var kvp in parent.Strings) merged.Strings[kvp.Key] = kvp.Value;
        }
        if (child.Strings is not null)
        {
            foreach (var kvp in child.Strings) merged.Strings[kvp.Key] = kvp.Value;
        }

        return merged;
    }

    private static string ReadAllTextShared(string path)
    {
        // Theme files are often edited live; allow read while another process is writing.
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static ResourceDictionary BuildResourceDictionary(ThemeJson theme)
    {
        var dict = new ResourceDictionary();

        if (theme.Brushes is not null)
        {
            foreach (var kvp in theme.Brushes)
            {
                try
                {
                    var obj = System.Windows.Media.ColorConverter.ConvertFromString(kvp.Value);
                    if (obj is System.Windows.Media.Color color)
                    {
                        var brush = new SolidColorBrush(color);
                        if (brush.CanFreeze) brush.Freeze();
                        dict[kvp.Key] = brush;
                    }
                }
                catch
                {
                }
            }
        }

        if (theme.Fonts is not null)
        {
            foreach (var kvp in theme.Fonts)
            {
                try
                {
                    dict[kvp.Key] = new System.Windows.Media.FontFamily(kvp.Value);
                }
                catch
                {
                }
            }
        }

        if (theme.CornerRadii is not null)
        {
            foreach (var kvp in theme.CornerRadii)
            {
                try
                {
                    dict[kvp.Key] = new CornerRadius(kvp.Value);
                }
                catch
                {
                }
            }
        }

        if (theme.Numbers is not null)
        {
            foreach (var kvp in theme.Numbers)
            {
                try
                {
                    dict[kvp.Key] = kvp.Value;
                }
                catch
                {
                }
            }
        }

        if (theme.Strings is not null)
        {
            foreach (var kvp in theme.Strings)
            {
                try
                {
                    dict[kvp.Key] = kvp.Value;
                }
                catch
                {
                }
            }
        }

        if (theme.Thickness is not null)
        {
            var converter = new ThicknessConverter();
            foreach (var kvp in theme.Thickness)
            {
                try
                {
                    var obj = converter.ConvertFromString(kvp.Value);
                    if (obj is Thickness thickness)
                    {
                        dict[kvp.Key] = thickness;
                    }
                }
                catch
                {
                }
            }
        }

        return dict;
    }

    private static void StartThemesDirectoryWatcher(string themesDir)
    {
        try
        {
            Directory.CreateDirectory(themesDir);
            var watcher = new FileSystemWatcher(themesDir, "*.json")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
                EnableRaisingEvents = true,
                IncludeSubdirectories = false
            };

            watcher.Changed += (_, _) => DebouncedApplySelectedTheme();
            watcher.Created += (_, _) => DebouncedApplySelectedTheme();
            watcher.Renamed += (_, _) => DebouncedApplySelectedTheme();
            watcher.Deleted += (_, _) => DebouncedApplySelectedTheme();

            _watchers.Add(watcher);
            Logger.Info($"Theme directory watcher active: {themesDir}");
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to start theme directory watcher", ex);
        }
    }

    private static void StartWatcher(string path)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            var file = Path.GetFileName(path);
            if (string.IsNullOrWhiteSpace(dir) || string.IsNullOrWhiteSpace(file))
            {
                return;
            }

            Directory.CreateDirectory(dir);

            var watcher = new FileSystemWatcher(dir, file)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
                EnableRaisingEvents = true
            };

            // If we're watching the selector files, trigger theme application;
            // otherwise treat it as a user override theme change.
            var legacySelector = Path.GetFullPath(DefaultBaseThemeSelectorPath);
            var selectedSelector = Path.GetFullPath(DefaultThemeSelectedPath);
            var targetPath = Path.GetFullPath(path);

            if (string.Equals(legacySelector, targetPath, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(selectedSelector, targetPath, StringComparison.OrdinalIgnoreCase))
            {
                watcher.Changed += (_, _) => DebouncedApplySelectedTheme();
                watcher.Created += (_, _) => DebouncedApplySelectedTheme();
                watcher.Renamed += (_, _) => DebouncedApplySelectedTheme();
                watcher.Deleted += (_, _) => DebouncedApplySelectedTheme();
            }
            else
            {
                watcher.Changed += (_, _) => DebouncedReload(path);
                watcher.Created += (_, _) => DebouncedReload(path);
                watcher.Renamed += (_, _) => DebouncedReload(path);
                watcher.Deleted += (_, _) => DebouncedReload(path);
            }

            _watchers.Add(watcher);
            Logger.Info($"Theme watcher active: {path}");
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to start theme watcher", ex);
        }
    }

    private static DateTime _lastReloadAttemptUtc = DateTime.MinValue;

    private static void DebouncedReload(string path)
    {
        // Keep this extremely lightweight; FS events can spam.
        var now = DateTime.UtcNow;
        if ((now - _lastReloadAttemptUtc).TotalMilliseconds < 150)
        {
            return;
        }

        _lastReloadAttemptUtc = now;

        System.Windows.Application.Current?.Dispatcher?.BeginInvoke(() =>
        {
            TryLoadUserTheme(path);
        });
    }

    private static void DebouncedApplySelectedTheme()
    {
        var now = DateTime.UtcNow;
        if ((now - _lastReloadAttemptUtc).TotalMilliseconds < 150)
        {
            return;
        }

        _lastReloadAttemptUtc = now;

        System.Windows.Application.Current?.Dispatcher?.BeginInvoke(() =>
        {
            ApplyThemeFromSelector();
        });
    }

    private static void TryLoadUserTheme(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                RemoveUserTheme();
                return;
            }

            // XAML loading can temporarily fail during writes; retry a couple times.
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    var loaded = XamlReader.Load(stream) as ResourceDictionary;
                    if (loaded is null)
                    {
                        Logger.Error($"User theme did not load as ResourceDictionary: {path}");
                        return;
                    }

                    ApplyUserTheme(loaded);
                    Logger.Info($"User theme loaded: {path}");
                    return;
                }
                catch (IOException)
                {
                    Thread.Sleep(30);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to load user theme", ex);
        }
    }

    private static void ApplyUserTheme(ResourceDictionary dictionary)
    {
        if (System.Windows.Application.Current is null)
        {
            return;
        }

        var merged = System.Windows.Application.Current.Resources.MergedDictionaries;

        if (_userDictionary is not null)
        {
            merged.Remove(_userDictionary);
            _userDictionary = null;
        }

        merged.Add(dictionary);
        _userDictionary = dictionary;
    }

    private static void RemoveUserTheme()
    {
        if (System.Windows.Application.Current is null)
        {
            return;
        }

        if (_userDictionary is null)
        {
            return;
        }

        System.Windows.Application.Current.Resources.MergedDictionaries.Remove(_userDictionary);
        _userDictionary = null;
        Logger.Info("User theme removed (theme file missing)");
    }
}
