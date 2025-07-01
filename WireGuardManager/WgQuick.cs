using System;
using System.IO;
using System.Threading.Tasks;
using WireGuardManager.Utilities;
using WireGuardManager.Exceptions;

namespace WireGuardManager
{
    /// <summary>
    /// Provides methods for interacting with the 'wg-quick' and 'wg' command-line tools
    /// to manage WireGuard interfaces, configurations, and systemd services.
    /// </summary>
    public static class WgQuick
    {
        /// <summary>
        /// Gets or sets the IProcessRunner instance used for executing external processes.
        /// This can be replaced with a mock for testing.
        /// </summary>
        public static IProcessRunner ProcessRunnerInstance { get; set; } = new ProcessRunner();
        public static IFileSystem FileSystemProvider { get; set; } = new StandardFileSystem();


        private static async Task<string> GetWgPathAsync(WgManagerConfig? config = null) // Made async
        {
            config ??= await WgManagerConfig.LoadAsync(); // Use await and LoadAsync
            return string.IsNullOrWhiteSpace(config.WgPath) ? "wg" : config.WgPath;
        }
        private static async Task<string> GetWgQuickPathAsync(WgManagerConfig? config = null) // Made async
        {
            config ??= await WgManagerConfig.LoadAsync(); // Use await and LoadAsync
            return string.IsNullOrWhiteSpace(config.WgQuickPath) ? "wg-quick" : config.WgQuickPath;
        }

        private static async Task<ProcessExecutionResult> RunWgCommandAsync(string arguments, WgManagerConfig? managerConfig = null, TimeSpan? explicitTimeout = null)
        {
            string toolPath = await GetWgPathAsync(managerConfig);

            TimeSpan finalTimeout;
            if (explicitTimeout.HasValue)
                finalTimeout = explicitTimeout.Value;
            else if (managerConfig?.DefaultShortOperationTimeoutSeconds.HasValue == true)
                finalTimeout = TimeSpan.FromSeconds(managerConfig.DefaultShortOperationTimeoutSeconds.Value);
            else
                finalTimeout = Utilities.ProcessRunner.DefaultShortOperationTimeout;

            var result = await ProcessRunnerInstance.RunAsync(toolPath, arguments, timeout: finalTimeout);
            if (!result.Success)
            {
                if (result.StandardError.Contains("Operation not permitted", StringComparison.OrdinalIgnoreCase) ||
                    result.StandardError.Contains("Permission denied", StringComparison.OrdinalIgnoreCase))
                {
                    throw new PermissionsException($"running '{toolPath} {arguments}'", null,
                        new ExternalToolException(toolPath, $"Permission error while running '{toolPath} {arguments}'.", result.ExitCode, result.StandardOutput, result.StandardError));
                }
                throw new ExternalToolException(toolPath, $"Command '{toolPath} {arguments}' failed.", result.ExitCode, result.StandardOutput, result.StandardError);
            }
            return result;
        }

        private static async Task<ProcessExecutionResult> RunWgQuickCommandAsync(string arguments, WgManagerConfig? managerConfig = null, TimeSpan? explicitTimeout = null)
        {
            string toolPath = await GetWgQuickPathAsync(managerConfig);

            TimeSpan finalTimeout;
            if (explicitTimeout.HasValue)
                finalTimeout = explicitTimeout.Value;
            else if (managerConfig?.DefaultLongOperationTimeoutSeconds.HasValue == true)
                finalTimeout = TimeSpan.FromSeconds(managerConfig.DefaultLongOperationTimeoutSeconds.Value);
            else
                finalTimeout = Utilities.ProcessRunner.DefaultLongOperationTimeout;

            var result = await ProcessRunnerInstance.RunAsync(toolPath, arguments, timeout: finalTimeout);
            if (!result.Success)
            {
                 if (result.StandardError.Contains("Operation not permitted", StringComparison.OrdinalIgnoreCase) ||
                     result.StandardError.Contains("Permission denied", StringComparison.OrdinalIgnoreCase))
                {
                    throw new PermissionsException($"running '{toolPath} {arguments}'", null,
                        new ExternalToolException(toolPath, $"Permission error while running '{toolPath} {arguments}'.", result.ExitCode, result.StandardOutput, result.StandardError));
                }
                throw new ExternalToolException(toolPath, $"Command '{toolPath} {arguments}' failed.", result.ExitCode, result.StandardOutput, result.StandardError);
            }
            return result;
        }


        public static async Task<ProcessExecutionResult> SyncConf(string interfaceName, string configFilePath, WgManagerConfig? customConfig = null, TimeSpan? timeout = null)
        {
            if (!ValidationUtils.IsValidInterfaceName(interfaceName))
                throw new InvalidInputException("Invalid interface name format.", nameof(interfaceName));
            if (string.IsNullOrWhiteSpace(configFilePath))
                throw new InvalidInputException("Config file path cannot be null or empty.", nameof(configFilePath));
            if (!FileSystemProvider.FileExists(configFilePath)) // Use IFileSystem
                throw new FileNotFoundException($"WireGuard configuration file not found for SyncConf at '{configFilePath}'.", configFilePath);

            var config = customConfig ?? await WgManagerConfig.LoadAsync(); // Use await and LoadAsync
            return await RunWgCommandAsync($"syncconf \"{interfaceName}\" \"{configFilePath}\"", config, timeout);
        }

        public static async Task<ProcessExecutionResult> SetConf(string interfaceName, string configFilePath, WgManagerConfig? customConfig = null, TimeSpan? timeout = null)
        {
            if (!ValidationUtils.IsValidInterfaceName(interfaceName))
                throw new InvalidInputException("Invalid interface name format.", nameof(interfaceName));
            if (string.IsNullOrWhiteSpace(configFilePath))
                throw new InvalidInputException("Config file path cannot be null or empty.", nameof(configFilePath));
            if (!FileSystemProvider.FileExists(configFilePath)) // Use IFileSystem
                throw new FileNotFoundException($"WireGuard configuration file not found for SetConf at '{configFilePath}'.", configFilePath);

            var config = customConfig ?? await WgManagerConfig.LoadAsync(); // Use await and LoadAsync
            return await RunWgCommandAsync($"setconf \"{interfaceName}\" \"{configFilePath}\"", config, timeout);
        }

        public static async Task<ProcessExecutionResult> Show(string interfaceName, WgManagerConfig? customConfig = null, TimeSpan? timeout = null)
        {
            if (!ValidationUtils.IsValidInterfaceName(interfaceName))
                throw new InvalidInputException("Invalid interface name format.", nameof(interfaceName));

            var config = customConfig ?? await WgManagerConfig.LoadAsync(); // Use await and LoadAsync
            return await RunWgCommandAsync($"show \"{interfaceName}\"", config, timeout);
        }

        public static async Task<ProcessExecutionResult> ShowAll(WgManagerConfig? customConfig = null, TimeSpan? timeout = null)
        {
            var config = customConfig ?? await WgManagerConfig.LoadAsync(); // Use await and LoadAsync
            return await RunWgCommandAsync("show", config, timeout);
        }

        public static async Task<ProcessExecutionResult> Up(string interfaceNameOrPath, WgManagerConfig? customConfig = null, TimeSpan? timeout = null)
        {
            if (string.IsNullOrWhiteSpace(interfaceNameOrPath))
                throw new InvalidInputException("Interface name or path cannot be null or empty.", nameof(interfaceNameOrPath));

            bool isInterfaceName = ValidationUtils.IsValidInterfaceName(interfaceNameOrPath);

            if (isInterfaceName)
            {
                var config = customConfig ?? await WgManagerConfig.LoadAsync();
                WgLogging.Logger.LogDebug($"Attempting implicit systemd service check for interface '{interfaceNameOrPath}'. AllowSystemdManagement: {config.AllowSystemdManagement}");
                if (config.AllowSystemdManagement)
                {
                    try
                    {
                        bool serviceOk = await WgSystemdManager.EnsureServiceExistsAndEnabled(interfaceNameOrPath, config, timeout); // Pass timeout
                        if (serviceOk)
                        {
                            WgLogging.Logger.LogInfo($"Systemd service for '{interfaceNameOrPath}' ensured successfully or already existed and enabled.");
                        }
                        else
                        {
                             // EnsureServiceExistsAndEnabled now logs its own warnings if it returns false for a known reason (like disabled by config)
                             // or throws for actual errors. So, a generic warning here might be redundant if EnsureServiceExistsAndEnabled is robust in its own logging/exceptions.
                             // However, if it returns false meaning "I tried but something minor stopped me from full success but didn't throw", a warning is okay.
                             // Given its current design, if it returns false and didn't throw, it means AllowSystemdManagement was false, which is already logged by EnsureServiceExistsAndEnabled.
                             // So, this else might not be strictly needed if EnsureServiceExistsAndEnabled handles its own logging/exceptions well.
                             // For now, let's keep a general info message if serviceOk is false but no exception was caught by Up().
                             WgLogging.Logger.LogInfo($"Systemd service check for '{interfaceNameOrPath}' completed; service may not have been modified if already ok or if management was disabled. 'wg-quick up' will proceed.");
                        }
                    }
                    catch (PermissionsException pex)
                    {
                        WgLogging.Logger.LogWarning($"Permission error during systemd management for '{interfaceNameOrPath}': {pex.Message}. 'wg-quick up' will proceed.");
                    }
                    catch (ExternalToolException etex)
                    {
                         WgLogging.Logger.LogWarning($"External tool error during systemd management for '{interfaceNameOrPath}': {etex.Message}. 'wg-quick up' will proceed.");
                    }
                    catch (ProcessTimeoutException ptex)
                    {
                         WgLogging.Logger.LogWarning($"Timeout during systemd management for '{interfaceNameOrPath}': {ptex.Message}. 'wg-quick up' will proceed.");
                    }
                    catch (Exception ex)
                    {
                        WgLogging.Logger.LogError($"Unexpected error during systemd management for '{interfaceNameOrPath}': {ex.Message}. 'wg-quick up' will proceed.", ex);
                    }
                }
            }
            // else if it's a path, ensure it's a valid path and ends with .conf - but wg-quick itself will validate this.
            // We might add a File.Exists check here if it's a path.

            var configForRun = customConfig ?? await WgManagerConfig.LoadAsync(); // Use await and LoadAsync
            return await RunWgQuickCommandAsync($"up \"{interfaceNameOrPath}\"", configForRun, timeout);
        }

        public static async Task<ProcessExecutionResult> Down(string interfaceNameOrPath, WgManagerConfig? customConfig = null, TimeSpan? timeout = null)
        {
            if (string.IsNullOrWhiteSpace(interfaceNameOrPath))
                throw new InvalidInputException("Interface name or path cannot be null or empty.", nameof(interfaceNameOrPath));

            var config = customConfig ?? await WgManagerConfig.LoadAsync(); // Use await and LoadAsync
            return await RunWgQuickCommandAsync($"down \"{interfaceNameOrPath}\"", config, timeout);
        }

        public static async Task<ProcessExecutionResult> Save(string interfaceName, WgManagerConfig? customConfig = null, TimeSpan? timeout = null)
        {
             if (!ValidationUtils.IsValidInterfaceName(interfaceName))
                throw new InvalidInputException("Invalid interface name format for Save command.", nameof(interfaceName));

            var config = customConfig ?? await WgManagerConfig.LoadAsync();
            return await RunWgQuickCommandAsync($"save \"{interfaceName}\"", config, timeout);
        }

        /// <summary>
        /// Sets or updates peer properties on a live WireGuard interface using the 'wg set' command.
        /// This method can add a new peer or modify an existing one.
        /// </summary>
        /// <param name="interfaceName">The name of the WireGuard interface.</param>
        /// <param name="peerPublicKey">The public key of the peer to add or modify.</param>
        /// <param name="options">A <see cref="WgPeerUpdateOptions"/> object specifying the properties to set.
        /// If <see cref="WgPeerUpdateOptions.Remove"/> is true, the peer is removed.
        /// For Preshared Keys:
        /// - If <see cref="WgPeerUpdateOptions.PresharedKeyFile"/> is set, it's used directly.
        /// - Else if <see cref="WgPeerUpdateOptions.PresharedKey"/> is "off", the PSK is removed.
        /// - Else if <see cref="WgPeerUpdateOptions.PresharedKey"/> is a key string, it's written to a temporary file which is then passed to 'wg set'.
        /// </param>
        /// <param name="customConfig">Optional custom library configuration which can specify tool paths.</param>
        /// <param name="timeout">Optional timeout for the 'wg set' command execution.</param>
        /// <returns>A <see cref="ProcessExecutionResult"/> indicating the success or failure of the 'wg set' command.</returns>
        /// <exception cref="InvalidInputException">If <paramref name="interfaceName"/>, <paramref name="peerPublicKey"/>, or <paramref name="options"/> (or its contents) are invalid.</exception>
        /// <exception cref="ExternalToolException">If the 'wg set' command itself fails for reasons other than permissions or timeout.</exception>
        /// <exception cref="PermissionsException">If running the 'wg set' command fails due to insufficient operating system permissions.</exception>
        /// <exception cref="ProcessTimeoutException">If the 'wg set' command execution exceeds the specified <paramref name="timeout"/>.</exception>
        /// <exception cref="CommandNotFoundException">If the 'wg' command (or a configured custom path for it) is not found.</exception>
        /// <exception cref="WireGuardManagerException">For other library-specific errors, such as issues with temporary file handling for preshared keys.</exception>
        public static async Task<ProcessExecutionResult> SetPeerAsync(
            string interfaceName,
            string peerPublicKey,
            WgPeerUpdateOptions options,
            WgManagerConfig? customConfig = null,
            TimeSpan? timeout = null)
        {
            if (!ValidationUtils.IsValidInterfaceName(interfaceName))
                throw new InvalidInputException("Invalid interface name format.", nameof(interfaceName));
            if (!ValidationUtils.IsValidWireGuardKey(peerPublicKey))
                throw new InvalidInputException("Invalid peer public key format.", nameof(peerPublicKey));
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            var config = customConfig ?? await WgManagerConfig.LoadAsync();

            var wgSetArgs = new List<string> { $"\"{interfaceName}\"", "peer", $"\"{peerPublicKey}\"" };

            if (options.Remove == true)
            {
                wgSetArgs.Add("remove");
            }
            else
            {
                if (options.AllowedIPs != null) // Allow empty list to clear AllowedIPs
                {
                    if (options.AllowedIPs.Any(ip => !ValidationUtils.IsValidCidr(ip)))
                        throw new InvalidInputException("One or more AllowedIPs are invalid CIDR format.", nameof(options.AllowedIPs));
                    wgSetArgs.Add("allowed-ips");
                    wgSetArgs.Add($"\"{string.Join(",", options.AllowedIPs.Select(ip => ip.Trim()))}\"");
                }

                if (!string.IsNullOrWhiteSpace(options.Endpoint))
                {
                    if (!ValidationUtils.IsValidEndpoint(options.Endpoint))
                        throw new InvalidInputException("Invalid Endpoint format in options.", nameof(options.Endpoint));
                    wgSetArgs.Add("endpoint");
                    wgSetArgs.Add($"\"{options.Endpoint}\"");
                }

                // PresharedKey handling logic (File > String > "off")
                if (!string.IsNullOrWhiteSpace(options.PresharedKeyFile))
                {
                    // 'wg set' expects a path to a file containing the key.
                    // No need to check FileSystemProvider.FileExists here, 'wg' tool will do that.
                    wgSetArgs.Add("preshared-key");
                    wgSetArgs.Add($"\"{options.PresharedKeyFile}\"");
                }
                else if (options.PresharedKey != null) // PresharedKey string or "off"
                {
                    if (options.PresharedKey.Equals("off", StringComparison.OrdinalIgnoreCase))
                    {
                        wgSetArgs.Add("preshared-key");
                        wgSetArgs.Add("off");
                    }
                    else // It's a key string, needs to be written to a temp file for 'wg set'
                    {
                        if (!ValidationUtils.IsValidWireGuardKey(options.PresharedKey))
                            throw new InvalidInputException("Invalid PresharedKey string format in options (must be valid key or 'off').", nameof(options.PresharedKey));

                        string tempPskFile = "";
                        try
                        {
                            tempPskFile = FileSystemProvider.GetTempFileName();
                            await FileSystemProvider.WriteAllTextAsync(tempPskFile, options.PresharedKey);
                            wgSetArgs.Add("preshared-key");
                            wgSetArgs.Add($"\"{tempPskFile}\"");
                        }
                        finally
                        {
                            if (!string.IsNullOrEmpty(tempPskFile) && FileSystemProvider.FileExists(tempPskFile))
                            {
                                try { FileSystemProvider.DeleteFile(tempPskFile); }
                                catch (Exception ex) { WgLogging.Logger.LogWarning($"Failed to delete temporary PSK file '{tempPskFile}': {ex.Message}"); }
                            }
                        }
                    }
                }

                if (options.PersistentKeepalive.HasValue)
                {
                    if (options.PersistentKeepalive < 0 || options.PersistentKeepalive > 65535)
                        throw new InvalidInputException("PersistentKeepalive value out of range (0-65535).", nameof(options.PersistentKeepalive));
                    wgSetArgs.Add("persistent-keepalive");
                    wgSetArgs.Add(options.PersistentKeepalive.Value.ToString());
                }
            }

            // If no actual update options were provided besides Remove=false (or null)
            if (wgSetArgs.Count == 3 && options.Remove != true)
            {
                // This means no peer properties were actually specified for update.
                // 'wg set <if> peer <key>' with no further args is not a valid command.
                // We could return a ProcessExecutionResult indicating no operation, or throw.
                // For now, let's assume the caller intends *some* change if not removing.
                // If options are all null (and not Remove), it's arguably an InvalidInput or no-op.
                // Let's return a "success" with no action, or throw InvalidInput if this is undesired.
                // For now, let it proceed; `wg` tool itself might complain if no actual operation is specified.
                // However, `wg set <if> peer <key>` with nothing else is a valid way to just add a peer if it wasn't there.
                // Our logic above ensures at least one property or "remove" is usually specified.
                // If AllowedIPs is an empty list, it will add `allowed-ips ""` which clears them.
                // If all settable options are null, and Remove is not true, it means just ensure peer exists.
                // `wg set <iface> peer <pubkey>` is valid to add a peer without specific attributes initially.
                 if (options.AllowedIPs == null &&
                    options.Endpoint == null &&
                    options.PresharedKeyFile == null &&
                    options.PresharedKey == null &&
                    options.PersistentKeepalive == null)
                {
                    WgLogging.Logger.LogDebug($"SetPeerAsync called for peer {peerPublicKey} on {interfaceName} with no specific properties to set (besides ensuring peer existence).");
                    // 'wg set <if> peer <key>' is a valid command to ensure peer exists or add it.
                    // No further args needed if this is the intent.
                }
            }


            string arguments = string.Join(" ", wgSetArgs);
            return await RunWgCommandAsync(arguments, config, timeout);
        }

        /// <summary>
        /// Sets the firewall mark for a live WireGuard interface using 'wg set &lt;interface&gt; fwmark &lt;mark|off&gt;'.
        /// </summary>
        /// <param name="interfaceName">The name of the WireGuard interface.</param>
        /// <param name="fwmarkValueOrOff">The firewall mark value (e.g., "0x123", "123") or "off" to disable it. If null or empty, "off" is assumed.</param>
        /// <param name="customConfig">Optional custom library configuration.</param>
        /// <param name="timeout">Optional timeout for the 'wg set' command execution.</param>
        /// <returns>A ProcessExecutionResult indicating success or failure.</returns>
        /// <exception cref="InvalidInputException">If interfaceName is invalid.</exception>
        /// <exception cref="ExternalToolException">If the 'wg set' command fails.</exception>
        public static async Task<ProcessExecutionResult> SetInterfaceFwMarkAsync(
            string interfaceName,
            string? fwmarkValueOrOff,
            WgManagerConfig? customConfig = null,
            TimeSpan? timeout = null)
        {
            if (!ValidationUtils.IsValidInterfaceName(interfaceName))
                throw new InvalidInputException("Invalid interface name format.", nameof(interfaceName));

            string fwmarkArg = "off"; // Default to "off"
            if (!string.IsNullOrWhiteSpace(fwmarkValueOrOff) && !fwmarkValueOrOff.Equals("off", StringComparison.OrdinalIgnoreCase))
            {
                // Basic validation: if not "off", it should not be empty. 'wg set' will validate the actual number format.
                fwmarkArg = fwmarkValueOrOff;
            }

            var config = customConfig ?? await WgManagerConfig.LoadAsync();
            string arguments = $"set \"{interfaceName}\" fwmark \"{fwmarkArg}\""; // Ensure fwmarkArg is quoted if it could contain spaces (though unlikely for fwmark)

            return await RunWgCommandAsync(arguments, config, timeout);
        }
    }
}
