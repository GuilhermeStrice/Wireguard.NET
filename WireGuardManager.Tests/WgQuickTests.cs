using NUnit.Framework;
using WireGuardManager;
using WireGuardManager.Utilities;
using WireGuardManager.Exceptions; // Added
using System.Threading.Tasks;
using System.IO;
using System;
using System.Text.Json;

namespace WireGuardManager.Tests
{
    [TestFixture]
    public class WgQuickTests
    {
        private string _testDir = null!;
        private string _tempConfigJsonPath = null!;
        private string _dummyConfFilePath = null!;

        [OneTimeSetUp]
        public void CheckCommands()
        {
            try { ProcessRunner.RunAsync("wg", "--version").Wait(); } // Check for wg
            catch { Assert.Inconclusive("'wg' command not found or not working. Skipping WgQuickTests."); }
            try { ProcessRunner.RunAsync("wg-quick", "--version").Wait(); } // Check for wg-quick
            catch { Assert.Inconclusive("'wg-quick' command not found or not working. Skipping WgQuickTests."); }
        }


        [SetUp]
        public void SetUp()
        {
            _testDir = Path.Combine(TestContext.CurrentContext.TestDirectory, "WgQuickTestDir");
            Directory.CreateDirectory(_testDir);
            _tempConfigJsonPath = Path.Combine(_testDir, "test_config.json");
            _dummyConfFilePath = Path.Combine(_testDir, "dummy.conf");
            // Create a minimal valid .conf file for tests that need one for wg-quick to not complain about file missing
            File.WriteAllText(_dummyConfFilePath, "[Interface]\nPrivateKey = AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=\nAddress = 10.0.0.1/24\n");
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
            var result = await WgQuick.ShowAll();
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Success, Is.True, $"wg show all failed. Stderr: {result.StandardError}");
        }

        [Test]
        public void Show_InvalidInterfaceName_ThrowsInvalidInputException()
        {
            var ex = Assert.ThrowsAsync<InvalidInputException>(() => WgQuick.Show("invalid!!name"));
            Assert.That(ex.Message, Does.Contain("Invalid interface name format"));
        }

        [Test]
        public void SyncConf_NonExistentFile_ThrowsFileNotFoundException()
        {
            Assert.ThrowsAsync<FileNotFoundException>(() => WgQuick.SyncConf("wg0", "nonexistent.conf"));
        }

        [Test]
        public void Up_InvalidInterfaceName_WhenCheckingSystemd_ThrowsInvalidInputException()
        {
            CreateTestWgManagerConfig(true, "/fake/systemd"); // Allow systemd to trigger interface name validation path
            var config = WgManagerConfig.Load(_tempConfigJsonPath);

            var ex = Assert.ThrowsAsync<InvalidInputException>(() => WgQuick.Up("invalid!!name", config));
            // The exception might come from WgSystemdManager if IsValidInterfaceName is called there first
            Assert.That(ex.Message, Does.Contain("Invalid interface name format"));
        }

        [Test]
        public async Task Up_InterfaceName_AllowSystemdFalse_SkipsSystemdAndCallsWgQuickUp()
        {
            CreateTestWgManagerConfig(false, Path.Combine(_testDir, "fakesystemd_disabled"));
            var config = WgManagerConfig.Load(_tempConfigJsonPath);

            using var sw = new StringWriter();
            Console.SetOut(sw); // Capture console output

            // Expect ExternalToolException because "wgtest_no_sysd" likely doesn't exist as a file or service.
            Assert.ThrowsAsync<ExternalToolException>(async () => await WgQuick.Up("wgtest_no_sysd", config));

            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()){AutoFlush = true}); // Restore console
            string output = sw.ToString();

            Assert.That(output, Does.Contain("Attempting implicit systemd service check for interface 'wgtest_no_sysd'. AllowSystemdManagement: False"));
            Assert.That(output, Does.Not.Contain("Ensuring systemd service for"), "Should not try to ensure service.");
        }

        [Test]
        public void Up_InterfaceName_AllowSystemdTrue_AttemptsSystemd()
        {
            CreateTestWgManagerConfig(true, Path.Combine(_testDir, "fakesystemd_enabled"));
            Directory.CreateDirectory(Path.Combine(_testDir, "fakesystemd_enabled")); // Ensure fake path exists for file creation
            var config = WgManagerConfig.Load(_tempConfigJsonPath);

            using var sw = new StringWriter();
            Console.SetOut(sw);

            // This will likely throw ExternalToolException from wg-quick up (interface not found)
            // or potentially PermissionsException/ExternalToolException from WgSystemdManager if systemctl fails.
            // The goal is to check the log output.
            try
            {
                 WgQuick.Up("wgtest_sysd_attempt", config).Wait(); // Use unique name
            }
            catch (AggregateException ae) when (ae.InnerExceptions.Any(e => e is ExternalToolException || e is PermissionsException ))
            { /* Expected if wg-quick or systemctl fails */ }
            catch (ExternalToolException) { /* Expected */ }
            catch (PermissionsException) { /* Expected */ }

            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()){AutoFlush = true});
            string output = sw.ToString();
            TestContext.Progress.WriteLine($"Console output for Up_InterfaceName_AllowSystemdTrue:\n{output}");

            Assert.That(output, Does.Contain("Attempting implicit systemd service check for interface 'wgtest_sysd_attempt'. AllowSystemdManagement: True"));
            Assert.That(output, Does.Contain("Ensuring systemd service for wgtest_sysd_attempt"), "Should try to ensure service.");

            if (output.Contains("Permission error during systemd management") || output.Contains("External tool error during systemd management"))
            {
                 Assert.Warn("Systemd management part seems to have failed (e.g., permissions or systemctl not functional). This is expected if not run as root or systemctl is problematic.");
            }
        }

        [Test]
        public void Up_FilePath_SkipsSystemd()
        {
            CreateTestWgManagerConfig(true, "/fake/systemd"); // Systemd allowed, but should be ignored
            var config = WgManagerConfig.Load(_tempConfigJsonPath);

            using var sw = new StringWriter();
            Console.SetOut(sw);

            // Expect ExternalToolException because dummy.conf might not be a fully working config for `wg-quick up`
            Assert.ThrowsAsync<ExternalToolException>(async () => await WgQuick.Up(_dummyConfFilePath, config));

            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()){AutoFlush = true});
            string output = sw.ToString();
            Assert.That(output, Does.Not.Contain("Attempting implicit systemd service check"), "Should not attempt systemd check for file paths.");
        }

        [Test]
        public void Save_InvalidInterfaceName_ThrowsInvalidInputException()
        {
            var ex = Assert.ThrowsAsync<InvalidInputException>(() => WgQuick.Save("invalid!!name.conf"));
            Assert.That(ex.Message, Does.Contain("Invalid interface name format"));
        }
    }
}
