using System.Collections.Generic;

namespace WireGuardManager
{
    /// <summary>
    /// Represents information about a WireGuard interface, typically parsed from 'wg show dump' output.
    /// </summary>
    public class WgInterfaceInfo
    {
        /// <summary>
        /// Gets or sets the name of the interface.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Base64 encoded public key of the interface.
        /// </summary>
        public string PublicKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Base64 encoded private key of the interface.
        /// Note: 'wg show dump' does not expose the private key. This field might be populated from other sources.
        /// </summary>
        public string? PrivateKey { get; set; }

        /// <summary>
        /// Gets or sets the listening port of the interface.
        /// </summary>
        public int? ListenPort { get; set; }

        /// <summary>
        /// Gets or sets the firewall mark for the interface.
        /// </summary>
        public string? FwMark { get; set; } // Can be "off" or a hex number

        /// <summary>
        /// Gets or sets the list of peers associated with this interface.
        /// </summary>
        public List<WgPeerInfo> Peers { get; set; } = new List<WgPeerInfo>();
    }
}
