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
            WgManagerConfig.FileSystemProvider = new StandardFileSystem(); // For WgSystemdManager loading its own config for paths
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
    }
}
