using System;

namespace WireGuardManager.Utilities
{
    /// <summary>
    /// Default implementation of <see cref="IWgLoggingProvider"/> that writes log messages to the console.
    /// Errors are written to Console.Error, other levels to Console.Out.
    /// Console colors can be disabled via the static <see cref="UseConsoleColors"/> property.
    /// </summary>
    public class ConsoleLoggingProvider : IWgLoggingProvider
    {
        /// <summary>
        /// Gets or sets a value indicating whether to use colors when logging to the console.
        /// Defaults to true. This can be updated by <see cref="WgManagerConfig.LoadAsync"/>
        /// based on the 'enableConsoleColors' setting in config.json.
        /// </summary>
        public static bool UseConsoleColors { get; set; } = true;

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
            if (UseConsoleColors) Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine(FormatMessage("TRACE", message));
            if (UseConsoleColors) Console.ResetColor();
        }

        /// <inheritdoc/>
        public void LogDebug(string message)
        {
            if (WgLogging.MinimumLogLevel > LogLevel.Debug) return;
            if (UseConsoleColors) Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine(FormatMessage("DEBUG", message));
            if (UseConsoleColors) Console.ResetColor();
        }

        /// <inheritdoc/>
        public void LogInfo(string message)
        {
            if (WgLogging.MinimumLogLevel > LogLevel.Info) return;
            // No specific color for Info, or use White if desired
            Console.WriteLine(FormatMessage("INFO", message));
        }

        /// <inheritdoc/>
        public void LogWarning(string message)
        {
            if (WgLogging.MinimumLogLevel > LogLevel.Warning) return;
            ConsoleColor originalColor = Console.ForegroundColor;
            if (UseConsoleColors) Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Error.WriteLine(FormatMessage("WARN", message));
            if (UseConsoleColors) Console.ForegroundColor = originalColor; // Reset to original, not necessarily Console.ResetColor()
        }

        /// <inheritdoc/>
        public void LogError(string message, Exception? ex = null)
        {
            if (WgLogging.MinimumLogLevel > LogLevel.Error) return;
            ConsoleColor originalColor = Console.ForegroundColor;
            if (UseConsoleColors) Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine(FormatMessage("ERROR", message, ex));
            if (UseConsoleColors) Console.ForegroundColor = originalColor;
        }
    }
}
