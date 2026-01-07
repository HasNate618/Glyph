using System.IO;
using System.Windows;
using System.Windows.Markup;

using Glyph.Core.Logging;

namespace Glyph.App.Overlay.Theming;

public static class ThemeManager
{
    // Select a built-in base theme by writing one of:
    //   Fluent
    //   CatppuccinMocha
    //   Light
    //   Nord
    //   Darcula
    //   RosePine
    // to: %APPDATA%\Glyph\theme.base
    public static readonly string DefaultBaseThemeSelectorPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Glyph",
        "theme.base");

    // Users can drop a XAML ResourceDictionary here to override any theme keys.
    // Example: %APPDATA%\Glyph\theme.xaml
    public static readonly string DefaultUserThemePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Glyph",
        "theme.xaml");

    private static readonly List<FileSystemWatcher> _watchers = new();
    private static ResourceDictionary? _userDictionary;
    private static ResourceDictionary? _baseDictionary;

    public static void Initialize(string? userThemePath = null)
    {
        var path = userThemePath ?? DefaultUserThemePath;

        ApplyBaseThemeFromSelector();

        TryLoadUserTheme(path);
        StartWatcher(path);
        // Also watch the base theme selector so runtime changes (via actions) apply immediately.
        StartWatcher(DefaultBaseThemeSelectorPath);
    }

    public static void Reload()
    {
        // Re-read base theme selector + reload user theme overrides.
        try
        {
            ApplyBaseThemeFromSelector();
            TryLoadUserTheme(DefaultUserThemePath);
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to reload theme", ex);
        }
    }

    private static void ApplyBaseThemeFromSelector()
    {
        try
        {
            var selectorPath = DefaultBaseThemeSelectorPath;
            var baseName = "Fluent";

            var dir = Path.GetDirectoryName(selectorPath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            if (File.Exists(selectorPath))
            {
                baseName = (File.ReadAllText(selectorPath) ?? string.Empty).Trim();
            }

            if (string.IsNullOrWhiteSpace(baseName))
            {
                baseName = "Fluent";
            }

            // Clamp to known built-ins.
            if (!string.Equals(baseName, "Fluent", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(baseName, "CatppuccinMocha", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(baseName, "Light", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(baseName, "Nord", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(baseName, "Darcula", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(baseName, "RosePine", StringComparison.OrdinalIgnoreCase))
            {
                Logger.Info($"Unknown base theme '{baseName}', falling back to Fluent");
                baseName = "Fluent";
            }

            ApplyBaseTheme(baseName);
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to apply base theme selector", ex);
            ApplyBaseTheme("Fluent");
        }
    }

    private static void ApplyBaseTheme(string baseName)
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

        var uri = new Uri($"Overlay/Themes/{baseName}.xaml", UriKind.Relative);
        var dict = new ResourceDictionary { Source = uri };
        merged.Insert(0, dict);
        _baseDictionary = dict;

        Logger.Info($"Base theme applied: {baseName}");
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

            // If we're watching the base selector file, trigger base theme application;
            // otherwise treat it as a user theme change.
            var selectorPath = Path.GetFullPath(DefaultBaseThemeSelectorPath);
            var targetPath = Path.GetFullPath(path);
            if (string.Equals(selectorPath, targetPath, StringComparison.OrdinalIgnoreCase))
            {
                watcher.Changed += (_, _) => DebouncedApplyBaseThemeSelector(path);
                watcher.Created += (_, _) => DebouncedApplyBaseThemeSelector(path);
                watcher.Renamed += (_, _) => DebouncedApplyBaseThemeSelector(path);
                watcher.Deleted += (_, _) => DebouncedApplyBaseThemeSelector(path);
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

    private static void DebouncedApplyBaseThemeSelector(string path)
    {
        var now = DateTime.UtcNow;
        if ((now - _lastReloadAttemptUtc).TotalMilliseconds < 150)
        {
            return;
        }

        _lastReloadAttemptUtc = now;

        System.Windows.Application.Current?.Dispatcher?.BeginInvoke(() =>
        {
            ApplyBaseThemeFromSelector();
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
