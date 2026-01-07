using System;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Glyph.Core.Logging;

public static class Logger
{
    private static readonly string _logDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Glyph",
        "logs");
    
    private static readonly string _logFile = Path.Combine(_logDir, $"glyph_{DateTime.Now:yyyy-MM-dd}.log");

    private static readonly Channel<string> _queue = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false,
        AllowSynchronousContinuations = true,
    });

    private static readonly CancellationTokenSource _cts = new();

    private static readonly Task _writerTask;

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

        _writerTask = Task.Run(async () =>
        {
            try
            {
                using var stream = new FileStream(_logFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                using var writer = new StreamWriter(stream) { AutoFlush = false };

                while (await _queue.Reader.WaitToReadAsync(_cts.Token).ConfigureAwait(false))
                {
                    var wroteAny = false;
                    while (_queue.Reader.TryRead(out var line))
                    {
                        writer.WriteLine(line);
                        wroteAny = true;
                    }

                    if (wroteAny)
                    {
                        writer.Flush();
                    }
                }
            }
            catch
            {
                // Never throw from background logger.
            }
        });

        try
        {
            AppDomain.CurrentDomain.ProcessExit += (_, _) => Shutdown();
        }
        catch
        {
            // Best-effort.
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
            _queue.Writer.TryWrite(entry);
        }
        catch
        {
            // Swallow logging errors.
        }
    }

    public static void Shutdown()
    {
        try
        {
            _cts.Cancel();
        }
        catch { }

        try
        {
            _queue.Writer.TryComplete();
        }
        catch { }

        try
        {
            _writerTask.Wait(millisecondsTimeout: 250);
        }
        catch { }
    }
}
