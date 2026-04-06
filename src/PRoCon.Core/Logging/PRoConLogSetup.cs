using System;
using Microsoft.Extensions.Logging;

namespace PRoCon.Core.Logging
{
    /// <summary>
    /// Centralized logging configuration for all PRoCon entry points (UI, Console, Service).
    /// Ensures consistent file + console logging across all modes.
    /// </summary>
    public static class PRoConLogSetup
    {
        private static ILoggerFactory _factory;
        private static PRoConFileLoggerProvider _fileProvider;

        /// <summary>
        /// Creates and initializes the shared ILoggerFactory with file and console providers.
        /// Call once at startup before constructing PRoConApplication.
        /// </summary>
        /// <param name="enableConsole">True to also log to stdout (UI/Console modes).</param>
        /// <param name="minimumLevel">Minimum log level to capture in file logs.</param>
        /// <returns>The configured ILoggerFactory (also registered with PRoConLog).</returns>
        public static ILoggerFactory Initialize(bool enableConsole = true, LogLevel minimumLevel = LogLevel.Information)
        {
            // Ensure the Logs directory exists
            string logsDir = ProConPaths.LogsDirectory;

            _fileProvider = new PRoConFileLoggerProvider(
                logDirectory: logsDir,
                baseFileName: "procon",
                maxFileSizeBytes: 10 * 1024 * 1024,  // 10 MB per file
                maxFiles: 10,                          // Keep 10 rotated files (100 MB total max)
                minimumLevel: minimumLevel);

            _factory = LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(minimumLevel);
                builder.AddProvider(_fileProvider);

                if (enableConsole)
                    builder.AddConsole();
            });

            PRoConLog.Initialize(_factory);

            // Write a startup marker so log files are easy to segment by session
            var startupLogger = _factory.CreateLogger("PRoCon.Startup");
            startupLogger.LogInformation(
                "=== PRoCon session started | PID={ProcessId} | DataDir={DataDir} | Container={IsContainer} | CLR={ClrVersion} ===",
                Environment.ProcessId,
                ProConPaths.DataDirectory,
                ProConPaths.IsContainer,
                Environment.Version);

            return _factory;
        }

        /// <summary>
        /// Flush and dispose the logging subsystem. Call during application shutdown
        /// to ensure all buffered entries are written to disk.
        /// </summary>
        public static void Shutdown()
        {
            try
            {
                _fileProvider?.Dispose();
                _factory?.Dispose();
            }
            catch { }
            finally
            {
                _fileProvider = null;
                _factory = null;
            }
        }
    }
}
