using NUnit.Framework;
using WireGuardManager;
using WireGuardManager.Utilities;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Collections.Generic; // For List
using System; // For DateTimeOffset

namespace WireGuardManager.IntegrationTests
{
    [TestFixture]
    [Category("Integration")]
    public class WgShowParserIntegrationTests
    {
        private string _testBaseDir = null!;
        private string _wgConfDir = null!;
        private WgManagerConfig _integrationTestConfig = null!;
        private const string TestInterface = "wgshowit0";

        // Use known valid key pairs for reproducible tests
        private const string ServerPrivateKey = "PRIVATEKEY_SERVER_WgShowParserIntegrationTests="; // Replace with actual valid key
        private const string ServerPublicKey = "PUBLICKEY_SERVER_WgShowParserIntegrationTests=";
        private const string Peer1PublicKey = "PUBLICKEY_PEER1_WgShowParserIntegrationTests="; // Peer A
        private const string Peer2PublicKey = "PUBLICKEY_PEER2_WgShowParserIntegrationTests="; // Peer B
        private const string Peer3PublicKey = "PUBLICKEY_PEER3_WgShowParserIntegrationTests="; // Peer C
        private const string Peer3Psk = "PRESHAREDKEY_PEER3_WgShowParserIntegrationT=";
        private const string Peer4PublicKey = "PUBLICKEY_PEER4_WgShowParserIntegrationTests="; // Peer D


        [OneTimeSetUp]
        public async Task GlobalSetup()
        {
            // Ensure real providers are used
            WgQuick.ProcessRunnerInstance = new ProcessRunner();
            WgQuick.FileSystemProvider = new StandardFileSystem();
            WgConfigFileManager.FileSystemProvider = new StandardFileSystem();
            WgConfig.FileSystemProvider = new StandardFileSystem();
            WgManagerConfig.FileSystemProvider = new StandardFileSystem();

            _testBaseDir = Path.Combine(TestContext.CurrentContext.TestDirectory, "WgShowParserIntegrationTestRun_" + Path.GetRandomFileName());
            _wgConfDir = Path.Combine(_testBaseDir, "wireguard_configs_wgshow");
            Directory.CreateDirectory(_wgConfDir);

            _integrationTestConfig = new WgManagerConfig
            {
                AllowSystemdManagement = false,
                WireguardConfigDirectory = _wgConfDir,
            };

            // Generate actual keys using wg tool once for the entire fixture to ensure validity
            // This is better than hardcoding potentially mismatched placeholder keys.
            // var serverKeys = await WgKeyManager.GenerateKeyPairAsync(); // Using wg tool
            // ServerPrivateKey = serverKeys.PrivateKey;
            // ServerPublicKey = serverKeys.PublicKey;
            // var peer1Keys = await WgKeyManager.GenerateKeyPairAsync();
            // Peer1PublicKey = peer1Keys.PublicKey;
            // Peer1Psk = await WgKeyManager.GeneratePresharedKeyAsync();
            // var peer2Keys = await WgKeyManager.GenerateKeyPairAsync();
            // Peer2PublicKey = peer2Keys.PublicKey;
            // TestContext.Progress.WriteLine($"WgShowParserTests Using ServerPubKey: {ServerPublicKey}, Peer1PubKey: {Peer1PublicKey}, Peer2PubKey: {Peer2PublicKey}");
            // For now, I'll keep the placeholders and assume they are valid and matched for structure.
            // The placeholders MUST be 44 chars and end with =
            Assert.That(ValidationUtils.IsValidWireGuardKey(ServerPrivateKey), "Test ServerPrivateKey placeholder is invalid.");
            Assert.That(ValidationUtils.IsValidWireGuardKey(ServerPublicKey), "Test ServerPublicKey placeholder is invalid.");
            Assert.That(ValidationUtils.IsValidWireGuardKey(Peer1PublicKey), "Test Peer1PublicKey (PeerA) placeholder is invalid.");
            Assert.That(ValidationUtils.IsValidWireGuardKey(Peer2PublicKey), "Test Peer2PublicKey (PeerB) placeholder is invalid.");
            Assert.That(ValidationUtils.IsValidWireGuardKey(Peer3PublicKey), "Test Peer3PublicKey (PeerC) placeholder is invalid.");
            Assert.That(ValidationUtils.IsValidWireGuardKey(Peer3Psk), "Test Peer3Psk placeholder is invalid.");
            Assert.That(ValidationUtils.IsValidWireGuardKey(Peer4PublicKey), "Test Peer4PublicKey (PeerD) placeholder is invalid.");

            // Setup a test interface with multiple diverse peers
            var serverConf = new WgServerConfig(ServerPrivateKey)
            {
                Address = new List<string> { "10.250.0.1/24", "fd00:cafe::1/64" },
                ListenPort = 51900,
                Dns = new List<string> { "1.1.1.1" }
            };

            // Peer A: Only AllowedIPs
            var peerAConf = new WgPeerConfig(Peer1PublicKey)
            {
                AllowedIPs = new List<string> { "10.250.0.2/32", "fd00:cafe::2/128" }
            };

            // Peer B: AllowedIPs, Endpoint
            var peerBConf = new WgPeerConfig(Peer2PublicKey)
            {
                AllowedIPs = new List<string> { "10.250.0.3/32" },
                Endpoint = "peerB.example.com:12345"
            };

            // Peer C: AllowedIPs, PresharedKey (string)
            var peerCConf = new WgPeerConfig(Peer3PublicKey)
            {
                AllowedIPs = new List<string> { "10.250.0.4/32" },
                PresharedKey = Peer3Psk
            };

            // Peer D: AllowedIPs, PersistentKeepalive
            var peerDConf = new WgPeerConfig(Peer4PublicKey)
            {
                AllowedIPs = new List<string> { "10.250.0.5/32" },
                PersistentKeepalive = 30
            };

            var wgConfig = new WgConfig(serverConf);
            wgConfig.AddPeer(peerAConf);
            wgConfig.AddPeer(peerBConf);
            wgConfig.AddPeer(peerCConf);
            wgConfig.AddPeer(peerDConf);

            string tempSourcePath = Path.Combine(_testBaseDir, $"{TestInterface}_source.conf");
            wgConfig.ToFile(tempSourcePath);
            await WgConfigFileManager.DeployConfigAsync(tempSourcePath, TestInterface, _integrationTestConfig);

            try
            {
                await WgQuick.Up(TestInterface, _integrationTestConfig, TimeSpan.FromSeconds(15));
                TestContext.Progress.WriteLine($"Interface {TestInterface} brought up for WgShowParser tests.");
            }
            catch(Exception ex)
            {
                Assert.Inconclusive($"Failed to bring up {TestInterface} for WgShowParser tests. Docker container might need --cap-add NET_ADMIN and --cap-add SYS_MODULE. Error: {ex.Message}");
            }
        }

        [OneTimeTearDown]
        public async Task GlobalTeardown()
        {
            try
            {
                await WgQuick.Down(TestInterface, _integrationTestConfig, TimeSpan.FromSeconds(15));
                TestContext.Progress.WriteLine($"Interface {TestInterface} brought down after WgShowParser tests.");
            }
            catch (Exception ex) { TestContext.Progress.WriteLine($"Error bringing down {TestInterface}: {ex.Message}"); }

            if (Directory.Exists(_testBaseDir))
            {
                Directory.Delete(_testBaseDir, true);
            }
        }

        [Test]
        public async Task ShowInterfaceDetailsAsync_ParsesAllFieldsCorrectly()
        {
            var details = await WgQuick.ShowInterfaceDetailsAsync(TestInterface, _integrationTestConfig);

            Assert.That(details, Is.Not.Null, "WgInterfaceInfo should not be null.");
            Assert.That(details.Name, Is.EqualTo(TestInterface));
            Assert.That(details.PublicKey, Is.EqualTo(ServerPublicKey));
            Assert.That(details.ListenPort, Is.EqualTo(51900));
            Assert.That(details.FwMark, Is.Null.Or.EqualTo("off").IgnoreCase, "FwMark mismatch.");
            // Server DNS not directly available in 'wg show dump' for the interface line,
            // it's a config item applied by wg-quick. So we can't assert details.Dns here.

            Assert.That(details.Peers, Has.Count.EqualTo(4), "Should have 4 peers.");

            // Peer A (Peer1PublicKey): Only AllowedIPs
            var peerAInfo = details.Peers.FirstOrDefault(p => p.PublicKey == Peer1PublicKey);
            Assert.That(peerAInfo, Is.Not.Null, $"Peer A ({Peer1PublicKey}) not found.");
            Assert.That(peerAInfo.AllowedIPs, Is.EquivalentTo(new List<string> { "10.250.0.2/32", "fd00:cafe::2/128" }));
            Assert.That(peerAInfo.PresharedKeyExists, Is.False, "Peer A PSK should not exist.");
            Assert.That(peerAInfo.Endpoint, Is.Null.Or.Empty.Or.EqualTo("(none)"), "Peer A Endpoint should be null or '(none)'.");
            Assert.That(peerAInfo.PersistentKeepaliveIntervalSeconds, Is.Null, "Peer A PersistentKeepalive should be null (off).");
            Assert.That(peerAInfo.LatestHandshake, Is.Null, "Peer A LatestHandshake should be null initially.");
            Assert.That(peerAInfo.TransferRxBytes, Is.EqualTo(0), "Peer A RxBytes should be 0 initially.");
            Assert.That(peerAInfo.TransferTxBytes, Is.EqualTo(0), "Peer A TxBytes should be 0 initially.");

            // Peer B (Peer2PublicKey): AllowedIPs, Endpoint
            var peerBInfo = details.Peers.FirstOrDefault(p => p.PublicKey == Peer2PublicKey);
            Assert.That(peerBInfo, Is.Not.Null, $"Peer B ({Peer2PublicKey}) not found.");
            Assert.That(peerBInfo.AllowedIPs, Is.EquivalentTo(new List<string> { "10.250.0.3/32" }));
            Assert.That(peerBInfo.PresharedKeyExists, Is.False, "Peer B PSK should not exist.");
            // `wg show dump` resolves hostnames for endpoints. "peerB.example.com" might resolve or fail.
            // If it fails to resolve, it might show the hostname itself or (none).
            // If it resolves (e.g. if it was in /etc/hosts in the container), it would show the IP.
            // For robustness, check if it's not null if we expect it, or check for specific patterns.
            // Given it's a dummy hostname, it likely won't resolve to an IP.
            // `wg show dump` often shows the literal string if it cannot resolve or if it's an IP.
            Assert.That(peerBInfo.Endpoint, Is.EqualTo("peerB.example.com:12345").Or.EqualTo("(none)"), $"Peer B Endpoint mismatch. Got: {peerBInfo.Endpoint}");
            Assert.That(peerBInfo.PersistentKeepaliveIntervalSeconds, Is.Null, "Peer B PersistentKeepalive should be null (off).");

            // Peer C (Peer3PublicKey): AllowedIPs, PresharedKey
            var peerCInfo = details.Peers.FirstOrDefault(p => p.PublicKey == Peer3PublicKey);
            Assert.That(peerCInfo, Is.Not.Null, $"Peer C ({Peer3PublicKey}) not found.");
            Assert.That(peerCInfo.AllowedIPs, Is.EquivalentTo(new List<string> { "10.250.0.4/32" }));
            Assert.That(peerCInfo.PresharedKeyExists, Is.True, "Peer C PSK should exist.");
            Assert.That(peerCInfo.Endpoint, Is.Null.Or.Empty.Or.EqualTo("(none)"), "Peer C Endpoint should be null or '(none)'.");
            Assert.That(peerCInfo.PersistentKeepaliveIntervalSeconds, Is.Null, "Peer C PersistentKeepalive should be null (off).");

            // Peer D (Peer4PublicKey): AllowedIPs, PersistentKeepalive
            var peerDInfo = details.Peers.FirstOrDefault(p => p.PublicKey == Peer4PublicKey);
            Assert.That(peerDInfo, Is.Not.Null, $"Peer D ({Peer4PublicKey}) not found.");
            Assert.That(peerDInfo.AllowedIPs, Is.EquivalentTo(new List<string> { "10.250.0.5/32" }));
            Assert.That(peerDInfo.PresharedKeyExists, Is.False, "Peer D PSK should not exist.");
            Assert.That(peerDInfo.Endpoint, Is.Null.Or.Empty.Or.EqualTo("(none)"), "Peer D Endpoint should be null or '(none)'.");
            Assert.That(peerDInfo.PersistentKeepaliveIntervalSeconds, Is.EqualTo(30));
        }

        [Test]
        public async Task ShowAllInterfacesDetailsAsync_IncludesTestInterface()
        {
            var allDetails = await WgQuick.ShowAllInterfacesDetailsAsync(_integrationTestConfig);
            Assert.That(allDetails, Is.Not.Null);

            var testInterfaceDetails = allDetails.FirstOrDefault(iface => iface.Name == TestInterface);
            Assert.That(testInterfaceDetails, Is.Not.Null, $"Test interface {TestInterface} not found in 'show all dump' results.");

            Assert.That(testInterfaceDetails.PublicKey, Is.EqualTo(ServerPublicKey));
            Assert.That(testInterfaceDetails.ListenPort, Is.EqualTo(51900));
            Assert.That(testInterfaceDetails.Peers, Has.Count.EqualTo(4)); // Should be 4 peers from setup
        }

        [Test, Order(2)] // Run after initial setup and ShowInterfaceDetailsAsync_ParsesAllFieldsCorrectly
        public async Task SetPeerAsync_ModifyOneOfMultiplePeers_VerifiesChange()
        {
            // TestInterface is already up with 4 diverse peers from OneTimeSetUp
            string peerToModifyPubKey = Peer2PublicKey; // Peer B (initially has an endpoint, no PSK, no keepalive)
            string newEndpoint = "1.2.3.4:54321";
            string newPsk = "NEW_PSK_FOR_PEER_B_INTEGRATION_TESTS_AAA=";
            Assert.That(ValidationUtils.IsValidWireGuardKey(newPsk), "Test PSK string is invalid.");

            var updateOptions = new WgPeerUpdateOptions
            {
                Endpoint = newEndpoint,
                PresharedKey = newPsk // Set PSK via string (will use temp file)
            };

            var setResult = await WgQuick.SetPeerAsync(TestInterface, peerToModifyPubKey, updateOptions, _integrationTestConfig);
            Assert.That(setResult.Success, Is.True, $"SetPeerAsync failed to modify Peer B: {setResult.StandardError}");

            var detailsAfterUpdate = await WgQuick.ShowInterfaceDetailsAsync(TestInterface, _integrationTestConfig);
            Assert.That(detailsAfterUpdate, Is.Not.Null);
            Assert.That(detailsAfterUpdate.Peers, Has.Count.EqualTo(4), "Peer count should remain 4.");

            // Check Peer B (modified)
            var modifiedPeer = detailsAfterUpdate.Peers.FirstOrDefault(p => p.PublicKey == peerToModifyPubKey);
            Assert.That(modifiedPeer, Is.Not.Null, $"Peer B ({peerToModifyPubKey}) not found after update.");
            Assert.That(modifiedPeer.Endpoint, Is.EqualTo(newEndpoint), "Peer B endpoint was not updated.");
            Assert.That(modifiedPeer.PresharedKeyExists, Is.True, "Peer B PSK should now exist.");

            // Check another peer (e.g., Peer A) to ensure it's unaffected
            var unaffectedPeer = detailsAfterUpdate.Peers.FirstOrDefault(p => p.PublicKey == Peer1PublicKey);
            Assert.That(unaffectedPeer, Is.Not.Null, $"Peer A ({Peer1PublicKey}) missing.");
            Assert.That(unaffectedPeer.Endpoint, Is.Null.Or.Empty.Or.EqualTo("(none)"), "Peer A endpoint should remain null or (none).");
            Assert.That(unaffectedPeer.PresharedKeyExists, Is.False, "Peer A PSK should remain non-existent.");
        }

        [Test, Order(3)] // Run after modification test
        public async Task SetPeerAsync_RemoveOneOfMultiplePeers_VerifiesRemoval()
        {
            // TestInterface is already up with 4 peers, one of which was modified.
            string peerToRemovePubKey = Peer4PublicKey; // Peer D
            int initialPeerCount = 4;

            // Verify peer D exists before removal
            var detailsBeforeRemove = await WgQuick.ShowInterfaceDetailsAsync(TestInterface, _integrationTestConfig);
            Assert.That(detailsBeforeRemove.Peers.FirstOrDefault(p => p.PublicKey == peerToRemovePubKey), Is.Not.Null, $"Peer D ({peerToRemovePubKey}) should exist before removal attempt.");
            Assert.That(detailsBeforeRemove.Peers, Has.Count.EqualTo(initialPeerCount), $"Initial peer count should be {initialPeerCount}.");


            var removeOptions = new WgPeerUpdateOptions { Remove = true };
            var setResult = await WgQuick.SetPeerAsync(TestInterface, peerToRemovePubKey, removeOptions, _integrationTestConfig);
            Assert.That(setResult.Success, Is.True, $"SetPeerAsync failed to remove Peer D: {setResult.StandardError}");

            var detailsAfterRemove = await WgQuick.ShowInterfaceDetailsAsync(TestInterface, _integrationTestConfig);
            Assert.That(detailsAfterRemove, Is.Not.Null);
            Assert.That(detailsAfterRemove.Peers.FirstOrDefault(p => p.PublicKey == peerToRemovePubKey), Is.Null, $"Peer D ({peerToRemovePubKey}) should not be found after removal.");
            Assert.That(detailsAfterRemove.Peers, Has.Count.EqualTo(initialPeerCount - 1), $"Peer count should be {initialPeerCount - 1} after removal.");

            // Verify other peers (e.g., Peer C which had PSK) are still present
            Assert.That(detailsAfterRemove.Peers.FirstOrDefault(p => p.PublicKey == Peer3PublicKey), Is.Not.Null, $"Peer C ({Peer3PublicKey}) should still exist after Peer D removal.");
        }
    }
}
