using NUnit.Framework;
using WireGuardManager;
using WireGuardManager.Utilities; // For ValidationUtils
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic; // For List
using System.Linq; // For FirstOrDefault

namespace WireGuardManager.IntegrationTests
{
    [TestFixture]
    [Category("Integration")]
    public class WgConfigIntegrationTests
    {
        private string _testDir = null!;
        private const string ValidPrivateKey = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";
        private const string ValidPublicKey = "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB=";


        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // Ensure WgConfig uses the real file system for these tests
            WgConfig.FileSystemProvider = new StandardFileSystem();
        }

        [SetUp]
        public void SetUp()
        {
            _testDir = Path.Combine(TestContext.CurrentContext.TestDirectory, "WgConfigIntegrationTestRun_" + Path.GetRandomFileName());
            Directory.CreateDirectory(_testDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, true);
            }
        }

        [Test]
        public void ToFile_And_FromFile_AreConsistent()
        {
            string testFilePath = Path.Combine(_testDir, "wg_config_io_test.conf");

            var serverConf = new WgServerConfig(ValidPrivateKey)
            {
                Address = new List<string> { "10.0.10.1/24" },
                ListenPort = 51820,
                Dns = new List<string> { "1.1.1.1" }
            };
            var originalConfig = new WgConfig(serverConf);
            var peerConf = new WgPeerConfig(ValidPublicKey)
            {
                AllowedIPs = new List<string> { "10.0.10.2/32" },
                Endpoint = "peer.example.com:12345"
            };
            originalConfig.AddPeer(peerConf);

            // Act: Save to file
            originalConfig.ToFile(testFilePath);

            Assert.That(File.Exists(testFilePath), Is.True, "Config file should have been created by ToFile.");

            // Act: Load from file
            var loadedConfig = WgConfig.FromFile(testFilePath);

            // Assert: Compare properties
            Assert.That(loadedConfig, Is.Not.Null);
            Assert.That(loadedConfig.Interface.PrivateKey, Is.EqualTo(originalConfig.Interface.PrivateKey));
            Assert.That(loadedConfig.Interface.Address, Is.EquivalentTo(originalConfig.Interface.Address));
            Assert.That(loadedConfig.Interface.ListenPort, Is.EqualTo(originalConfig.Interface.ListenPort));
            Assert.That(loadedConfig.Interface.Dns, Is.EquivalentTo(originalConfig.Interface.Dns));

            Assert.That(loadedConfig.Peers.Count, Is.EqualTo(1));
            var loadedPeer = loadedConfig.Peers.FirstOrDefault();
            Assert.That(loadedPeer, Is.Not.Null);
            Assert.That(loadedPeer.PublicKey, Is.EqualTo(peerConf.PublicKey));
            Assert.That(loadedPeer.AllowedIPs, Is.EquivalentTo(peerConf.AllowedIPs));
            Assert.That(loadedPeer.Endpoint, Is.EqualTo(peerConf.Endpoint));
        }

        [Test]
        public void FromFile_NonExistentFile_ThrowsFileNotFound()
        {
            string nonExistentPath = Path.Combine(_testDir, "non_existent.conf");
            Assert.Throws<FileNotFoundException>(() => WgConfig.FromFile(nonExistentPath));
        }

        [Test]
        public void ToFile_PathIsInvalid_ThrowsRelevantIOException()
        {
            // Using an invalid path name for the OS, e.g. containing null char or other forbidden chars
            // This is OS dependent. For Linux, null char is a good test.
            string invalidPath = Path.Combine(_testDir, "invalid\0path.conf");
            var serverConf = new WgServerConfig(ValidPrivateKey);
            var config = new WgConfig(serverConf);

            // Exact exception type can vary (ArgumentException for invalid chars, IOException, etc.)
            // WireGuardManagerException is the wrapper for general IO issues from ToFile.
            Assert.Throws<WireGuardManagerException>(() => config.ToFile(invalidPath));
        }
    }
}
