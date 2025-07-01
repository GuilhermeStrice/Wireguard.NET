using NUnit.Framework;
using WireGuardManager;
using WireGuardManager.Utilities;
using WireGuardManager.Exceptions; // Added
using System.IO;
using System.Threading.Tasks;
using System; // For ArgumentNullException

namespace WireGuardManager.Tests
{
using NUnit.Framework;
using WireGuardManager;
using WireGuardManager.Utilities;
using WireGuardManager.Exceptions;
using Moq; // Added
using System.IO;
using System.Threading.Tasks;
using System;

namespace WireGuardManager.Tests
{
    [TestFixture]
    public class WgSystemdManagerTests
    {
        private Mock<IProcessRunner> _mockProcessRunner = null!;
        private Mock<IFileSystem> _mockFileSystem = null!;
        private WgManagerConfig _configAllowSystemd = null!;
        private WgManagerConfig _configDisallowSystemd = null!;
        private const string TestInterfaceName = "wgtest0";
        private string _testSystemdServiceDir = null!; // For mocked service files
        private string _expectedServiceFilePath = null!;

        [SetUp]
        public void SetUp()
        {
            _mockProcessRunner = new Mock<IProcessRunner>();
            _mockFileSystem = new Mock<IFileSystem>();

            WgSystemdManager.ProcessRunnerInstance = _mockProcessRunner.Object;
            WgSystemdManager.FileSystemProvider = _mockFileSystem.Object;

            // For WgManagerConfig.LoadAsync calls within WgSystemdManager if config isn't passed directly (e.g. GetSystemctlPathAsync)
            // We need to ensure WgManagerConfig.FileSystemProvider is also the mock if we rely on its LoadAsync.
            // However, WgSystemdManager methods take WgManagerConfig as a parameter, so we can control it directly.
            // WgManagerConfig.FileSystemProvider = _mockFileSystem.Object; // Not strictly needed if config is always passed

            _testSystemdServiceDir = Path.Combine(TestContext.CurrentContext.TestDirectory, "fake_systemd_services_for_WgSystemdManagerTests");
            // No need to Directory.CreateDirectory(_testSystemdServiceDir) as FileSystemProvider is mocked.

            _configAllowSystemd = new WgManagerConfig { AllowSystemdManagement = true, SystemdServicePath = _testSystemdServiceDir };
            _configDisallowSystemd = new WgManagerConfig { AllowSystemdManagement = false, SystemdServicePath = _testSystemdServiceDir };
            _expectedServiceFilePath = Path.Combine(_testSystemdServiceDir, $"wg-quick@{TestInterfaceName}.service");
        }

        [TearDown]
        public void TearDown()
        {
            WgSystemdManager.ProcessRunnerInstance = new ProcessRunner();
            WgSystemdManager.FileSystemProvider = new StandardFileSystem();
            // WgManagerConfig.FileSystemProvider = new StandardFileSystem();
        }

        [Test]
        public void EnsureServiceExistsAndEnabled_NullConfig_ThrowsArgumentNullException()
        {
            Assert.ThrowsAsync<ArgumentNullException>(() => WgSystemdManager.EnsureServiceExistsAndEnabled(TestInterfaceName, null!));
        }

        [Test]
        public void EnsureServiceExistsAndEnabled_InvalidInterfaceName_ThrowsInvalidInputException()
        {
             var ex = Assert.ThrowsAsync<InvalidInputException>(() => WgSystemdManager.EnsureServiceExistsAndEnabled("invalid!!name", _configAllowSystemd));
             Assert.That(ex.Message, Does.Contain("Invalid interface name format"));
        }

        [Test]
        public async Task EnsureServiceExistsAndEnabled_SystemdDisabledInConfig_ReturnsFalseAndNoAction()
        {
            var result = await WgSystemdManager.EnsureServiceExistsAndEnabled(TestInterfaceName, _configDisallowSystemd);
            Assert.That(result, Is.False);
            _mockFileSystem.Verify(fs => fs.FileExists(It.IsAny<string>()), Times.Never);
            _mockProcessRunner.Verify(pr => pr.RunAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()), Times.Never);
        }

        [Test]
        public async Task EnsureServiceExistsAndEnabled_ServiceFileAlreadyExists_ReturnsTrueAndSkipsFileWriteAndDaemonReload()
        {
            _mockFileSystem.Setup(fs => fs.FileExists(_expectedServiceFilePath)).Returns(true);
            _mockProcessRunner.Setup(p => p.RunAsync(It.Is<string>(s => s.EndsWith("systemctl")), $"is-enabled wg-quick@{TestInterfaceName}.service", null, It.IsAny<TimeSpan?>()))
                              .ReturnsAsync(new ProcessExecutionResult(0, "enabled", "")); // Simulate already enabled

            var result = await WgSystemdManager.EnsureServiceExistsAndEnabled(TestInterfaceName, _configAllowSystemd);

            Assert.That(result, Is.True);
            _mockFileSystem.Verify(fs => fs.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _mockProcessRunner.Verify(pr => pr.RunAsync(It.Is<string>(s => s.EndsWith("systemctl")), "daemon-reload", null, It.IsAny<TimeSpan?>()), Times.Never);
            _mockProcessRunner.Verify(pr => pr.RunAsync(It.Is<string>(s => s.EndsWith("systemctl")), $"enable wg-quick@{TestInterfaceName}.service", null, It.IsAny<TimeSpan?>()), Times.Never); // Should not call enable if already enabled
        }

        [Test]
        public async Task EnsureServiceExistsAndEnabled_ServiceNotEnabled_EnablesService()
        {
            _mockFileSystem.Setup(fs => fs.FileExists(_expectedServiceFilePath)).Returns(true); // File exists
            _mockProcessRunner.Setup(p => p.RunAsync(It.Is<string>(s => s.EndsWith("systemctl")), $"is-enabled wg-quick@{TestInterfaceName}.service", null, It.IsAny<TimeSpan?>()))
                              .ReturnsAsync(new ProcessExecutionResult(1, "disabled", "")); // Simulate disabled
            _mockProcessRunner.Setup(p => p.RunAsync(It.Is<string>(s => s.EndsWith("systemctl")), $"enable wg-quick@{TestInterfaceName}.service", null, It.IsAny<TimeSpan?>()))
                              .ReturnsAsync(new ProcessExecutionResult(0, "enabled", "")); // Simulate successful enable

            var result = await WgSystemdManager.EnsureServiceExistsAndEnabled(TestInterfaceName, _configAllowSystemd);
            Assert.That(result, Is.True);
            _mockProcessRunner.Verify(pr => pr.RunAsync(It.Is<string>(s => s.EndsWith("systemctl")), $"enable wg-quick@{TestInterfaceName}.service", null, It.IsAny<TimeSpan?>()), Times.Once);
        }


        [Test]
        public async Task EnsureServiceExistsAndEnabled_CreatesServiceFile_RunsDaemonReloadAndEnable_WhenAllowedAndNeeded()
        {
            _mockFileSystem.Setup(fs => fs.FileExists(_expectedServiceFilePath)).Returns(false); // File does not exist
            _mockFileSystem.Setup(fs => fs.WriteAllTextAsync(_expectedServiceFilePath, It.IsAny<string>())).Returns(Task.CompletedTask);

            _mockProcessRunner.Setup(p => p.RunAsync(It.Is<string>(s => s.EndsWith("systemctl")), "daemon-reload", null, It.IsAny<TimeSpan?>()))
                              .ReturnsAsync(new ProcessExecutionResult(0, "", ""));
            _mockProcessRunner.Setup(p => p.RunAsync(It.Is<string>(s => s.EndsWith("systemctl")), $"is-enabled wg-quick@{TestInterfaceName}.service", null, It.IsAny<TimeSpan?>()))
                              .ReturnsAsync(new ProcessExecutionResult(1, "disabled", "")); // Simulate disabled
            _mockProcessRunner.Setup(p => p.RunAsync(It.Is<string>(s => s.EndsWith("systemctl")), $"enable wg-quick@{TestInterfaceName}.service", null, It.IsAny<TimeSpan?>()))
                              .ReturnsAsync(new ProcessExecutionResult(0, "", ""));

            var result = await WgSystemdManager.EnsureServiceExistsAndEnabled(TestInterfaceName, _configAllowSystemd);

            Assert.That(result, Is.True);
            _mockFileSystem.Verify(fs => fs.WriteAllTextAsync(_expectedServiceFilePath, It.IsAny<string>()), Times.Once);
            _mockProcessRunner.Verify(pr => pr.RunAsync(It.Is<string>(s => s.EndsWith("systemctl")), "daemon-reload", null, It.IsAny<TimeSpan?>()), Times.Once);
            _mockProcessRunner.Verify(pr => pr.RunAsync(It.Is<string>(s => s.EndsWith("systemctl")), $"enable wg-quick@{TestInterfaceName}.service", null, It.IsAny<TimeSpan?>()), Times.Once);
        }

        [Test]
        public void EnsureServiceExistsAndEnabled_FileWriteFails_ThrowsPermissionsException()
        {
            _mockFileSystem.Setup(fs => fs.FileExists(_expectedServiceFilePath)).Returns(false);
            _mockFileSystem.Setup(fs => fs.WriteAllTextAsync(_expectedServiceFilePath, It.IsAny<string>()))
                           .ThrowsAsync(new UnauthorizedAccessException("Cannot write"));

            var ex = Assert.ThrowsAsync<PermissionsException>(() =>
                WgSystemdManager.EnsureServiceExistsAndEnabled(TestInterfaceName, _configAllowSystemd)
            );
            Assert.That(ex.Operation, Is.EqualTo("write systemd service file"));
            Assert.That(ex.Resource, Is.EqualTo(_expectedServiceFilePath));
        }

        [Test]
        public void EnsureServiceExistsAndEnabled_DaemonReloadFails_ThrowsExternalToolException()
        {
            _mockFileSystem.Setup(fs => fs.FileExists(_expectedServiceFilePath)).Returns(false);
            _mockFileSystem.Setup(fs => fs.WriteAllTextAsync(_expectedServiceFilePath, It.IsAny<string>())).Returns(Task.CompletedTask);
            _mockProcessRunner.Setup(p => p.RunAsync(It.Is<string>(s => s.EndsWith("systemctl")), "daemon-reload", null, It.IsAny<TimeSpan?>()))
                              .ReturnsAsync(new ProcessExecutionResult(1, "", "daemon reload failed"));

            var ex = Assert.ThrowsAsync<ExternalToolException>(() =>
                WgSystemdManager.EnsureServiceExistsAndEnabled(TestInterfaceName, _configAllowSystemd)
            );
            Assert.That(ex.ToolName, Does.EndWith("systemctl"));
            Assert.That(ex.Message, Does.Contain("daemon-reload"));
        }

        [Test]
        public void EnsureServiceExistsAndEnabled_EnableFails_ThrowsExternalToolException()
        {
            _mockFileSystem.Setup(fs => fs.FileExists(_expectedServiceFilePath)).Returns(true); // File exists
            _mockProcessRunner.Setup(p => p.RunAsync(It.Is<string>(s => s.EndsWith("systemctl")), $"is-enabled wg-quick@{TestInterfaceName}.service", null, It.IsAny<TimeSpan?>()))
                              .ReturnsAsync(new ProcessExecutionResult(1, "disabled", "")); // Simulate disabled
            _mockProcessRunner.Setup(p => p.RunAsync(It.Is<string>(s => s.EndsWith("systemctl")), $"enable wg-quick@{TestInterfaceName}.service", null, It.IsAny<TimeSpan?>()))
                              .ReturnsAsync(new ProcessExecutionResult(127, "", "enable command failed")); // Simulate failed enable

            var ex = Assert.ThrowsAsync<ExternalToolException>(() =>
                WgSystemdManager.EnsureServiceExistsAndEnabled(TestInterfaceName, _configAllowSystemd)
            );
            Assert.That(ex.ToolName, Does.EndWith("systemctl"));
            Assert.That(ex.Message, Does.Contain("enable wg-quick@wgtest0.service"));
        }

        // --- Timeout Behavior Tests ---
        [Test]
        public void EnsureServiceExistsAndEnabled_UsesConfiguredTimeoutsForSystemctl()
        {
            // Test that systemctl daemon-reload and enable use DefaultLongOperationTimeoutSeconds from config
            // And systemctl is-enabled uses DefaultShortOperationTimeoutSeconds from config

            var configWithTimeouts = new WgManagerConfig {
                AllowSystemdManagement = true,
                SystemdServicePath = _testSystemdServiceDir,
                DefaultShortOperationTimeoutSeconds = 3,
                DefaultLongOperationTimeoutSeconds = 7
            };
            TimeSpan expectedLongTimeout = TimeSpan.FromSeconds(7);
            TimeSpan expectedShortTimeout = TimeSpan.FromSeconds(3);

            _mockFileSystem.Setup(fs => fs.FileExists(_expectedServiceFilePath)).Returns(false); // Ensure file creation + daemon-reload
            _mockFileSystem.Setup(fs => fs.WriteAllTextAsync(_expectedServiceFilePath, It.IsAny<string>())).Returns(Task.CompletedTask);

            _mockProcessRunner.Setup(p => p.RunAsync(It.Is<string>(s => s.EndsWith("systemctl")), "daemon-reload", null, expectedLongTimeout))
                              .ReturnsAsync(new ProcessExecutionResult(0, "", ""));
            _mockProcessRunner.Setup(p => p.RunAsync(It.Is<string>(s => s.EndsWith("systemctl")), $"is-enabled wg-quick@{TestInterfaceName}.service", null, expectedShortTimeout))
                              .ReturnsAsync(new ProcessExecutionResult(1, "disabled", "")); // Simulate disabled
            _mockProcessRunner.Setup(p => p.RunAsync(It.Is<string>(s => s.EndsWith("systemctl")), $"enable wg-quick@{TestInterfaceName}.service", null, expectedLongTimeout))
                              .ReturnsAsync(new ProcessExecutionResult(0, "", ""));

            Assert.DoesNotThrowAsync(async () => await WgSystemdManager.EnsureServiceExistsAndEnabled(TestInterfaceName, configWithTimeouts));

            _mockProcessRunner.Verify(p => p.RunAsync(It.Is<string>(s => s.EndsWith("systemctl")), "daemon-reload", null, expectedLongTimeout), Times.Once);
            _mockProcessRunner.Verify(p => p.RunAsync(It.Is<string>(s => s.EndsWith("systemctl")), $"is-enabled wg-quick@{TestInterfaceName}.service", null, expectedShortTimeout), Times.Once);
            _mockProcessRunner.Verify(p => p.RunAsync(It.Is<string>(s => s.EndsWith("systemctl")), $"enable wg-quick@{TestInterfaceName}.service", null, expectedLongTimeout), Times.Once);
        }

        [Test]
        public void EnsureServiceExistsAndEnabled_UsesExplicitTimeoutOverride()
        {
            var configWithDefaults = new WgManagerConfig {
                AllowSystemdManagement = true,
                SystemdServicePath = _testSystemdServiceDir,
                DefaultShortOperationTimeoutSeconds = 3, // These should be overridden
                DefaultLongOperationTimeoutSeconds = 7
            };
            TimeSpan explicitTimeout = TimeSpan.FromSeconds(11); // Explicitly passed timeout

            _mockFileSystem.Setup(fs => fs.FileExists(_expectedServiceFilePath)).Returns(false);
            _mockFileSystem.Setup(fs => fs.WriteAllTextAsync(_expectedServiceFilePath, It.IsAny<string>())).Returns(Task.CompletedTask);

            _mockProcessRunner.Setup(p => p.RunAsync(It.Is<string>(s => s.EndsWith("systemctl")), "daemon-reload", null, explicitTimeout))
                              .ReturnsAsync(new ProcessExecutionResult(0, "", ""));
            _mockProcessRunner.Setup(p => p.RunAsync(It.Is<string>(s => s.EndsWith("systemctl")), $"is-enabled wg-quick@{TestInterfaceName}.service", null, explicitTimeout)) // is-enabled also gets the explicit overall timeout
                              .ReturnsAsync(new ProcessExecutionResult(1, "disabled", ""));
            _mockProcessRunner.Setup(p => p.RunAsync(It.Is<string>(s => s.EndsWith("systemctl")), $"enable wg-quick@{TestInterfaceName}.service", null, explicitTimeout))
                              .ReturnsAsync(new ProcessExecutionResult(0, "", ""));

            Assert.DoesNotThrowAsync(async () => await WgSystemdManager.EnsureServiceExistsAndEnabled(TestInterfaceName, configWithDefaults, explicitTimeout));

            _mockProcessRunner.VerifyAll();
        }
    }
}
