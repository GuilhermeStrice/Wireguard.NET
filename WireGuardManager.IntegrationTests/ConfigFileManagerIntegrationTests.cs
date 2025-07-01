using NUnit.Framework;
using WireGuardManager;
using WireGuardManager.Utilities; // For ValidationUtils
using WireGuardManager.Exceptions;
using System.Threading.Tasks;
using System.IO;

namespace WireGuardManager.IntegrationTests
{
    [TestFixture]
    [Category("Integration")]
    public class ConfigFileManagerIntegrationTests
    {
        private string _testBaseDir = null!;
        private string _sourceConfDir = null!;
        private string _deployConfDir = null!;
        private WgManagerConfig _wgManagerConfig = null!;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // Ensure we are using real file system for these integration tests
            WgConfigFileManager.FileSystemProvider = new StandardFileSystem();
            WgManagerConfig.FileSystemProvider = new StandardFileSystem(); // If WgConfigFileManager loads its own config
        }

        [SetUp]
        public void SetUp()
        {
            // Using a unique directory for each test run within TestContext.CurrentContext.TestDirectory
            // which is usually inside the container's /app/tests/bin/Release/net6.0/
            _testBaseDir = Path.Combine(TestContext.CurrentContext.TestDirectory, "ConfigFileManagerTestRun_" + Path.GetRandomFileName());
            _sourceConfDir = Path.Combine(_testBaseDir, "source_configs");
            _deployConfDir = Path.Combine(_testBaseDir, "deployed_configs");

            Directory.CreateDirectory(_sourceConfDir);
            Directory.CreateDirectory(_deployConfDir);

            _wgManagerConfig = new WgManagerConfig
            {
                WireguardConfigDirectory = _deployConfDir,
                // other paths can be default as they are not used by WgConfigFileManager directly
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

        private string CreateDummySourceConfFile(string interfaceName, string content = "[Interface]\nPrivateKey=DummyKeyIntegrationTest=================\nAddress=10.1.1.1/24")
        {
            string sourcePath = Path.Combine(_sourceConfDir, $"{interfaceName}.conf");
            File.WriteAllText(sourcePath, content);
            return sourcePath;
        }

        [Test]
        public async Task DeployConfigAsync_ValidSourceAndNewDestination_DeploysFile()
        {
            string interfaceName = "wg_it_deploy0";
            string sourceFile = CreateDummySourceConfFile(interfaceName);
            string expectedDestFile = Path.Combine(_deployConfDir, $"{interfaceName}.conf");

            await WgConfigFileManager.DeployConfigAsync(sourceFile, interfaceName, _wgManagerConfig, false);

            Assert.That(File.Exists(expectedDestFile), Is.True, "Deployed config file should exist.");
            Assert.That(File.ReadAllText(expectedDestFile), Is.EqualTo(File.ReadAllText(sourceFile)), "Deployed file content should match source.");
        }

        [Test]
        public async Task DeployConfigAsync_DestinationExists_OverwriteFalse_ThrowsException()
        {
            string interfaceName = "wg_it_deploy1";
            string sourceFile = CreateDummySourceConfFile(interfaceName, "Source Content V1");
            string expectedDestFile = Path.Combine(_deployConfDir, $"{interfaceName}.conf");

            // Create dummy destination file
            File.WriteAllText(expectedDestFile, "Existing Destination Content");

            var ex = Assert.ThrowsAsync<WireGuardManagerException>(() =>
                WgConfigFileManager.DeployConfigAsync(sourceFile, interfaceName, _wgManagerConfig, false) // overwrite = false
            );
            Assert.That(ex.Message, Does.Contain("already exists and overwrite is false"));
            Assert.That(File.ReadAllText(expectedDestFile), Is.EqualTo("Existing Destination Content"), "Destination file should not be overwritten.");
        }

        [Test]
        public async Task DeployConfigAsync_DestinationExists_OverwriteTrue_OverwritesFile()
        {
            string interfaceName = "wg_it_deploy2";
            string sourceContent = "Source Content V2 - Overwrite";
            string sourceFile = CreateDummySourceConfFile(interfaceName, sourceContent);
            string expectedDestFile = Path.Combine(_deployConfDir, $"{interfaceName}.conf");

            File.WriteAllText(expectedDestFile, "Initial Destination Content To Be Overwritten");

            await WgConfigFileManager.DeployConfigAsync(sourceFile, interfaceName, _wgManagerConfig, true); // overwrite = true

            Assert.That(File.Exists(expectedDestFile), Is.True);
            Assert.That(File.ReadAllText(expectedDestFile), Is.EqualTo(sourceContent), "Destination file should be overwritten with source content.");
        }

        [Test]
        public void DeployConfigAsync_SourceFileDoesNotExist_ThrowsFileNotFoundException()
        {
            string interfaceName = "wg_it_deploy3";
            string nonExistentSourceFile = Path.Combine(_sourceConfDir, "nonexistent.conf");

            Assert.ThrowsAsync<FileNotFoundException>(() =>
                WgConfigFileManager.DeployConfigAsync(nonExistentSourceFile, interfaceName, _wgManagerConfig)
            );
        }

        [Test]
        public void DeployConfigAsync_InvalidInterfaceName_ThrowsInvalidInputException()
        {
            string sourceFile = CreateDummySourceConfFile("wg_it_deploy_valid");
            Assert.ThrowsAsync<InvalidInputException>(() =>
                WgConfigFileManager.DeployConfigAsync(sourceFile, "invalid!!name", _wgManagerConfig)
            );
        }

        // PermissionsException tests are hard to do reliably without manipulating actual file system permissions
        // which is risky in automated tests. We rely on unit tests with mocks for that.
        // However, if running in Docker as non-root and trying to write to /etc/wireguard, it would fail.
        // For these tests, _deployConfDir is in a writable temp location.
    }
}
