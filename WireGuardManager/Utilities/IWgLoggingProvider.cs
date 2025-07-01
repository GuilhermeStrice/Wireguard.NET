using System;

namespace WireGuardManager.Utilities
{
    /// <summary>
    /// Defines a contract for a logging provider that can be used by the WireGuardManager library.
    /// </summary>
    public interface IWgLoggingProvider
    {
        /// <summary>
        /// Logs a trace-level message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        void LogTrace(string message);

        /// <summary>
        /// Logs a debug-level message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        void LogDebug(string message);

        /// <summary>
        /// Logs an informational message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        void LogInfo(string message);

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        void LogWarning(string message);

        /// <summary>
        /// Logs an error message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <param name="ex">Optional exception associated with the error.</param>
        void LogError(string message, Exception? ex = null);
    }
}
