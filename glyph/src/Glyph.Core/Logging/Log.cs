using System;
using System.IO;

namespace Glyph.Core.Logging
{
    public static class Log
    {
        private static readonly string logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "glyph.log");
        
        static Log()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logFilePath));
        }

        public static void Info(string message)
        {
            WriteLog("INFO", message);
        }

        public static void Warning(string message)
        {
            WriteLog("WARNING", message);
        }

        public static void Error(string message)
        {
            WriteLog("ERROR", message);
        }

        private static void WriteLog(string level, string message)
        {
            var logEntry = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} [{level}] {message}";
            File.AppendAllText(logFilePath, logEntry + Environment.NewLine);
        }
    }
}