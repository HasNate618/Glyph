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
    }

    public static void Initialize(string? userThemePath = null)
    {
        var path = userThemePath ?? DefaultUserThemePath;

        EnsureBuiltInThemesExtracted();
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
            ApplyThemeFromSelector();
            TryLoadUserTheme(DefaultUserThemePath);
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to reload theme", ex);
        }
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

        var merged = System.Windows.Application.Current.Resources.MergedDictionaries;
        if (_baseDictionary is not null)
        {
            merged.Remove(_baseDictionary);
            _baseDictionary = null;
        }

        // Prefer JSON themes in %APPDATA%\Glyph\themes
        if (TryLoadThemeJson(themeId, out var jsonDict))
        {
            merged.Insert(0, jsonDict);
            _baseDictionary = jsonDict;
            Logger.Info($"Theme applied (JSON): {themeId}");
            return;
        }

        // Fallback to legacy built-in XAML dictionaries.
        try
        {
            var uri = new Uri($"Overlay/Themes/{themeId}.xaml", UriKind.Relative);
            var dict = new ResourceDictionary { Source = uri };
            merged.Insert(0, dict);
            _baseDictionary = dict;
            Logger.Info($"Theme applied (XAML fallback): {themeId}");
        }
        catch
        {
            var uri = new Uri("Overlay/Themes/Fluent.xaml", UriKind.Relative);
            var dict = new ResourceDictionary { Source = uri };
            merged.Insert(0, dict);
            _baseDictionary = dict;
            Logger.Info("Theme applied (XAML fallback): Fluent");
        }
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

        var jsonText = File.ReadAllText(path);
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
            CornerRadii = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
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

        return merged;
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
