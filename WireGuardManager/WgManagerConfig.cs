using System;
using System.IO;
using System.Text.Json;
using System.Reflection;
using WireGuardManager.Utilities; // For IFileSystem

namespace WireGuardManager
{
    /// <summary>
    /// Represents the library's operational configuration, loaded from 'config.json'.
    /// Controls features like systemd management and custom tool paths.
    /// </summary>
    public class WgManagerConfig
    {
        /// <summary>
        /// Gets or sets the IFileSystem instance used for file operations.
        /// This can be replaced with a mock for testing.
        /// </summary>
        public static IFileSystem FileSystemProvider { get; set; } = new StandardFileSystem();

        public bool AllowSystemdManagement { get; set; } = false;
        public string SystemdServicePath { get; set; } = "/etc/systemd/system";
        public string? WgPath { get; set; } = null;
        public string? WgQuickPath { get; set; } = null;
        public string? SystemctlPath { get; set; } = null;
        public string WireguardConfigDirectory { get; set; } = "/etc/wireguard"; // Default path

        private static string GetDefaultConfigPath()
        {
            var assemblyLocation = Assembly.GetExecutingAssembly().Location;
            var assemblyDirectory = Path.GetDirectoryName(assemblyLocation);
            if (assemblyDirectory == null)
            {
                return "config.json";
            }
            return Path.Combine(assemblyDirectory, "config.json");
        }

        public static async Task<WgManagerConfig> LoadAsync(string? configFilePath = null) // Renamed to LoadAsync and made async
        {
            string actualPath = configFilePath ?? GetDefaultConfigPath();

            if (!FileSystemProvider.FileExists(actualPath))
            {
                Console.WriteLine($"Warning: Configuration file not found at '{actualPath}'. Using default settings.");
                return new WgManagerConfig();
            }

            try
            {
                var jsonString = await FileSystemProvider.ReadAllTextAsync(actualPath); // Use async provider method
                var config = JsonSerializer.Deserialize<WgManagerConfig>(jsonString, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return config ?? new WgManagerConfig(); // Return default if deserialization results in null
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Error parsing configuration file '{actualPath}': {ex.Message}. Using default settings.");
                return new WgManagerConfig();
            }
            catch (Exception ex) // Catch other potential errors like permission issues
            {
                Console.WriteLine($"Error loading configuration file '{actualPath}': {ex.Message}. Using default settings.");
                return new WgManagerConfig();
            }
        }
    }
}
