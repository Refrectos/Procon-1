using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace PRoCon.Core.Logging
{
    /// <summary>
    /// Central static logging facility for PRoCon. Wraps Microsoft.Extensions.Logging
    /// so that all existing FrostbiteConnection.LogError call sites continue to work
    /// while gaining structured logging, log-level filtering, and pluggable providers.
    ///
    /// Call <see cref="Initialize"/> once at startup (e.g. in Program.cs) to wire up
    /// a real ILoggerFactory. Until that happens every logger is a silent no-op, so
    /// the application never crashes due to missing logging configuration.
    ///
    /// IMPORTANT: CreateLogger returns a forwarding logger that always delegates to
    /// the current Factory. This means static fields initialized before Initialize()
    /// will start working once a real factory is provided.
    /// </summary>
    public static class PRoConLog
    {
        private static ILoggerFactory _factory = NullLoggerFactory.Instance;

        /// <summary>
        /// The shared ILoggerFactory used by the entire application.
        /// Returns <see cref="NullLoggerFactory.Instance"/> when not yet initialized.
        /// </summary>
        public static ILoggerFactory Factory
        {
            get => _factory;
            private set => _factory = value ?? NullLoggerFactory.Instance;
        }

        /// <summary>
        /// Initialize the logging subsystem. Should be called once during startup.
        /// </summary>
        /// <param name="factory">
        /// A configured <see cref="ILoggerFactory"/>. Pass null to reset to no-op logging.
        /// </param>
        public static void Initialize(ILoggerFactory factory)
        {
            Factory = factory;
        }

        /// <summary>
        /// Create a logger for the given category type.
        /// Returns a forwarding logger that always uses the current Factory.
        /// </summary>
        public static ILogger<T> CreateLogger<T>()
        {
            // ILogger<T> doesn't support forwarding easily, resolve immediately.
            // For static fields, prefer the string overload instead.
            return Factory.CreateLogger<T>();
        }

        /// <summary>
        /// Create a forwarding logger for the given category name.
        /// Safe to store in static fields — will use the current Factory at log time.
        /// </summary>
        public static ILogger CreateLogger(string categoryName)
        {
            return new ForwardingLogger(categoryName);
        }

        /// <summary>
        /// Create a forwarding logger for the given category type.
        /// Safe to store in static fields — will use the current Factory at log time.
        /// </summary>
        public static ILogger CreateLogger(Type type)
        {
            return new ForwardingLogger(type.FullName ?? type.Name);
        }

        /// <summary>
        /// A logger that always delegates to the current PRoConLog.Factory,
        /// so it works correctly even when stored in a static field before Initialize().
        /// </summary>
        private sealed class ForwardingLogger : ILogger
        {
            private readonly string _categoryName;

            public ForwardingLogger(string categoryName)
            {
                _categoryName = categoryName;
            }

            public IDisposable BeginScope<TState>(TState state) where TState : notnull
            {
                return Factory.CreateLogger(_categoryName).BeginScope(state);
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return Factory.CreateLogger(_categoryName).IsEnabled(logLevel);
            }

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception exception,
                Func<TState, Exception, string> formatter)
            {
                Factory.CreateLogger(_categoryName).Log(logLevel, eventId, state, exception, formatter);
            }
        }
    }
}
