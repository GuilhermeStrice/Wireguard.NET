using System;
using System.IO;
using System.Threading.Tasks;
using WireGuardManager.Utilities;
using WireGuardManager.Exceptions;

namespace WireGuardManager
{
    public static class WgSystemdManager
    {
        // TODO: Make SystemctlPath and WgQuickPath configurable in Step 2.2 (Phase 2)
        private const string SystemctlPath = "systemctl";
        private static string GetWgQuickPath() => "wg-quick";

        private static string GetServiceFileContent(string interfaceName)
        {
            string wgQuickPath = GetWgQuickPath();
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

        private static async Task RunSystemctlCommandAsync(string arguments, string operationDescription, TimeSpan? timeout = null)
        {
            var result = await ProcessRunner.RunAsync(SystemctlPath, arguments, timeout: timeout ?? ProcessRunner.DefaultLongOperationTimeout);
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
        private static readonly TimeSpan DefaultSystemctlTimeout = TimeSpan.FromSeconds(30);

        public static async Task<bool> EnsureServiceExistsAndEnabled(string interfaceName, WgManagerConfig config, TimeSpan? operationTimeout = null)
        {
            if (!ValidationUtils.IsValidInterfaceName(interfaceName)) // Updated validation
                throw new InvalidInputException("Invalid interface name format for systemd management.", nameof(interfaceName));

            if (config == null) throw new ArgumentNullException(nameof(config));

            TimeSpan timeout = operationTimeout ?? DefaultSystemctlTimeout;

            if (!config.AllowSystemdManagement)
            {
                Console.WriteLine($"Systemd management is disabled by library configuration (AllowSystemdManagement=false). Skipping service check for {interfaceName}.");
                return false;
            }

            var serviceFileName = $"wg-quick@{interfaceName}.service";
            var fullServicePath = Path.Combine(config.SystemdServicePath, serviceFileName);

            Console.WriteLine($"Ensuring systemd service for {interfaceName} at {fullServicePath} (AllowSystemdManagement=true)");

            bool serviceFileExisted = File.Exists(fullServicePath);

            if (!serviceFileExisted)
            {
                Console.WriteLine($"Service file {fullServicePath} does not exist. Attempting to create...");
                try
                {
                    string serviceContent = GetServiceFileContent(interfaceName);
                    await File.WriteAllTextAsync(fullServicePath, serviceContent); // File IO is typically fast, timeout not applied here.
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
                await RunSystemctlCommandAsync("daemon-reload", "reloading systemd daemons", timeout);
            }

            Console.WriteLine($"Checking if service {serviceFileName} is enabled...");
            var isEnabledResult = await ProcessRunner.RunAsync(SystemctlPath, $"is-enabled {serviceFileName}", timeout: operationTimeout ?? ProcessRunner.DefaultShortOperationTimeout);

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
                await RunSystemctlCommandAsync($"enable {serviceFileName}", $"enabling service {serviceFileName}", timeout);
            }
            return true;
        }
    }
}
