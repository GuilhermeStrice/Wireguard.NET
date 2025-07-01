# WireGuardManager for C#

**WireGuardManager** is a .NET library written in C# for managing WireGuard server configurations on Linux. It allows you to programmatically create, parse, modify, and save WireGuard configuration files. It also provides helpers to interact with the `wg` and `wg-quick` command-line tools for tasks like key generation, applying configurations, and managing systemd services for WireGuard interfaces.

This library is designed to be used in applications that need to automate WireGuard setup, such as custom VPN management panels or backend services.

## Features

*   **WireGuard `.conf` File Management:**
    *   Create and define `[Interface]` sections for the WireGuard server.
    *   Create and define `[Peer]` sections for clients.
    *   Support for common properties: `PrivateKey`, `PublicKey`, `Address`, `ListenPort`, `DNS`, `AllowedIPs`, `Endpoint`, `PresharedKey`, `PersistentKeepalive`, `MTU`, `PostUp`, `PostDown`.
    *   Parse existing WireGuard configuration files into C# objects.
    *   Save C# configuration objects back to WireGuard configuration file format.
*   **Key Management:**
    *   Generate new WireGuard public/private key pairs using the `wg genkey` and `wg pubkey` commands.
    *   Generate new preshared keys using the `wg genpsk` command.
*   **Command-Line Interaction (`wg`, `wg-quick`):**
    *   Apply configurations using `wg syncconf <interface> <config_file>` (recommended for live updates).
    *   Apply configurations using `wg setconf <interface> <config_file>`.
    *   Show current WireGuard interface status and configuration using `wg show`.
    *   Bring interfaces up/down using `wg-quick up <interface/config_file>` and `wg-quick down <interface/config_file>`.
    *   Save `wg-quick` configurations using `wg-quick save <interface>`.
*   **Systemd Service Management (Implicit):**
    *   When calling `WgQuick.Up(interfaceName)`, if configured and permitted, the library can automatically:
        *   Check if the `wg-quick@<interfaceName>.service` systemd unit exists.
        *   Create the service file if it's missing.
        *   Run `systemctl daemon-reload`.
        *   Run `systemctl enable wg-quick@<interfaceName>.service`.
    *   This behavior is controlled by a `config.json` file.
*   **Cross-Platform (Conceptual):** While designed with Linux `wg`, `wg-quick`, and `systemctl` in mind for CLI interactions, the core `.conf` file parsing/generation logic is platform-independent. The CLI interaction parts will only work where these tools are available.

## Dependencies

*   **.NET 6.0 or later.**
*   **WireGuard Tools (`wg`, `wg-quick`):** For key generation, applying `.conf` files, and bringing interfaces up/down. Must be installed and in PATH.
*   **`systemctl`:** For systemd service management features. Must be installed and in PATH.
*   **Root Privileges:** Required for writing systemd service files, running most `systemctl` commands, and applying configurations to live network interfaces.

## Library Configuration (`config.json`)

The library's behavior for certain privileged operations can be controlled by a `config.json` file. This file should be placed in the same directory as the `WireGuardManager.dll` assembly at runtime. If not found, default (safe) settings are used (`AllowSystemdManagement: false`). The `WireGuardManager` project itself includes a `config.json` which is copied to its output directory.

**Default `config.json` content:**
```json
{
  "allowSystemdManagement": false,
  "systemdServicePath": "/etc/systemd/system"
}
```
*   `allowSystemdManagement` (boolean): If `true`, allows the library (specifically `WgQuick.Up` when called with an interface name) to attempt to create and enable systemd service files for WireGuard interfaces. Defaults to `false`. **Requires root privileges for the application.**
*   `systemdServicePath` (string): The directory path where systemd service files are managed. Defaults to `/etc/systemd/system`.

## Core Classes

*   `WireGuardManager.WgServerConfig`: Represents the `[Interface]` section of a WireGuard `.conf` file.
*   `WireGuardManager.WgPeerConfig`: Represents a `[Peer]` section.
*   `WireGuardManager.WgConfig`: Represents an entire `.conf` file, holding one `WgServerConfig` and a list of `WgPeerConfig` objects. Provides methods for parsing, saving, and managing peers.
*   `WireGuardManager.WgKeyManager`: Static class for generating `KeyPair` (public/private) and `PresharedKey` strings by calling `wg` commands.
*   `WireGuardManager.WgQuick`: Static class for interacting with `wg` and `wg-quick` commands. The `Up` method includes implicit systemd service management if configured.
*   `WireGuardManager.WgSystemdManager`: Static class used internally by `WgQuick` to handle systemd service file creation and enabling.
*   `WireGuardManager.WgManagerConfig`: Represents the structure of `config.json` and provides a `Load()` method.
*   `WireGuardManager.Utilities.ProcessRunner`: A utility class for running external processes.

## Basic Usage

See the `WireGuardManager.ExampleConsole` project for a runnable demonstration.

**1. Initialize WireGuard `.conf` Structure:**
```csharp
using WireGuardManager;
using System.Threading.Tasks;

// Generate server keys
var serverKeys = await WgKeyManager.GenerateKeyPairAsync();

var serverInterface = new WgServerConfig(serverKeys.PrivateKey)
{
    Address = new() { "10.0.0.1/24" },
    ListenPort = 51820,
    Dns = new() { "1.1.1.1" }
};
var wgConfigFile = new WgConfig(serverInterface); // Represents the content of a .conf file
```

**2. Add Peers to `.conf` Structure:**
```csharp
var peerKeys = await WgKeyManager.GenerateKeyPairAsync();
var peerConfig = new WgPeerConfig(peerKeys.PublicKey) { AllowedIPs = new() { "10.0.0.2/32" } };
wgConfigFile.AddPeer(peerConfig);
```

**3. Save `.conf` File:**
```csharp
string confPath = "./wg0_example.conf";
wgConfigFile.ToFile(confPath);
Console.WriteLine($"WireGuard .conf file saved to {confPath}");
```

**4. Bring Interface Up (using `wg-quick up`):**
This example demonstrates calling `WgQuick.Up` for an interface name. It will implicitly attempt systemd service management if `config.json` has `allowSystemdManagement: true` and the application has root privileges.

```csharp
string interfaceName = "wg0"; // The WireGuard interface name

// Load the library's operational config (config.json)
// WgQuick.Up will also load this internally if not passed explicitly.
// The path to config.json is resolved relative to the executing assembly (WireGuardManager.dll).
var libraryConfig = WgManagerConfig.Load();

Console.WriteLine($"Attempting to bring up '{interfaceName}'. Systemd management allowed via config.json: {libraryConfig.AllowSystemdManagement}");
Console.WriteLine("If systemd management is allowed and this app has root privileges, it will try to create/enable the systemd service.");
Console.WriteLine("For this to work, wg-quick also needs a corresponding /etc/wireguard/wg0.conf file.");

// Make sure confPath (e.g., "./wg0_example.conf") is copied to /etc/wireguard/wg0.conf for wg-quick to find it by interface name.
// File.Copy(confPath, $"/etc/wireguard/{interfaceName}.conf", true); // This requires root.

// ProcessRunner.ProcessResult upResult = await WgQuick.Up(interfaceName, libraryConfig);
// if (upResult.Success)
// {
//    Console.WriteLine($"Interface {interfaceName} is up.");
// }
// else
// {
//    Console.WriteLine($"Failed to bring up {interfaceName}: {upResult.StandardError}");
// }
Console.WriteLine("Actual call to WgQuick.Up is commented out in README. See ExampleConsole project for more details.");
```
**Note:** For `wg-quick up <interfaceName>` to work, it usually requires a corresponding `/etc/wireguard/<interfaceName>.conf` file. The library's systemd management will create the service file, but you are responsible for ensuring the WireGuard `.conf` file is in the location `wg-quick` expects (typically `/etc/wireguard/`). Alternatively, `WgQuick.Up()` can take a direct path to a `.conf` file, in which case systemd management is not implicitly invoked.

**5. Apply Configuration Directly (e.g., using `wg syncconf`):**
This typically requires root privileges and an existing interface.
```csharp
// Assuming confPath from step 3 contains the desired configuration for an existing 'wg0' interface
// ProcessRunner.ProcessResult syncResult = await WgQuick.SyncConf(interfaceName, confPath);
// ... check syncResult ...
```

**6. Managing Live Interfaces (using `wg set`)**

The library provides methods to modify live WireGuard interfaces using the `wg set` command.

**Updating Peer Properties with `SetPeerAsync`:**

Use `WgQuick.SetPeerAsync` along with `WgPeerUpdateOptions` to change specific peer properties like `AllowedIPs`, `Endpoint`, `PresharedKey`, or `PersistentKeepalive`. You can also remove a peer.

```csharp
// string interfaceName = "wg0";
// string peerPublicKey = "PEER_PUBLIC_KEY_TO_UPDATE=";
// WgManagerConfig config = await WgManagerConfig.LoadAsync(); // Or pass your loaded config

var updateOptions = new WgPeerUpdateOptions
{
    AllowedIPs = new List<string> { "10.0.0.5/32" },
    Endpoint = "new.peer.address.com:12345"
    // PresharedKey = "NEW_PSK_STRING===============================", // Library handles temp file
    // PresharedKeyFile = "/path/to/new_psk.key", // Alternative to PresharedKey string
    // PresharedKey = "off", // To remove PSK
    // PersistentKeepalive = 30,
    // Remove = true // To remove the peer entirely
};

// try
// {
//     var setResult = await WgQuick.SetPeerAsync(interfaceName, peerPublicKey, updateOptions, config);
//     if (setResult.Success) Console.WriteLine("Peer updated!");
//     else Console.WriteLine($"Peer update failed: {setResult.StandardError}");
// }
// catch (Exception ex) { Console.WriteLine($"Error: {ex.Message}"); }
```
*   If `PresharedKeyFile` is set, its content is used.
*   If `PresharedKey` is a Base64 string, the library writes it to a temporary file for `wg set`.
*   If `PresharedKey` is "off", the preshared key is removed.

**Setting Interface Firewall Mark (`fwmark`) with `SetInterfaceFwMarkAsync`:**

```csharp
// string interfaceName = "wg0";
// WgManagerConfig config = await WgManagerConfig.LoadAsync();

// Set fwmark
// await WgQuick.SetInterfaceFwMarkAsync(interfaceName, "0xCAFE", config);
// Console.WriteLine("Fwmark set to 0xCAFE.");

// Remove fwmark
// await WgQuick.SetInterfaceFwMarkAsync(interfaceName, "off", config); // or null
// Console.WriteLine("Fwmark removed.");
```

## Building the Library

The library is a standard .NET 6 project.
```bash
cd WireGuardManager
dotnet build
```
The `config.json` file in the `WireGuardManager` project is set to "Copy to Output Directory: PreserveNewest", so it will be available next to the DLL.

## Running Tests

Tests are written using NUnit.
```bash
cd WireGuardManager.Tests
dotnet test
```
**Note:** Some tests interact with `wg`, `wg-quick`, and `systemctl`. These tests will only pass if these tools are installed and executable. Tests requiring administrative privileges are generally marked `[Explicit]` or have notes indicating their requirements. Tests for systemd management may be inconclusive or report warnings if run without root.

## Error Handling

Methods interacting with external processes will throw exceptions for critical errors (e.g., command not found) or return a `ProcessResult` object containing `ExitCode`, `StandardOutput`, and `StandardError`. Parsing methods in `WgConfig` can throw `FormatException`. `WgManagerConfig.Load` returns default settings upon error (e.g., if `config.json` is missing or malformed).

## Limitations & Future Considerations

*   **Privilege Requirements:** Most useful operations (managing live interfaces, systemd services) require administrative (root) privileges. The library itself doesn't handle privilege escalation.
*   **Linux Focus:** CLI and systemd interactions are Linux-centric.
*   **`.conf` File Placement:** When using `wg-quick up <interfaceName>`, the library manages the systemd service, but the user is responsible for placing the actual WireGuard `.conf` file (e.g., `wg0.conf`) in the location expected by `wg-quick` (usually `/etc/wireguard/`).
*   **Pure C# Key Generation:** Key generation currently shells out to `wg`.

## Logging

The `WireGuardManager` library uses a simple logging abstraction to output informational messages, warnings, and errors. By default, it logs to the console. You can customize this behavior by providing your own implementation of `WireGuardManager.Utilities.IWgLoggingProvider`.

**Controlling Log Level:**

The minimum log level can be controlled statically via `WireGuardManager.WgLogging.MinimumLogLevel`. The default is `LogLevel.Info`.
```csharp
using WireGuardManager;
using WireGuardManager.Utilities; // For LogLevel enum

// Set minimum log level (e.g., in your application startup)
WgLogging.MinimumLogLevel = LogLevel.Debug; // Show Debug, Info, Warning, Error
// or
// WgLogging.MinimumLogLevel = LogLevel.None; // Disable all library logging
```
The `WgManagerConfig` class also loads a `MinimumLogLevel` string from `config.json`. Your application can parse this string and set `WgLogging.MinimumLogLevel` accordingly:
```csharp
// Example: In your application startup after loading WgManagerConfig
var libConfig = await WgManagerConfig.LoadAsync("path/to/your/config.json"); // Or default path
if (!string.IsNullOrWhiteSpace(libConfig.MinimumLogLevel) &&
    Enum.TryParse<LogLevel>(libConfig.MinimumLogLevel, true, out LogLevel configuredLevel))
{
    WgLogging.MinimumLogLevel = configuredLevel;
}

// Example: Configure console color output (optional, defaults to true)
// This assumes ConsoleLoggingProvider.UseConsoleColors is settable,
// and WgManagerConfig.LoadAsync sets it from enableConsoleColors in config.json
// ConsoleLoggingProvider.UseConsoleColors = libConfig.EnableConsoleColors;
```

The `config.json` file can control these:
```json
{
  "allowSystemdManagement": false,
  "systemdServicePath": "/etc/systemd/system",
  "wgPath": null,
  "wgQuickPath": null,
  "systemctlPath": null,
  "wireguardConfigDirectory": "/etc/wireguard",
  "minimumLogLevel": "Info",
  "enableConsoleColors": true,
  "defaultShortOperationTimeoutSeconds": 15,
  "defaultLongOperationTimeoutSeconds": 60
}
```
*   `defaultShortOperationTimeoutSeconds` (integer, optional): Default timeout in seconds for quick external commands (e.g., `wg show`, `wg genkey`, `systemctl is-enabled`). Defaults to 15 seconds if not set.
*   `defaultLongOperationTimeoutSeconds` (integer, optional): Default timeout in seconds for potentially longer external commands (e.g., `wg-quick up/down`, `systemctl enable/daemon-reload`). Defaults to 60 seconds if not set.

Individual methods that call external tools may also accept an explicit `TimeSpan? timeout` parameter, which will override these configured defaults if provided.

**Replacing the Logger:**

To integrate with your application's logging framework (e.g., `Microsoft.Extensions.Logging`, Serilog), create a class that implements `IWgLoggingProvider` and then assign an instance of it to `WgLogging.Logger`.

**Example: Adapter for `Microsoft.Extensions.Logging.ILogger`**
```csharp
using Microsoft.Extensions.Logging; // NuGet: Microsoft.Extensions.Logging.Abstractions
using WireGuardManager.Utilities;
using System;

public class MelLoggingProvider : IWgLoggingProvider
{
    private readonly ILogger _logger;

    public MelLoggingProvider(ILogger logger)
    {
        _logger = logger;
    }

    public void LogTrace(string message) => _logger.LogTrace(message);
    public void LogDebug(string message) => _logger.LogDebug(message);
    public void LogInfo(string message) => _logger.LogInformation(message);
    public void LogWarning(string message) => _logger.LogWarning(message);
    public void LogError(string message, Exception? ex = null) => _logger.LogError(ex, message);
}

// In your application setup (e.g., after configuring ILogger):
// ILogger myAppLogger = ... ; // Get your ILogger instance (e.g., from DI)
// WgLogging.Logger = new MelLoggingProvider(myAppLogger);
```

## Contributing

Contributions are welcome! Please feel free to submit issues or pull requests.
