using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace GlobalTextHelper
{
    internal static class AppLogger
    {
        private static readonly object _lock = new();
        private static bool _initialized;
        private static bool _initializationFailed;
        private static StreamWriter? _writer;
        private static string? _logFilePath;

        public static void Initialize()
        {
            lock (_lock)
            {
                if (_initialized || _initializationFailed)
                {
                    return;
                }

                try
                {
                    var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    if (string.IsNullOrEmpty(appData))
                    {
                        throw new InvalidOperationException("LocalApplicationData folder path is unavailable.");
                    }

                    var logDirectory = Path.Combine(appData, "WritingHelper", "logs");
                    Directory.CreateDirectory(logDirectory);

                    _logFilePath = Path.Combine(logDirectory, "writing-helper.log");
                    var fileStream = new FileStream(_logFilePath, FileMode.Append, FileAccess.Write, FileShare.Read);
                    _writer = new StreamWriter(fileStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
                    {
                        AutoFlush = true
                    };

                    _initialized = true;
                    WriteLineInternal("Logger initialized.");
                    WriteLineInternal($"Logging to '{_logFilePath}'.");
                }
                catch (Exception ex)
                {
                    _initializationFailed = true;
                    _writer = null;
                    _logFilePath = null;
                    WriteFallback($"Failed to initialize logger: {ex}");
                }
            }
        }

        public static void Log(string message)
        {
            Write("INFO", message, null);
        }

        public static void LogError(string message, Exception exception)
        {
            Write("ERROR", message, exception);
        }

        public static string? LogFilePath
        {
            get
            {
                lock (_lock)
                {
                    return _logFilePath;
                }
            }
        }

        public static void Shutdown()
        {
            lock (_lock)
            {
                _writer?.Dispose();
                _writer = null;
                _initialized = false;
            }
        }

        private static void Write(string level, string message, Exception? exception)
        {
            EnsureInitialized();

            var timestamp = DateTime.Now;
            var builder = new StringBuilder();
            builder.Append('[')
                .Append(timestamp.ToString("O"))
                .Append("] [")
                .Append(level)
                .Append("] ")
                .Append(message);

            if (exception != null)
            {
                builder.AppendLine();
                builder.Append(exception);
            }

            WriteLineInternal(builder.ToString());
        }

        private static void EnsureInitialized()
        {
            if (_initialized || _initializationFailed)
            {
                return;
            }

            Initialize();
        }

        private static void WriteLineInternal(string line)
        {
            try
            {
                lock (_lock)
                {
                    _writer?.WriteLine(line);
                }
            }
            catch
            {
                // Swallow exceptions while writing to the log file, but still attempt fallbacks.
            }

            WriteFallback(line);
        }

        private static void WriteFallback(string line)
        {
            try
            {
                Debug.WriteLine(line);
            }
            catch
            {
                // Ignore debug write failures (e.g. no listeners).
            }

            try
            {
                Console.WriteLine(line);
            }
            catch
            {
                // Ignore console write failures (e.g. no console attached).
            }
        }
    }
}
