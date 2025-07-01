using System.Collections.Generic;

namespace WireGuardManager
{
    /// <summary>
    /// Defines the set of properties that can be updated for a WireGuard peer
    /// using the 'wg set' command functionality.
    /// Null properties are ignored during an update; set a property to a new value to change it.
    /// Special values like "off" for PresharedKey or 0 for PersistentKeepalive might be needed for removal/disabling,
    /// matching 'wg set' behavior. The 'Remove' flag takes precedence.
    /// </summary>
    public class WgPeerUpdateOptions
    {
        /// <summary>
        /// Gets or sets the list of allowed IPs for the peer.
        /// If set, this list will replace all existing AllowedIPs for the peer.
        /// An empty list might be used to clear AllowedIPs, depending on 'wg set' behavior.
        /// </summary>
        public List<string>? AllowedIPs { get; set; } = null;

        /// <summary>
        /// Gets or sets the endpoint for the peer (e.g., "host:port" or "[ipv6]:port").
        /// If set, replaces the existing endpoint.
        /// </summary>
        public string? Endpoint { get; set; } = null;

        /// <summary>
        /// Gets or sets the preshared key for the peer.
        /// If set, replaces the existing preshared key.
        /// To remove a preshared key, use the specific value "off".
        /// </summary>
        public string? PresharedKey { get; set; } = null;

        /// <summary>
        /// Gets or sets the persistent keepalive interval in seconds.
        /// If set, replaces the existing value. A value of 0 or "off" typically disables it.
        /// </summary>
        public int? PersistentKeepalive { get; set; } = null; // 'wg set' also accepts "off"

        /// <summary>
        /// Gets or sets a value indicating whether the peer should be removed entirely.
        /// If true, all other options are ignored, and the peer is removed from the interface.
        /// </summary>
        public bool? Remove { get; set; } = null;
    }
}
