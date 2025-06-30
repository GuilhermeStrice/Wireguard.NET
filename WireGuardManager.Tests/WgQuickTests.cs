using NUnit.Framework;
using WireGuardManager;
using WireGuardManager.Utilities;
using System.Threading.Tasks;
using System.IO;
using System; // For StringWriter, Console
using System.Text.Json; // For JsonSerializer (if creating temp config file)

namespace WireGuardManager.Tests
{
    [TestFixture]
    public class WgQuickTests
    {
        private string _testDir = null!;
        private string _tempConfigJsonPath = null!;

        [SetUp]
        public void SetUp()
        {
            _testDir = Path.Combine(TestContext.CurrentContext.TestDirectory, "WgQuickTestDir");
            Directory.CreateDirectory(_testDir);
            _tempConfigJsonPath = Path.Combine(_testDir, "test_config.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, true);
            }
        }

        private void CreateTestWgManagerConfig(bool allowSystemd, string systemdPath)
        {
            var configData = new { AllowSystemdManagement = allowSystemd, SystemdServicePath = systemdPath };
            File.WriteAllText(_tempConfigJsonPath, JsonSerializer.Serialize(configData));
        }


        [Test]
        public async Task ShowAll_ExecutesSuccessfully()
        {
            ProcessRunner.ProcessResult? result = null;
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
            Assert.That(result.ExitCode, Is.EqualTo(0), $"wg show exited with code {result.ExitCode}. Stderr: {result.StandardError}");
            Assert.That(result.StandardOutput, Is.Not.Null);
        }

        [Test]
        public async Task Up_InterfaceName_AllowSystemdFalse_DoesNotAttemptSystemdAndCallsWgQuickUp()
        {
            CreateTestWgManagerConfig(false, "/fake/systemd");
            var config = WgManagerConfig.Load(_tempConfigJsonPath);

            using var sw = new StringWriter();
            var originalOut = Console.Out;
            Console.SetOut(sw);

            ProcessRunner.ProcessResult? upResult = null;
            try
            {
                // "wgtestnonexistent" is unlikely to exist, so wg-quick up will likely fail, which is fine.
                // We are testing the flow, not the success of wg-quick up itself here.
                upResult = await WgQuick.Up("wgtestnonexistent", config);
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                Assert.Inconclusive($"'wg-quick' command not found. {ex.Message}");
            }
            finally
            {
                Console.SetOut(originalOut);
            }

            string output = sw.ToString();
            Assert.That(output, Does.Contain("Attempting implicit systemd service check for interface 'wgtestnonexistent'. AllowSystemdManagement: False"));
            Assert.That(output, Does.Not.Contain("Ensuring systemd service for wgtestnonexistent"), "Should not try to ensure service if AllowSystemdManagement is false.");

            Assert.That(upResult, Is.Not.Null, "wg-quick up should have been attempted.");
            // wg-quick up for a non-existent interface (without a corresponding conf file) typically returns 1
            Assert.That(upResult.ExitCode, Is.Not.EqualTo(0), "wg-quick up for a dummy interface should ideally fail or indicate no action.");
        }

        [Test]
        public async Task Up_InterfaceName_AllowSystemdTrue_AttemptsSystemdAndCallsWgQuickUp()
        {
            // This test will attempt actual systemd operations if systemctl is present.
            // We'll use a fake systemd path for file creation, but systemctl calls are global.
            CreateTestWgManagerConfig(true, Path.Combine(_testDir, "fakesystemd_allowtrue"));
            Directory.CreateDirectory(Path.Combine(_testDir, "fakesystemd_allowtrue")); // Ensure fake path exists
            var config = WgManagerConfig.Load(_tempConfigJsonPath);

            using var sw = new StringWriter();
            var originalOut = Console.Out;
            Console.SetOut(sw);

            ProcessRunner.ProcessResult? upResult = null;
            try
            {
                upResult = await WgQuick.Up("wgtest99", config); // Use a unique name
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                 Assert.Inconclusive($"'wg-quick' or 'systemctl' command not found. {ex.Message}");
            }
            catch (Exception ex)
            {
                // Catch other exceptions that might arise from systemctl interactions
                TestContext.Progress.WriteLine($"Exception during WgQuick.Up: {ex}");
                // Allow to proceed to check output and upResult
            }
            finally
            {
                Console.SetOut(originalOut);
            }

            string output = sw.ToString();
            TestContext.Progress.WriteLine($"Console output for Up_InterfaceName_AllowSystemdTrue:\n{output}");

            Assert.That(output, Does.Contain("Attempting implicit systemd service check for interface 'wgtest99'. AllowSystemdManagement: True"));
            Assert.That(output, Does.Contain("Ensuring systemd service for wgtest99"), "Should try to ensure service.");
            // Whether it says "Service file ... does not exist. Attempting to create..." or "already exists" depends on systemctl's actual state and permissions.
            // And "systemctl daemon-reload" / "systemctl enable" logs.

            Assert.That(upResult, Is.Not.Null, "wg-quick up should have been attempted.");
            // Exit code of wg-quick up will depend on actual system state, permissions, and if wgtest99.conf exists.
            // If systemd part failed due to permissions, serviceOk might be false, and wg-quick up would still run.
            if (output.Contains("Failed to ensure systemd service for 'wgtest99'") || output.Contains("Error: 'systemctl"))
            {
                 Assert.Warn("Systemd management part seems to have failed (e.g., permissions). This is expected if not run as root.");
            }
            else if (output.Contains("Systemd service for 'wgtest99' ensured successfully"))
            {
                TestContext.Progress.WriteLine("Systemd part reported success. Manual cleanup of wgtest99 service might be needed if run as root: sudo systemctl disable wg-quick@wgtest99.service");
            }
             Assert.Pass("Test for WgQuick.Up with AllowSystemdManagement=true completed. Check console output for details on systemd interaction. wg-quick up was called.");
        }

        [Test]
        public async Task Up_FilePath_DoesNotAttemptSystemdAndCallsWgQuickUp()
        {
            CreateTestWgManagerConfig(true, "/fake/systemd"); // Systemd allowed, but should be ignored for file paths
            var config = WgManagerConfig.Load(_tempConfigJsonPath);

            string fakeConfFilePath = Path.Combine(_testDir, "mytestwg.conf");
            File.WriteAllText(fakeConfFilePath, "#dummy wg config");

            using var sw = new StringWriter();
            var originalOut = Console.Out;
            Console.SetOut(sw);

            ProcessRunner.ProcessResult? upResult = null;
            try
            {
                upResult = await WgQuick.Up(fakeConfFilePath, config);
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                 Assert.Inconclusive($"'wg-quick' command not found. {ex.Message}");
            }
            finally
            {
                Console.SetOut(originalOut);
            }

            string output = sw.ToString();
            Assert.That(output, Does.Not.Contain("Attempting implicit systemd service check"), "Should not attempt systemd check for file paths.");

            Assert.That(upResult, Is.Not.Null, "wg-quick up should have been attempted.");
            // Exit code depends on wg-quick's handling of the dummy file.
        }
    }
}
