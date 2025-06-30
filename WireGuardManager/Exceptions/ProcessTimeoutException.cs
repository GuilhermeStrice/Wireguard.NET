using System;

namespace WireGuardManager.Exceptions
{
    /// <summary>
    /// Exception thrown when a process started by ProcessRunner exceeds its timeout duration.
    /// </summary>
    public class ProcessTimeoutException : ExternalToolException
    {
        public TimeSpan TimeoutDuration { get; }

        public ProcessTimeoutException(string toolName, TimeSpan timeoutDuration, string command, int? exitCode = null, string? stdOut = null, string? stdErr = null)
            : base(toolName, $"Execution of '{command}' timed out after {timeoutDuration.TotalSeconds} seconds.", exitCode, stdOut, stdErr)
        {
            TimeoutDuration = timeoutDuration;
        }

        public ProcessTimeoutException(string toolName, TimeSpan timeoutDuration, string command, Exception innerException, int? exitCode = null, string? stdOut = null, string? stdErr = null)
            : base(toolName, $"Execution of '{command}' timed out after {timeoutDuration.TotalSeconds} seconds.", exitCode, stdOut, stdErr, innerException)
        {
            TimeoutDuration = timeoutDuration;
        }
    }
}
