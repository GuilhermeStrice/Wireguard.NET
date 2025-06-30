using System;

namespace WireGuardManager.Exceptions
{
    /// <summary>
    /// Exception thrown when an operation fails due to insufficient permissions.
    /// This can occur during file operations (like writing service files) or when executing external tools.
    /// </summary>
    public class PermissionsException : WireGuardManagerException // Or could derive from ExternalToolException if primarily from tools
    {
        public string? Operation { get; }
        public string? Resource { get; }

        public PermissionsException(string message) : base(message)
        {
        }

        public PermissionsException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public PermissionsException(string operation, string? resource)
            : base($"Permission denied while attempting to {operation}{(string.IsNullOrWhiteSpace(resource) ? "" : $" resource '{resource}'")}. Ensure the application has sufficient privileges (e.g., root).")
        {
            Operation = operation;
            Resource = resource;
        }

        public PermissionsException(string operation, string? resource, Exception innerException)
            : base($"Permission denied while attempting to {operation}{(string.IsNullOrWhiteSpace(resource) ? "" : $" resource '{resource}'")}. Ensure the application has sufficient privileges (e.g., root).", innerException)
        {
            Operation = operation;
            Resource = resource;
        }
    }
}
