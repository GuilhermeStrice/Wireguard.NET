using System;

namespace WireGuardManager.Exceptions
{
    /// <summary>
    /// Exception thrown when a method is called with an invalid or inappropriate argument value.
    /// This can include issues like incorrect formatting for keys, IP addresses, or other specific input constraints.
    /// </summary>
    public class InvalidInputException : WireGuardManagerException
    {
        /// <summary>
        /// Gets the name of the parameter that caused the exception, if available.
        /// </summary>
        public string? ParameterName { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidInputException"/> class.
        /// </summary>
        public InvalidInputException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidInputException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public InvalidInputException(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidInputException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
        public InvalidInputException(string message, Exception innerException) : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidInputException"/> class with a specified error message and the name of the parameter that causes this exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="parameterName">The name of the parameter that caused the current exception.</param>
        public InvalidInputException(string message, string? parameterName) : base(message)
        {
            ParameterName = parameterName;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidInputException"/> class with a specified error message, the parameter name, and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="parameterName">The name of the parameter that caused the current exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
        public InvalidInputException(string message, string? parameterName, Exception innerException) : base(message, innerException)
        {
            ParameterName = parameterName;
        }

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
