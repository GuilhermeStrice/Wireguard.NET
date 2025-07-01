using System;

namespace WireGuardManager.Utilities
{
    /// <summary>
    /// Encapsulates the result of an external process execution, including exit code and standard/error output.
    /// </summary>
    public class ProcessExecutionResult
    {
        /// <summary>
        /// Gets the exit code returned by the executed process.
        /// </summary>
        public int ExitCode { get; }

        /// <summary>
        /// Gets the content written by the process to its standard output stream.
        /// </summary>
        public string StandardOutput { get; }

        /// <summary>
        /// Gets the content written by the process to its standard error stream.
        /// </summary>
        public string StandardError { get; }

        /// <summary>
        /// Gets a value indicating whether the process executed successfully, typically indicated by an <see cref="ExitCode"/> of 0.
        /// </summary>
        public bool Success => ExitCode == 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProcessExecutionResult"/> class.
        /// </summary>
        /// <param name="exitCode">The exit code of the process.</param>
        /// <param name="standardOutput">The standard output of the process.</param>
        /// <param name="standardError">The standard error output of the process.</param>
        public ProcessExecutionResult(int exitCode, string standardOutput, string standardError)
        {
            ExitCode = exitCode;
            StandardOutput = standardOutput;
            StandardError = standardError;
        }
    }
}
