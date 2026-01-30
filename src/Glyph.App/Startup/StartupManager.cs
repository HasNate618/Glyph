using System;
using System.IO;
using System.Reflection;
using System.Diagnostics;
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
                    Trace.WriteLine($"StartupManager: Set Run key '{ValueName}' -> {exe}");
                }
                else
                {
                    Trace.WriteLine("StartupManager: Could not determine current exe path; not setting Run value.");
                }
            }
            else
            {
                key.DeleteValue(ValueName, false);
                Trace.WriteLine($"StartupManager: Removed Run key '{ValueName}'");
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"StartupManager: Exception in SetEnabled: {ex}");
            // Best-effort; swallow errors so UI doesn't crash.
        }
    }

    private static string? GetCurrentExePath()
    {
        try
        {
            // 1) Environment.ProcessPath (preferred on .NET 6+)
            try
            {
                var env = Environment.ProcessPath;
                if (!string.IsNullOrWhiteSpace(env) && File.Exists(env))
                {
                    return Path.GetFullPath(env);
                }
            }
            catch { }

            // 2) Process main module
            try
            {
                using var proc = Process.GetCurrentProcess();
                var main = proc.MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(main) && File.Exists(main))
                {
                    return Path.GetFullPath(main);
                }
            }
            catch { }

            // 3) Entry assembly
            try
            {
                var entry = Assembly.GetEntryAssembly()?.Location;
                if (!string.IsNullOrWhiteSpace(entry) && File.Exists(entry))
                {
                    return Path.GetFullPath(entry);
                }
            }
            catch { }

            // 4) Executing assembly as last resort
            try
            {
                var exec = Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrWhiteSpace(exec) && File.Exists(exec))
                {
                    return Path.GetFullPath(exec);
                }
            }
            catch { }

            return null;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"StartupManager: Exception in GetCurrentExePath: {ex}");
            return null;
        }
    }
}
