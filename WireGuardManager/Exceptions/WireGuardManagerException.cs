using System;

namespace WireGuardManager.Exceptions
{
    /// <summary>
    /// Base exception for all custom exceptions thrown by the WireGuardManager library.
    /// </summary>
    public class WireGuardManagerException : Exception
    {
        public WireGuardManagerException()
        {
        }

        public WireGuardManagerException(string message) : base(message)
        {
        }

        public WireGuardManagerException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
