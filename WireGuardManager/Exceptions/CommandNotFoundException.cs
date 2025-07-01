using System;

namespace WireGuardManager.Exceptions
{
    /// <summary>
    /// Exception thrown when an external command-line tool (e.g., wg, wg-quick, systemctl) cannot be found during execution.
    /// This typically occurs if the tool is not installed or not available in the system's PATH.
    /// </summary>
    public class CommandNotFoundException : ExternalToolException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CommandNotFoundException"/> class.
        /// </summary>
        /// <param name="toolName">The name of the tool that was not found.</param>
        public CommandNotFoundException(string toolName)
            : base(toolName, $"The command-line tool '{toolName}' was not found. Please ensure it is installed and in the system PATH.")
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandNotFoundException"/> class with a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="toolName">The name of the tool that was not found.</param>
        /// <param name="innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
        public CommandNotFoundException(string toolName, Exception innerException)
            : base(toolName, $"The command-line tool '{toolName}' was not found. Please ensure it is installed and in the system PATH.", innerException)
        {
        }
    }
}
