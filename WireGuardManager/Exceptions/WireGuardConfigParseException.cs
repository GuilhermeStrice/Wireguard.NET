using System;

namespace WireGuardManager.Exceptions
{
    /// <summary>
    /// Exception thrown when errors are encountered during the parsing of WireGuard .conf configuration files or strings.
    /// Provides context like line number and content where the parsing error occurred, if available.
    /// </summary>
    public class WireGuardConfigParseException : WireGuardManagerException
    {
        /// <summary>
        /// Gets the line number in the configuration where the parsing error occurred, if applicable.
        /// </summary>
        public int? LineNumber { get; }

        /// <summary>
        /// Gets the content of the line that caused the parsing error, if applicable.
        /// </summary>
        public string? LineContent { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="WireGuardConfigParseException"/> class.
        /// </summary>
        public WireGuardConfigParseException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WireGuardConfigParseException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public WireGuardConfigParseException(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WireGuardConfigParseException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
        public WireGuardConfigParseException(string message, Exception innerException) : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WireGuardConfigParseException"/> class with a specified error message, line number, and line content.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="lineNumber">The line number where the error occurred.</param>
        /// <param name="lineContent">The content of the line that caused the error.</param>
        public WireGuardConfigParseException(string message, int? lineNumber, string? lineContent) : base(message)
        {
            LineNumber = lineNumber;
            LineContent = lineContent;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WireGuardConfigParseException"/> class with a specified error message, line number, line content, and a reference to the inner exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="lineNumber">The line number where the error occurred.</param>
        /// <param name="lineContent">The content of the line that caused the error.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public WireGuardConfigParseException(string message, int? lineNumber, string? lineContent, Exception innerException) : base(message, innerException)
        {
            LineNumber = lineNumber;
            LineContent = lineContent;
        }

        public override string Message
        {
            get
            {
                var baseMessage = base.Message;
                if (LineNumber.HasValue)
                {
                    baseMessage += $" (Line: {LineNumber.Value})";
                }
                if (!string.IsNullOrWhiteSpace(LineContent))
                {
                    baseMessage += $" (Content: \"{LineContent}\")";
                }
                return baseMessage;
            }
        }
    }
}
