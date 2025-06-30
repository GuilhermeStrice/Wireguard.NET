using System;
using System.IO;
using System.Threading.Tasks;
using WireGuardManager.Utilities;

namespace WireGuardManager
{
    public static class WgQuick
    {
        private static string GetWgPath()
        {
            return "wg"; // Assume in PATH
        }

        private static string GetWgQuickPath()
        {
            // This could be made configurable if needed, perhaps via WgManagerConfig
            return "wg-quick"; // Assume in PATH
        }

        /// <summary>
        /// Applies the configuration from a file to a WireGuard interface using 'wg syncconf'.
        /// This command is atomic and less disruptive than 'wg setconf' or 'wg-quick down/up'.
        /// The configuration file should be in the format output by WgConfig.ToString().
        /// </summary>
        /// <param name="interfaceName">The name of the WireGuard interface (e.g., wg0).</param>
        /// <param name="configFilePath">The path to the WireGuard configuration file.</param>
        /// <returns>A ProcessResult indicating success or failure.</returns>
        public static async Task<ProcessRunner.ProcessResult> SyncConf(string interfaceName, string configFilePath)
        {
            if (string.IsNullOrWhiteSpace(interfaceName))
                throw new ArgumentException("Interface name cannot be null or empty.", nameof(interfaceName));
            if (string.IsNullOrWhiteSpace(configFilePath)) // This should be a file path
                throw new ArgumentException("Config file path cannot be null or empty.", nameof(configFilePath));
            if (!File.Exists(configFilePath))
                throw new FileNotFoundException("WireGuard configuration file not found for SyncConf.", configFilePath);


            // wg syncconf <interface> <config_file>
            return await ProcessRunner.RunAsync(GetWgPath(), $"syncconf \"{interfaceName}\" \"{configFilePath}\"");
        }

        /// <summary>
        /// Applies the configuration from a file to a WireGuard interface using 'wg setconf'.
        /// Note: This command is powerful but can be disruptive as it completely replaces the configuration.
        /// 'syncconf' is generally preferred for live updates.
        /// </summary>
        /// <param name="interfaceName">The name of the WireGuard interface (e.g., wg0).</param>
        /// <param name="configFilePath">The path to the WireGuard configuration file.</param>
        /// <returns>A ProcessResult indicating success or failure.</returns>
        public static async Task<ProcessRunner.ProcessResult> SetConf(string interfaceName, string configFilePath)
        {
            if (string.IsNullOrWhiteSpace(interfaceName))
                throw new ArgumentException("Interface name cannot be null or empty.", nameof(interfaceName));
            if (string.IsNullOrWhiteSpace(configFilePath)) // This should be a file path
                throw new ArgumentException("Config file path cannot be null or empty.", nameof(configFilePath));
            if (!File.Exists(configFilePath))
                throw new FileNotFoundException("WireGuard configuration file not found for SetConf.", configFilePath);

            // wg setconf <interface> <config_file>
            return await ProcessRunner.RunAsync(GetWgPath(), $"setconf \"{interfaceName}\" \"{configFilePath}\"");
        }

        /// <summary>
        /// Shows the current configuration and status for a WireGuard interface.
        /// </summary>
        /// <param name="interfaceName">The name of the WireGuard interface (e.g., wg0).</param>
        /// <returns>A ProcessResult containing the output of 'wg show'.</returns>
        public static async Task<ProcessRunner.ProcessResult> Show(string interfaceName)
        {
            if (string.IsNullOrWhiteSpace(interfaceName))
                throw new ArgumentException("Interface name cannot be null or empty.", nameof(interfaceName));

            // wg show <interface>
            return await ProcessRunner.RunAsync(GetWgPath(), $"show \"{interfaceName}\"");
        }

        /// <summary>
        /// Shows all WireGuard interfaces and their configurations.
        /// </summary>
        /// <returns>A ProcessResult containing the output of 'wg show'.</returns>
        public static async Task<ProcessRunner.ProcessResult> ShowAll()
        {
            return await ProcessRunner.RunAsync(GetWgPath(), "show");
        }

        // wg-quick specific commands below. These are useful for managing interfaces
        // that are set up using wg-quick (e.g., /etc/wireguard/wg0.conf).

        /// <summary>
        /// Brings up a WireGuard interface using 'wg-quick up'.
        /// If the argument is an interface name (not a path) and systemd management is allowed in config.json,
        /// this method will implicitly ensure the systemd service unit exists and is enabled before calling 'wg-quick up'.
        /// </summary>
        /// <param name="interfaceNameOrPath">The interface name (e.g., wg0) or path to config file (e.g., /etc/wireguard/wg0.conf).</param>
        /// <param name="customConfig">Optional custom library configuration. If null, loads from default config.json path.</param>
        /// <returns>A ProcessResult indicating success or failure of the 'wg-quick up' command.</returns>
        public static async Task<ProcessRunner.ProcessResult> Up(string interfaceNameOrPath, WgManagerConfig? customConfig = null)
        {
            if (string.IsNullOrWhiteSpace(interfaceNameOrPath))
                throw new ArgumentException("Interface name or path cannot be null or empty.", nameof(interfaceNameOrPath));

            // Try to determine if interfaceNameOrPath is a direct interface name (e.g., "wg0")
            // vs. a file path (e.g., "./wg0.conf" or "/etc/wireguard/wg0.conf")
            // A simple heuristic: if it doesn't contain directory separators and doesn't end with .conf.
            // This might need refinement for edge cases (e.g. interface names with dots).
            bool isInterfaceName = !interfaceNameOrPath.Contains(Path.DirectorySeparatorChar) &&
                                   !interfaceNameOrPath.Contains(Path.AltDirectorySeparatorChar) &&
                                   !interfaceNameOrPath.EndsWith(".conf", StringComparison.OrdinalIgnoreCase);

            if (isInterfaceName)
            {
                var config = customConfig ?? WgManagerConfig.Load(); // Load config if not provided
                Console.WriteLine($"Attempting implicit systemd service check for interface '{interfaceNameOrPath}'. AllowSystemdManagement: {config.AllowSystemdManagement}");
                if (config.AllowSystemdManagement)
                {
                    bool serviceOk = await WgSystemdManager.EnsureServiceExistsAndEnabled(interfaceNameOrPath, config);
                    if (serviceOk)
                    {
                        Console.WriteLine($"Systemd service for '{interfaceNameOrPath}' ensured successfully or already existed and enabled.");
                    }
                    else
                    {
                        Console.WriteLine($"Warning: Failed to ensure systemd service for '{interfaceNameOrPath}'. 'wg-quick up' might still work if config file is present, or fail if service is expected.");
                        // We proceed anyway, as wg-quick up might work with a local config file
                        // or the user might have other means of service management.
                    }
                }
            }

            return await ProcessRunner.RunAsync(GetWgQuickPath(), $"up \"{interfaceNameOrPath}\"");
        }

        /// <summary>
        /// Brings down a WireGuard interface using 'wg-quick down'.
        /// </summary>
        /// <param name="interfaceNameOrPath">The interface name (e.g., wg0) or path to config file (e.g., /etc/wireguard/wg0.conf).</param>
        /// <returns>A ProcessResult indicating success or failure.</returns>
        public static async Task<ProcessRunner.ProcessResult> Down(string interfaceNameOrPath)
        {
            if (string.IsNullOrWhiteSpace(interfaceNameOrPath))
                throw new ArgumentException("Interface name or path cannot be null or empty.", nameof(interfaceNameOrPath));

            // No implicit systemd management for 'down' in this version.
            // 'wg-quick down' will handle finding the service or config file.
            return await ProcessRunner.RunAsync(GetWgQuickPath(), $"down \"{interfaceNameOrPath}\"");
        }

        /// <summary>
        /// Saves the configuration of a wg-quick interface.
        /// This is equivalent to 'wg-quick save <interface>'.
        /// </summary>
        /// <param name="interfaceName">The name of the WireGuard interface (e.g., wg0). This must be an interface name, not a path.</param>
        /// <returns>A ProcessResult indicating success or failure.</returns>
        public static async Task<ProcessRunner.ProcessResult> Save(string interfaceName)
        {
             if (string.IsNullOrWhiteSpace(interfaceName))
                throw new ArgumentException("Interface name cannot be null or empty.", nameof(interfaceName));
             if (interfaceName.Contains(Path.DirectorySeparatorChar) ||
                 interfaceName.Contains(Path.AltDirectorySeparatorChar) ||
                 interfaceName.EndsWith(".conf", StringComparison.OrdinalIgnoreCase))
             {
                throw new ArgumentException("Save command requires an interface name, not a file path.", nameof(interfaceName));
             }

            return await ProcessRunner.RunAsync(GetWgQuickPath(), $"save \"{interfaceName}\"");
        }
    }
}
