using System;
using System.IO;
using System.Threading.Tasks;
using WireGuardManager.Utilities;

namespace WireGuardManager
{
    public static class WgSystemdManager
    {
        private const string SystemctlPath = "systemctl"; // Assuming systemctl is in PATH

        private static string GetServiceFileContent(string interfaceName)
        {
            // Standard wg-quick@.service template.
            // The interface name is passed as an argument to wg-quick by systemd (%I).
            return $@"# This service is managed by WireGuardManager
[Unit]
Description=WireGuard via wg-quick for %I
After=network.target nss-lookup.target
Wants=network.target nss-lookup.target

[Service]
Type=oneshot
RemainAfterExit=yes
ExecStart={GetWgQuickPath()} up %i
ExecStop={GetWgQuickPath()} down %i
Environment=WG_QUICK_USERSPACE_IMPLEMENTATION=boringtun # Or other, if needed. Often not specified.
# Standard service files might also include:
# ExecReload=/bin/bash -c 'exec {GetWgQuickPath()} strip %i && exec {GetWgQuickPath()} up %i'
# Or just:
# ExecReload={GetWgQuickPath()} reload %i
# For simplicity, we'll omit ExecReload for now unless it's specifically requested.
# SaveState=yes # Also sometimes seen, related to wg-quick saveconfig behavior

[Install]
WantedBy=multi-user.target
";
        }

        private static string GetWgQuickPath()
        {
            // This could be made configurable if needed, perhaps via WgManagerConfig
            return "wg-quick"; // Assume in PATH
        }


        /// <summary>
        /// Ensures a systemd service unit for the given WireGuard interface exists and is enabled.
        /// This method requires appropriate (root) permissions to write to systemd directories and run systemctl.
        /// </summary>
        /// <param name="interfaceName">The name of the WireGuard interface (e.g., wg0).</param>
        /// <param name="config">The library configuration specifying if systemd management is allowed.</param>
        /// <returns>True if the service exists and is enabled (or was successfully made so), false otherwise or if not allowed by config.</returns>
        public static async Task<bool> EnsureServiceExistsAndEnabled(string interfaceName, WgManagerConfig config)
        {
            if (string.IsNullOrWhiteSpace(interfaceName))
            {
                Console.WriteLine("Error: Interface name cannot be empty for systemd management.");
                return false;
            }

            if (!config.AllowSystemdManagement)
            {
                Console.WriteLine($"Systemd management is disabled by library configuration (AllowSystemdManagement=false). Skipping service check for {interfaceName}.");
                return false; // Indicate no action taken due to config
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
                    // Directory creation should typically not be needed for /etc/systemd/system, but good practice if path was highly configurable
                    // string? directory = Path.GetDirectoryName(fullServicePath);
                    // if (directory != null && !Directory.Exists(directory)) { Directory.CreateDirectory(directory); }
                    await File.WriteAllTextAsync(fullServicePath, serviceContent);
                    Console.WriteLine($"Successfully wrote service file {fullServicePath}.");
                }
                catch (UnauthorizedAccessException ex)
                {
                    Console.WriteLine($"Error: Permission denied writing service file {fullServicePath}. Ensure you have root privileges. {ex.Message}");
                    return false;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error writing service file {fullServicePath}: {ex.Message}");
                    return false;
                }
            }
            else
            {
                Console.WriteLine($"Service file {fullServicePath} already exists.");
            }

            // Only run daemon-reload if we actually created or modified the file.
            // For simplicity here, we run it if it didn't exist, assuming we created it.
            // A more advanced check might compare content and rewrite/reload if different.
            if (!serviceFileExisted)
            {
                Console.WriteLine("Running 'systemctl daemon-reload'...");
                var daemonReloadResult = await ProcessRunner.RunAsync(SystemctlPath, "daemon-reload");
                if (!daemonReloadResult.Success)
                {
                    Console.WriteLine($"Error: 'systemctl daemon-reload' failed. Exit Code: {daemonReloadResult.ExitCode}. Stderr: {daemonReloadResult.StandardError}");
                    // Potentially clean up the created service file if daemon-reload fails?
                    // For now, we'll leave it and report failure.
                    return false;
                }
                Console.WriteLine("'systemctl daemon-reload' completed successfully.");
            }

            // Check if service is enabled, enable if not.
            // 'systemctl is-enabled' returns exit code 0 if enabled, 1 if disabled.
            Console.WriteLine($"Checking if service {serviceFileName} is enabled...");
            var isEnabledResult = await ProcessRunner.RunAsync(SystemctlPath, $"is-enabled {serviceFileName}");

            // is-enabled: 0 (enabled), 1 (disabled), >1 (error or not found)
            // We treat "static" (exit code 0, stdout "static") also as effectively enabled for our purpose here,
            // as it means it has no [Install] section but is available. Our template has [Install].
            if (isEnabledResult.ExitCode == 0 && (isEnabledResult.StandardOutput.Trim() == "enabled" || isEnabledResult.StandardOutput.Trim() == "static"))
            {
                 Console.WriteLine($"Service {serviceFileName} is already enabled.");
                 return true;
            }
            else if (isEnabledResult.ExitCode == 1 || (isEnabledResult.ExitCode == 0 && isEnabledResult.StandardOutput.Trim() == "disabled" )) // disabled or "bad" exit code for not enabled
            {
                Console.WriteLine($"Service {serviceFileName} is not enabled. Attempting to enable...");
                var enableResult = await ProcessRunner.RunAsync(SystemctlPath, $"enable {serviceFileName}");
                if (!enableResult.Success)
                {
                    Console.WriteLine($"Error: 'systemctl enable {serviceFileName}' failed. Exit Code: {enableResult.ExitCode}. Stderr: {enableResult.StandardError}");
                    return false;
                }
                Console.WriteLine($"Successfully enabled service {serviceFileName}.");
                return true;
            }
            else // Some other error with is-enabled
            {
                 Console.WriteLine($"Warning: 'systemctl is-enabled {serviceFileName}' returned unexpected status. Exit Code: {isEnabledResult.ExitCode}. Stdout: {isEnabledResult.StandardOutput} Stderr: {isEnabledResult.StandardError}. Assuming not enabled and attempting to enable.");
                 var enableResult = await ProcessRunner.RunAsync(SystemctlPath, $"enable {serviceFileName}");
                if (!enableResult.Success)
                {
                    Console.WriteLine($"Error: 'systemctl enable {serviceFileName}' failed after is-enabled check. Exit Code: {enableResult.ExitCode}. Stderr: {enableResult.StandardError}");
                    return false;
                }
                Console.WriteLine($"Successfully enabled service {serviceFileName} (after unexpected is-enabled status).");
                return true;
            }
        }
    }
}
