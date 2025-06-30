using System;

namespace WireGuardManager.Utilities
{
    /// <summary>
    /// Represents the result of an external process execution.
    /// </summary>
    public class ProcessExecutionResult
    {
        /// <summary>
        /// Gets the exit code of the process.
        /// </summary>
        public int ExitCode { get; }

        /// <summary>
        /// Gets the standard output of the process.
        /// </summary>
        public string StandardOutput { get; }

        /// <summary>
        /// Gets the standard error output of the process.
        /// </summary>
        public string StandardError { get; }

        /// <summary>
        /// Gets a value indicating whether the process executed successfully (ExitCode == 0).
        /// </summary>
        public bool Success => ExitCode == 0;

        public ProcessExecutionResult(int exitCode, string standardOutput, string standardError)
        {
            ExitCode = exitCode;
            StandardOutput = standardOutput;
            StandardError = standardError;
        }
    }
}
