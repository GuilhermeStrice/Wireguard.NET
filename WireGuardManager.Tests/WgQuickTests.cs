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
        private Mock<IProcessRunner> _mockProcessRunner = null!;
        private Mock<IFileSystem> _mockFileSystem = null!;

        // For WgSystemdManager's dependencies, as WgQuick calls it.
        private Mock<IProcessRunner> _mockSystemdProcessRunner = null!;
        private Mock<IFileSystem> _mockSystemdFileSystem = null!;

        private string _testDir = null!;
        private string _tempConfigJsonPath = null!; // For WgManagerConfig.LoadAsync
        private string _dummyConfFilePath = null!;


        [SetUp]
        public void SetUp()
        {
            _mockProcessRunner = new Mock<IProcessRunner>();
            _mockFileSystem = new Mock<IFileSystem>();
            _mockSystemdProcessRunner = new Mock<IProcessRunner>();
            _mockSystemdFileSystem = new Mock<IFileSystem>();

            WgQuick.ProcessRunnerInstance = _mockProcessRunner.Object;
            WgQuick.FileSystemProvider = _mockFileSystem.Object;

            // Setup mocks for WgSystemdManager's static providers as WgQuick calls it directly
            WgSystemdManager.ProcessRunnerInstance = _mockSystemdProcessRunner.Object;
            WgSystemdManager.FileSystemProvider = _mockSystemdFileSystem.Object;

            // Setup mock for WgManagerConfig's static FileSystemProvider
            // This allows us to control what WgManagerConfig.LoadAsync() reads
            _testDir = Path.Combine(TestContext.CurrentContext.TestDirectory, "WgQuickTest_ConfigDir");
            Directory.CreateDirectory(_testDir); // Ensure it exists for Path.Combine
            _tempConfigJsonPath = Path.Combine(_testDir, "config.json"); // Standard name WgManagerConfig.LoadAsync looks for
            WgManagerConfig.FileSystemProvider = _mockFileSystem.Object; // WgManagerConfig uses its own static provider

            _dummyConfFilePath = Path.Combine(_testDir, "dummy.conf");
            // Don't write real files anymore, mock FileSystemProvider.FileExists for paths if needed
        }

        [TearDown]
        public void TearDown()
        {
            // Reset static providers to avoid interference between test fixtures
            WgQuick.ProcessRunnerInstance = new ProcessRunner();
            WgQuick.FileSystemProvider = new StandardFileSystem();
            WgSystemdManager.ProcessRunnerInstance = new ProcessRunner();
            WgSystemdManager.FileSystemProvider = new StandardFileSystem();
            WgManagerConfig.FileSystemProvider = new StandardFileSystem();

            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, true);
            }
        }

        private void SetupMockWgManagerConfig(bool allowSystemd, string systemdPath, string? wgPath = null, string? wgQuickPath = null, string? systemctlPath = null)
        {
            var configData = new {
                AllowSystemdManagement = allowSystemd,
                SystemdServicePath = systemdPath,
                WgPath = wgPath,
                WgQuickPath = wgQuickPath,
                SystemctlPath = systemctlPath
                // WireguardConfigDirectory will use default if not specified here
            };
            string jsonConfig = JsonSerializer.Serialize(configData);
            // _tempConfigJsonPath is where WgManagerConfig.LoadAsync will look if no path is specified to it.
            // WgQuick methods load it like: customConfig ?? await WgManagerConfig.LoadAsync();
            // So we need to mock what WgManagerConfig.FileSystemProvider.ReadAllTextAsync returns for _tempConfigJsonPath
            _mockFileSystem.Setup(fs => fs.FileExists(_tempConfigJsonPath)).Returns(true);
            _mockFileSystem.Setup(fs => fs.ReadAllTextAsync(_tempConfigJsonPath)).ReturnsAsync(jsonConfig);
        }


        [Test]
        public async Task ShowAll_CallsWgShowCorrectly()
        {
            SetupMockWgManagerConfig(false, "/fake/systemd"); // Config for tool path resolution
            _mockProcessRunner.Setup(p => p.RunAsync("wg", "show", null, It.IsAny<TimeSpan?>()))
                              .ReturnsAsync(new ProcessExecutionResult(0, "wg show output", ""));

            var result = await WgQuick.ShowAll(); // Will use internal WgManagerConfig.LoadAsync

            Assert.That(result.Success, Is.True);
            Assert.That(result.StandardOutput, Is.EqualTo("wg show output"));
            _mockProcessRunner.Verify(p => p.RunAsync("wg", "show", null, It.IsAny<TimeSpan?>()), Times.Once);
        }

        [Test]
        public async Task Show_CallsWgShowInterfaceCorrectly()
        {
            SetupMockWgManagerConfig(false, "/fake/systemd");
            string interfaceName = "wg0";
            _mockProcessRunner.Setup(p => p.RunAsync("wg", $"show \"{interfaceName}\"", null, It.IsAny<TimeSpan?>()))
                              .ReturnsAsync(new ProcessExecutionResult(0, $"output for {interfaceName}", ""));

            var result = await WgQuick.Show(interfaceName);
            Assert.That(result.Success, Is.True);
             _mockProcessRunner.Verify(p => p.RunAsync("wg", $"show \"{interfaceName}\"", null, It.IsAny<TimeSpan?>()), Times.Once);
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
            SetupMockWgManagerConfig(false, "/fake/systemd");
            _mockFileSystem.Setup(fs => fs.FileExists("nonexistent.conf")).Returns(false);
            Assert.ThrowsAsync<FileNotFoundException>(() => WgQuick.SyncConf("wg0", "nonexistent.conf"));
        }

        [Test]
        public async Task SyncConf_ValidFile_CallsWgSyncConf()
        {
            SetupMockWgManagerConfig(false, "/fake/systemd", wgPath: "custom_wg");
            string interfaceName = "wg0";
            string confPath = _dummyConfFilePath; // Use a path we know
            _mockFileSystem.Setup(fs => fs.FileExists(confPath)).Returns(true);
            _mockProcessRunner.Setup(p => p.RunAsync("custom_wg", $"syncconf \"{interfaceName}\" \"{confPath}\"", null, It.IsAny<TimeSpan?>()))
                              .ReturnsAsync(new ProcessExecutionResult(0, "sync success", ""));

            var result = await WgQuick.SyncConf(interfaceName, confPath);
            Assert.That(result.Success, Is.True);
            _mockProcessRunner.Verify(p => p.RunAsync("custom_wg", $"syncconf \"{interfaceName}\" \"{confPath}\"", null, It.IsAny<TimeSpan?>()), Times.Once);
        }


        [Test]
        public void Up_InvalidInterfaceName_WhenCheckingSystemd_ThrowsInvalidInputException()
        {
            // Setup config that would allow systemd management to ensure IsValidInterfaceName is hit in that path.
            SetupMockWgManagerConfig(true, "/fake/systemd");

            var ex = Assert.ThrowsAsync<InvalidInputException>(() => WgQuick.Up("invalid!!name"));
            Assert.That(ex.Message, Does.Contain("Invalid interface name format"));
        }

        [Test]
        public async Task Up_InterfaceName_AllowSystemdFalse_SkipsSystemdAndCallsWgQuickUp()
        {
            SetupMockWgManagerConfig(false, Path.Combine(_testDir, "fakesystemd_disabled"), wgQuickPath: "custom_wg-quick");

            using var sw = new StringWriter();
            Console.SetOut(sw);

            _mockProcessRunner.Setup(p => p.RunAsync("custom_wg-quick", "up \"wgtest_no_sysd\"", null, It.IsAny<TimeSpan?>()))
                              .ReturnsAsync(new ProcessExecutionResult(1, "", "interface not found or similar")); // Mock failure of wg-quick itself

            Assert.ThrowsAsync<ExternalToolException>(async () => await WgQuick.Up("wgtest_no_sysd"));

            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()){AutoFlush = true});
            string output = sw.ToString();

            Assert.That(output, Does.Contain("Attempting implicit systemd service check for interface 'wgtest_no_sysd'. AllowSystemdManagement: False"));
            Assert.That(output, Does.Not.Contain("Ensuring systemd service for"), "Should not try to ensure service.");
            _mockProcessRunner.Verify(p => p.RunAsync("custom_wg-quick", "up \"wgtest_no_sysd\"", null, It.IsAny<TimeSpan?>()), Times.Once);
        }

        [Test]
        public async Task Up_InterfaceName_AllowSystemdTrue_AttemptsSystemdAndCallsWgQuickUp()
        {
            string interfaceName = "wg_sysd_true";
            string fakeSystemdPath = Path.Combine(_testDir, "fakesystemd_enabled_true");
            SetupMockWgManagerConfig(true, fakeSystemdPath, wgQuickPath: "custom_wg-quick", systemctlPath: "custom_systemctl");

            // Mock WgSystemdManager's FileSystemProvider to control service file existence check
            _mockSystemdFileSystem.Setup(fs => fs.FileExists(Path.Combine(fakeSystemdPath, $"wg-quick@{interfaceName}.service"))).Returns(false);
            _mockSystemdFileSystem.Setup(fs => fs.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

            // Mock WgSystemdManager's ProcessRunnerInstance for systemctl calls
            _mockSystemdProcessRunner.Setup(p => p.RunAsync("custom_systemctl", "daemon-reload", null, It.IsAny<TimeSpan?>()))
                                     .ReturnsAsync(new ProcessExecutionResult(0, "", ""));
            _mockSystemdProcessRunner.Setup(p => p.RunAsync("custom_systemctl", $"is-enabled wg-quick@{interfaceName}.service", null, It.IsAny<TimeSpan?>()))
                                     .ReturnsAsync(new ProcessExecutionResult(1, "disabled", "")); // Simulate service is initially disabled
            _mockSystemdProcessRunner.Setup(p => p.RunAsync("custom_systemctl", $"enable wg-quick@{interfaceName}.service", null, It.IsAny<TimeSpan?>()))
                                     .ReturnsAsync(new ProcessExecutionResult(0, "", ""));

            // Mock WgQuick's own ProcessRunnerInstance for the final wg-quick up call
            _mockProcessRunner.Setup(p => p.RunAsync("custom_wg-quick", $"up \"{interfaceName}\"", null, It.IsAny<TimeSpan?>()))
                              .ReturnsAsync(new ProcessExecutionResult(0, "Successfully up", ""));


            using var sw = new StringWriter();
            Console.SetOut(sw);

            var result = await WgQuick.Up(interfaceName);

            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()){AutoFlush = true});
            string output = sw.ToString();

            Assert.That(result.Success, Is.True);
            Assert.That(output, Does.Contain($"Attempting implicit systemd service check for interface '{interfaceName}'. AllowSystemdManagement: True"));
            Assert.That(output, Does.Contain($"Ensuring systemd service for {interfaceName}"));
            Assert.That(output, Does.Contain("Successfully wrote service file"));
            Assert.That(output, Does.Contain("Successfully enabled service"));

            _mockSystemdFileSystem.Verify(fs => fs.WriteAllTextAsync(Path.Combine(fakeSystemdPath, $"wg-quick@{interfaceName}.service"), It.IsAny<string>()), Times.Once);
            _mockSystemdProcessRunner.Verify(p => p.RunAsync("custom_systemctl", "daemon-reload", null, It.IsAny<TimeSpan?>()), Times.Once);
            _mockSystemdProcessRunner.Verify(p => p.RunAsync("custom_systemctl", $"enable wg-quick@{interfaceName}.service", null, It.IsAny<TimeSpan?>()), Times.Once);
            _mockProcessRunner.Verify(p => p.RunAsync("custom_wg-quick", $"up \"{interfaceName}\"", null, It.IsAny<TimeSpan?>()), Times.Once);
        }

        [Test]
        public async Task Up_FilePath_SkipsSystemdAndCallsWgQuickUp()
        {
            SetupMockWgManagerConfig(true, "/fake/systemd", wgQuickPath: "path_wg-quick"); // Systemd allowed, but should be ignored

            _mockFileSystem.Setup(fs => fs.FileExists(_dummyConfFilePath)).Returns(true); // Assume conf file exists
            _mockProcessRunner.Setup(p => p.RunAsync("path_wg-quick", $"up \"{_dummyConfFilePath}\"", null, It.IsAny<TimeSpan?>()))
                              .ReturnsAsync(new ProcessExecutionResult(0, "up success from path", ""));

            using var sw = new StringWriter();
            Console.SetOut(sw);

            var result = await WgQuick.Up(_dummyConfFilePath);

            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()){AutoFlush = true});
            string output = sw.ToString();

            Assert.That(result.Success, Is.True);
            Assert.That(output, Does.Not.Contain("Attempting implicit systemd service check"), "Should not attempt systemd check for file paths.");
            _mockProcessRunner.Verify(p => p.RunAsync("path_wg-quick", $"up \"{_dummyConfFilePath}\"", null, It.IsAny<TimeSpan?>()), Times.Once);
        }

        [Test]
        public void Save_InvalidInterfaceName_ThrowsInvalidInputException()
        {
            var ex = Assert.ThrowsAsync<InvalidInputException>(() => WgQuick.Save("invalid!!name.conf"));
            Assert.That(ex.Message, Does.Contain("Invalid interface name format"));
        }

        // --- SetPeerAsync Unit Tests ---
        [Test]
        public async Task SetPeerAsync_WithPresharedKeyFile_FormsCorrectCommand()
        {
            SetupMockWgManagerConfig(false, "/fake/systemd", wgPath: "wg_mock");
            var options = new WgPeerUpdateOptions { PresharedKeyFile = "/path/to/psk.key" };
            _mockProcessRunner.Setup(p => p.RunAsync("wg_mock", It.Is<string>(s => s.Contains("preshared-key \"/path/to/psk.key\"")), null, It.IsAny<TimeSpan?>()))
                              .ReturnsAsync(new ProcessExecutionResult(0, "", ""));

            await WgQuick.SetPeerAsync("wg0", AnotherPeerPublicKey, options);
            _mockProcessRunner.Verify();
        }

        [Test]
        public async Task SetPeerAsync_WithPresharedKeyString_UsesTempFileAndFormsCorrectCommand()
        {
            SetupMockWgManagerConfig(false, "/fake/systemd", wgPath: "wg_mock");
            string pskString = "PRESHARED_KEY_STRING_FOR_TESTINGAAAAAAAAAAA=";
            var options = new WgPeerUpdateOptions { PresharedKey = pskString };

            _mockFileSystem.Setup(fs => fs.GetTempFileName()).Returns("temp_psk.txt");
            _mockFileSystem.Setup(fs => fs.WriteAllTextAsync("temp_psk.txt", pskString)).Returns(Task.CompletedTask);
            _mockFileSystem.Setup(fs => fs.FileExists("temp_psk.txt")).Returns(true); // For deletion check
            _mockFileSystem.Setup(fs => fs.DeleteFile("temp_psk.txt"));

            _mockProcessRunner.Setup(p => p.RunAsync("wg_mock", It.Is<string>(s => s.Contains("preshared-key \"temp_psk.txt\"")), null, It.IsAny<TimeSpan?>()))
                              .ReturnsAsync(new ProcessExecutionResult(0, "", ""));

            await WgQuick.SetPeerAsync("wg0", AnotherPeerPublicKey, options);

            _mockFileSystem.Verify(fs => fs.GetTempFileName(), Times.Once);
            _mockFileSystem.Verify(fs => fs.WriteAllTextAsync("temp_psk.txt", pskString), Times.Once);
            _mockProcessRunner.Verify(); // Verifies the RunAsync call with the specific command part
            _mockFileSystem.Verify(fs => fs.DeleteFile("temp_psk.txt"), Times.Once);
        }

        [Test]
        public async Task SetPeerAsync_WithPresharedKeyOff_FormsCorrectCommand()
        {
            SetupMockWgManagerConfig(false, "/fake/systemd", wgPath: "wg_mock");
            var options = new WgPeerUpdateOptions { PresharedKey = "off" };
             _mockProcessRunner.Setup(p => p.RunAsync("wg_mock", It.Is<string>(s => s.Contains("preshared-key off")), null, It.IsAny<TimeSpan?>()))
                              .ReturnsAsync(new ProcessExecutionResult(0, "", ""));

            await WgQuick.SetPeerAsync("wg0", AnotherPeerPublicKey, options);
            _mockProcessRunner.Verify();
        }

        [Test]
        public void SetPeerAsync_InvalidPresharedKeyString_ThrowsInvalidInputException()
        {
            SetupMockWgManagerConfig(false, "/fake/systemd");
            var options = new WgPeerUpdateOptions { PresharedKey = "not_a_valid_key_or_off" };
            var ex = Assert.ThrowsAsync<InvalidInputException>(() => WgQuick.SetPeerAsync("wg0", AnotherPeerPublicKey, options));
            Assert.That(ex.Message, Does.Contain("Invalid PresharedKey string format"));
        }

        // --- SetInterfaceFwMarkAsync Unit Tests ---
        [TestCase("wg0", "12345", "set \"wg0\" fwmark \"12345\"")]
        [TestCase("wg-north", "0xABC", "set \"wg-north\" fwmark \"0xABC\"")]
        [TestCase("wg1", "off", "set \"wg1\" fwmark \"off\"")]
        [TestCase("wg2", null, "set \"wg2\" fwmark \"off\"")] // Null should default to "off"
        [TestCase("wg3", "  ", "set \"wg3\" fwmark \"off\"")]  // Whitespace should default to "off"
        public async Task SetInterfaceFwMarkAsync_FormsCorrectCommand(string interfaceName, string? fwmarkInput, string expectedCommandArgs)
        {
            SetupMockWgManagerConfig(false, "/fake/systemd", wgPath: "wg_mocked");
            _mockProcessRunner.Setup(p => p.RunAsync("wg_mocked", expectedCommandArgs, null, It.IsAny<TimeSpan?>()))
                              .ReturnsAsync(new ProcessExecutionResult(0, "success", ""));

            await WgQuick.SetInterfaceFwMarkAsync(interfaceName, fwmarkInput);

            _mockProcessRunner.Verify(p => p.RunAsync("wg_mocked", expectedCommandArgs, null, It.IsAny<TimeSpan?>()), Times.Once);
        }

        [Test]
        public void SetInterfaceFwMarkAsync_InvalidInterfaceName_ThrowsInvalidInputException()
        {
            SetupMockWgManagerConfig(false, "/fake/systemd");
            var ex = Assert.ThrowsAsync<InvalidInputException>(() => WgQuick.SetInterfaceFwMarkAsync("!!invalid", "123"));
            Assert.That(ex.ParamName, Is.EqualTo("interfaceName"));
        }

        [Test]
        public void SetInterfaceFwMarkAsync_WgSetFails_ThrowsExternalToolException()
        {
            SetupMockWgManagerConfig(false, "/fake/systemd", wgPath: "wg_mocked_fail");
            _mockProcessRunner.Setup(p => p.RunAsync("wg_mocked_fail", It.IsAny<string>(), null, It.IsAny<TimeSpan?>()))
                              .ReturnsAsync(new ProcessExecutionResult(1, "", "fwmark error"));

            var ex = Assert.ThrowsAsync<ExternalToolException>(() => WgQuick.SetInterfaceFwMarkAsync("wg0", "123"));
            Assert.That(ex.ToolName, Is.EqualTo("wg_mocked_fail"));
            Assert.That(ex.StandardError, Is.EqualTo("fwmark error"));
        }

        // --- Timeout Behavior Tests ---
        [Test]
        public async Task ShowAll_WithExplicitTimeout_UsesExplicitTimeout()
        {
            SetupMockWgManagerConfig(false, "/fake/systemd", wgPath: "wg_mock_timeout");
            var explicitTimeout = TimeSpan.FromSeconds(3);
            _mockProcessRunner.Setup(p => p.RunAsync("wg_mock_timeout", "show", null, explicitTimeout))
                              .ReturnsAsync(new ProcessExecutionResult(0, "output", ""));

            await WgQuick.ShowAll(null, explicitTimeout); // Pass explicit timeout, null config (will load default)
            _mockProcessRunner.Verify();
        }

        [Test]
        public async Task ShowAll_WithConfiguredTimeout_UsesConfiguredTimeout()
        {
            SetupMockWgManagerConfig(false, "/fake/systemd", wgPath: "wg_mock_cfg_timeout", defaultShortSeconds: 7);
            var expectedTimeout = TimeSpan.FromSeconds(7);
             // Config object is loaded internally by ShowAll when customConfig is null
            _mockProcessRunner.Setup(p => p.RunAsync("wg_mock_cfg_timeout", "show", null, expectedTimeout))
                              .ReturnsAsync(new ProcessExecutionResult(0, "output", ""));

            await WgQuick.ShowAll(null, null); // No explicit timeout, should pick from config
            _mockProcessRunner.Verify();
        }

        [Test]
        public async Task ShowAll_WithNoExplicitOrConfiguredTimeout_UsesStaticDefault()
        {
            // Setup config with null for timeout to ensure static default is used
            SetupMockWgManagerConfig(false, "/fake/systemd", wgPath: "wg_mock_static_timeout", defaultShortSeconds: null);
            var expectedTimeout = Utilities.ProcessRunner.DefaultShortOperationTimeout;
            _mockProcessRunner.Setup(p => p.RunAsync("wg_mock_static_timeout", "show", null, expectedTimeout))
                              .ReturnsAsync(new ProcessExecutionResult(0, "output", ""));

            await WgQuick.ShowAll(null, null);
            _mockProcessRunner.Verify();
        }

        // Helper method for setting up WgManagerConfig mock for timeout tests
        private void SetupMockWgManagerConfig(bool allowSystemd, string systemdPath, string? wgPath = null, string? wgQuickPath = null, string? systemctlPath = null, int? defaultShortSeconds = null, int? defaultLongSeconds = null)
        {
            var configData = new {
                AllowSystemdManagement = allowSystemd,
                SystemdServicePath = systemdPath,
                WgPath = wgPath,
                WgQuickPath = wgQuickPath,
                SystemctlPath = systemctlPath,
                DefaultShortOperationTimeoutSeconds = defaultShortSeconds,
                DefaultLongOperationTimeoutSeconds = defaultLongSeconds
            };
            string jsonConfig = JsonSerializer.Serialize(configData);
            _mockFileSystem.Setup(fs => fs.FileExists(_tempConfigJsonPath)).Returns(true);
            _mockFileSystem.Setup(fs => fs.ReadAllTextAsync(_tempConfigJsonPath)).ReturnsAsync(jsonConfig);
        }
    }
}
