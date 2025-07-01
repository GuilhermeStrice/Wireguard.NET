using System.Collections.Generic;

namespace WireGuardManager
{
    /// <summary>
    /// Defines the set of properties that can be updated for a WireGuard peer
    /// using the 'wg set' command functionality.
    /// Null properties are ignored during an update; set a property to a new value to change it.
    /// Special values like "off" for PresharedKey or 0 for PersistentKeepalive might be needed for removal/disabling,
    /// matching 'wg set' behavior. The 'Remove' flag takes precedence.
    /// If PresharedKeyFile is set, it takes precedence over PresharedKey string.
    /// </summary>
    public class WgPeerUpdateOptions
    {
        /// <summary>
        /// Gets or sets the list of allowed IPs for the peer.
        /// If set, this list will replace all existing AllowedIPs for the peer.
        /// </summary>
        public List<string>? AllowedIPs { get; set; } = null;

        /// <summary>
        /// Gets or sets the endpoint for the peer (e.g., "host:port" or "[ipv6]:port").
        /// If set, replaces the existing endpoint.
        /// </summary>
        public string? Endpoint { get; set; } = null;

        /// <summary>
        /// Gets or sets the preshared key for the peer as a Base64 string.
        /// If set (and <see cref="PresharedKeyFile"/> is null), this key will be used.
        /// To remove an existing preshared key, set this property to the literal string "off".
        /// This property is ignored if <see cref="PresharedKeyFile"/> is set.
        /// </summary>
        public string? PresharedKey { get; set; } = null;

        /// <summary>
        /// Gets or sets the path to a file containing the preshared key for the peer.
        /// If set, this file's content will be used as the preshared key, taking precedence over the <see cref="PresharedKey"/> string property.
        /// The file should contain the Base64 encoded preshared key.
        /// </summary>
        public string? PresharedKeyFile { get; set; } = null;

        /// <summary>
        /// Gets or sets the persistent keepalive interval in seconds.
        /// If set, replaces the existing value. A value of 0 disables the persistent keepalive.
        /// </summary>
        public int? PersistentKeepalive { get; set; } = null;

        /// <summary>
        /// Gets or sets a value indicating whether the peer should be removed entirely.
        /// If true, all other options are ignored, and the peer is removed from the interface.
        /// </summary>
        public bool? Remove { get; set; } = null;
    }
}
