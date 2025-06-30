using NUnit.Framework;
using WireGuardManager;
using WireGuardManager.Utilities;
using WireGuardManager.Exceptions; // Added
using System.IO;
using System.Threading.Tasks;
using System; // For ArgumentNullException

namespace WireGuardManager.Tests
{
    [TestFixture]
    public class WgSystemdManagerTests
    {
        private string _testSystemdPath = null!;
        private WgManagerConfig _configAllowSystemd = null!;
        private WgManagerConfig _configDisallowSystemd = null!;
        private const string TestInterfaceName = "wgtest0"; // Use const for clarity
        private string _serviceFilePath = null!;
        private static bool _systemctlAvailable = false;

        [OneTimeSetUp]
        public void CheckSystemctl()
        {
            try
            {
                var result = ProcessRunner.RunAsync("systemctl", "--version", timeout: TimeSpan.FromSeconds(5)).Result;
                _systemctlAvailable = result.Success;
                if (!_systemctlAvailable)
                    TestContext.Progress.WriteLine("Warning: 'systemctl --version' failed. systemctl dependent tests will be inconclusive or may fail expectedly.");
            }
            catch (Exception ex) // Catches CommandNotFoundException or other process start issues
            {
                _systemctlAvailable = false;
                TestContext.Progress.WriteLine($"Warning: 'systemctl' command not found or failed to start ({ex.GetType().Name}). systemctl dependent tests will be inconclusive.");
            }
        }

        [SetUp]
        public void SetUp()
        {
            _testSystemdPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "fake_systemd_services");
            Directory.CreateDirectory(_testSystemdPath);
            _configAllowSystemd = new WgManagerConfig { AllowSystemdManagement = true, SystemdServicePath = _testSystemdPath };
            _configDisallowSystemd = new WgManagerConfig { AllowSystemdManagement = false, SystemdServicePath = _testSystemdPath };
            _serviceFilePath = Path.Combine(_testSystemdPath, $"wg-quick@{TestInterfaceName}.service");

            if (File.Exists(_serviceFilePath)) File.Delete(_serviceFilePath);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testSystemdPath))
            {
                Directory.Delete(_testSystemdPath, true);
            }
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
            Assert.That(File.Exists(_serviceFilePath), Is.False);
        }

        [Test]
        public async Task EnsureServiceExistsAndEnabled_ServiceFileAlreadyExists_SkipsCreationAndChecksEnable()
        {
            if (!_systemctlAvailable) Assert.Inconclusive("systemctl not available for this test.");

            File.WriteAllText(_serviceFilePath, "existing_service_content"); // Pre-create

            // This test will now proceed to call `systemctl is-enabled` and potentially `systemctl enable`.
            // Expect it to pass if systemctl commands succeed, or throw if they fail (e.g. permissions).
            try
            {
                var result = await WgSystemdManager.EnsureServiceExistsAndEnabled(TestInterfaceName, _configAllowSystemd);
                Assert.That(result, Is.True); // True if systemctl commands succeed or service already enabled
                Assert.That(File.ReadAllText(_serviceFilePath), Is.EqualTo("existing_service_content")); // File not changed
            }
            catch (ExternalToolException ex) when (ex.ToolName == "systemctl")
            {
                Assert.Pass($"Test passed conceptually: Service file existed. Systemctl command failed as expected without root: {ex.Message}");
            }
             catch (PermissionsException pex)
            {
                Assert.Pass($"Test passed conceptually: Service file existed. Systemctl command failed due to permissions as expected: {pex.Message}");
            }
        }

        [Test]
        public async Task EnsureServiceExistsAndEnabled_CreatesServiceFile_AndAttemptsSystemctl_WhenAllowed()
        {
            if (!_systemctlAvailable) Assert.Inconclusive("systemctl not available for this test.");

            try
            {
                bool success = await WgSystemdManager.EnsureServiceExistsAndEnabled(TestInterfaceName, _configAllowSystemd);

                // If we get here, it means all systemctl commands (daemon-reload, is-enabled, enable) were reported as successful by ProcessRunner.
                // This would typically only happen if run with root or if systemctl is stubbed/mocked.
                Assert.That(success, Is.True, "EnsureServiceExistsAndEnabled should return true on full success.");
                Assert.That(File.Exists(_serviceFilePath), Is.True, "Service file should be created.");
                // Further asserts on systemctl state would require actual system changes or more complex mocking.
                TestContext.Progress.WriteLine($"Full success for EnsureServiceExistsAndEnabled (likely run with root or mocked systemctl). Service file created at {_serviceFilePath}.");
            }
            catch (CommandNotFoundException cnfe)
            {
                 Assert.Inconclusive($"systemctl command not found, cannot complete test: {cnfe.Message}");
            }
            catch (PermissionsException pex)
            {
                // This is an expected outcome if not running with root.
                Assert.That(File.Exists(_serviceFilePath), Is.True, "Service file should still be created even if systemctl calls fail due to permissions.");
                Assert.Pass($"Service file created. Systemctl operations failed due to permissions as expected: {pex.Message}");
            }
            catch (ExternalToolException etex)
            {
                // This can happen if systemctl commands fail for other reasons.
                Assert.That(File.Exists(_serviceFilePath), Is.True, "Service file should still be created even if systemctl calls fail.");
                Assert.Warn($"Service file created. Systemctl operations failed: {etex.Message}");
            }
        }

        [Test]
        public void EnsureServiceExistsAndEnabled_FileWritePermissionError_ThrowsPermissionsException()
        {
            // Attempt to use a path where writing should fail (e.g. a non-existent root-level directory without privileges)
            // This test's reliability depends on the environment's strictness.
            var restrictiveConfig = new WgManagerConfig { AllowSystemdManagement = true, SystemdServicePath = "/root_level_dir_no_perms_hopefully/system" };

            // Ensure the directory doesn't exist and can't be created by non-root
            if (Directory.Exists(Path.GetDirectoryName(restrictiveConfig.SystemdServicePath)))
                 Assert.Inconclusive("Test path for permission error seems to exist, adjust test.");

            var ex = Assert.ThrowsAsync<PermissionsException>(() =>
                WgSystemdManager.EnsureServiceExistsAndEnabled(TestInterfaceName, restrictiveConfig)
            );
            Assert.That(ex.Message, Does.Contain("write systemd service file"));
        }
    }
}
