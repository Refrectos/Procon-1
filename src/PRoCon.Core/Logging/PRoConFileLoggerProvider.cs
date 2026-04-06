using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace PRoCon.Core.Logging
{
    /// <summary>
    /// File-based ILoggerProvider that writes structured log entries optimized for
    /// diagnostic analysis. Outputs one JSON object per line (JSONL format) so that
    /// each entry is self-contained and machine-parseable while remaining human-readable.
    ///
    /// Features:
    ///   - Size-based rotation (default 10 MB per file, 10 files retained)
    ///   - Thread-safe writes with periodic flush (every 2 seconds or on Warning+)
    ///   - Graceful fallback if the log directory is inaccessible
    ///   - Captures exception details with full chain
    ///   - Thread ID in output for concurrency diagnosis
    /// </summary>
    public sealed class PRoConFileLoggerProvider : ILoggerProvider
    {
        private readonly string _logDirectory;
        private readonly string _baseFileName;
        private readonly long _maxFileSize;
        private readonly int _maxFiles;
        internal readonly LogLevel MinimumLevel;
        private readonly ConcurrentDictionary<string, PRoConFileLogger> _loggers = new();
        private readonly object _writeLock = new();
        private readonly Timer _flushTimer;
        private StreamWriter _writer;
        private string _currentFilePath;
        private long _bytesWritten;
        private bool _disposed;

        public PRoConFileLoggerProvider(
            string logDirectory,
            string baseFileName = "procon",
            long maxFileSizeBytes = 10 * 1024 * 1024,
            int maxFiles = 10,
            LogLevel minimumLevel = LogLevel.Information)
        {
            _logDirectory = logDirectory;
            _baseFileName = baseFileName;
            _maxFileSize = maxFileSizeBytes;
            _maxFiles = maxFiles;
            MinimumLevel = minimumLevel;

            // Periodic flush every 2 seconds to balance durability and performance
            _flushTimer = new Timer(_ => FlushSafe(), null, 2000, 2000);

            try
            {
                Directory.CreateDirectory(_logDirectory);
                OpenWriter();
            }
            catch
            {
                // If we can't create the log directory, we'll silently degrade.
                // The ILogger instances will check _writer != null before writing.
            }
        }

        public ILogger CreateLogger(string categoryName)
        {
            return _loggers.GetOrAdd(categoryName, name => new PRoConFileLogger(name, this));
        }

        internal void WriteEntry(
            LogLevel level,
            string category,
            EventId eventId,
            string message,
            Exception exception)
        {
            if (_disposed || level < MinimumLevel)
                return;

            lock (_writeLock)
            {
                if (_writer == null)
                    return;

                try
                {
                    RotateIfNeeded();

                    var timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
                    var levelStr = LevelToString(level);

                    // JSONL: one self-contained JSON object per line, optimized for diagnostic analysis.
                    // Fields: ts (ISO 8601), level (3-char code), cat (component), tid (thread),
                    //         msg (message), err (exception with full chain)
                    var sb = new StringBuilder(512);
                    sb.Append("{\"ts\":\"").Append(timestamp).Append('"');
                    sb.Append(",\"level\":\"").Append(levelStr).Append('"');
                    sb.Append(",\"cat\":\"").Append(EscapeJson(category)).Append('"');
                    sb.Append(",\"tid\":").Append(Environment.CurrentManagedThreadId);

                    if (eventId.Id != 0)
                        sb.Append(",\"eventId\":").Append(eventId.Id);

                    sb.Append(",\"msg\":\"").Append(EscapeJson(message ?? "")).Append('"');

                    if (exception != null)
                    {
                        sb.Append(",\"err\":{");
                        sb.Append("\"type\":\"").Append(EscapeJson(exception.GetType().FullName ?? "Unknown")).Append('"');
                        sb.Append(",\"msg\":\"").Append(EscapeJson(exception.Message ?? "")).Append('"');
                        // Use ToString() for full exception chain including all inner exceptions
                        sb.Append(",\"full\":\"").Append(EscapeJson(exception.ToString())).Append('"');
                        sb.Append('}');
                    }

                    sb.Append('}');

                    string line = sb.ToString();
                    _writer.WriteLine(line);
                    _bytesWritten += Encoding.UTF8.GetByteCount(line) + Environment.NewLine.Length;

                    // Flush immediately for warnings and above to ensure errors are persisted
                    if (level >= LogLevel.Warning)
                        _writer.Flush();
                }
                catch
                {
                    // Logging must never crash the application
                }
            }
        }

        private void FlushSafe()
        {
            if (_disposed) return;
            lock (_writeLock)
            {
                try { _writer?.Flush(); } catch { }
            }
        }

        private void OpenWriter()
        {
            _currentFilePath = Path.Combine(_logDirectory, $"{_baseFileName}.log");
            var stream = new FileStream(_currentFilePath, FileMode.Append, FileAccess.Write, FileShare.Read);
            _writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = false };

            // Seed byte counter from existing file size
            try { _bytesWritten = new FileInfo(_currentFilePath).Length; } catch { _bytesWritten = 0; }
        }

        private void RotateIfNeeded()
        {
            if (_bytesWritten < _maxFileSize)
                return;

            try
            {
                _writer?.Flush();
                _writer?.Dispose();
                _writer = null;

                // Rotate: procon.9.log -> delete, procon.8.log -> procon.9.log, ... procon.log -> procon.1.log
                for (int i = _maxFiles - 1; i >= 1; i--)
                {
                    string source = i == 1
                        ? Path.Combine(_logDirectory, $"{_baseFileName}.log")
                        : Path.Combine(_logDirectory, $"{_baseFileName}.{i - 1}.log");
                    string dest = Path.Combine(_logDirectory, $"{_baseFileName}.{i}.log");

                    if (File.Exists(source))
                    {
                        if (File.Exists(dest))
                            File.Delete(dest);
                        File.Move(source, dest);
                    }
                }

                _bytesWritten = 0;
                OpenWriter();
            }
            catch
            {
                // If rotation fails, try to reopen the main file
                try { OpenWriter(); } catch { }
            }
        }

        private static string LevelToString(LogLevel level)
        {
            return level switch
            {
                LogLevel.Trace => "TRC",
                LogLevel.Debug => "DBG",
                LogLevel.Information => "INF",
                LogLevel.Warning => "WRN",
                LogLevel.Error => "ERR",
                LogLevel.Critical => "CRT",
                _ => "NON"
            };
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            var sb = new StringBuilder(value.Length + 16);
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 0x20)
                            sb.Append("\\u").Append(((int)c).ToString("X4"));
                        else
                            sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            _flushTimer?.Dispose();

            lock (_writeLock)
            {
                _writer?.Flush();
                _writer?.Dispose();
                _writer = null;
            }
        }
    }

    /// <summary>
    /// Individual logger instance that delegates all writes to the provider.
    /// </summary>
    internal sealed class PRoConFileLogger : ILogger
    {
        private readonly string _category;
        private readonly PRoConFileLoggerProvider _provider;

        public PRoConFileLogger(string category, PRoConFileLoggerProvider provider)
        {
            _category = category;
            _provider = provider;
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= _provider.MinimumLevel && logLevel != LogLevel.None;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception exception,
            Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            string message = formatter(state, exception);
            _provider.WriteEntry(logLevel, _category, eventId, message, exception);
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
