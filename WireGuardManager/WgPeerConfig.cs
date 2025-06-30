using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WireGuardManager.Exceptions;
using WireGuardManager.Utilities;

namespace WireGuardManager
{
    public class WgPeerConfig
    {
        private string _publicKey;
        public string PublicKey
        {
            get => _publicKey;
            set
            {
                if (!ValidationUtils.IsValidWireGuardKey(value))
                    throw new InvalidInputException("Invalid PublicKey format.", nameof(PublicKey));
                _publicKey = value;
            }
        }

        private string? _presharedKey;
        public string? PresharedKey
        {
            get => _presharedKey;
            set
            {
                if (!string.IsNullOrEmpty(value) && !ValidationUtils.IsValidWireGuardKey(value))
                    throw new InvalidInputException("Invalid PresharedKey format.", nameof(PresharedKey));
                _presharedKey = value;
            }
        }

        private List<string> _allowedIPs = new List<string>();
        public List<string> AllowedIPs
        {
            get => _allowedIPs;
            set
            {
                if (value == null || value.Any(a => !ValidationUtils.IsValidCidr(a)))
                    throw new InvalidInputException("One or more AllowedIPs entries are not valid CIDR notation.", nameof(AllowedIPs));
                _allowedIPs = value;
            }
        }

        private string? _endpoint;
        public string? Endpoint
        {
            get => _endpoint;
            set
            {
                if (!string.IsNullOrEmpty(value) && !ValidationUtils.IsValidEndpoint(value))
                    throw new InvalidInputException("Invalid Endpoint format. Expected 'host:port' or '[ipv6]:port'.", nameof(Endpoint));
                _endpoint = value;
            }
        }

        private int? _persistentKeepalive;
        public int? PersistentKeepalive
        {
            get => _persistentKeepalive;
            set
            {
                if (value.HasValue && (value < 0 || value > 65535)) // 0 disables it, common range for actual values might be 1-65535
                    throw new InvalidInputException("PersistentKeepalive must be between 0 and 65535.", nameof(PersistentKeepalive));
                _persistentKeepalive = value;
            }
        }


        public WgPeerConfig(string publicKey)
        {
            if (string.IsNullOrWhiteSpace(publicKey)) // Basic check
                throw new InvalidInputException("PublicKey cannot be null or whitespace.", nameof(publicKey));
            if (!ValidationUtils.IsValidWireGuardKey(publicKey))
                throw new InvalidInputException("Invalid PublicKey format.", nameof(publicKey));
            _publicKey = publicKey;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[Peer]");
            sb.AppendLine($"PublicKey = {PublicKey}"); // Uses validated property

            if (!string.IsNullOrWhiteSpace(PresharedKey)) // Uses validated property
            {
                sb.AppendLine($"PresharedKey = {PresharedKey}");
            }

            if (AllowedIPs.Any()) // Uses validated property
            {
                sb.AppendLine($"AllowedIPs = {string.Join(", ", AllowedIPs)}");
            }

            if (!string.IsNullOrWhiteSpace(Endpoint)) // Uses validated property
            {
                sb.AppendLine($"Endpoint = {Endpoint}");
            }

            if (PersistentKeepalive.HasValue) // Uses validated property
            {
                sb.AppendLine($"PersistentKeepalive = {PersistentKeepalive.Value}");
            }

            return sb.ToString();
        }
    }
}
