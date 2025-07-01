using System;
using System.IO;
using System.Threading.Tasks;
using WireGuardManager.Exceptions;
using WireGuardManager.Utilities; // For ValidationUtils and WgManagerConfig

namespace WireGuardManager
{
    /// <summary>
    /// Provides functionality for managing WireGuard .conf files, specifically for deploying them
    /// to the system's WireGuard configuration directory (e.g., /etc/wireguard).
    /// </summary>
    public static class WgConfigFileManager
    {
        public static IFileSystem FileSystemProvider { get; set; } = new StandardFileSystem();

        public static async Task DeployConfigAsync(string sourceConfigPath, string interfaceName, WgManagerConfig config, bool overwrite = false)
        {
            if (string.IsNullOrWhiteSpace(sourceConfigPath))
                throw new InvalidInputException("Source configuration file path cannot be null or empty.", nameof(sourceConfigPath));
            if (!ValidationUtils.IsValidInterfaceName(interfaceName))
                throw new InvalidInputException("Invalid interface name format.", nameof(interfaceName));
            if (config == null)
                throw new System.ArgumentNullException(nameof(config));
            if (string.IsNullOrWhiteSpace(config.WireguardConfigDirectory))
                throw new InvalidInputException("WireguardConfigDirectory in WgManagerConfig cannot be null or empty.", $"{nameof(config)}.{nameof(config.WireguardConfigDirectory)}");

            if (!FileSystemProvider.FileExists(sourceConfigPath)) // Use IFileSystem
                throw new FileNotFoundException($"Source configuration file not found at '{sourceConfigPath}'.", sourceConfigPath);

            string destFileName = $"{interfaceName}.conf";
            string destFilePath = Path.Combine(config.WireguardConfigDirectory, destFileName);

            try
            {
                // Path.GetDirectoryName is non-IO, so direct use is fine.
                // Alternatively, add it to IFileSystem if strict abstraction is desired.
                string? destDirectory = Path.GetDirectoryName(destFilePath);
                if (!string.IsNullOrWhiteSpace(destDirectory))
                {
                    // Use EnsureDirectoryExists, which was added to IFileSystem
                    FileSystemProvider.EnsureDirectoryExists(destDirectory);
                    Console.WriteLine($"Ensured destination directory '{destDirectory}' exists.");
                }

                Console.WriteLine($"Deploying '{sourceConfigPath}' to '{destFilePath}' (Overwrite: {overwrite})");
                FileSystemProvider.CopyFile(sourceConfigPath, destFilePath, overwrite); // Use IFileSystem
                Console.WriteLine($"Successfully deployed configuration to '{destFilePath}'.");
                await Task.CompletedTask; // Keep async signature, though CopyFile is sync
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new PermissionsException($"deploy configuration to '{destFilePath}'", destFilePath, ex);
            }
            catch (FileNotFoundException ex) when (ex.FileName == sourceConfigPath)
            {
                throw;
            }
            catch (IOException ex) when (ex.Message.Contains("already exists") && !overwrite)
            {
                throw new WireGuardManagerException($"Destination file '{destFilePath}' already exists and overwrite is false.", ex);
            }
            catch (Exception ex)
            {
                throw new WireGuardManagerException($"Failed to deploy configuration from '{sourceConfigPath}' to '{destFilePath}'.", ex);
            }
        }
    }
}
