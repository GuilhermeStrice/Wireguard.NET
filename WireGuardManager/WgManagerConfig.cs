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
        public string WireguardConfigDirectory { get; set; } = "/etc/wireguard";
        public string? MinimumLogLevel { get; set; } = "Info";
        public bool EnableConsoleColors { get; set; } = true; // Default to true

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

                var loadedConfig = config ?? new WgManagerConfig();

                // Set global log level from config
                if (!string.IsNullOrWhiteSpace(loadedConfig.MinimumLogLevel))
                {
                    if (Enum.TryParse<LogLevel>(loadedConfig.MinimumLogLevel, true, out LogLevel parsedLevel))
                    {
                        WgLogging.MinimumLogLevel = parsedLevel;
                        // WgLogging.Logger.LogDebug($"Global MinimumLogLevel set to '{parsedLevel}' from config file."); // Might log before level is set effectively
                    }
                    else
                    {
                        WgLogging.Logger.LogWarning($"Invalid MinimumLogLevel value '{loadedConfig.MinimumLogLevel}' in configuration file '{actualPath}'. Using current default: {WgLogging.MinimumLogLevel}.");
                    }
                }
                // If MinimumLogLevel is null/whitespace in config, WgLogging.MinimumLogLevel retains its static default or previously set value.

                // Set console color preference from config
                ConsoleLoggingProvider.UseConsoleColors = loadedConfig.EnableConsoleColors;

                return loadedConfig;
            }
            catch (JsonException ex)
            {
                // Use the logger here, but be mindful that its own level might not be configured yet if this is the first load.
                // Default logger (Console) with default level (Info) should catch this Warning.
                WgLogging.Logger.LogWarning($"Error parsing configuration file '{actualPath}': {ex.Message}. Using default WgManagerConfig settings.");
                return new WgManagerConfig(); // Return default WgManagerConfig
            }
            catch (Exception ex)
            {
                WgLogging.Logger.LogError($"Error loading configuration file '{actualPath}': {ex.Message}. Using default WgManagerConfig settings.", ex);
                return new WgManagerConfig();
            }
        }
    }
}
