using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WireGuardManager
{
    public class WgServerConfig
    {
        public string PrivateKey { get; set; }
        public List<string> Address { get; set; } = new List<string>();
        public int? ListenPort { get; set; }
        public List<string> Dns { get; set; } = new List<string>();
        public List<string> PostUp { get; set; } = new List<string>();
        public List<string> PostDown { get; set; } = new List<string>();
        // SaveConfig is typically not part of the [Interface] section for wg syncconf,
        // but wg-quick uses it. We can include it for broader compatibility if needed,
        // or decide later if it should be exclusively handled by a wg-quick wrapper.
        // For now, let's assume it might be useful for some scenarios.
        public bool? SaveConfig { get; set; }
        public int? Mtu { get; set; } // MTU is another common Interface option

        public WgServerConfig(string privateKey)
        {
            if (string.IsNullOrWhiteSpace(privateKey))
                throw new ArgumentException("PrivateKey cannot be null or whitespace.", nameof(privateKey));
            PrivateKey = privateKey;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[Interface]");
            sb.AppendLine($"PrivateKey = {PrivateKey}");

            if (Address.Any())
            {
                sb.AppendLine($"Address = {string.Join(", ", Address)}");
            }

            if (ListenPort.HasValue)
            {
                sb.AppendLine($"ListenPort = {ListenPort.Value}");
            }

            if (Dns.Any())
            {
                sb.AppendLine($"DNS = {string.Join(", ", Dns)}");
            }

            if (Mtu.HasValue)
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

            if (SaveConfig.HasValue) // wg-quick specific
            {
                sb.AppendLine($"SaveConfig = {SaveConfig.Value.ToString().ToLower()}");
            }

            return sb.ToString();
        }
    }
}
