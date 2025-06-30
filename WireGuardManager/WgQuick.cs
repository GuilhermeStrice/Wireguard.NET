using System;
using System.IO;
using System.Threading.Tasks;
using WireGuardManager.Utilities;
using WireGuardManager.Exceptions;

namespace WireGuardManager
{
    public static class WgQuick
    {
        private static string GetWgPath(WgManagerConfig? config = null)
        {
            config ??= WgManagerConfig.Load();
            return string.IsNullOrWhiteSpace(config.WgPath) ? "wg" : config.WgPath;
        }
        private static string GetWgQuickPath(WgManagerConfig? config = null)
        {
            config ??= WgManagerConfig.Load();
            return string.IsNullOrWhiteSpace(config.WgQuickPath) ? "wg-quick" : config.WgQuickPath;
        }

        private static async Task<ProcessRunner.ProcessResult> RunWgCommandAsync(string arguments, WgManagerConfig? managerConfig = null, TimeSpan? timeout = null)
        {
            string toolPath = GetWgPath(managerConfig);
            var result = await ProcessRunner.RunAsync(toolPath, arguments, timeout: timeout ?? ProcessRunner.DefaultShortOperationTimeout);
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

        private static async Task<ProcessRunner.ProcessResult> RunWgQuickCommandAsync(string arguments, WgManagerConfig? managerConfig = null, TimeSpan? timeout = null)
        {
            string toolPath = GetWgQuickPath(managerConfig);
            var result = await ProcessRunner.RunAsync(toolPath, arguments, timeout: timeout ?? ProcessRunner.DefaultLongOperationTimeout);
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


        public static async Task<ProcessRunner.ProcessResult> SyncConf(string interfaceName, string configFilePath, WgManagerConfig? customConfig = null, TimeSpan? timeout = null)
        {
            if (!ValidationUtils.IsValidInterfaceName(interfaceName))
                throw new InvalidInputException("Invalid interface name format.", nameof(interfaceName));
            if (string.IsNullOrWhiteSpace(configFilePath))
                throw new InvalidInputException("Config file path cannot be null or empty.", nameof(configFilePath));
            if (!File.Exists(configFilePath))
                throw new FileNotFoundException($"WireGuard configuration file not found for SyncConf at '{configFilePath}'.", configFilePath);

            var config = customConfig ?? WgManagerConfig.Load();
            return await RunWgCommandAsync($"syncconf \"{interfaceName}\" \"{configFilePath}\"", config, timeout);
        }

        public static async Task<ProcessRunner.ProcessResult> SetConf(string interfaceName, string configFilePath, WgManagerConfig? customConfig = null, TimeSpan? timeout = null)
        {
            if (!ValidationUtils.IsValidInterfaceName(interfaceName))
                throw new InvalidInputException("Invalid interface name format.", nameof(interfaceName));
            if (string.IsNullOrWhiteSpace(configFilePath))
                throw new InvalidInputException("Config file path cannot be null or empty.", nameof(configFilePath));
            if (!File.Exists(configFilePath))
                throw new FileNotFoundException($"WireGuard configuration file not found for SetConf at '{configFilePath}'.", configFilePath);

            var config = customConfig ?? WgManagerConfig.Load();
            return await RunWgCommandAsync($"setconf \"{interfaceName}\" \"{configFilePath}\"", config, timeout);
        }

        public static async Task<ProcessRunner.ProcessResult> Show(string interfaceName, WgManagerConfig? customConfig = null, TimeSpan? timeout = null)
        {
            if (!ValidationUtils.IsValidInterfaceName(interfaceName))
                throw new InvalidInputException("Invalid interface name format.", nameof(interfaceName));

            var config = customConfig ?? WgManagerConfig.Load();
            return await RunWgCommandAsync($"show \"{interfaceName}\"", config, timeout);
        }

        public static async Task<ProcessRunner.ProcessResult> ShowAll(WgManagerConfig? customConfig = null, TimeSpan? timeout = null)
        {
            var config = customConfig ?? WgManagerConfig.Load();
            return await RunWgCommandAsync("show", config, timeout);
        }

        public static async Task<ProcessRunner.ProcessResult> Up(string interfaceNameOrPath, WgManagerConfig? customConfig = null, TimeSpan? timeout = null)
        {
            if (string.IsNullOrWhiteSpace(interfaceNameOrPath))
                throw new InvalidInputException("Interface name or path cannot be null or empty.", nameof(interfaceNameOrPath));

            bool isInterfaceName = ValidationUtils.IsValidInterfaceName(interfaceNameOrPath); // Use ValidationUtils

            if (isInterfaceName)
            {
                var config = customConfig ?? WgManagerConfig.Load();
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

            var configForRun = customConfig ?? WgManagerConfig.Load(); // Load if not passed, for GetWgQuickPath
            return await RunWgQuickCommandAsync($"up \"{interfaceNameOrPath}\"", configForRun, timeout);
        }

        public static async Task<ProcessRunner.ProcessResult> Down(string interfaceNameOrPath, WgManagerConfig? customConfig = null, TimeSpan? timeout = null)
        {
            if (string.IsNullOrWhiteSpace(interfaceNameOrPath)) // Could use IsValidInterfaceName OR check if it's a valid file path
                throw new InvalidInputException("Interface name or path cannot be null or empty.", nameof(interfaceNameOrPath));

            var config = customConfig ?? WgManagerConfig.Load();
            return await RunWgQuickCommandAsync($"down \"{interfaceNameOrPath}\"", config, timeout);
        }

        public static async Task<ProcessRunner.ProcessResult> Save(string interfaceName, WgManagerConfig? customConfig = null, TimeSpan? timeout = null)
        {
             if (!ValidationUtils.IsValidInterfaceName(interfaceName))
                throw new InvalidInputException("Invalid interface name format for Save command.", nameof(interfaceName));

            var config = customConfig ?? WgManagerConfig.Load();
            return await RunWgQuickCommandAsync($"save \"{interfaceName}\"", config, timeout);
        }
    }
}
