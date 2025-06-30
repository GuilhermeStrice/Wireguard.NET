using System;
using System.IO;
using System.Text.Json;
using System.Reflection;

namespace WireGuardManager
{
    public class WgManagerConfig
    {
        public bool AllowSystemdManagement { get; set; } = false;
        public string SystemdServicePath { get; set; } = "/etc/systemd/system";

        private static string GetDefaultConfigPath()
        {
            // Default to config.json in the same directory as the library's assembly
            // This makes it easier for applications consuming the library to bundle it.
            // For development, this means it expects config.json in WireGuardManager/bin/Debug/net6.0 or similar.
            // We also created a config.json in the project root for reference, but at runtime this is what matters.
            var assemblyLocation = Assembly.GetExecutingAssembly().Location;
            var assemblyDirectory = Path.GetDirectoryName(assemblyLocation);
            if (assemblyDirectory == null)
            {
                // Fallback if directory can't be determined, though unlikely for a loaded assembly
                return "config.json";
            }
            return Path.Combine(assemblyDirectory, "config.json");
        }

        public static WgManagerConfig Load(string? configFilePath = null)
        {
            string actualPath = configFilePath ?? GetDefaultConfigPath();

            if (!File.Exists(actualPath))
            {
                // If the config file doesn't exist, return a default configuration
                // This prevents crashes if the file is missing and allows the library
                // to function with default (safe) settings.
                Console.WriteLine($"Warning: Configuration file not found at '{actualPath}'. Using default settings.");
                return new WgManagerConfig(); // Defaults: AllowSystemdManagement = false
            }

            try
            {
                var jsonString = File.ReadAllText(actualPath);
                var config = JsonSerializer.Deserialize<WgManagerConfig>(jsonString, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true // Be flexible with casing in JSON file
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
