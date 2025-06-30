using System;
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
            if (string.IsNullOrWhiteSpace(configFilePath))
                throw new ArgumentException("Config file path cannot be null or empty.", nameof(configFilePath));

            // wg syncconf <interface> <config_file>
            // Note: syncconf expects a "uapi" type config, which is what our WgConfig.ToString() should produce.
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
            if (string.IsNullOrWhiteSpace(configFilePath))
                throw new ArgumentException("Config file path cannot be null or empty.", nameof(configFilePath));

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
        /// </summary>
        /// <param name="interfaceNameOrPath">The interface name (e.g., wg0) or path to config file (e.g., /etc/wireguard/wg0.conf).</param>
        /// <returns>A ProcessResult indicating success or failure.</returns>
        public static async Task<ProcessRunner.ProcessResult> Up(string interfaceNameOrPath)
        {
            if (string.IsNullOrWhiteSpace(interfaceNameOrPath))
                throw new ArgumentException("Interface name or path cannot be null or empty.", nameof(interfaceNameOrPath));

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

            return await ProcessRunner.RunAsync(GetWgQuickPath(), $"down \"{interfaceNameOrPath}\"");
        }

        /// <summary>
        /// Saves the configuration of a wg-quick interface.
        /// This is equivalent to 'wg-quick save <interface>'.
        /// </summary>
        /// <param name="interfaceName">The name of the WireGuard interface (e.g., wg0).</param>
        /// <returns>A ProcessResult indicating success or failure.</returns>
        public static async Task<ProcessRunner.ProcessResult> Save(string interfaceName)
        {
             if (string.IsNullOrWhiteSpace(interfaceName))
                throw new ArgumentException("Interface name cannot be null or empty.", nameof(interfaceName));

            return await ProcessRunner.RunAsync(GetWgQuickPath(), $"save \"{interfaceName}\"");
        }
    }
}
