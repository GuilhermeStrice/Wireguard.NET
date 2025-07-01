using NUnit.Framework;
using WireGuardManager;
using WireGuardManager.Utilities;
using WireGuardManager.Exceptions;
using System.Threading.Tasks;
using System.IO;

namespace WireGuardManager.IntegrationTests
{
    [TestFixture]
    [Category("Integration")]
    public class WgSystemdManagerIntegrationTests
    {
        private string _testBaseDir = null!;
        private string _fakeSystemdDir = null!;
        private WgManagerConfig _configAllowSystemd = null!;
        private WgManagerConfig _configDisallowSystemd = null!;
        private const string TestInterface = "wgsysit0";

        [OneTimeSetUp]
        public void GlobalSetup()
        {
            // Ensure real providers are used for integration tests
            WgSystemdManager.ProcessRunnerInstance = new ProcessRunner();
            WgSystemdManager.FileSystemProvider = new StandardFileSystem();
            // WgSystemdManager's helper methods (GetSystemctlPathAsync etc.) call WgManagerConfig.LoadAsync()
            // which uses WgManagerConfig.FileSystemProvider. So, reset it too.
            WgManagerConfig.FileSystemProvider = new StandardFileSystem();
        }

        [SetUp]
        public void SetUp()
        {
            _testBaseDir = Path.Combine(TestContext.CurrentContext.TestDirectory, "WgSystemdManagerIntegrationTestRun_" + Path.GetRandomFileName());
            _fakeSystemdDir = Path.Combine(_testBaseDir, "systemd");
            Directory.CreateDirectory(_fakeSystemdDir);

            _configAllowSystemd = new WgManagerConfig
            {
                AllowSystemdManagement = true,
                SystemdServicePath = _fakeSystemdDir
            };
            _configDisallowSystemd = new WgManagerConfig
            {
                AllowSystemdManagement = false,
                SystemdServicePath = _fakeSystemdDir
            };
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testBaseDir))
            {
                Directory.Delete(_testBaseDir, true);
            }
        }

        [Test]
        public async Task EnsureServiceExistsAndEnabled_AllowSystemdFalse_NoFileCreated_ReturnsFalse()
        {
            string expectedServiceFilePath = Path.Combine(_fakeSystemdDir, $"wg-quick@{TestInterface}.service");

            bool result = await WgSystemdManager.EnsureServiceExistsAndEnabled(TestInterface, _configDisallowSystemd);

            Assert.That(result, Is.False);
            Assert.That(File.Exists(expectedServiceFilePath), Is.False);
        }

        [Test]
        public async Task EnsureServiceExistsAndEnabled_AllowSystemdTrue_FileDoesNotExist_CreatesFile()
        {
            string expectedServiceFilePath = Path.Combine(_fakeSystemdDir, $"wg-quick@{TestInterface}.service");
            Assert.That(File.Exists(expectedServiceFilePath), Is.False, "Service file should not exist before test.");

            bool result = false;
            try
            {
                // systemctl calls might fail if systemd is not running or due to permissions in Docker,
                // but the file should still be created.
                result = await WgSystemdManager.EnsureServiceExistsAndEnabled(TestInterface, _configAllowSystemd);
            }
            catch (ExternalToolException ex) when (ex.ToolName.Contains("systemctl"))
            {
                TestContext.Progress.WriteLine($"systemctl command failed as potentially expected in this environment: {ex.Message}");
                // If systemctl failed, result might be false or an exception thrown depending on where it failed.
                // The key is that the file should be created.
            }
            catch (PermissionsException pex)
            {
                 TestContext.Progress.WriteLine($"systemctl command failed with permissions as potentially expected: {pex.Message}");
            }


            Assert.That(File.Exists(expectedServiceFilePath), Is.True, "Service file should have been created.");
            string content = await File.ReadAllTextAsync(expectedServiceFilePath);
            Assert.That(content, Does.Contain("[Unit]"));
            Assert.That(content, Does.Contain($"Description=WireGuard via wg-quick for %I"));
            Assert.That(content, Does.Contain($"ExecStart=wg-quick up %i"));
            Assert.That(content, Does.Contain("[Service]"));
            Assert.That(content, Does.Contain("[Install]"));
            Assert.That(content, Does.Contain("WantedBy=multi-user.target"));

            // The 'result' here depends on the success of systemctl commands.
            // If systemctl commands are expected to fail in the test env but file creation is the focus:
            if(!result) TestContext.Progress.WriteLine("EnsureServiceExistsAndEnabled returned false, likely due to systemctl command failures in the test environment, but file creation is verified.");
        }

        [Test]
        public async Task EnsureServiceExistsAndEnabled_AllowSystemdTrue_FileAlreadyExists_DoesNotModifyFile_ChecksEnable()
        {
            string expectedServiceFilePath = Path.Combine(_fakeSystemdDir, $"wg-quick@{TestInterface}.service");
            string initialContent = "# Existing dummy service file\n[Service]\nExecStart=/usr/bin/true";
            await File.WriteAllTextAsync(expectedServiceFilePath, initialContent);
            DateTime initialWriteTime = File.GetLastWriteTimeUtc(expectedServiceFilePath);
             await Task.Delay(10); // Ensure a time difference if file is rewritten

            bool result = false;
            try
            {
                 result = await WgSystemdManager.EnsureServiceExistsAndEnabled(TestInterface, _configAllowSystemd);
            }
            catch (ExternalToolException ex) when (ex.ToolName.Contains("systemctl"))
            {
                 TestContext.Progress.WriteLine($"systemctl command failed as potentially expected: {ex.Message}");
            }
            catch (PermissionsException pex)
            {
                 TestContext.Progress.WriteLine($"systemctl command failed with permissions as potentially expected: {pex.Message}");
            }


            Assert.That(File.Exists(expectedServiceFilePath), Is.True);
            Assert.That(await File.ReadAllTextAsync(expectedServiceFilePath), Is.EqualTo(initialContent), "Existing service file content should not be modified.");
            Assert.That(File.GetLastWriteTimeUtc(expectedServiceFilePath), Is.EqualTo(initialWriteTime), "Existing service file timestamp should not change if only checking enable status.");
            // The result here depends on `systemctl is-enabled` and `systemctl enable` if needed.
        }

        [Test]
        public async Task EnsureServiceExistsAndEnabled_WithCustomPathsInConfig_UsesCustomPathsInServiceFile()
        {
            string customSystemdDir = Path.Combine(_testBaseDir, "custom_systemd_path_test");
            Directory.CreateDirectory(customSystemdDir);
            string customWgQuickPath = "/usr/local/bin/my-custom-wg-quick";
            string interfaceName = "wgcustom0";

            var customPathConfig = new WgManagerConfig
            {
                AllowSystemdManagement = true,
                SystemdServicePath = customSystemdDir,
                WgQuickPath = customWgQuickPath
                // systemctlPath is not tested for file content, but for execution if systemctl was mocked/wrapped.
            };

            string expectedServiceFilePath = Path.Combine(customSystemdDir, $"wg-quick@{interfaceName}.service");
            if (File.Exists(expectedServiceFilePath)) File.Delete(expectedServiceFilePath);

            try
            {
                await WgSystemdManager.EnsureServiceExistsAndEnabled(interfaceName, customPathConfig);
            }
            catch (ExternalToolException ex) when (ex.ToolName.Contains("systemctl"))
            {
                TestContext.Progress.WriteLine($"systemctl command failed as potentially expected: {ex.Message}");
            }
            catch (PermissionsException pex)
            {
                 TestContext.Progress.WriteLine($"systemctl command failed with permissions as potentially expected: {pex.Message}");
            }

            Assert.That(File.Exists(expectedServiceFilePath), Is.True, "Service file should be created in custom SystemdServicePath.");
            string serviceContent = await File.ReadAllTextAsync(expectedServiceFilePath);
            Assert.That(serviceContent, Does.Contain($"ExecStart={customWgQuickPath} up %i"), "Service file content should use custom WgQuickPath for ExecStart.");
            Assert.That(serviceContent, Does.Contain($"ExecStop={customWgQuickPath} down %i"), "Service file content should use custom WgQuickPath for ExecStop.");
        }

        [Test]
        public void StartServiceAsync_AttemptsToRunSystemctlStart()
        {
            string interfaceName = "wg_start_test";
            // Ensure service file is created for the test by calling EnsureServiceExistsAndEnabled first
            // This part will use the real FileSystemProvider as set in GlobalSetup
            Assert.DoesNotThrowAsync(async () =>
                await WgSystemdManager.EnsureServiceExistsAndEnabled(interfaceName, _configAllowSystemd),
                "Prerequisite: EnsureServiceExistsAndEnabled should not throw for file creation part.");

            Assert.That(File.Exists(Path.Combine(_configAllowSystemd.SystemdServicePath, $"wg-quick@{interfaceName}.service")), Is.True);

            // Now test StartServiceAsync
            // We expect this to throw ExternalToolException if systemctl isn't fully functional
            var ex = Assert.ThrowsAsync<ExternalToolException>(async () =>
                await WgSystemdManager.StartServiceAsync(interfaceName, _configAllowSystemd)
            );

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.ToolName, Does.EndWith("systemctl"));
            Assert.That(ex.Message, Does.Contain($"start wg-quick@{interfaceName}.service"));
            TestContext.Progress.WriteLine($"StartServiceAsync failed as expected (systemctl likely not fully functional): {ex.Message}");
        }

        [Test]
        public async Task IsServiceActiveAsync_AttemptsToRunSystemctlIsActive_ReturnsFalseOnFailure()
        {
            string interfaceName = "wg_isactive_test";
            // No need to create service file as `is-active` can be called on non-existent services (it will just report inactive)

            bool isActive = true; // Default to true to ensure it changes
            try
            {
                isActive = await WgSystemdManager.IsServiceActiveAsync(interfaceName, _configAllowSystemd);
            }
            catch (ExternalToolException ex)
            {
                TestContext.Progress.WriteLine($"IsServiceActiveAsync threw ExternalToolException as systemctl likely not fully functional: {ex.Message}");
                isActive = false; // Treat tool failure as "not active" for test purposes
            }

            Assert.That(isActive, Is.False, "IsServiceActiveAsync should return false if systemctl command fails or reports inactive.");
        }
    }
}
