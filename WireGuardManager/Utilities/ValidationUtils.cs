using System;
using System.Net;
using System.Text.RegularExpressions;

using System.IO; // For Path related constants
using System.Net;
using System.Text.RegularExpressions;

namespace WireGuardManager.Utilities
{
    /// <summary>
    /// Provides static utility methods for validating various input formats relevant to WireGuard configurations.
    /// </summary>
    public static class ValidationUtils
    {
        // WireGuard keys are 44 characters long, Base64 encoded, ending with '='.
        // Regex: ^[A-Za-z0-9+/]{43}=$
        private static readonly Regex WireGuardKeyRegex = new Regex(@"^[A-Za-z0-9+/]{43}=$", RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        // Basic Linux interface name regex: starts with a letter or number, can contain letters, numbers, '_', '-'.
        // Max length IFNAMSIZ (16 including null) is common. This regex is a bit more general on start char.
        // Crucially, it must not contain path separators or ".conf" to distinguish from file paths.
        private static readonly Regex InterfaceNameRegex = new Regex(@"^[a-zA-Z0-9][a-zA-Z0-9_-]{0,14}$", RegexOptions.Compiled | RegexOptions.ExplicitCapture);


        /// <summary>
        /// Validates if the provided string is a correctly formatted WireGuard key (public, private, or preshared).
        /// WireGuard keys must be 44 characters long, Base64 encoded, and end with an '=' character.
        /// </summary>
        /// <param name="key">The key string to validate. Can be null or empty.</param>
        /// <returns>true if the key is valid; otherwise, false.</returns>
        public static bool IsValidWireGuardKey(string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return false;
            return WireGuardKeyRegex.IsMatch(key);
        }

        /// <summary>
        /// Validates if the provided string is a valid IP address (either IPv4 or IPv6).
        /// </summary>
        /// <param name="ipAddress">The IP address string to validate. Can be null or empty.</param>
        /// <returns>true if the string represents a valid IP address; otherwise, false.</returns>
        public static bool IsValidIpAddress(string? ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
                return false;
            return IPAddress.TryParse(ipAddress, out _);
        }

        /// <summary>
        /// Validates if the provided string is a valid CIDR (Classless Inter-Domain Routing) notation
        /// (e.g., "10.0.0.1/24" or "fd00::1/64").
        /// </summary>
        /// <param name="cidr">The CIDR string to validate. Can be null or empty.</param>
        /// <returns>true if the string represents a valid CIDR notation; otherwise, false.</returns>
        public static bool IsValidCidr(string? cidr)
        {
            if (string.IsNullOrWhiteSpace(cidr))
                return false;

            var parts = cidr.Split('/');
            if (parts.Length != 2)
                return false;

            if (!IsValidIpAddress(parts[0])) // Validate the IP address part
                return false;

            if (!int.TryParse(parts[1], out int prefixLength)) // Validate the prefix length part
                return false;

            // Check prefix length based on IP address family
            if (IPAddress.TryParse(parts[0], out IPAddress? ipAddrObj))
            {
                if (ipAddrObj.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) // IPv4
                {
                    return prefixLength >= 0 && prefixLength <= 32;
                }
                else if (ipAddrObj.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6) // IPv6
                {
                    return prefixLength >= 0 && prefixLength <= 128;
                }
            }
            return false; // Should be unreachable if IsValidIpAddress passed and family was determined
        }

        /// <summary>
        /// Validates if the provided string is a plausible Linux network interface name.
        /// It checks for common character constraints and length, and ensures it doesn't resemble a file path.
        /// </summary>
        /// <param name="name">The interface name string to validate. Can be null or empty.</param>
        /// <returns>true if the string is a valid interface name; otherwise, false.</returns>
        public static bool IsValidInterfaceName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            // Must not contain directory separators or end with typical config file extension
            if (name.Contains(Path.DirectorySeparatorChar) ||
                name.Contains(Path.AltDirectorySeparatorChar) ||
                name.EndsWith(".conf", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            return InterfaceNameRegex.IsMatch(name);
        }

        /// <summary>
        /// Validates if the provided string is a valid network endpoint, typically in the format "host:port" or "[IPv6Address]:port".
        /// Host can be a hostname, IPv4 address, or IPv6 address. Port must be a valid port number.
        /// </summary>
        /// <param name="endpoint">The endpoint string to validate. Can be null or empty.</param>
        /// <returns>true if the string represents a valid endpoint; otherwise, false.</returns>
        public static bool IsValidEndpoint(string? endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
                return false;

            // Uri.TryCreate is robust for various host/port combinations including IPv6 with brackets.
            // We need to provide a scheme for it to parse correctly.
            string prefixedEndpoint = endpoint.Contains("://") ? endpoint : "wg://" + endpoint;

            if (Uri.TryCreate(prefixedEndpoint, UriKind.Absolute, out Uri? uri))
            {
                // Host must not be empty, and port must be in valid range.
                // Uri.Port returns -1 if no port is specified, which is invalid for WireGuard endpoints.
                if (string.IsNullOrWhiteSpace(uri.Host) || uri.Port == -1) return false;
                return uri.Port >= 0 && uri.Port <= 65535;
            }
            return false;
        }
    }
}
