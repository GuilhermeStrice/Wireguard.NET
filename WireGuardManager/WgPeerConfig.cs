using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WireGuardManager
{
    public class WgPeerConfig
    {
        public string PublicKey { get; set; }
        public string PresharedKey { get; set; } // Optional
        public List<string> AllowedIPs { get; set; } = new List<string>();
        public string Endpoint { get; set; } // Optional
        public int? PersistentKeepalive { get; set; } // Optional

        public WgPeerConfig(string publicKey)
        {
            if (string.IsNullOrWhiteSpace(publicKey))
                throw new ArgumentException("PublicKey cannot be null or whitespace.", nameof(publicKey));
            PublicKey = publicKey;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[Peer]");
            sb.AppendLine($"PublicKey = {PublicKey}");

            if (!string.IsNullOrWhiteSpace(PresharedKey))
            {
                sb.AppendLine($"PresharedKey = {PresharedKey}");
            }

            if (AllowedIPs.Any())
            {
                sb.AppendLine($"AllowedIPs = {string.Join(", ", AllowedIPs)}");
            }

            if (!string.IsNullOrWhiteSpace(Endpoint))
            {
                sb.AppendLine($"Endpoint = {Endpoint}");
            }

            if (PersistentKeepalive.HasValue)
            {
                sb.AppendLine($"PersistentKeepalive = {PersistentKeepalive.Value}");
            }

            return sb.ToString();
        }
    }
}
