using System;

namespace WireGuardManager.Exceptions
{
    /// <summary>
    /// Base exception for errors related to the execution of external command-line tools (wg, wg-quick, systemctl).
    /// </summary>
    public class ExternalToolException : WireGuardManagerException
    {
        public string ToolName { get; }
        public int? ExitCode { get; }
        public string? StandardOutput { get; }
        public string? StandardError { get; }

        public ExternalToolException(string toolName, string message)
            : base(message)
        {
            ToolName = toolName;
        }

        public ExternalToolException(string toolName, string message, Exception innerException)
            : base(message, innerException)
        {
            ToolName = toolName;
        }

        public ExternalToolException(string toolName, string message, int? exitCode, string? stdOut, string? stdErr)
            : base(message)
        {
            ToolName = toolName;
            ExitCode = exitCode;
            StandardOutput = stdOut;
            StandardError = stdErr;
        }

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
