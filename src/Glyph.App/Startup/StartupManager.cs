using System;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace Glyph.App.Startup;

public static class StartupManager
{
    private const string RunRegKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
    private const string ValueName = "Glyph";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunRegKey, false);
            if (key is null) return false;
            var val = key.GetValue(ValueName) as string;
            return !string.IsNullOrWhiteSpace(val);
        }
        catch
        {
            return false;
        }
    }

    public static void SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunRegKey, true) ?? Registry.CurrentUser.CreateSubKey(RunRegKey);

            if (enabled)
            {
                var exe = GetCurrentExePath();
                if (!string.IsNullOrWhiteSpace(exe))
                {
                    key.SetValue(ValueName, $"\"{exe}\"");
                }
            }
            else
            {
                key.DeleteValue(ValueName, false);
            }
        }
        catch
        {
            // Best-effort; swallow errors so UI doesn't crash.
        }
    }

    private static string? GetCurrentExePath()
    {
        try
        {
            var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            var path = asm.Location;
            if (string.IsNullOrWhiteSpace(path)) return null;
            // If running as framework-dependent in dev, prefer the host exe where available.
            if (File.Exists(path)) return Path.GetFullPath(path);
            return null;
        }
        catch
        {
            return null;
        }
    }
}
