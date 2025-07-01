using System;

namespace WireGuardManager.Exceptions
{
    /// <summary>
    /// Exception thrown when an external process started by <see cref="Utilities.ProcessRunner"/> exceeds its specified timeout duration.
    /// </summary>
    public class ProcessTimeoutException : ExternalToolException
    {
        /// <summary>
        /// Gets the duration of the timeout that was exceeded.
        /// </summary>
        public TimeSpan TimeoutDuration { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProcessTimeoutException"/> class.
        /// </summary>
        /// <param name="toolName">The name of the tool that timed out.</param>
        /// <param name="timeoutDuration">The duration of the timeout.</param>
        /// <param name="command">The command (filename and arguments) that was being executed.</param>
        /// <param name="exitCode">The exit code of the process, if available (may not be reliable if process was killed).</param>
        /// <param name="stdOut">The standard output collected before the timeout, if any.</param>
        /// <param name="stdErr">The standard error output collected before the timeout, if any.</param>
        public ProcessTimeoutException(string toolName, TimeSpan timeoutDuration, string command, int? exitCode = null, string? stdOut = null, string? stdErr = null)
            : base(toolName, $"Execution of '{command}' timed out after {timeoutDuration.TotalSeconds} seconds.", exitCode, stdOut, stdErr)
        {
            TimeoutDuration = timeoutDuration;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProcessTimeoutException"/> class with a reference to the inner exception.
        /// </summary>
        /// <param name="toolName">The name of the tool that timed out.</param>
        /// <param name="timeoutDuration">The duration of the timeout.</param>
        /// <param name="command">The command (filename and arguments) that was being executed.</param>
        /// <param name="innerException">The exception that is the cause of the current exception (e.g., TaskCanceledException).</param>
        /// <param name="exitCode">The exit code of the process, if available.</param>
        /// <param name="stdOut">The standard output collected before the timeout, if any.</param>
        /// <param name="stdErr">The standard error output collected before the timeout, if any.</param>
        public ProcessTimeoutException(string toolName, TimeSpan timeoutDuration, string command, Exception innerException, int? exitCode = null, string? stdOut = null, string? stdErr = null)
            : base(toolName, $"Execution of '{command}' timed out after {timeoutDuration.TotalSeconds} seconds.", exitCode, stdOut, stdErr, innerException)
        {
            TimeoutDuration = timeoutDuration;
        }
    }
}
