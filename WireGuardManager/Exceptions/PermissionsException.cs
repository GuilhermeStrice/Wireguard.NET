using System;

namespace WireGuardManager.Exceptions
{
    /// <summary>
    /// Exception thrown when an operation fails due to insufficient operating system permissions.
    /// This can occur during file system operations (e.g., writing service files to restricted directories)
    /// or when executing external tools that require elevated privileges (e.g., root or administrator).
    /// </summary>
    public class PermissionsException : WireGuardManagerException
    {
        /// <summary>
        /// Gets the operation that was being attempted when the permission error occurred.
        /// </summary>
        public string? Operation { get; }

        /// <summary>
        /// Gets the resource (e.g., file path, command) that was being accessed or executed.
        /// </summary>
        public string? Resource { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PermissionsException"/> class with a general message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public PermissionsException(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PermissionsException"/> class with a general message and a reference to the inner exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public PermissionsException(string message, Exception innerException) : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PermissionsException"/> class with a specific operation and resource.
        /// </summary>
        /// <param name="operation">The operation that was being attempted (e.g., "write file", "execute command").</param>
        /// <param name="resource">The resource involved (e.g., file path, command name).</param>
        public PermissionsException(string operation, string? resource)
            : base($"Permission denied while attempting to {operation}{(string.IsNullOrWhiteSpace(resource) ? "" : $" resource '{resource}'")}. Ensure the application has sufficient privileges (e.g., root).")
        {
            Operation = operation;
            Resource = resource;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PermissionsException"/> class with a specific operation, resource, and a reference to the inner exception.
        /// </summary>
        /// <param name="operation">The operation that was being attempted.</param>
        /// <param name="resource">The resource involved.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public PermissionsException(string operation, string? resource, Exception innerException)
            : base($"Permission denied while attempting to {operation}{(string.IsNullOrWhiteSpace(resource) ? "" : $" resource '{resource}'")}. Ensure the application has sufficient privileges (e.g., root).", innerException)
        {
            Operation = operation;
            Resource = resource;
        }
    }
}
