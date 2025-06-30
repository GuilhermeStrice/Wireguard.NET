using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
// using System.Text.RegularExpressions; // No longer needed with new parsing approach
using WireGuardManager.Exceptions;

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
                // Consider throwing InvalidOperationException or a custom DuplicatePeerException
                Console.WriteLine($"Warning: Peer with PublicKey {peer.PublicKey} already exists. Not adding duplicate.");
                return;
            }
            Peers.Add(peer);
        }

        public bool RemovePeer(string publicKey)
        {
            if (string.IsNullOrWhiteSpace(publicKey))
                throw new InvalidInputException("PublicKey cannot be null or whitespace for RemovePeer.", nameof(publicKey));

            var peerToRemove = Peers.FirstOrDefault(p => p.PublicKey == publicKey);
            if (peerToRemove != null)
            {
                return Peers.Remove(peerToRemove);
            }
            return false;
        }

        public WgPeerConfig? GetPeer(string publicKey)
        {
            if (string.IsNullOrWhiteSpace(publicKey))
                throw new InvalidInputException("PublicKey cannot be null or whitespace for GetPeer.", nameof(publicKey));
            return Peers.FirstOrDefault(p => p.PublicKey == publicKey);
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(Interface.ToString());
            sb.AppendLine();

            foreach (var peer in Peers)
            {
                sb.Append(peer.ToString());
                sb.AppendLine();
            }
            return sb.ToString().TrimEnd();
        }

        public void ToFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new InvalidInputException("File path cannot be null or empty for ToFile.", nameof(filePath));
            try
            {
                File.WriteAllText(filePath, ToString());
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new PermissionsException("write configuration file", filePath, ex);
            }
            catch (Exception ex) // Catch other IO exceptions like DirectoryNotFoundException, IOException, etc.
            {
                throw new WireGuardManagerException($"Failed to write configuration to file '{filePath}'.", ex);
            }
        }

        public static WgConfig Parse(string configString)
        {
            if (string.IsNullOrWhiteSpace(configString))
                throw new InvalidInputException("Config string cannot be null or empty for Parse.", nameof(configString));

            var linesWithNumbers = configString.Split(new[] { '\r', '\n' }, StringSplitOptions.None)
                                            .Select((line, index) => new { Text = line.Trim(), Number = index + 1 })
                                            .Where(l => !string.IsNullOrEmpty(l.Text) && !l.Text.StartsWith("#"))
                                            .ToList();

            WgServerConfig? serverConfig = null;
            List<WgPeerConfig> peers = new List<WgPeerConfig>();
            WgPeerConfig? currentPeer = null;

            int currentLineNumForException = 0;
            string currentLineTextForException = string.Empty;

            try
            {
                // First pass to find [Interface] PrivateKey, as it's mandatory for WgServerConfig constructor
                string? serverPrivateKey = null;
                bool inInterfaceSectionForPK = false;
                foreach (var lineInfo in linesWithNumbers)
                {
                    currentLineNumForException = lineInfo.Number;
                    currentLineTextForException = lineInfo.Text;

                    if (lineInfo.Text.Equals("[Interface]", StringComparison.OrdinalIgnoreCase))
                    {
                        inInterfaceSectionForPK = true;
                        continue;
                    }
                    if (lineInfo.Text.Equals("[Peer]", StringComparison.OrdinalIgnoreCase))
                    {
                        inInterfaceSectionForPK = false;
                        continue;
                    }

                    if (inInterfaceSectionForPK)
                    {
                        var parts = lineInfo.Text.Split(new[] { '=' }, 2);
                        if (parts.Length == 2 && parts[0].Trim().Equals("PrivateKey", StringComparison.OrdinalIgnoreCase))
                        {
                            serverPrivateKey = parts[1].Trim();
                            break;
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(serverPrivateKey))
                {
                    throw new WireGuardConfigParseException("Mandatory PrivateKey not found in [Interface] section or [Interface] section missing.", null, null);
                }
                // Assuming WgServerConfig constructor will validate the private key internally later (Step 2.1)
                serverConfig = new WgServerConfig(serverPrivateKey);


                bool inInterfaceSection = false;
                foreach (var lineInfo in linesWithNumbers)
                {
                    currentLineNumForException = lineInfo.Number;
                    currentLineTextForException = lineInfo.Text;

                    if (lineInfo.Text.Equals("[Interface]", StringComparison.OrdinalIgnoreCase))
                    {
                        inInterfaceSection = true;
                        if (currentPeer != null) peers.Add(currentPeer);
                        currentPeer = null;
                        continue;
                    }

                    if (lineInfo.Text.Equals("[Peer]", StringComparison.OrdinalIgnoreCase))
                    {
                        inInterfaceSection = false;
                        if (currentPeer != null) peers.Add(currentPeer);
                        currentPeer = null;
                        continue;
                    }

                    var kvp = lineInfo.Text.Split(new[] { '=' }, 2);
                    if (kvp.Length != 2) {
                        // Allow empty lines or lines without '=' if they are not section headers and not within a section expecting K=V
                         if (!lineInfo.Text.StartsWith("[") && (inInterfaceSection || currentPeer != null))
                            throw new WireGuardConfigParseException("Malformed key-value pair.", lineInfo.Number, lineInfo.Text);
                        continue;
                    }

                    string key = kvp[0].Trim();
                    string value = kvp[1].Trim();

                    if (string.IsNullOrEmpty(key))
                         throw new WireGuardConfigParseException("Key cannot be empty in a key-value pair.", lineInfo.Number, lineInfo.Text);


                    if (inInterfaceSection)
                    {
                        if (key.Equals("PrivateKey", StringComparison.OrdinalIgnoreCase)) continue;
                        ParseInterfaceLine(serverConfig, key, value, lineInfo.Number, lineInfo.Text);
                    }
                    else // In Peer section or preparing for one
                    {
                        if (key.Equals("PublicKey", StringComparison.OrdinalIgnoreCase))
                        {
                            if (currentPeer != null && currentPeer.PublicKey != value) peers.Add(currentPeer);
                            if (currentPeer == null || currentPeer.PublicKey != value)
                            {
                                // Assuming WgPeerConfig constructor will validate the public key internally later (Step 2.1)
                                currentPeer = new WgPeerConfig(value);
                            }
                        }
                        else if (currentPeer != null)
                        {
                            ParsePeerLine(currentPeer, key, value, lineInfo.Number, lineInfo.Text);
                        }
                        else // Key/value outside a known peer context (PublicKey not yet defined for current peer)
                        {
                             throw new WireGuardConfigParseException($"Orphaned peer configuration line found before a [Peer] section's PublicKey.", lineInfo.Number, lineInfo.Text);
                        }
                    }
                }

                if (currentPeer != null) peers.Add(currentPeer);
            }
            catch (WireGuardManagerException) { throw; } // Re-throw our custom exceptions
            catch (ArgumentException ex) // Catch ArgumentExceptions from WgServerConfig/WgPeerConfig constructors if they throw early
            {
                throw new WireGuardConfigParseException($"Invalid value for key (e.g. PrivateKey or PublicKey). {ex.Message}", currentLineNumForException, currentLineTextForException, ex);
            }
            catch (Exception ex) // Catch any other unexpected errors during parsing
            {
                throw new WireGuardConfigParseException($"An unexpected error occurred during configuration parsing at/near line {currentLineNumForException}: '{currentLineTextForException}'.", currentLineNumForException, currentLineTextForException, ex);
            }

            if (serverConfig == null) // Should be caught by PrivateKey check, but as a safeguard
            {
                 throw new WireGuardConfigParseException("[Interface] section not found or is incomplete.", null, null);
            }

            var config = new WgConfig(serverConfig);
            config.Peers = peers;
            return config;
        }

        private static void ParseInterfaceLine(WgServerConfig serverConfig, string key, string value, int lineNumber, string lineContent)
        {
            try
            {
                if (key.Equals("Address", StringComparison.OrdinalIgnoreCase))
                    serverConfig.Address.AddRange(value.Split(',').Select(s => s.Trim()));
                else if (key.Equals("ListenPort", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(value, out int port) && port >= 0 && port <= 65535) serverConfig.ListenPort = port;
                    else throw new WireGuardConfigParseException($"Invalid ListenPort value: '{value}'. Must be an integer between 0 and 65535.", lineNumber, lineContent);
                }
                else if (key.Equals("DNS", StringComparison.OrdinalIgnoreCase))
                    serverConfig.Dns.AddRange(value.Split(',').Select(s => s.Trim()));
                else if (key.Equals("MTU", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(value, out int mtu) && mtu >= 576) serverConfig.Mtu = mtu; // Typical lower bound for MTU
                    else throw new WireGuardConfigParseException($"Invalid MTU value: '{value}'. Must be a reasonable integer (e.g., >= 576).", lineNumber, lineContent);
                }
                else if (key.Equals("PostUp", StringComparison.OrdinalIgnoreCase))
                    serverConfig.PostUp.Add(value);
                else if (key.Equals("PostDown", StringComparison.OrdinalIgnoreCase))
                    serverConfig.PostDown.Add(value);
                else if (key.Equals("SaveConfig", StringComparison.OrdinalIgnoreCase))
                {
                     if (bool.TryParse(value, out bool sc)) serverConfig.SaveConfig = sc;
                     else throw new WireGuardConfigParseException($"Invalid SaveConfig value: '{value}'. Must be 'true' or 'false'.", lineNumber, lineContent);
                }
                // else { Console.WriteLine($"Warning: Unknown key '{key}' in [Interface] section at line {lineNumber}. Ignoring."); }
            }
            catch (WireGuardConfigParseException) { throw; }
            catch (Exception ex)
            {
                throw new WireGuardConfigParseException($"Error processing [Interface] key '{key}' with value '{value}'", lineNumber, lineContent, ex);
            }
        }

        private static void ParsePeerLine(WgPeerConfig peerConfig, string key, string value, int lineNumber, string lineContent)
        {
            try
            {
                if (key.Equals("PresharedKey", StringComparison.OrdinalIgnoreCase))
                    peerConfig.PresharedKey = value; // Validation will be added in Step 2.1
                else if (key.Equals("AllowedIPs", StringComparison.OrdinalIgnoreCase))
                    peerConfig.AllowedIPs.AddRange(value.Split(',').Select(s => s.Trim())); // Validation in Step 2.2
                else if (key.Equals("Endpoint", StringComparison.OrdinalIgnoreCase))
                    peerConfig.Endpoint = value; // Validation in Step 2.2
                else if (key.Equals("PersistentKeepalive", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(value, out int pk) && pk >= 0 && pk <= 65535) peerConfig.PersistentKeepalive = pk; // Common range
                    else throw new WireGuardConfigParseException($"Invalid PersistentKeepalive value: '{value}'. Must be an integer (e.g., 0-65535).", lineNumber, lineContent);
                }
                // else { Console.WriteLine($"Warning: Unknown key '{key}' in [Peer] section for peer {peerConfig.PublicKey.Substring(0,8)}... at line {lineNumber}. Ignoring."); }
            }
            catch (WireGuardConfigParseException) { throw; }
            catch (Exception ex)
            {
                throw new WireGuardConfigParseException($"Error processing [Peer] key '{key}' with value '{value}' for peer {peerConfig.PublicKey.Substring(0,8)}...", lineNumber, lineContent, ex);
            }
        }

        public static WgConfig FromFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new InvalidInputException("File path cannot be null or empty for FromFile.", nameof(filePath));
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Configuration file not found at '{filePath}'.", filePath);

            try
            {
                var configString = File.ReadAllText(filePath);
                return Parse(configString);
            }
            catch (FileNotFoundException) { throw; }
            catch (UnauthorizedAccessException ex)
            {
                throw new PermissionsException("read configuration file", filePath, ex);
            }
            catch (Exception ex)
            {
                throw new WireGuardManagerException($"Failed to read or parse configuration file '{filePath}'.", ex);
            }
        }
    }
}
