using System;

namespace WireGuardManager.Exceptions
{
    /// <summary>
    /// Represents errors that occur during the execution of external command-line tools
    /// such as 'wg', 'wg-quick', or 'systemctl'. This is a base class for more specific tool exceptions.
    /// </summary>
    public class ExternalToolException : WireGuardManagerException
    {
        /// <summary>
        /// Gets the name of the external tool that caused the error.
        /// </summary>
        public string ToolName { get; }

        /// <summary>
        /// Gets the exit code of the external tool, if available.
        /// </summary>
        public int? ExitCode { get; }

        /// <summary>
        /// Gets the standard output from the external tool, if available.
        /// </summary>
        public string? StandardOutput { get; }

        /// <summary>
        /// Gets the standard error output from the external tool, if available.
        /// </summary>
        public string? StandardError { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExternalToolException"/> class.
        /// </summary>
        /// <param name="toolName">The name of the tool.</param>
        /// <param name="message">The message that describes the error.</param>
        public ExternalToolException(string toolName, string message)
            : base(message)
        {
            ToolName = toolName;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExternalToolException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="toolName">The name of the tool.</param>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
        public ExternalToolException(string toolName, string message, Exception innerException)
            : base(message, innerException)
        {
            ToolName = toolName;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExternalToolException"/> class with detailed execution information.
        /// </summary>
        /// <param name="toolName">The name of the tool.</param>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="exitCode">The exit code of the process.</param>
        /// <param name="stdOut">The standard output of the process.</param>
        /// <param name="stdErr">The standard error output of the process.</param>
        public ExternalToolException(string toolName, string message, int? exitCode, string? stdOut, string? stdErr)
            : base(message)
        {
            ToolName = toolName;
            ExitCode = exitCode;
            StandardOutput = stdOut;
            StandardError = stdErr;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExternalToolException"/> class with detailed execution information and a reference to the inner exception.
        /// </summary>
        /// <param name="toolName">The name of the tool.</param>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="exitCode">The exit code of the process.</param>
        /// <param name="stdOut">The standard output of the process.</param>
        /// <param name="stdErr">The standard error output of the process.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public ExternalToolException(string toolName, string message, int? exitCode, string? stdOut, string? stdErr, Exception innerException)
            : base(message, innerException)
        {
            ToolName = toolName;
            ExitCode = exitCode;
            StandardOutput = stdOut;
            StandardError = stdErr;
        }

        public override string Message
        {
            get
            {
                var msg = base.Message;
                msg += $" (Tool: {ToolName}";
                if (ExitCode.HasValue)
                {
                    msg += $", ExitCode: {ExitCode.Value}";
                }
                if (!string.IsNullOrWhiteSpace(StandardError))
                {
                    msg += $", Stderr: \"{StandardError.Trim().Replace("\n", " ").Replace("\r", "")}\"";
                }
                 else if (!string.IsNullOrWhiteSpace(StandardOutput) && ExitCode.HasValue && ExitCode != 0) // Include StdOut if Stderr is empty but there was an error
                {
                    msg += $", Stdout: \"{StandardOutput.Trim().Replace("\n", " ").Replace("\r", "")}\"";
                }
                msg += ")";
                return msg;
            }
        }
    }
}
