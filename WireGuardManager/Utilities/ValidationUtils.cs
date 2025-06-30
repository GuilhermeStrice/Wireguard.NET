using System;
using System.Net;
using System.Text.RegularExpressions;

namespace WireGuardManager.Utilities
{
    public static class ValidationUtils
    {
        // WireGuard keys are 43 base64 characters followed by an '='.
        private static readonly Regex WireGuardKeyRegex = new Regex(@"^[A-Za-z0-9+/]{43}=$", RegexOptions.Compiled);

        // Basic Linux interface name regex: starts with a letter, can contain letters, numbers, '_', '-'.
        // Length is typically limited (e.g., IFNAMSIZ is 16 in Linux including null terminator).
        // This regex is a bit more lenient on length but captures common patterns.
        // It also prevents it from looking like a file path.
        private static readonly Regex InterfaceNameRegex = new Regex(@"^[a-zA-Z][a-zA-Z0-9_-]{0,14}$", RegexOptions.Compiled);


        /// <summary>
        /// Validates a WireGuard key (public, private, or preshared).
        /// WireGuard keys are 44 characters long, Base64 encoded, ending with '='.
        /// </summary>
        public static bool IsValidWireGuardKey(string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return false;
            return WireGuardKeyRegex.IsMatch(key);
        }

        /// <summary>
        /// Validates if the string is a valid IP address (IPv4 or IPv6).
        /// </summary>
        public static bool IsValidIpAddress(string? ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
                return false;
            return IPAddress.TryParse(ipAddress, out _);
        }

        /// <summary>
        /// Validates if the string is a valid CIDR notation (e.g., "10.0.0.1/24" or "fd00::1/64").
        /// </summary>
        public static bool IsValidCidr(string? cidr)
        {
            if (string.IsNullOrWhiteSpace(cidr))
                return false;

            var parts = cidr.Split('/');
            if (parts.Length != 2)
                return false;

            if (!IsValidIpAddress(parts[0]))
                return false;

            if (!int.TryParse(parts[1], out int prefixLength))
                return false;

            // Determine address family to set appropriate prefix limits
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
            return false; // Should not happen if IsValidIpAddress passed
        }

        /// <summary>
        /// Validates if the string is a valid Linux network interface name.
        /// </summary>
        public static bool IsValidInterfaceName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            // Cannot contain directory separators or look like a file path with .conf
            if (name.Contains(Path.DirectorySeparatorChar) ||
                name.Contains(Path.AltDirectorySeparatorChar) ||
                name.EndsWith(".conf", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            return InterfaceNameRegex.IsMatch(name);
        }

        /// <summary>
        /// Validates an endpoint string (e.g., "hostname_or_ip:port" or "[ipv6_address]:port").
        /// </summary>
        public static bool IsValidEndpoint(string? endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
                return false;

            // Try to parse with Uri, as it handles IPv6 bracket notation and port separation.
            // We need to give it a scheme for Uri.TryCreate to work well.
            if (!endpoint.Contains("://")) // Add a dummy scheme if not present
            {
                endpoint = "wg://" + endpoint;
            }

            if (Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? uri))
            {
                if (string.IsNullOrWhiteSpace(uri.Host)) return false;
                if (uri.Port < 0 || uri.Port > 65535) return false; // Port 0 is valid but often problematic. -1 means no port.

                // Uri.HostNameType can be Dns, IPv4, IPv6. All are acceptable.
                // IPAddress.TryParse can give a more definitive IP validation if HostNameType is Basic (meaning not DNS)
                // but Uri class is generally good enough for endpoint structure.
                return true;
            }
            return false;
        }
    }
}
