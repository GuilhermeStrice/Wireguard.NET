using System;

namespace WireGuardManager.Utilities
{
    /// <summary>
    /// Default implementation of <see cref="IWgLoggingProvider"/> that writes log messages to the console.
    /// Errors are written to Console.Error, other levels to Console.Out.
    /// </summary>
    public class ConsoleLoggingProvider : IWgLoggingProvider
    {
        private static string FormatMessage(string level, string message, Exception? ex = null)
        {
            string logMessage = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
            if (ex != null)
            {
                logMessage += $"{Environment.NewLine}Exception: {ex}"; // Includes stack trace via ex.ToString()
            }
            return logMessage;
        }

        /// <inheritdoc/>
        public void LogTrace(string message)
        {
            if (WgLogging.MinimumLogLevel > LogLevel.Trace) return;
            // Console.ForegroundColor = ConsoleColor.DarkGray; // Optional styling
            Console.WriteLine(FormatMessage("TRACE", message));
            // Console.ResetColor();
        }

        /// <inheritdoc/>
        public void LogDebug(string message)
        {
            if (WgLogging.MinimumLogLevel > LogLevel.Debug) return;
            // Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine(FormatMessage("DEBUG", message));
            // Console.ResetColor();
        }

        /// <inheritdoc/>
        public void LogInfo(string message)
        {
            if (WgLogging.MinimumLogLevel > LogLevel.Info) return;
            // Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(FormatMessage("INFO", message));
            // Console.ResetColor();
        }

        /// <inheritdoc/>
        public void LogWarning(string message)
        {
            if (WgLogging.MinimumLogLevel > LogLevel.Warning) return;
            ConsoleColor originalColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Error.WriteLine(FormatMessage("WARN", message)); // Warnings often go to Stderr
            Console.ForegroundColor = originalColor;
        }

        /// <inheritdoc/>
        public void LogError(string message, Exception? ex = null)
        {
            if (WgLogging.MinimumLogLevel > LogLevel.Error) return;
            ConsoleColor originalColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine(FormatMessage("ERROR", message, ex));
            Console.ForegroundColor = originalColor;
        }
    }
}
