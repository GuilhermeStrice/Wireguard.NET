using NUnit.Framework;
using WireGuardManager;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace WireGuardManager.Tests
{
    [TestFixture]
    public class WgConfigTests
    {
        private string _testPrivateKeyServer = "server_private_key_test_12345";
        private string _testPublicKeyPeer1 = "peer1_public_key_test_abcde";
        private string _testPublicKeyPeer2 = "peer2_public_key_test_fghij";
        private string _testPresharedKeyPeer1 = "psk_peer1_test_zyxw";

        [Test]
        public void WgServerConfig_ToString_GeneratesCorrectFormat()
        {
            var serverConfig = new WgServerConfig(_testPrivateKeyServer)
            {
                Address = new List<string> { "10.0.0.1/24", "fd00::1/64" },
                ListenPort = 51820,
                Dns = new List<string> { "1.1.1.1", "8.8.8.8" },
                Mtu = 1420,
                PostUp = new List<string> { "iptables -A FORWARD -i %i -j ACCEPT", "iptables -t nat -A POSTROUTING -o eth0 -j MASQUERADE" },
                PostDown = new List<string> { "iptables -D FORWARD -i %i -j ACCEPT" },
                SaveConfig = true
            };

            string expected = @"[Interface]
PrivateKey = server_private_key_test_12345
Address = 10.0.0.1/24, fd00::1/64
ListenPort = 51820
DNS = 1.1.1.1, 8.8.8.8
MTU = 1420
PostUp = iptables -A FORWARD -i %i -j ACCEPT
PostUp = iptables -t nat -A POSTROUTING -o eth0 -j MASQUERADE
PostDown = iptables -D FORWARD -i %i -j ACCEPT
SaveConfig = true
";
            Assert.That(serverConfig.ToString(), Is.EqualTo(expected.Replace("\r\n", "\n")));
        }

        [Test]
        public void WgPeerConfig_ToString_GeneratesCorrectFormat()
        {
            var peerConfig = new WgPeerConfig(_testPublicKeyPeer1)
            {
                PresharedKey = _testPresharedKeyPeer1,
                AllowedIPs = new List<string> { "10.0.0.2/32", "fd00::2/128" },
                Endpoint = "peer1.example.com:12345",
                PersistentKeepalive = 25
            };

            string expected = @"[Peer]
PublicKey = peer1_public_key_test_abcde
PresharedKey = psk_peer1_test_zyxw
AllowedIPs = 10.0.0.2/32, fd00::2/128
Endpoint = peer1.example.com:12345
PersistentKeepalive = 25
";
            Assert.That(peerConfig.ToString(), Is.EqualTo(expected.Replace("\r\n", "\n")));
        }

        [Test]
        public void WgConfig_ToString_FullConfig_GeneratesCorrectFormat()
        {
            var serverConfig = new WgServerConfig(_testPrivateKeyServer)
            {
                Address = new List<string> { "10.0.0.1/24" },
                ListenPort = 51820
            };
            var config = new WgConfig(serverConfig);

            var peer1 = new WgPeerConfig(_testPublicKeyPeer1)
            {
                AllowedIPs = new List<string> { "10.0.0.2/32" }
            };
            config.AddPeer(peer1);

            var peer2 = new WgPeerConfig(_testPublicKeyPeer2)
            {
                AllowedIPs = new List<string> { "10.0.0.3/32" },
                Endpoint = "peer2.example.com:54321"
            };
            config.AddPeer(peer2);

            string expected = @"[Interface]
PrivateKey = server_private_key_test_12345
Address = 10.0.0.1/24
ListenPort = 51820

[Peer]
PublicKey = peer1_public_key_test_abcde
AllowedIPs = 10.0.0.2/32

[Peer]
PublicKey = peer2_public_key_test_fghij
AllowedIPs = 10.0.0.3/32
Endpoint = peer2.example.com:54321
".TrimStart(); // Trim start to remove initial newline if any from formatting

            // Normalize line endings and compare
            string actual = config.ToString().Replace("\r\n", "\n").TrimEnd('\n');
            string expectedNormalized = expected.Replace("\r\n", "\n").TrimEnd('\n');

            Assert.That(actual, Is.EqualTo(expectedNormalized));
        }

        [Test]
        public void WgConfig_Parse_SimpleConfig_ParsesCorrectly()
        {
            string configString = @"
# This is a comment
[Interface]
PrivateKey = server_private_key_test_12345 # inline comment
Address = 10.0.0.1/24
ListenPort = 51820
DNS = 1.1.1.1, 8.8.8.8

[Peer] # Peer 1
PublicKey = peer1_public_key_test_abcde
AllowedIPs = 10.0.0.2/32
PresharedKey = psk_peer1_test_zyxw

[Peer] # Peer 2
PublicKey = peer2_public_key_test_fghij
AllowedIPs = 10.0.0.3/32, 10.0.0.4/32
Endpoint = peer2.example.com:12345
PersistentKeepalive = 21
";
            var config = WgConfig.Parse(configString);

            Assert.That(config.Interface, Is.Not.Null);
            Assert.That(config.Interface.PrivateKey, Is.EqualTo(_testPrivateKeyServer));
            Assert.That(config.Interface.Address, Is.EquivalentTo(new[] { "10.0.0.1/24" }));
            Assert.That(config.Interface.ListenPort, Is.EqualTo(51820));
            Assert.That(config.Interface.Dns, Is.EquivalentTo(new[] { "1.1.1.1", "8.8.8.8" }));

            Assert.That(config.Peers.Count, Is.EqualTo(2));

            var peer1 = config.Peers.FirstOrDefault(p => p.PublicKey == _testPublicKeyPeer1);
            Assert.That(peer1, Is.Not.Null);
            Assert.That(peer1.AllowedIPs, Is.EquivalentTo(new[] { "10.0.0.2/32" }));
            Assert.That(peer1.PresharedKey, Is.EqualTo(_testPresharedKeyPeer1));

            var peer2 = config.Peers.FirstOrDefault(p => p.PublicKey == _testPublicKeyPeer2);
            Assert.That(peer2, Is.Not.Null);
            Assert.That(peer2.AllowedIPs, Is.EquivalentTo(new[] { "10.0.0.3/32", "10.0.0.4/32" }));
            Assert.That(peer2.Endpoint, Is.EqualTo("peer2.example.com:12345"));
            Assert.That(peer2.PersistentKeepalive, Is.EqualTo(21));
        }

        [Test]
        public void WgConfig_Parse_MultiplePostUpPostDown_ParsesCorrectly()
        {
            string configString = @"
[Interface]
PrivateKey = some_server_private_key
Address = 10.1.0.1/24
PostUp = cmd1 up
PostUp = cmd2 up %i
PostDown = cmd1 down
PostDown = cmd2 down %i
";
            var config = WgConfig.Parse(configString);
            Assert.That(config.Interface.PostUp, Is.EquivalentTo(new[] { "cmd1 up", "cmd2 up %i" }));
            Assert.That(config.Interface.PostDown, Is.EquivalentTo(new[] { "cmd1 down", "cmd2 down %i" }));
        }

        [Test]
        public void WgConfig_ToFile_And_FromFile_AreConsistent()
        {
            var tempFile = Path.GetTempFileName();
            try
            {
                var serverConfig = new WgServerConfig(_testPrivateKeyServer) { Address = new List<string> { "192.168.1.1/24" } };
                var originalConfig = new WgConfig(serverConfig);
                originalConfig.AddPeer(new WgPeerConfig(_testPublicKeyPeer1) { AllowedIPs = new List<string> { "192.168.1.2/32" } });

                originalConfig.ToFile(tempFile);
                var loadedConfig = WgConfig.FromFile(tempFile);

                Assert.That(loadedConfig.Interface.PrivateKey, Is.EqualTo(originalConfig.Interface.PrivateKey));
                Assert.That(loadedConfig.Interface.Address, Is.EquivalentTo(originalConfig.Interface.Address));
                Assert.That(loadedConfig.Peers.Count, Is.EqualTo(originalConfig.Peers.Count));
                Assert.That(loadedConfig.Peers[0].PublicKey, Is.EqualTo(originalConfig.Peers[0].PublicKey));
                Assert.That(loadedConfig.Peers[0].AllowedIPs, Is.EquivalentTo(originalConfig.Peers[0].AllowedIPs));
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Test]
        public void WgConfig_Parse_MissingInterface_ThrowsFormatException()
        {
            string configString = @"
[Peer]
PublicKey = some_peer_key
AllowedIPs = 10.0.0.1/32
";
            Assert.Throws<System.FormatException>(() => WgConfig.Parse(configString));
        }

        [Test]
        public void WgConfig_Parse_MissingPrivateKeyInInterface_ThrowsFormatException()
        {
            string configString = @"
[Interface]
Address = 10.0.0.1/24
";
            Assert.Throws<System.FormatException>(() => WgConfig.Parse(configString));
        }

        [Test]
        public void WgConfig_Parse_PeerMissingPublicKey_IgnoresPeerOrSection()
        {
            // The current parser might create a peer if a previous valid PublicKey was found,
            // or it might skip the section. This test is to document and verify current behavior.
            // A truly robust parser would probably throw or log a warning for a malformed peer.
            // Current logic: A [Peer] section without a PublicKey will likely result in its lines being ignored
            // or misattributed if a previous currentPeer context exists.
            // Let's test that it doesn't crash and ideally doesn't create an empty/invalid peer.
            string configString = @"
[Interface]
PrivateKey = server_key_blah
Address = 10.0.0.1/24

[Peer]
AllowedIPs = 10.0.0.2/32 # This peer is missing PublicKey

[Peer]
PublicKey = valid_peer_key
AllowedIPs = 10.0.0.3/32
";
            var config = WgConfig.Parse(configString);

            Assert.That(config.Peers.Count, Is.EqualTo(1)); // Only the valid peer should be parsed
            Assert.That(config.Peers.Any(p => p.PublicKey == "valid_peer_key"), Is.True);
            // Ensure no peer was created with a null/empty PublicKey
            Assert.That(config.Peers.All(p => !string.IsNullOrEmpty(p.PublicKey)), Is.True);
        }
    }
}
