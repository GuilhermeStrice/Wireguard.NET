using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace WireGuardManager
{
    public class WgConfig
    {
        public WgServerConfig Interface { get; set; }
        public List<WgPeerConfig> Peers { get; set; } = new List<WgPeerConfig>();

        public WgConfig(WgServerConfig interfaceConfig)
        {
            Interface = interfaceConfig ?? throw new ArgumentNullException(nameof(interfaceConfig));
        }

        public void AddPeer(WgPeerConfig peer)
        {
            if (peer == null) throw new ArgumentNullException(nameof(peer));
            if (Peers.Any(p => p.PublicKey == peer.PublicKey))
            {
                // Or throw an exception, or update existing
                Console.WriteLine($"Warning: Peer with PublicKey {peer.PublicKey} already exists. Consider updating instead of adding.");
                return;
            }
            Peers.Add(peer);
        }

        public bool RemovePeer(string publicKey)
        {
            var peerToRemove = Peers.FirstOrDefault(p => p.PublicKey == publicKey);
            if (peerToRemove != null)
            {
                return Peers.Remove(peerToRemove);
            }
            return false;
        }

        public WgPeerConfig GetPeer(string publicKey)
        {
            return Peers.FirstOrDefault(p => p.PublicKey == publicKey);
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(Interface.ToString());
            sb.AppendLine(); // Ensure a blank line after [Interface] section

            foreach (var peer in Peers)
            {
                sb.Append(peer.ToString());
                sb.AppendLine(); // Ensure a blank line after each [Peer] section
            }
            return sb.ToString().TrimEnd(); // Trim trailing newlines
        }

        public void ToFile(string filePath)
        {
            File.WriteAllText(filePath, ToString());
        }

        public static WgConfig Parse(string configString)
        {
            if (string.IsNullOrWhiteSpace(configString))
                throw new ArgumentException("Config string cannot be null or empty.", nameof(configString));

            var lines = configString.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                    .Select(line => line.Trim())
                                    .Where(line => !string.IsNullOrEmpty(line) && !line.StartsWith("#")) // Ignore comments and empty lines
                                    .ToList();

            WgServerConfig serverConfig = null;
            List<WgPeerConfig> peers = new List<WgPeerConfig>();
            WgPeerConfig currentPeer = null;

            // First pass to find [Interface] PrivateKey, as it's mandatory for WgServerConfig constructor
            string serverPrivateKey = null;
            bool inInterfaceSection = false;
            foreach (var line in lines)
            {
                if (line.Equals("[Interface]", StringComparison.OrdinalIgnoreCase))
                {
                    inInterfaceSection = true;
                    continue;
                }
                if (line.Equals("[Peer]", StringComparison.OrdinalIgnoreCase))
                {
                    inInterfaceSection = false; // Moved to a peer section
                    continue;
                }

                if (inInterfaceSection)
                {
                    var parts = line.Split(new[] { '=' }, 2).Select(p => p.Trim()).ToArray();
                    if (parts.Length == 2 && parts[0].Equals("PrivateKey", StringComparison.OrdinalIgnoreCase))
                    {
                        serverPrivateKey = parts[1];
                        break;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(serverPrivateKey))
            {
                throw new FormatException("PrivateKey not found in [Interface] section or [Interface] section missing.");
            }
            serverConfig = new WgServerConfig(serverPrivateKey);


            inInterfaceSection = false; // Reset for second pass
            foreach (var line in lines)
            {
                if (line.Equals("[Interface]", StringComparison.OrdinalIgnoreCase))
                {
                    inInterfaceSection = true;
                    if (currentPeer != null) // Finalize previous peer if any
                    {
                        peers.Add(currentPeer);
                        currentPeer = null;
                    }
                    continue;
                }

                if (line.Equals("[Peer]", StringComparison.OrdinalIgnoreCase))
                {
                    inInterfaceSection = false;
                    if (currentPeer != null) // Add the previously parsed peer before starting a new one
                    {
                        peers.Add(currentPeer);
                    }
                    // We need PublicKey for constructor, but it might not be the first line.
                    // We'll create a temporary peer and fill its PublicKey later.
                    // This is tricky. Let's find PublicKey first for the current peer block.
                    // This parsing logic needs to be more robust.

                    // Simplified approach: Assume PublicKey is the first key after [Peer] or parse the whole block.
                    // For now, we'll assume PublicKey is present.
                    // A better parser would collect all key-values for a section then build the object.
                    currentPeer = null; // Will be created when PublicKey is found
                    continue;
                }

                var kvp = line.Split(new[] { '=' }, 2);
                if (kvp.Length != 2) continue; // Skip malformed lines

                string key = kvp[0].Trim();
                string value = kvp[1].Trim();

                if (inInterfaceSection && serverConfig != null)
                {
                    ParseInterfaceLine(serverConfig, key, value);
                }
                else if (!inInterfaceSection)
                {
                    if (key.Equals("PublicKey", StringComparison.OrdinalIgnoreCase))
                    {
                        if (currentPeer != null && currentPeer.PublicKey != value) // Starting a new peer implicitly by new PublicKey
                        {
                            peers.Add(currentPeer);
                        }
                        // Only create a new peer if the public key is different or if currentPeer is null
                        if (currentPeer == null || currentPeer.PublicKey != value)
                        {
                             currentPeer = new WgPeerConfig(value);
                        }
                    }
                    else if (currentPeer != null) // Ensure currentPeer is initialized (PublicKey was found)
                    {
                        ParsePeerLine(currentPeer, key, value);
                    }
                }
            }

            if (currentPeer != null) // Add the last peer
            {
                peers.Add(currentPeer);
            }

            if (serverConfig == null) // Should have been created if PrivateKey was found
            {
                throw new FormatException("[Interface] section not found or is incomplete.");
            }

            var config = new WgConfig(serverConfig);
            config.Peers = peers;
            return config;
        }

        private static void ParseInterfaceLine(WgServerConfig serverConfig, string key, string value)
        {
            // PrivateKey is handled by constructor
            if (key.Equals("Address", StringComparison.OrdinalIgnoreCase))
                serverConfig.Address.AddRange(value.Split(',').Select(s => s.Trim()));
            else if (key.Equals("ListenPort", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out int port))
                serverConfig.ListenPort = port;
            else if (key.Equals("DNS", StringComparison.OrdinalIgnoreCase))
                serverConfig.Dns.AddRange(value.Split(',').Select(s => s.Trim()));
            else if (key.Equals("MTU", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out int mtu))
                serverConfig.Mtu = mtu;
            else if (key.Equals("PostUp", StringComparison.OrdinalIgnoreCase))
                serverConfig.PostUp.Add(value); // wg allows multiple PostUp lines
            else if (key.Equals("PostDown", StringComparison.OrdinalIgnoreCase))
                serverConfig.PostDown.Add(value); // wg allows multiple PostDown lines
            else if (key.Equals("SaveConfig", StringComparison.OrdinalIgnoreCase) && bool.TryParse(value, out bool sc))
                 serverConfig.SaveConfig = sc;
            // Ignore unknown keys for now
        }

        private static void ParsePeerLine(WgPeerConfig peerConfig, string key, string value)
        {
            // PublicKey is handled by constructor
            if (key.Equals("PresharedKey", StringComparison.OrdinalIgnoreCase))
                peerConfig.PresharedKey = value;
            else if (key.Equals("AllowedIPs", StringComparison.OrdinalIgnoreCase))
                peerConfig.AllowedIPs.AddRange(value.Split(',').Select(s => s.Trim()));
            else if (key.Equals("Endpoint", StringComparison.OrdinalIgnoreCase))
                peerConfig.Endpoint = value;
            else if (key.Equals("PersistentKeepalive", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out int pk))
                peerConfig.PersistentKeepalive = pk;
            // Ignore unknown keys
        }


        public static WgConfig FromFile(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Configuration file not found.", filePath);

            var configString = File.ReadAllText(filePath);
            return Parse(configString);
        }
    }
}
