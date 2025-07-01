using NUnit.Framework;
using WireGuardManager;
using WireGuardManager.Utilities;
using WireGuardManager.Exceptions;
using Moq;
using System.Threading.Tasks;
using System.IO; // For Path methods, FileNotFoundException
using System; // For ArgumentNullException

namespace WireGuardManager.Tests
{
    [TestFixture]
    public class WgConfigFileManagerTests
    {
        private Mock<IFileSystem> _mockFileSystem = null!;
        private WgManagerConfig _testWgManagerConfig = null!;
        private const string ValidInterfaceName = "wg0";
        private const string ValidSourcePath = "/path/to/source.conf";
        private const string ValidConfigDir = "/etc/wireguard";
        private string _expectedDestPath = null!;

        [SetUp]
        public void SetUp()
        {
            _mockFileSystem = new Mock<IFileSystem>();
            WgConfigFileManager.FileSystemProvider = _mockFileSystem.Object; // Inject mock

            _testWgManagerConfig = new WgManagerConfig
            {
                WireguardConfigDirectory = ValidConfigDir
                // Other properties can be default for these tests
            };
            _expectedDestPath = Path.Combine(ValidConfigDir, $"{ValidInterfaceName}.conf");
        }

        [Test]
        public async Task DeployConfigAsync_SuccessfulDeployment_CopiesFileAndEnsuresDirectory()
        {
            _mockFileSystem.Setup(fs => fs.FileExists(ValidSourcePath)).Returns(true);
            _mockFileSystem.Setup(fs => fs.EnsureDirectoryExists(ValidConfigDir)); // Expect call
            _mockFileSystem.Setup(fs => fs.CopyFile(ValidSourcePath, _expectedDestPath, false)); // Expect call, overwrite=false

            await WgConfigFileManager.DeployConfigAsync(ValidSourcePath, ValidInterfaceName, _testWgManagerConfig, false);

            _mockFileSystem.Verify(fs => fs.FileExists(ValidSourcePath), Times.Once);
            _mockFileSystem.Verify(fs => fs.EnsureDirectoryExists(ValidConfigDir), Times.Once);
            _mockFileSystem.Verify(fs => fs.CopyFile(ValidSourcePath, _expectedDestPath, false), Times.Once);
        }

        [Test]
        public async Task DeployConfigAsync_SuccessfulDeploymentWithOverwrite_CopiesFileAndEnsuresDirectory()
        {
            _mockFileSystem.Setup(fs => fs.FileExists(ValidSourcePath)).Returns(true);
            _mockFileSystem.Setup(fs => fs.EnsureDirectoryExists(ValidConfigDir));
            _mockFileSystem.Setup(fs => fs.CopyFile(ValidSourcePath, _expectedDestPath, true)); // overwrite=true

            await WgConfigFileManager.DeployConfigAsync(ValidSourcePath, ValidInterfaceName, _testWgManagerConfig, true);

            _mockFileSystem.Verify(fs => fs.CopyFile(ValidSourcePath, _expectedDestPath, true), Times.Once);
        }

        [Test]
        public void DeployConfigAsync_NullSourcePath_ThrowsInvalidInputException()
        {
            var ex = Assert.ThrowsAsync<InvalidInputException>(() =>
                WgConfigFileManager.DeployConfigAsync(null!, ValidInterfaceName, _testWgManagerConfig)
            );
            Assert.That(ex.ParamName, Is.EqualTo("sourceConfigPath"));
        }

        [Test]
        public void DeployConfigAsync_InvalidInterfaceName_ThrowsInvalidInputException()
        {
            var ex = Assert.ThrowsAsync<InvalidInputException>(() =>
                WgConfigFileManager.DeployConfigAsync(ValidSourcePath, "invalid!!name", _testWgManagerConfig)
            );
            Assert.That(ex.ParamName, Is.EqualTo("interfaceName"));
        }

        [Test]
        public void DeployConfigAsync_NullConfig_ThrowsArgumentNullException()
        {
            Assert.ThrowsAsync<ArgumentNullException>(() =>
                WgConfigFileManager.DeployConfigAsync(ValidSourcePath, ValidInterfaceName, null!)
            );
        }

        [Test]
        public void DeployConfigAsync_EmptyWireguardConfigDirectory_ThrowsInvalidInputException()
        {
            var badConfig = new WgManagerConfig { WireguardConfigDirectory = "" };
            var ex = Assert.ThrowsAsync<InvalidInputException>(() =>
                WgConfigFileManager.DeployConfigAsync(ValidSourcePath, ValidInterfaceName, badConfig)
            );
            Assert.That(ex.ParamName, Is.EqualTo("config.WireguardConfigDirectory"));
        }

        [Test]
        public void DeployConfigAsync_SourceFileDoesNotExist_ThrowsFileNotFoundException()
        {
            _mockFileSystem.Setup(fs => fs.FileExists(ValidSourcePath)).Returns(false);

            Assert.ThrowsAsync<FileNotFoundException>(() =>
                WgConfigFileManager.DeployConfigAsync(ValidSourcePath, ValidInterfaceName, _testWgManagerConfig)
            );
        }

        [Test]
        public void DeployConfigAsync_EnsureDirectoryThrowsPermissionError_ThrowsPermissionsException()
        {
            _mockFileSystem.Setup(fs => fs.FileExists(ValidSourcePath)).Returns(true);
            _mockFileSystem.Setup(fs => fs.EnsureDirectoryExists(ValidConfigDir))
                           .Throws(new PermissionsException("create directory", ValidConfigDir));

            var ex = Assert.ThrowsAsync<PermissionsException>(() =>
                WgConfigFileManager.DeployConfigAsync(ValidSourcePath, ValidInterfaceName, _testWgManagerConfig)
            );
            Assert.That(ex.Operation, Is.EqualTo("create directory"));
        }

        [Test]
        public void DeployConfigAsync_CopyFileThrowsPermissionError_ThrowsPermissionsException()
        {
            _mockFileSystem.Setup(fs => fs.FileExists(ValidSourcePath)).Returns(true);
            _mockFileSystem.Setup(fs => fs.EnsureDirectoryExists(ValidConfigDir));
            _mockFileSystem.Setup(fs => fs.CopyFile(ValidSourcePath, _expectedDestPath, false))
                           .Throws(new PermissionsException("copy file", _expectedDestPath));

            var ex = Assert.ThrowsAsync<PermissionsException>(() =>
                WgConfigFileManager.DeployConfigAsync(ValidSourcePath, ValidInterfaceName, _testWgManagerConfig, false)
            );
            Assert.That(ex.Operation, Is.EqualTo("copy file"));
        }

        [Test]
        public void DeployConfigAsync_CopyFileThrowsIOExceptionAlreadyExistsNoOverwrite_ThrowsWireGuardManagerException()
        {
            _mockFileSystem.Setup(fs => fs.FileExists(ValidSourcePath)).Returns(true);
            _mockFileSystem.Setup(fs => fs.EnsureDirectoryExists(ValidConfigDir));
            // Simulate File.Copy throwing IOException when file exists and overwrite is false
            _mockFileSystem.Setup(fs => fs.CopyFile(ValidSourcePath, _expectedDestPath, false))
                           .Throws(new IOException($"The file '{_expectedDestPath}' already exists."));

            var ex = Assert.ThrowsAsync<WireGuardManagerException>(() =>
                WgConfigFileManager.DeployConfigAsync(ValidSourcePath, ValidInterfaceName, _testWgManagerConfig, false) // overwrite is false
            );
            Assert.That(ex.Message, Does.Contain("already exists and overwrite is false"));
        }

        [Test]
        public async Task DeployConfigAsync_CopyFileThrowsIOExceptionAlreadyExistsWithOverwrite_DoesNotThrowThatSpecificException()
        {
            _mockFileSystem.Setup(fs => fs.FileExists(ValidSourcePath)).Returns(true);
            _mockFileSystem.Setup(fs => fs.EnsureDirectoryExists(ValidConfigDir));
            _mockFileSystem.Setup(fs => fs.CopyFile(ValidSourcePath, _expectedDestPath, true)); // Overwrite is true

            // We don't expect the "already exists and overwrite is false" exception
            // It should either succeed, or throw a different PermissionsException if the mock for CopyFile was more specific
            // For this test, we just verify CopyFile is called with overwrite=true
            await WgConfigFileManager.DeployConfigAsync(ValidSourcePath, ValidInterfaceName, _testWgManagerConfig, true);
            _mockFileSystem.Verify(fs => fs.CopyFile(ValidSourcePath, _expectedDestPath, true), Times.Once());
        }
    }
}
