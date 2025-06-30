using System;

namespace WireGuardManager.Exceptions
{
    /// <summary>
    /// Exception thrown for errors encountered during the parsing of WireGuard .conf files.
    /// </summary>
    public class WireGuardConfigParseException : WireGuardManagerException
    {
        public int? LineNumber { get; }
        public string? LineContent { get; }

        public WireGuardConfigParseException()
        {
        }

        public WireGuardConfigParseException(string message) : base(message)
        {
        }

        public WireGuardConfigParseException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public WireGuardConfigParseException(string message, int? lineNumber, string? lineContent) : base(message)
        {
            LineNumber = lineNumber;
            LineContent = lineContent;
        }

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
