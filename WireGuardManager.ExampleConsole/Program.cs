using System;
using System.IO;
using System.Threading.Tasks;
using WireGuardManager;
using WireGuardManager.Utilities; // For ProcessResult if you want to inspect it directly

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("WireGuardManager Library Example\n");

        string wgInterfaceName = "wg0"; // Example interface name
        string configFilePath = $"./{wgInterfaceName}.conf";
        string parsedConfigFilePath = $"./{wgInterfaceName}_parsed.conf";

        try
        {
            // 1. Generate Server Key Pair
            Console.WriteLine("Generating server key pair...");
            var serverKeys = await WgKeyManager.GenerateKeyPairAsync();
            Console.WriteLine($"  Server Private Key: {serverKeys.PrivateKey}");
            Console.WriteLine($"  Server Public Key: {serverKeys.PublicKey}\n");

            // 2. Create Server Configuration
            Console.WriteLine("Creating server configuration...");
            var serverInterface = new WgServerConfig(serverKeys.PrivateKey)
            {
                Address = new() { "10.0.10.1/24", "fd00:1234::1/64" },
                ListenPort = 51820,
                Dns = new() { "1.1.1.1" },
                PostUp = new() { "iptables -A FORWARD -i %i -j ACCEPT", "iptables -t nat -A POSTROUTING -o eth0 -j MASQUERADE" },
                PostDown = new() { "iptables -D FORWARD -i %i -j ACCEPT", "iptables -t nat -D POSTROUTING -o eth0 -j MASQUERADE" }
            };
            var wgConfig = new WgConfig(serverInterface);
            Console.WriteLine("  Server configuration object created.\n");

            // 3. Generate Peer Key Pair
            Console.WriteLine("Generating key pair for Peer 1...");
            var peer1Keys = await WgKeyManager.GenerateKeyPairAsync();
            Console.WriteLine($"  Peer 1 Private Key (client side): {peer1Keys.PrivateKey}");
            Console.WriteLine($"  Peer 1 Public Key (for server config): {peer1Keys.PublicKey}\n");

            // Optionally, generate a PresharedKey
            // Console.WriteLine("Generating PresharedKey for Peer 1...");
            // string peer1Psk = await WgKeyManager.GeneratePresharedKeyAsync();
            // Console.WriteLine($"  Peer 1 PresharedKey: {peer1Psk}\n");


            // 4. Create Peer Configuration
            Console.WriteLine("Creating configuration for Peer 1...");
            var peer1Config = new WgPeerConfig(peer1Keys.PublicKey)
            {
                AllowedIPs = new() { "10.0.10.2/32", "fd00:1234::2/128" },
                // PresharedKey = peer1Psk, // Uncomment if using PSK
                PersistentKeepalive = 25
            };
            Console.WriteLine("  Peer 1 configuration object created.\n");

            // 5. Add Peer to Server Configuration
            Console.WriteLine("Adding Peer 1 to server configuration...");
            wgConfig.AddPeer(peer1Config);
            Console.WriteLine("  Peer 1 added.\n");

            // (Optional) Add another peer
            Console.WriteLine("Generating key pair for Peer 2...");
            var peer2Keys = await WgKeyManager.GenerateKeyPairAsync();
            var peer2Config = new WgPeerConfig(peer2Keys.PublicKey)
            {
                AllowedIPs = new() { "10.0.10.3/32" },
                Endpoint = "peer2.example.com:12345" // Example if endpoint is known
            };
            wgConfig.AddPeer(peer2Config);
            Console.WriteLine("  Peer 2 added.\n");


            // 6. Print the generated configuration
            Console.WriteLine("--- Generated WireGuard Configuration ---");
            Console.WriteLine(wgConfig.ToString());
            Console.WriteLine("---------------------------------------\n");

            // 7. Save the configuration to a file
            Console.WriteLine($"Saving configuration to '{configFilePath}'...");
            wgConfig.ToFile(configFilePath);
            Console.WriteLine($"  Configuration saved.\n");

            // 8. Show how SyncConf or SetConf would be called
            //    IMPORTANT: These commands modify the system's network configuration
            //    and usually require root privileges. Uncomment with caution.
            Console.WriteLine("To apply this configuration (requires 'wg' tool and privileges):");
            Console.WriteLine($"  sudo wg syncconf {wgInterfaceName} {Path.GetFullPath(configFilePath)}");
            Console.WriteLine($"  OR");
            Console.WriteLine($"  sudo wg setconf {wgInterfaceName} {Path.GetFullPath(configFilePath)}");
            Console.WriteLine("  (After applying, you might need 'ip link set up dev wg0' or 'wg-quick up ./wg0.conf')\n");

            /*
            // Example of calling SyncConf (UNCOMMENT AND RUN WITH SUDO IF YOU WANT TO TRY):
            if (File.Exists(configFilePath))
            {
                Console.WriteLine($"Attempting to apply configuration to '{wgInterfaceName}' using syncconf...");
                Console.WriteLine($"Command: wg syncconf {wgInterfaceName} {Path.GetFullPath(configFilePath)}");
                Console.WriteLine("This will likely fail without root privileges or if 'wg0' does not exist or 'wg' is not installed.");

                // ProcessRunner.ProcessResult syncResult = await WgQuick.SyncConf(wgInterfaceName, Path.GetFullPath(configFilePath));
                // if (syncResult.Success)
                // {
                //     Console.WriteLine("  syncconf command executed successfully.");
                // }
                // else
                // {
                //     Console.WriteLine($"  syncconf command failed. Exit Code: {syncResult.ExitCode}");
                //     Console.WriteLine($"  Error Output: {syncResult.StandardError}");
                // }
            }
            */

            // 9. Parse an existing configuration file
            Console.WriteLine($"Attempting to parse the saved configuration file '{configFilePath}'...");
            if (File.Exists(configFilePath))
            {
                WgConfig loadedConfig = WgConfig.FromFile(configFilePath);
                Console.WriteLine("  Configuration parsed successfully.");
                Console.WriteLine($"  Loaded Interface PrivateKey: {loadedConfig.Interface.PrivateKey.Substring(0, 5)}..."); // Show partial for brevity
                Console.WriteLine($"  Loaded Peer Count: {loadedConfig.Peers.Count}");
                if (loadedConfig.Peers.Any())
                {
                    Console.WriteLine($"  First Peer PublicKey: {loadedConfig.Peers.First().PublicKey.Substring(0,5)}...");
                }
                // You can then work with the loadedConfig object
                // For example, save it again or modify it
                loadedConfig.Interface.Dns.Add("9.9.9.9"); // Modify
                loadedConfig.ToFile(parsedConfigFilePath);
                Console.WriteLine($"  Modified config saved to '{parsedConfigFilePath}' (added 9.9.9.9 as DNS).\n");

            }
            else
            {
                Console.WriteLine($"  File '{configFilePath}' not found for parsing.\n");
            }

            // 10. Show current WireGuard status (if wg is installed)
            Console.WriteLine("Attempting to show current WireGuard interfaces ('wg show')...");
            try
            {
                ProcessRunner.ProcessResult showResult = await WgQuick.ShowAll();
                if (showResult.Success)
                {
                    Console.WriteLine("  'wg show' executed successfully:");
                    Console.WriteLine(showResult.StandardOutput);
                }
                else
                {
                    Console.WriteLine($"  'wg show' command failed. Exit Code: {showResult.ExitCode}");
                    Console.WriteLine($"  Error Output: {showResult.StandardError}");
                     if(showResult.StandardError.Contains("Operation not permitted") || showResult.ExitCode == 1 && string.IsNullOrWhiteSpace(showResult.StandardOutput) && string.IsNullOrWhiteSpace(showResult.StandardError))
                     {
                         Console.WriteLine("  This might be because no interfaces are active or due to permissions.");
                     }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"  Could not execute 'wg show'. Ensure 'wg' is installed and in PATH. Error: {e.Message}");
            }

        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nAn error occurred: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            Console.ResetColor();
            Console.WriteLine("\nNOTE: Some operations like key generation or 'wg show' require the 'wg' command-line tool to be installed and accessible in your system's PATH.");
        }
        finally
        {
            // Clean up example files
            // if (File.Exists(configFilePath)) File.Delete(configFilePath);
            // if (File.Exists(parsedConfigFilePath)) File.Delete(parsedConfigFilePath);
            Console.WriteLine($"\nExample run finished. Check '{configFilePath}' and '{parsedConfigFilePath}'.");
        }
    }
}
