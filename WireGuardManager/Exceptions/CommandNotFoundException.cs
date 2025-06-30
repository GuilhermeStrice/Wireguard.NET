using System;

namespace WireGuardManager.Exceptions
{
    /// <summary>
    /// Exception thrown when an external command-line tool (e.g., wg, wg-quick, systemctl) cannot be found.
    /// </summary>
    public class CommandNotFoundException : ExternalToolException
    {
        public CommandNotFoundException(string toolName)
            : base(toolName, $"The command-line tool '{toolName}' was not found. Please ensure it is installed and in the system PATH.")
        {
        }

        public CommandNotFoundException(string toolName, Exception innerException)
            : base(toolName, $"The command-line tool '{toolName}' was not found. Please ensure it is installed and in the system PATH.", innerException)
        {
        }
    }
}
