using System;

namespace WireGuardManager.Exceptions
{
    /// <summary>
    /// Exception thrown when invalid input parameters are provided to library methods.
    /// </summary>
    public class InvalidInputException : WireGuardManagerException
    {
        public string? ParameterName { get; }

        public InvalidInputException()
        {
        }

        public InvalidInputException(string message) : base(message)
        {
        }

        public InvalidInputException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public InvalidInputException(string message, string? parameterName) : base(message)
        {
            ParameterName = parameterName;
        }

        public InvalidInputException(string message, string? parameterName, Exception innerException) : base(message, innerException)
        {
            ParameterName = parameterName;
        }

        // Override Message to include ParameterName if available
        public override string Message
        {
            get
            {
                if (!string.IsNullOrEmpty(ParameterName))
                {
                    return $"{base.Message} (Parameter: {ParameterName})";
                }
                return base.Message;
            }
        }
    }
}
