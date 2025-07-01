using System;
using System.IO;
using System.Threading.Tasks;
using WireGuardManager.Utilities;
using WireGuardManager.Exceptions;

namespace WireGuardManager
{
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

        private static async Task<ProcessExecutionResult> RunWgCommandAsync(string arguments, WgManagerConfig? managerConfig = null, TimeSpan? timeout = null)
        {
            string toolPath = await GetWgPathAsync(managerConfig); // Use await
            var result = await ProcessRunnerInstance.RunAsync(toolPath, arguments, timeout: timeout ?? Utilities.ProcessRunner.DefaultShortOperationTimeout);
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

        private static async Task<ProcessExecutionResult> RunWgQuickCommandAsync(string arguments, WgManagerConfig? managerConfig = null, TimeSpan? timeout = null)
        {
            string toolPath = await GetWgQuickPathAsync(managerConfig); // Use await
            var result = await ProcessRunnerInstance.RunAsync(toolPath, arguments, timeout: timeout ?? Utilities.ProcessRunner.DefaultLongOperationTimeout);
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
                var config = customConfig ?? await WgManagerConfig.LoadAsync(); // Use await and LoadAsync
                Console.WriteLine($"Attempting implicit systemd service check for interface '{interfaceNameOrPath}'. AllowSystemdManagement: {config.AllowSystemdManagement}");
                if (config.AllowSystemdManagement)
                {
                    try
                    {
                        // Pass timeout to systemd operations as well, if applicable (EnsureServiceExistsAndEnabled might need modification or use a default)
                        bool serviceOk = await WgSystemdManager.EnsureServiceExistsAndEnabled(interfaceNameOrPath, config /*, timeout */);
                        if (serviceOk)
                        {
                            Console.WriteLine($"Systemd service for '{interfaceNameOrPath}' ensured successfully or already existed and enabled.");
                        }
                        else
                        {
                             Console.WriteLine($"Warning: Could not ensure systemd service for '{interfaceNameOrPath}'. 'wg-quick up' will proceed.");
                        }
                    }
                    catch (PermissionsException pex)
                    {
                        Console.WriteLine($"Warning: Permission error during systemd management for '{interfaceNameOrPath}': {pex.Message}. 'wg-quick up' will proceed.");
                    }
                    catch (ExternalToolException etex)
                    {
                         Console.WriteLine($"Warning: External tool error during systemd management for '{interfaceNameOrPath}': {etex.Message}. 'wg-quick up' will proceed.");
                    }
                    catch (ProcessTimeoutException ptex) // Catch timeout from systemd operations
                    {
                         Console.WriteLine($"Warning: Timeout during systemd management for '{interfaceNameOrPath}': {ptex.Message}. 'wg-quick up' will proceed.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Warning: Unexpected error during systemd management for '{interfaceNameOrPath}': {ex.Message}. 'wg-quick up' will proceed.");
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

            var config = customConfig ?? await WgManagerConfig.LoadAsync(); // Use await and LoadAsync
            return await RunWgQuickCommandAsync($"save \"{interfaceName}\"", config, timeout);
        }
    }
}
