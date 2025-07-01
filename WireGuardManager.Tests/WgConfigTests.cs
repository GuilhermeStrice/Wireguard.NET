using NUnit.Framework;
using WireGuardManager;
using WireGuardManager.Exceptions; // Added
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System; // For ArgumentNullException

using Moq; // Added for Moq

namespace WireGuardManager.Tests
{
    [TestFixture]
    public class WgConfigTests
    {
        private Mock<IFileSystem> _mockFileSystem = null!;

        // Valid keys for testing constructors and successful parsing
        private const string ValidServerPrivateKey = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";
        private const string ValidPeerPublicKey = "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB=";
        private const string AnotherPeerPublicKey = "CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC=";
        private const string ValidPresharedKey = "DDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDD=";

        [SetUp] // Added SetUp
        public void SetUp()
        {
            _mockFileSystem = new Mock<IFileSystem>();
            WgConfig.FileSystemProvider = _mockFileSystem.Object;
            // Also for WgManagerConfig if its FileSystemProvider is used by WgConfig, but it's not directly.
        }

        [TearDown] // Added TearDown
        public void TearDown()
        {
            WgConfig.FileSystemProvider = new StandardFileSystem(); // Reset
        }

        [Test]
        public void WgConfig_ToFile_WritesCorrectContent()
        {
            var serverConfig = new WgServerConfig(ValidServerPrivateKey);
            var config = new WgConfig(serverConfig);
            string expectedContent = config.ToString();
            string testPath = "test.conf";

            _mockFileSystem.Setup(fs => fs.WriteAllTextAsync(testPath, expectedContent))
                           .Returns(Task.CompletedTask); // Mock the async write

            config.ToFile(testPath); // This now blocks on the async call internally

            _mockFileSystem.Verify(fs => fs.WriteAllTextAsync(testPath, expectedContent), Times.Once);
        }

        [Test]
        public void WgServerConfig_Constructor_ValidKey_Succeeds()
        {
            Assert.DoesNotThrow(() => new WgServerConfig(ValidServerPrivateKey));
        }

        [Test]
        public void WgServerConfig_Constructor_InvalidKey_ThrowsInvalidInputException()
        {
            var ex = Assert.Throws<InvalidInputException>(() => new WgServerConfig("invalid_key"));
            Assert.That(ex.Message, Does.Contain("Invalid PrivateKey format."));
        }

        [Test]
        public void WgServerConfig_SetInvalidProperties_ThrowsInvalidInputException()
        {
            var server = new WgServerConfig(ValidServerPrivateKey);
            Assert.Throws<InvalidInputException>(() => server.PrivateKey = "short", "PrivateKey");
            Assert.Throws<InvalidInputException>(() => server.Address = new List<string> { "not-a-cidr" }, "Address CIDR");
            Assert.Throws<InvalidInputException>(() => server.ListenPort = -1, "ListenPort negative");
            Assert.Throws<InvalidInputException>(() => server.ListenPort = 65536, "ListenPort too high");
            Assert.Throws<InvalidInputException>(() => server.Dns = new List<string> { "not-an-ip" }, "DNS IP");
            Assert.Throws<InvalidInputException>(() => server.Mtu = 500, "MTU too low");
        }


        [Test]
        public void WgPeerConfig_Constructor_ValidKey_Succeeds()
        {
            Assert.DoesNotThrow(() => new WgPeerConfig(ValidPeerPublicKey));
        }

        [Test]
        public void WgPeerConfig_Constructor_InvalidKey_ThrowsInvalidInputException()
        {
            var ex = Assert.Throws<InvalidInputException>(() => new WgPeerConfig("invalid_key"));
            Assert.That(ex.Message, Does.Contain("Invalid PublicKey format."));
        }

        [Test]
        public void WgPeerConfig_SetInvalidProperties_ThrowsInvalidInputException()
        {
            var peer = new WgPeerConfig(ValidPeerPublicKey);
            Assert.Throws<InvalidInputException>(() => peer.PublicKey = "short", "PublicKey");
            Assert.Throws<InvalidInputException>(() => peer.PresharedKey = "short", "PresharedKey");
            Assert.Throws<InvalidInputException>(() => peer.AllowedIPs = new List<string> { "not-a-cidr" }, "AllowedIPs CIDR");
            Assert.Throws<InvalidInputException>(() => peer.Endpoint = "bad-endpoint", "Endpoint format");
            Assert.Throws<InvalidInputException>(() => peer.PersistentKeepalive = -1, "PersistentKeepalive negative");
        }


        [Test]
        public void WgConfig_ToString_FullConfig_GeneratesCorrectFormat()
        {
            var serverConfig = new WgServerConfig(ValidServerPrivateKey)
            {
                Address = new List<string> { "10.0.0.1/24" },
                ListenPort = 51820
            };
            var config = new WgConfig(serverConfig);

            var peer1 = new WgPeerConfig(ValidPeerPublicKey)
            {
                AllowedIPs = new List<string> { "10.0.0.2/32" }
            };
            config.AddPeer(peer1);

            string expected = @"[Interface]
PrivateKey = AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=
Address = 10.0.0.1/24
ListenPort = 51820

[Peer]
PublicKey = BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB=
AllowedIPs = 10.0.0.2/32
".TrimStart();

            string actual = config.ToString().Replace("\r\n", "\n").TrimEnd('\n');
            string expectedNormalized = expected.Replace("\r\n", "\n").TrimEnd('\n');
            Assert.That(actual, Is.EqualTo(expectedNormalized));
        }

        [Test]
        public void WgConfig_Parse_SimpleConfig_ParsesCorrectly()
        {
            string configString = $@"
[Interface]
PrivateKey = {ValidServerPrivateKey}
Address = 10.0.0.1/24, fd00::1/64
ListenPort = 51820
DNS = 1.1.1.1, 8.8.8.8

[Peer]
PublicKey = {ValidPeerPublicKey}
AllowedIPs = 10.0.0.2/32
PresharedKey = {ValidPresharedKey}
Endpoint = peer1.example.com:12345
PersistentKeepalive = 25
";
            var config = WgConfig.Parse(configString);

            Assert.That(config.Interface, Is.Not.Null);
            Assert.That(config.Interface.PrivateKey, Is.EqualTo(ValidServerPrivateKey));
            Assert.That(config.Interface.Address, Is.EquivalentTo(new[] { "10.0.0.1/24", "fd00::1/64" }));
            Assert.That(config.Interface.ListenPort, Is.EqualTo(51820));
            Assert.That(config.Interface.Dns, Is.EquivalentTo(new[] { "1.1.1.1", "8.8.8.8" }));

            Assert.That(config.Peers.Count, Is.EqualTo(1));
            var peer1 = config.Peers[0];
            Assert.That(peer1.PublicKey, Is.EqualTo(ValidPeerPublicKey));
            Assert.That(peer1.AllowedIPs, Is.EquivalentTo(new[] { "10.0.0.2/32" }));
            Assert.That(peer1.PresharedKey, Is.EqualTo(ValidPresharedKey));
            Assert.That(peer1.Endpoint, Is.EqualTo("peer1.example.com:12345"));
            Assert.That(peer1.PersistentKeepalive, Is.EqualTo(25));
        }

        [Test]
        public void WgConfig_Parse_MissingInterface_ThrowsWireGuardConfigParseException()
        {
            string configString = @"[Peer]\nPublicKey = some_peer_key"; // Using valid key format for the part that exists
            var ex = Assert.Throws<WireGuardConfigParseException>(() => WgConfig.Parse(configString));
            Assert.That(ex.Message, Does.Contain("PrivateKey not found"));
        }

        [Test]
        public void WgConfig_Parse_MissingPrivateKeyInInterface_ThrowsWireGuardConfigParseException()
        {
            string configString = @"[Interface]\nAddress = 10.0.0.1/24";
            var ex = Assert.Throws<WireGuardConfigParseException>(() => WgConfig.Parse(configString));
            Assert.That(ex.Message, Does.Contain("PrivateKey not found"));
        }

        [Test]
        public void WgConfig_Parse_MalformedKeyValue_ThrowsWireGuardConfigParseException()
        {
            string configString = $"[Interface]\nPrivateKey = {ValidServerPrivateKey}\nAddress = 10.0.0.1/24\nMalformedLine";
            var ex = Assert.Throws<WireGuardConfigParseException>(() => WgConfig.Parse(configString));
            Assert.That(ex.Message, Does.Contain("Malformed key-value pair"));
            Assert.That(ex.LineNumber, Is.EqualTo(4)); // Assuming PrivateKey is line 2, Address line 3
            Assert.That(ex.LineContent, Is.EqualTo("MalformedLine"));
        }

        [Test]
        public void WgConfig_Parse_InvalidListenPortValue_ThrowsWireGuardConfigParseException()
        {
            string configString = $"[Interface]\nPrivateKey = {ValidServerPrivateKey}\nListenPort = not_a_number";
            var ex = Assert.Throws<WireGuardConfigParseException>(() => WgConfig.Parse(configString));
            Assert.That(ex.Message, Does.Contain("Invalid ListenPort value"));
            Assert.That(ex.LineContent, Does.Contain("not_a_number"));
        }

        [Test]
        public void WgConfig_Parse_OrphanedPeerProperty_ThrowsWireGuardConfigParseException()
        {
            string configString = $"[Interface]\nPrivateKey = {ValidServerPrivateKey}\nAllowedIPs = 10.0.0.2/32"; // AllowedIPs without [Peer]
            var ex = Assert.Throws<WireGuardConfigParseException>(() => WgConfig.Parse(configString));
            Assert.That(ex.Message, Does.Contain("Orphaned peer configuration line"));
        }

        [Test]
        public void WgConfig_ToFile_PermissionDenied_ThrowsPermissionsException()
        {
            var serverConfig = new WgServerConfig(ValidServerPrivateKey);
            var config = new WgConfig(serverConfig);
            string testPath = "test.conf";

            _mockFileSystem.Setup(fs => fs.WriteAllTextAsync(testPath, It.IsAny<string>()))
                           .ThrowsAsync(new UnauthorizedAccessException("Permission denied.")); // Simulate underlying exception

            var ex = Assert.Throws<PermissionsException>(() => config.ToFile(testPath));
            Assert.That(ex.Operation, Is.EqualTo("write configuration file"));
            Assert.That(ex.Resource, Is.EqualTo(testPath));
            Assert.That(ex.InnerException, Is.TypeOf<UnauthorizedAccessException>());
        }

        [Test]
        public void WgConfig_FromFile_NonExistent_ThrowsFileNotFoundException()
        {
            string testPath = "non_existent_file.conf";
            _mockFileSystem.Setup(fs => fs.FileExists(testPath)).Returns(false);

            Assert.Throws<FileNotFoundException>(() => WgConfig.FromFile(testPath));
            _mockFileSystem.Verify(fs => fs.FileExists(testPath), Times.Once);
        }

        [Test]
        public void WgConfig_FromFile_ReadsAndParsesCorrectly()
        {
            string testPath = "existing.conf";
            string fileContent = $"[Interface]\nPrivateKey = {ValidServerPrivateKey}\nAddress = 10.0.0.1/24\n";

            _mockFileSystem.Setup(fs => fs.FileExists(testPath)).Returns(true);
            _mockFileSystem.Setup(fs => fs.ReadAllTextAsync(testPath))
                           .ReturnsAsync(fileContent); // Mock async read

            var config = WgConfig.FromFile(testPath); // This now blocks on async call

            Assert.That(config, Is.Not.Null);
            Assert.That(config.Interface.PrivateKey, Is.EqualTo(ValidServerPrivateKey));
            Assert.That(config.Interface.Address.First(), Is.EqualTo("10.0.0.1/24"));
            _mockFileSystem.Verify(fs => fs.FileExists(testPath), Times.Once);
            _mockFileSystem.Verify(fs => fs.ReadAllTextAsync(testPath), Times.Once);
        }

        [Test]
        public void WgConfig_FromFile_ReadPermissionDenied_ThrowsPermissionsException()
        {
            string testPath = "existing_restricted.conf";
            _mockFileSystem.Setup(fs => fs.FileExists(testPath)).Returns(true);
            _mockFileSystem.Setup(fs => fs.ReadAllTextAsync(testPath))
                           .ThrowsAsync(new UnauthorizedAccessException("Read permission denied."));

            var ex = Assert.Throws<PermissionsException>(() => WgConfig.FromFile(testPath));
            Assert.That(ex.Operation, Is.EqualTo("read configuration file"));
            Assert.That(ex.Resource, Is.EqualTo(testPath));
            Assert.That(ex.InnerException, Is.TypeOf<UnauthorizedAccessException>());
        }


        [Test]
        public void WgConfig_AddPeer_Null_ThrowsArgumentNullException()
        {
            var server = new WgServerConfig(ValidServerPrivateKey);
            var config = new WgConfig(server);
            Assert.Throws<ArgumentNullException>(() => config.AddPeer(null!));
        }

        [Test]
        public void WgConfig_RemovePeer_NullOrEmptyKey_ThrowsInvalidInputException()
        {
            var server = new WgServerConfig(ValidServerPrivateKey);
            var config = new WgConfig(server);
            Assert.Throws<InvalidInputException>(() => config.RemovePeer(null!));
            Assert.Throws<InvalidInputException>(() => config.RemovePeer(" "));
        }

        [Test]
        public void WgConfig_GetPeer_NullOrEmptyKey_ThrowsInvalidInputException()
        {
            var server = new WgServerConfig(ValidServerPrivateKey);
            var config = new WgConfig(server);
            Assert.Throws<InvalidInputException>(() => config.GetPeer(null!));
            Assert.Throws<InvalidInputException>(() => config.GetPeer(" "));
        }
    }
}
