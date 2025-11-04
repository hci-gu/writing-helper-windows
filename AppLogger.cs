using System;
using System.Diagnostics;
using System.IO;

namespace GlobalTextHelper
{
    internal static class AppLogger
    {
        private static readonly object _lock = new();
        private static bool _initialized;
        private static string? _logFilePath;

        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            lock (_lock)
            {
                if (_initialized)
                {
                    return;
                }

                try
                {
                    var baseDirectory = AppDomain.CurrentDomain.BaseDirectory ?? Environment.CurrentDirectory;
                    var logDirectory = Path.Combine(baseDirectory, "logs");
                    Directory.CreateDirectory(logDirectory);

                    _logFilePath = Path.Combine(logDirectory, "writing-helper.log");
                    var textListener = new TextWriterTraceListener(_logFilePath)
                    {
                        Name = "FileLogger"
                    };

                    Trace.Listeners.Add(textListener);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[{DateTime.Now:O}] Failed to initialize file logger: {ex}");
                }
                finally
                {
                    bool hasConsoleListener = false;
                    foreach (TraceListener listener in Trace.Listeners)
                    {
                        if (listener is ConsoleTraceListener)
                        {
                            hasConsoleListener = true;
                            break;
                        }
                    }

                    if (!hasConsoleListener)
                    {
                        Trace.Listeners.Add(new ConsoleTraceListener());
                    }

                    Trace.AutoFlush = true;
                    _initialized = true;
                    Log("Logger initialized.");
                    if (!string.IsNullOrEmpty(_logFilePath))
                    {
                        Log($"Logging to '{_logFilePath}'.");
                    }
                }
            }
        }

        public static void Log(string message)
        {
            Trace.WriteLine($"[{DateTime.Now:O}] {message}");
        }

        public static void LogError(string message, Exception exception)
        {
            Trace.WriteLine($"[{DateTime.Now:O}] ERROR: {message} {exception}");
        }
    }
}
