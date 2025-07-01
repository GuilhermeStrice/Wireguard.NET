using System;

namespace WireGuardManager.Exceptions
{
    /// <summary>
    /// Represents the base class for custom exceptions thrown by the WireGuardManager library.
    /// This allows consumers to catch all library-specific exceptions with a single catch block if desired.
    /// </summary>
    public class WireGuardManagerException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WireGuardManagerException"/> class.
        /// </summary>
        public WireGuardManagerException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WireGuardManagerException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public WireGuardManagerException(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WireGuardManagerException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
        public WireGuardManagerException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
