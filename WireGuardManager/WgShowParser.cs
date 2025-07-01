using System;
using System.Collections.Generic;
using System.Globalization; // For NumberStyles
using System.Linq;

namespace WireGuardManager
{
    /// <summary>
    /// Provides static methods for parsing the output of 'wg show dump' commands.
    /// </summary>
    public static class WgShowParser
    {
        // Correct format for `wg show all dump` based on man page and typical output:
        // Each line can be an interface definition or a peer definition.
        // Interface: <ifname>\t<private_key_base64>\t<publickey_base64>\t<listen_port>\t<fwmark>
        // Peer:      <ifname>\t<peer_publickey_base64>\t<presharedkey_base64_or_off>\t<endpoint_host:port>\t<allowed_ips_comma_sep>\t<latest_handshake_unix_epoch>\t<bytes_rx>\t<bytes_tx>\t<persistent_keepalive_seconds_or_off>
        // Note: The private key is shown in `wg show all dump`.

        /// <summary>
        /// Parses the output of a 'wg show all dump' command.
        /// </summary>
        /// <param name="dumpOutput">The string output from 'wg show all dump'.</param>
        /// <returns>A list of WgInterfaceInfo objects representing the parsed interfaces and their peers.</returns>
        /// <exception cref="ArgumentNullException">Thrown if dumpOutput is null.</exception>
        /// <exception cref="WireGuardConfigParseException">Thrown if parsing fails due to unexpected format.</exception>
        public static List<WgInterfaceInfo> ParseShowAllDumpOutput(string dumpOutput)
        {
            if (dumpOutput == null) throw new ArgumentNullException(nameof(dumpOutput));

            var interfacesMap = new Dictionary<string, WgInterfaceInfo>();
            var lines = dumpOutput.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var fields = line.Split('\t');
                if (fields.Length < 1) continue;

                string interfaceName = fields[0];
                WgInterfaceInfo currentInterface;

                if (fields.Length == 5) // Potential Interface line
                {
                    if (!interfacesMap.TryGetValue(interfaceName, out currentInterface!))
                    {
                        currentInterface = new WgInterfaceInfo { Name = interfaceName };
                        interfacesMap[interfaceName] = currentInterface;
                    }
                    // Field 1 is private key - not typically used by WgInterfaceInfo directly for security.
                    // We can store it if WgInterfaceInfo.PrivateKey is made non-nullable and used. For now, we'll skip.
                    // currentInterface.PrivateKey = fields[1];
                    currentInterface.PublicKey = fields[2];
                    currentInterface.ListenPort = int.TryParse(fields[3], out var port) ? port : (int?)null;
                    currentInterface.FwMark = fields[4] == "off" ? null : fields[4];
                }
                else if (fields.Length == 9) // Potential Peer line
                {
                    if (!interfacesMap.TryGetValue(interfaceName, out currentInterface!))
                    {
                        // This case (peer before interface) shouldn't happen with `wg show all dump`
                        // but if it did, we could create a placeholder interface or throw.
                        // For now, assume interface line comes first or is implicitly created if needed.
                         currentInterface = new WgInterfaceInfo { Name = interfaceName };
                         interfacesMap[interfaceName] = currentInterface;
                         // Log warning or handle as error if interface details are expected to be complete.
                         Console.WriteLine($"Warning: Peer data found for interface '{interfaceName}' before its own definition line in dump. Interface details might be incomplete.");
                    }

                    var peerInfo = new WgPeerInfo
                    {
                        PublicKey = fields[1],
                        PresharedKeyExists = fields[2] != "(none)" && fields[2] != "off", // "(none)" is common, "off" also seen
                        Endpoint = string.IsNullOrWhiteSpace(fields[3]) || fields[3] == "(none)" ? null : fields[3],
                        AllowedIPs = fields[4].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList(),
                        LatestHandshake = long.TryParse(fields[5], out var handshake) && handshake > 0 ? DateTimeOffset.FromUnixTimeSeconds(handshake) : (DateTimeOffset?)null,
                        TransferRxBytes = long.TryParse(fields[6], out var rx) ? rx : 0,
                        TransferTxBytes = long.TryParse(fields[7], out var tx) ? tx : 0,
                        PersistentKeepaliveIntervalSeconds = fields[8] == "off" ? null : (int.TryParse(fields[8], out var pka) ? pka : (int?)null)
                    };
                    currentInterface.Peers.Add(peerInfo);
                }
                else
                {
                    // Gracefully ignore lines that don't match expected field counts for dump format, or throw
                    // For robustness, could log a warning.
                    // throw new WireGuardConfigParseException($"Unexpected number of fields ({fields.Length}) in 'wg show all dump' line: {line}", null, line);
                     Console.WriteLine($"Warning: Skipping line with unexpected number of fields ({fields.Length}) in 'wg show all dump': {line}");
                }
            }
            return interfacesMap.Values.ToList();
        }

        /// <summary>
        /// Parses the output of a 'wg show [interface] dump' command.
        /// This command typically only lists peers for the specified interface.
        /// The interface's own details (public key, listen port) must be known beforehand or fetched separately.
        /// </summary>
        /// <param name="interfaceName">The name of the interface for which peers are being parsed.</param>
        /// <param name="dumpOutput">The string output from 'wg show [interface] dump'.</param>
        /// <param name="interfacePublicKey">The known public key of the interface.</param>
        /// <param name="listenPort">The known listening port of the interface.</param>
        /// <param name="fwMark">The known fwmark of the interface (can be null if "off").</param>
        /// <returns>A WgInterfaceInfo object populated with its peers.</returns>
        /// <exception cref="ArgumentNullException">Thrown if dumpOutput or interfaceName is null.</exception>
        /// <exception cref="WireGuardConfigParseException">Thrown if parsing fails due to unexpected format.</exception>
        public static WgInterfaceInfo ParseShowInterfaceDumpOutput(string interfaceName, string dumpOutput, string interfacePublicKey, int? listenPort, string? fwMark)
        {
            if (interfaceName == null) throw new ArgumentNullException(nameof(interfaceName));
            if (dumpOutput == null) throw new ArgumentNullException(nameof(dumpOutput));
            if (interfacePublicKey == null) throw new ArgumentNullException(nameof(interfacePublicKey)); // Essential for identification

            var wgInterface = new WgInterfaceInfo
            {
                Name = interfaceName,
                PublicKey = interfacePublicKey,
                ListenPort = listenPort,
                FwMark = fwMark
            };

            var lines = dumpOutput.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var fields = line.Split('\t');
                // `wg show <iface> dump` output for each peer:
                // <public_key> \t <preshared_key> \t <endpoint> \t <allowed_ips> \t <latest_handshake> \t <rx_bytes> \t <tx_bytes> \t <persistent_keepalive>
                if (fields.Length == 8) // Peer line
                {
                    var peerInfo = new WgPeerInfo
                    {
                        PublicKey = fields[0],
                        PresharedKeyExists = fields[1] != "(none)" && fields[1] != "off",
                        Endpoint = string.IsNullOrWhiteSpace(fields[2]) || fields[2] == "(none)" ? null : fields[2],
                        AllowedIPs = fields[3].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList(),
                        LatestHandshake = long.TryParse(fields[4], out var handshake) && handshake > 0 ? DateTimeOffset.FromUnixTimeSeconds(handshake) : (DateTimeOffset?)null,
                        TransferRxBytes = long.TryParse(fields[5], out var rx) ? rx : 0,
                        TransferTxBytes = long.TryParse(fields[6], out var tx) ? tx : 0,
                        PersistentKeepaliveIntervalSeconds = fields[7] == "off" ? null : (int.TryParse(fields[7], out var pka) ? pka : (int?)null)
                    };
                    wgInterface.Peers.Add(peerInfo);
                }
                else
                {
                    // throw new WireGuardConfigParseException($"Unexpected number of fields ({fields.Length}) in 'wg show {interfaceName} dump' line: {line}", null, line);
                    Console.WriteLine($"Warning: Skipping line with unexpected number of fields ({fields.Length}) in 'wg show {interfaceName} dump': {line}");
                }
            }
            return wgInterface;
        }
    }
}
