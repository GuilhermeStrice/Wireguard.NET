using System;
using System.IO;
using System.Threading.Tasks;
using WireGuardManager.Utilities;
using WireGuardManager.Exceptions;

namespace WireGuardManager
{
    /// <summary>
    /// Provides functionality for managing WireGuard systemd services (e.g., wg-quick@.service).
    /// Allows ensuring service files exist and are enabled.
    /// </summary>
    public static class WgSystemdManager
    {
        /// <summary>
        /// Gets or sets the IProcessRunner instance used for executing external processes.
        /// This can be replaced with a mock for testing.
        /// </summary>
        public static IProcessRunner ProcessRunnerInstance { get; set; } = new ProcessRunner();
        public static IFileSystem FileSystemProvider { get; set; } = new StandardFileSystem();


        private static async Task<string> GetSystemctlPathAsync(WgManagerConfig? config = null) // Made async
        {
            config ??= await WgManagerConfig.LoadAsync(); // Use await and LoadAsync
            return string.IsNullOrWhiteSpace(config.SystemctlPath) ? "systemctl" : config.SystemctlPath;
        }

        private static async Task<string> GetWgQuickPathAsync(WgManagerConfig? config = null) // Made async
        {
            config ??= await WgManagerConfig.LoadAsync(); // Use await and LoadAsync
            return string.IsNullOrWhiteSpace(config.WgQuickPath) ? "wg-quick" : config.WgQuickPath;
        }

        private static async Task<string> GetServiceFileContentAsync(string interfaceName, WgManagerConfig? config = null) // Made async
        {
            string wgQuickPath = await GetWgQuickPathAsync(config); // Use await
            return $@"# This service is managed by WireGuardManager
[Unit]
Description=WireGuard via wg-quick for %I
After=network.target nss-lookup.target
Wants=network.target nss-lookup.target

[Service]
Type=oneshot
RemainAfterExit=yes
ExecStart={wgQuickPath} up %i
ExecStop={wgQuickPath} down %i
Environment=WG_QUICK_USERSPACE_IMPLEMENTATION=boringtun

[Install]
WantedBy=multi-user.target
";
        }

        private static async Task RunSystemctlCommandAsync(string arguments, string operationDescription, WgManagerConfig? managerConfig = null, TimeSpan? explicitTimeout = null)
        {
            string systemctlPath = await GetSystemctlPathAsync(managerConfig);

            TimeSpan finalTimeout;
            if (explicitTimeout.HasValue)
                finalTimeout = explicitTimeout.Value;
            else if (managerConfig?.DefaultLongOperationTimeoutSeconds.HasValue == true) // systemctl commands generally use long timeout
                finalTimeout = TimeSpan.FromSeconds(managerConfig.DefaultLongOperationTimeoutSeconds.Value);
            else
                finalTimeout = Utilities.ProcessRunner.DefaultLongOperationTimeout;

            var result = await ProcessRunnerInstance.RunAsync(systemctlPath, arguments, timeout: finalTimeout);
            if (!result.Success)
            {
                if (result.StandardError.Contains("Access denied", StringComparison.OrdinalIgnoreCase) ||
                    result.StandardError.Contains("Operation not permitted", StringComparison.OrdinalIgnoreCase) ||
                    result.StandardError.Contains("Interactive authentication required", StringComparison.OrdinalIgnoreCase))
                {
                    throw new PermissionsException(operationDescription, $"systemctl {arguments}",
                        new ExternalToolException(SystemctlPath, $"Permission error while {operationDescription}.", result.ExitCode, result.StandardOutput, result.StandardError));
                }
                throw new ExternalToolException(SystemctlPath, $"Command 'systemctl {arguments}' failed during {operationDescription}.", result.ExitCode, result.StandardOutput, result.StandardError);
            }
            Console.WriteLine($"Successfully executed 'systemctl {arguments}'.");
        }

        // Default timeout for systemctl operations, can be overridden by WgQuick if needed there.
        // private static readonly TimeSpan DefaultSystemctlTimeout = TimeSpan.FromSeconds(30); // Now passed from WgQuick or default in RunSystemctlCommandAsync

        public static async Task<bool> EnsureServiceExistsAndEnabled(string interfaceName, WgManagerConfig config, TimeSpan? operationTimeout = null)
        {
            if (!ValidationUtils.IsValidInterfaceName(interfaceName))
                throw new InvalidInputException("Invalid interface name format for systemd management.", nameof(interfaceName));

            if (config == null) throw new ArgumentNullException(nameof(config));

            // TimeSpan timeout = operationTimeout ?? DefaultSystemctlTimeout; // Timeout is now handled by RunSystemctlCommandAsync default or WgQuick override

            if (!config.AllowSystemdManagement)
            {
                Console.WriteLine($"Systemd management is disabled by library configuration (AllowSystemdManagement=false). Skipping service check for {interfaceName}.");
                return false;
            }

            var serviceFileName = $"wg-quick@{interfaceName}.service";
            var fullServicePath = Path.Combine(config.SystemdServicePath, serviceFileName);

            Console.WriteLine($"Ensuring systemd service for {interfaceName} at {fullServicePath} (AllowSystemdManagement=true)");

            bool serviceFileExisted = FileSystemProvider.FileExists(fullServicePath); // Use IFileSystem

            if (!serviceFileExisted)
            {
                Console.WriteLine($"Service file {fullServicePath} does not exist. Attempting to create...");
                try
                {
                    string serviceContent = await GetServiceFileContentAsync(interfaceName, config); // Use await
                    await FileSystemProvider.WriteAllTextAsync(fullServicePath, serviceContent); // Use IFileSystem
                    Console.WriteLine($"Successfully wrote service file {fullServicePath}.");
                }
                catch (UnauthorizedAccessException ex)
                {
                    throw new PermissionsException("write systemd service file", fullServicePath, ex);
                }
                catch (Exception ex)
                {
                    throw new WireGuardManagerException($"Error writing systemd service file {fullServicePath}.", ex);
                }
            }
            else
            {
                Console.WriteLine($"Service file {fullServicePath} already exists.");
            }

            if (!serviceFileExisted)
            {
                // Pass the WgManagerConfig (config) to RunSystemctlCommandAsync
                await RunSystemctlCommandAsync("daemon-reload", "reloading systemd daemons", config, operationTimeout);
            }

            Console.WriteLine($"Checking if service {serviceFileName} is enabled...");
            string systemctlPath = await GetSystemctlPathAsync(config);

            TimeSpan isEnabledFinalTimeout;
            if (operationTimeout.HasValue)
                isEnabledFinalTimeout = operationTimeout.Value;
            else if (config.DefaultShortOperationTimeoutSeconds.HasValue)
                isEnabledFinalTimeout = TimeSpan.FromSeconds(config.DefaultShortOperationTimeoutSeconds.Value);
            else
                isEnabledFinalTimeout = Utilities.ProcessRunner.DefaultShortOperationTimeout;

            var isEnabledResult = await ProcessRunnerInstance.RunAsync(systemctlPath, $"is-enabled {serviceFileName}", timeout: isEnabledFinalTimeout);

            bool needsEnable = true;
            if (isEnabledResult.ExitCode == 0)
            {
                string outputTrimmed = isEnabledResult.StandardOutput.Trim();
                if (outputTrimmed == "enabled" || outputTrimmed == "static")
                {
                    Console.WriteLine($"Service {serviceFileName} is already {outputTrimmed}.");
                    needsEnable = false;
                }
                else
                {
                     Console.WriteLine($"Service {serviceFileName} reported status: {outputTrimmed} (ExitCode: {isEnabledResult.ExitCode}). Will attempt to enable.");
                }
            }
            else
            {
                 Console.WriteLine($"'systemctl is-enabled {serviceFileName}' indicated not enabled (ExitCode: {isEnabledResult.ExitCode}, Stdout: '{isEnabledResult.StandardOutput.Trim()}', Stderr: '{isEnabledResult.StandardError.Trim()}'). Will attempt to enable.");
            }

            if (needsEnable)
            {
                Console.WriteLine($"Attempting to enable service {serviceFileName}...");
                // Pass the WgManagerConfig (config) to RunSystemctlCommandAsync
                await RunSystemctlCommandAsync($"enable {serviceFileName}", $"enabling service {serviceFileName}", config, operationTimeout);
            }
            return true;
        }
    }
}
