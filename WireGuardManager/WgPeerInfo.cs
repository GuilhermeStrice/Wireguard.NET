using System;
using System.Collections.Generic;

namespace WireGuardManager
{
    /// <summary>
    /// Represents information about a WireGuard peer, typically parsed from 'wg show dump' output.
    /// </summary>
    public class WgPeerInfo
    {
        /// <summary>
        /// Gets or sets the Base64 encoded public key of the peer.
        /// </summary>
        public string PublicKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether a preshared key is configured for this peer.
        /// 'wg show dump' indicates this with "(有)" or "(none)".
        /// </summary>
        public bool PresharedKeyExists { get; set; }

        /// <summary>
        /// Gets or sets the endpoint for the peer (IPAddress:Port).
        /// </summary>
        public string? Endpoint { get; set; } // e.g., "1.2.3.4:56789"

        /// <summary>
        /// Gets or sets the list of allowed IPs for this peer.
        /// </summary>
        public List<string> AllowedIPs { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the time of the latest handshake with this peer.
        /// Null if no handshake has occurred.
        /// </summary>
        public DateTimeOffset? LatestHandshake { get; set; } // Unix timestamp in 'wg show dump'

        /// <summary>
        /// Gets or sets the number of bytes received from this peer.
        /// </summary>
        public long TransferRxBytes { get; set; }

        /// <summary>
        /// Gets or sets the number of bytes transmitted to this peer.
        /// </summary>
        public long TransferTxBytes { get; set; }

        /// <summary>
        /// Gets or sets the persistent keepalive interval configured for this peer, in seconds.
        /// Null if not set (or "off").
        /// </summary>
        public int? PersistentKeepaliveIntervalSeconds { get; set; } // "off" or number
    }
}
