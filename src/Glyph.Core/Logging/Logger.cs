using System;
using System.IO;

namespace Glyph.Core.Logging;

public static class Logger
{
    private static readonly string _logDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Glyph",
        "logs");
    
    private static readonly string _logFile = Path.Combine(_logDir, $"glyph_{DateTime.Now:yyyy-MM-dd}.log");

    static Logger()
    {
        try
        {
            Directory.CreateDirectory(_logDir);
        }
        catch
        {
            // Swallow: if we can't create log dir, just skip logging.
        }
    }

    public static string LogDirectory => _logDir;
    public static string LogFile => _logFile;

    public static void Info(string message)
    {
        WriteLog("INFO", message);
    }

    public static void Warning(string message)
    {
        WriteLog("WARN", message);
    }

    public static void Error(string message)
    {
        WriteLog("ERROR", message);
    }

    public static void Error(string message, Exception ex)
    {
        WriteLog("ERROR", $"{message}: {ex.GetType().Name}: {ex.Message}");
    }

    private static void WriteLog(string level, string message)
    {
        try
        {
            var entry = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
            File.AppendAllText(_logFile, entry + Environment.NewLine);
        }
        catch
        {
            // Swallow logging errors.
        }
    }
}
