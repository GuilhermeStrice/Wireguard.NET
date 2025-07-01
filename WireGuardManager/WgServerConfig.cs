using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WireGuardManager.Exceptions;
using WireGuardManager.Utilities;

namespace WireGuardManager
{
    /// <summary>
    /// Represents the [Interface] section of a WireGuard configuration file.
    /// </summary>
    public class WgServerConfig
    {
        private string _privateKey;
        public string PrivateKey
        {
            get => _privateKey;
            set
            {
                if (!ValidationUtils.IsValidWireGuardKey(value))
                    throw new InvalidInputException("Invalid PrivateKey format.", nameof(PrivateKey));
                _privateKey = value;
            }
        }

        private List<string> _address = new List<string>();
        public List<string> Address
        {
            get => _address;
            set
            {
                if (value == null || value.Any(a => !ValidationUtils.IsValidCidr(a)))
                    throw new InvalidInputException("One or more Address entries are not valid CIDR notation.", nameof(Address));
                _address = value;
            }
        }

        private int? _listenPort;
        public int? ListenPort
        {
            get => _listenPort;
            set
            {
                if (value.HasValue && (value < 0 || value > 65535))
                    throw new InvalidInputException("ListenPort must be between 0 and 65535.", nameof(ListenPort));
                _listenPort = value;
            }
        }

        private List<string> _dns = new List<string>();
        public List<string> Dns
        {
            get => _dns;
            set
            {
                if (value == null || value.Any(d => !ValidationUtils.IsValidIpAddress(d) && !ValidationUtils.IsValidCidr(d))) // DNS can be IP or IP/prefix
                    throw new InvalidInputException("One or more DNS entries are not valid IP addresses or CIDR.", nameof(Dns));
                _dns = value;
            }
        }

        public List<string> PostUp { get; set; } = new List<string>();
        public List<string> PostDown { get; set; } = new List<string>();

        public bool? SaveConfig { get; set; }

        private int? _mtu;
        public int? Mtu
        {
            get => _mtu;
            set
            {
                if (value.HasValue && value < 576) // Minimum MTU typically 576 for IPv4, higher for IPv6. WireGuard itself often uses 1280-1420.
                    throw new InvalidInputException("MTU value is too low (should typically be >= 576, often 1280-1500).", nameof(Mtu));
                _mtu = value;
            }
        }

        public WgServerConfig(string privateKey)
        {
            if (string.IsNullOrWhiteSpace(privateKey)) // Basic check before specific format validation
                throw new InvalidInputException("PrivateKey cannot be null or whitespace.", nameof(privateKey));
            if (!ValidationUtils.IsValidWireGuardKey(privateKey))
                throw new InvalidInputException("Invalid PrivateKey format.", nameof(privateKey));
            _privateKey = privateKey;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[Interface]");
            sb.AppendLine($"PrivateKey = {PrivateKey}"); // Uses validated property

            if (Address.Any()) // Uses validated property
            {
                sb.AppendLine($"Address = {string.Join(", ", Address)}");
            }

            if (ListenPort.HasValue) // Uses validated property
            {
                sb.AppendLine($"ListenPort = {ListenPort.Value}");
            }

            if (Dns.Any()) // Uses validated property
            {
                sb.AppendLine($"DNS = {string.Join(", ", Dns)}");
            }

            if (Mtu.HasValue) // Uses validated property
            {
                sb.AppendLine($"MTU = {Mtu.Value}");
            }

            foreach (var cmd in PostUp)
            {
                sb.AppendLine($"PostUp = {cmd}");
            }

            foreach (var cmd in PostDown)
            {
                sb.AppendLine($"PostDown = {cmd}");
            }

            if (SaveConfig.HasValue)
            {
                sb.AppendLine($"SaveConfig = {SaveConfig.Value.ToString().ToLower()}");
            }

            return sb.ToString();
        }
    }
}
