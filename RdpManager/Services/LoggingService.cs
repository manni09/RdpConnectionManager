using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace RdpManager.Services
{
    /// <summary>
    /// Production-grade logging service with file and debug output.
    /// Supports log levels, automatic rotation, and structured logging.
    /// </summary>
    public static class LoggingService
    {
        private static readonly object _lock = new();
        private static readonly string LogFolder;
        private static readonly string LogFilePath;
        private static readonly long MaxLogSizeBytes = 10 * 1024 * 1024; // 10 MB
        private static readonly int MaxLogFiles = 5;
        private static LogLevel _minimumLevel = LogLevel.Information;

        public enum LogLevel
        {
            Debug = 0,
            Information = 1,
            Warning = 2,
            Error = 3,
            Critical = 4
        }

        static LoggingService()
        {
            LogFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RdpManager", "Logs");
            
            LogFilePath = Path.Combine(LogFolder, "rdpmanager.log");

            try
            {
                if (!Directory.Exists(LogFolder))
                {
                    Directory.CreateDirectory(LogFolder);
                }
            }
            catch
            {
                // If we can't create log folder, we'll just log to debug output
            }
        }

        /// <summary>
        /// Sets the minimum log level to write.
        /// </summary>
        public static void SetMinimumLevel(LogLevel level)
        {
            _minimumLevel = level;
        }

        /// <summary>
        /// Logs a debug message.
        /// </summary>
        public static void Debug(string message,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            Log(LogLevel.Debug, message, null, memberName, filePath, lineNumber);
        }

        /// <summary>
        /// Logs an informational message.
        /// </summary>
        public static void Info(string message,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            Log(LogLevel.Information, message, null, memberName, filePath, lineNumber);
        }

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        public static void Warn(string message,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            Log(LogLevel.Warning, message, null, memberName, filePath, lineNumber);
        }

        /// <summary>
        /// Logs an error message.
        /// </summary>
        public static void Error(string message, Exception? ex = null,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            Log(LogLevel.Error, message, ex, memberName, filePath, lineNumber);
        }

        /// <summary>
        /// Logs a critical error message.
        /// </summary>
        public static void Critical(string message, Exception? ex = null,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            Log(LogLevel.Critical, message, ex, memberName, filePath, lineNumber);
        }

        private static void Log(LogLevel level, string message, Exception? ex,
            string memberName, string filePath, int lineNumber)
        {
            if (level < _minimumLevel)
                return;

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var fileName = Path.GetFileName(filePath);
            var levelStr = level.ToString().ToUpperInvariant().PadRight(11);
            
            var logMessage = $"[{timestamp}] [{levelStr}] [{fileName}:{lineNumber}] {memberName}: {message}";
            
            if (ex != null)
            {
                logMessage += Environment.NewLine + 
                    $"    Exception: {ex.GetType().Name}: {ex.Message}" + Environment.NewLine +
                    $"    Stack: {ex.StackTrace}";
                
                if (ex.InnerException != null)
                {
                    logMessage += Environment.NewLine +
                        $"    Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}";
                }
            }

            // Write to debug output
            System.Diagnostics.Debug.WriteLine(logMessage);

            // Write to file
            WriteToFile(logMessage);
        }

        private static void WriteToFile(string message)
        {
            lock (_lock)
            {
                try
                {
                    // Check for rotation
                    if (File.Exists(LogFilePath))
                    {
                        var fileInfo = new FileInfo(LogFilePath);
                        if (fileInfo.Length >= MaxLogSizeBytes)
                        {
                            RotateLogs();
                        }
                    }

                    // Append to log file
                    File.AppendAllText(LogFilePath, message + Environment.NewLine);
                }
                catch
                {
                    // Silently fail - logging should never crash the app
                }
            }
        }

        private static void RotateLogs()
        {
            try
            {
                // Delete oldest log if at max
                var oldestLog = Path.Combine(LogFolder, $"rdpmanager.{MaxLogFiles}.log");
                if (File.Exists(oldestLog))
                {
                    File.Delete(oldestLog);
                }

                // Rotate existing logs
                for (int i = MaxLogFiles - 1; i >= 1; i--)
                {
                    var oldPath = Path.Combine(LogFolder, $"rdpmanager.{i}.log");
                    var newPath = Path.Combine(LogFolder, $"rdpmanager.{i + 1}.log");
                    if (File.Exists(oldPath))
                    {
                        File.Move(oldPath, newPath);
                    }
                }

                // Rotate current log
                var rotatedPath = Path.Combine(LogFolder, "rdpmanager.1.log");
                File.Move(LogFilePath, rotatedPath);
            }
            catch
            {
                // Silently fail
            }
        }

        /// <summary>
        /// Gets the path to the log folder.
        /// </summary>
        public static string GetLogFolder() => LogFolder;

        /// <summary>
        /// Cleans up old log files.
        /// </summary>
        public static void CleanupOldLogs(int daysToKeep = 30)
        {
            try
            {
                var cutoff = DateTime.Now.AddDays(-daysToKeep);
                foreach (var file in Directory.GetFiles(LogFolder, "*.log"))
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.LastWriteTime < cutoff)
                    {
                        File.Delete(file);
                    }
                }
            }
            catch
            {
                // Silently fail
            }
        }
    }
}
