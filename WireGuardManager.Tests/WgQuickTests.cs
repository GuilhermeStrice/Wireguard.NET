using NUnit.Framework;
using WireGuardManager;
using WireGuardManager.Utilities;
using System.Threading.Tasks;
using System.IO;

namespace WireGuardManager.Tests
{
    [TestFixture]
    public class WgQuickTests
    {
        // These tests interact with 'wg' and 'wg-quick' commands.
        // Some may require specific setup or permissions to run successfully.

        [Test]
        public async Task ShowAll_ExecutesSuccessfully()
        {
            ProcessRunner.ProcessResult result = null;
            try
            {
                result = await WgQuick.ShowAll();
            }
            catch (System.ComponentModel.Win32Exception ex) // Catches "file not found" for 'wg'
            {
                Assert.Inconclusive($"'wg' command is likely not installed or not in PATH: {ex.Message}");
            }
            catch (System.Exception ex) // Other exceptions during process execution
            {
                 Assert.Inconclusive($"Failed to execute 'wg show'. 'wg' command might not be available or an error occurred: {ex.Message}");
            }

            Assert.That(result, Is.Not.Null, "ProcessResult should not be null.");
            // 'wg show' with no interfaces up/configured might return exit code 0 and empty stdout,
            // or exit code 1 if it considers "no interfaces" an error state for 'show all'.
            // Typically, it returns 0 even if no interfaces exist.
            // If 'wg' is not installed, ProcessRunner should ideally throw.
            Assert.That(result.ExitCode, Is.EqualTo(0), $"wg show exited with code {result.ExitCode}. Stderr: {result.StandardError}");
            // StandardOutput can be empty if no interfaces are configured.
            Assert.That(result.StandardOutput, Is.Not.Null);
        }

        // To properly test SyncConf, SetConf, Up, Down, we would need:
        // 1. A dummy interface or a way to manage WireGuard interfaces in a test environment (e.g., network namespaces).
        // 2. Root privileges or CAP_NET_ADMIN capability for the test runner.
        // This is complex for automated unit/integration tests without a dedicated test environment.
        // For now, we'll focus on what can be tested with minimal privilege.

        // Example of how a SyncConf test *might* look (requires setup):
        /*
        [Test]
        [Explicit("Requires WireGuard interface 'wgtest0' and root/CAP_NET_ADMIN")]
        public async Task SyncConf_AppliesConfiguration()
        {
            // Arrange: Create a dummy interface 'wgtest0' (e.g., using ip link add wgtest0 type wireguard)
            // Ensure user has rights: sudo setcap cap_net_admin+eip $(which wg) OR run tests as root.

            string interfaceName = "wgtest0"; // A pre-existing or test-created interface
            var serverKey = await WgKeyManager.GenerateKeyPairAsync(); // Requires wg
            var serverConfig = new WgServerConfig(serverKey.PrivateKey) { ListenPort = 51899 }; // Use a unique port
            var wgConfig = new WgConfig(serverConfig);

            var tempConfigFile = Path.GetTempFileName();
            try
            {
                wgConfig.ToFile(tempConfigFile);

                // Act
                var result = await WgQuick.SyncConf(interfaceName, tempConfigFile);

                // Assert
                Assert.That(result.Success, Is.True, $"SyncConf failed: {result.StandardError}");

                // Optionally, verify with 'wg show wgtest0'
                var showResult = await WgQuick.Show(interfaceName);
                Assert.That(showResult.Success, Is.True);
                Assert.That(showResult.StandardOutput, Does.Contain(serverKey.PublicKey.Substring(0,10))); // Check if public key part is shown
                Assert.That(showResult.StandardOutput, Does.Contain("listening port: 51899"));
            }
            finally
            {
                if (File.Exists(tempConfigFile)) File.Delete(tempConfigFile);
                // Clean up dummy interface: sudo ip link del wgtest0
            }
        }
        */
    }
}
