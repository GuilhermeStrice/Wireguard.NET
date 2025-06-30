using System;
using System.IO;
using System.Threading.Tasks;
using WireGuardManager;
using WireGuardManager.Utilities; // For ProcessResult

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("WireGuardManager Library Example\n");

        string wgInterfaceName = "wg0"; // Example interface name
        // For wg-quick, config files are often in /etc/wireguard/ or a local path.
        // The library itself doesn't mandate a location for user-generated .conf files.
        string confFilePathUser = $"./{wgInterfaceName}_example.conf"; // User-generated conf file for this example
        string parsedConfFilePathUser = $"./{wgInterfaceName}_example_parsed.conf";

        // Path to the library's operational config file (config.json)
        // WgManagerConfig.Load() will look for "config.json" next to the WireGuardManager.dll by default.
        // The ExampleConsole project is set up to copy its own config.json to its output dir.
        string libraryConfigJsonPath = "config.json"; // Relative to executing assembly of ExampleConsole

        try
        {
            // 0. Load Library Configuration (config.json)
            // Although WgQuick.Up will load it internally if not passed,
            // it's good to show how it can be loaded explicitly if needed for other purposes.
            Console.WriteLine($"Loading library operational configuration from '{libraryConfigJsonPath}'...");
            var libraryConfig = WgManagerConfig.Load(libraryConfigJsonPath); // Load config specific to this example's output dir
            Console.WriteLine($"  AllowSystemdManagement: {libraryConfig.AllowSystemdManagement}");
            Console.WriteLine($"  SystemdServicePath: {libraryConfig.SystemdServicePath}\n");
            Console.WriteLine($"NOTE: For systemd management to work, 'AllowSystemdManagement' must be true in '{libraryConfigJsonPath}' AND the application must run with root privileges.\n");

            // 1. Generate Server Key Pair
            Console.WriteLine("Generating server key pair...");
            var serverKeys = await WgKeyManager.GenerateKeyPairAsync();
            Console.WriteLine($"  Server Private Key: {serverKeys.PrivateKey}");
            Console.WriteLine($"  Server Public Key: {serverKeys.PublicKey}\n");

            // 2. Create Server Configuration
            Console.WriteLine("Creating server configuration for WireGuard .conf file...");
            var serverInterface = new WgServerConfig(serverKeys.PrivateKey)
            {
                Address = new() { "10.0.10.1/24", "fd00:1234::1/64" },
                ListenPort = 51820,
                Dns = new() { "1.1.1.1" },
                PostUp = new() { "iptables -A FORWARD -i %i -j ACCEPT", "iptables -t nat -A POSTROUTING -o eth0 -j MASQUERADE" },
                PostDown = new() { "iptables -D FORWARD -i %i -j ACCEPT", "iptables -t nat -D POSTROUTING -o eth0 -j MASQUERADE" }
            };
            var wgConfForFile = new WgConfig(serverInterface);
            Console.WriteLine("  Server .conf structure created.\n");

            // 3. Generate Peer Key Pair
            Console.WriteLine("Generating key pair for Peer 1...");
            var peer1Keys = await WgKeyManager.GenerateKeyPairAsync();
            Console.WriteLine($"  Peer 1 Private Key (client side): {peer1Keys.PrivateKey}");
            Console.WriteLine($"  Peer 1 Public Key (for server .conf): {peer1Keys.PublicKey}\n");

            // 4. Create Peer Configuration
            Console.WriteLine("Creating configuration for Peer 1...");
            var peer1Config = new WgPeerConfig(peer1Keys.PublicKey)
            {
                AllowedIPs = new() { "10.0.10.2/32", "fd00:1234::2/128" },
                PersistentKeepalive = 25
            };
            wgConfForFile.AddPeer(peer1Config);
            Console.WriteLine("  Peer 1 added to .conf structure.\n");

            // 5. Print and Save the WireGuard .conf file
            Console.WriteLine("--- Generated WireGuard .conf File Content ---");
            Console.WriteLine(wgConfForFile.ToString());
            Console.WriteLine("-------------------------------------------\n");
            Console.WriteLine($"Saving .conf content to '{confFilePathUser}'...");
            wgConfForFile.ToFile(confFilePathUser);
            Console.WriteLine($"  .conf content saved.\n");

            // 6. Demonstrate WgQuick.Up with implicit systemd management
            Console.WriteLine($"Demonstrating WgQuick.Up for interface '{wgInterfaceName}'...");
            Console.WriteLine("This will attempt 'wg-quick up wg0'.");
            Console.WriteLine("If 'AllowSystemdManagement' is true in config.json and this app has root privileges,");
            Console.WriteLine("it will also try to create/enable the systemd service 'wg-quick@wg0.service'.\n");
            Console.WriteLine($"To test systemd management: ensure 'wg-quick' and 'systemctl' are installed, edit '{libraryConfigJsonPath}' to set 'allowSystemdManagement': true, and run this example with 'sudo'.\n");

            // We pass 'libraryConfig' explicitly here to use the one we loaded and showed.
            // If we passed null, WgQuick.Up would load its own default config.json.
            // ProcessRunner.ProcessResult upResult = await WgQuick.Up(wgInterfaceName, libraryConfig);
            // Console.WriteLine($"  'wg-quick up {wgInterfaceName}' attempt finished.");
            // if (upResult.Success)
            // {
            //     Console.WriteLine($"  Successfully brought up {wgInterfaceName}.");
            // }
            // else
            // {
            //     Console.WriteLine($"  Failed to bring up {wgInterfaceName}. Exit Code: {upResult.ExitCode}");
            //     Console.WriteLine($"  Stdout: {upResult.StandardOutput}");
            //     Console.WriteLine($"  Stderr: {upResult.StandardError}");
            // }
            Console.WriteLine($"CALL TO 'WgQuick.Up(\"{wgInterfaceName}\", libraryConfig);' IS COMMENTED OUT TO PREVENT ACCIDENTAL SYSTEM CHANGES.");
            Console.WriteLine("Uncomment the lines above in Program.cs to test this functionality.\n");


            // 7. Show how SyncConf or SetConf would be called (using the .conf file we generated)
            Console.WriteLine("To apply the generated .conf file directly (requires 'wg' tool and privileges):");
            Console.WriteLine($"  sudo wg syncconf {wgInterfaceName} {Path.GetFullPath(confFilePathUser)}");
            Console.WriteLine($"  OR");
            Console.WriteLine($"  sudo wg setconf {wgInterfaceName} {Path.GetFullPath(confFilePathUser)}");
            Console.WriteLine("  (After applying, you might need 'ip link set up dev wg0')\n");

            // 8. Parse an existing .conf file
            Console.WriteLine($"Attempting to parse the saved .conf file '{confFilePathUser}'...");
            if (File.Exists(confFilePathUser))
            {
                WgConfig loadedWgConf = WgConfig.FromFile(confFilePathUser);
                Console.WriteLine("  .conf file parsed successfully.");
                Console.WriteLine($"  Loaded Interface PrivateKey (first 5 chars): {loadedWgConf.Interface.PrivateKey.Substring(0, 5)}...");
                Console.WriteLine($"  Loaded Peer Count: {loadedWgConf.Peers.Count}");

                loadedWgConf.Interface.Dns.Add("9.9.9.9"); // Modify
                loadedWgConf.ToFile(parsedConfFilePathUser);
                Console.WriteLine($"  Modified .conf saved to '{parsedConfFilePathUser}' (added 9.9.9.9 as DNS).\n");
            }
            else
            {
                Console.WriteLine($"  File '{confFilePathUser}' not found for parsing.\n");
            }

            // 9. Show current WireGuard status (if wg is installed)
            Console.WriteLine("Attempting to show current WireGuard interfaces ('wg show')...");
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
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nAn error occurred: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            Console.ResetColor();
            Console.WriteLine("\nNOTE: Some operations require the 'wg', 'wg-quick', or 'systemctl' command-line tools to be installed and accessible in your system's PATH.");
            Console.WriteLine("Systemd management and applying configurations typically require root privileges.");
        }
        finally
        {
            Console.WriteLine($"\nExample run finished. Check '{confFilePathUser}' and '{parsedConfFilePathUser}'.");
            Console.WriteLine($"Library operational config was read from '{libraryConfigJsonPath}' (if it existed next to the executable).");
        }
    }
}
